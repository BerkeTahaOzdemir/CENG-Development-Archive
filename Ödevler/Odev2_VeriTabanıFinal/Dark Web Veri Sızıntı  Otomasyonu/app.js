const path = require("path");
const express = require("express");
const sql = require("mssql");

const app = express();
const port = 3000;

const dbConfig = {
  server: "127.0.0.1",
  port: 1434,
  database: "DarkWebBreachMonitoringDB",
  user: "n8n_demo",
  password: "Demo123456",
  options: {
    encrypt: true,
    trustServerCertificate: true
  },
  pool: {
    max: 10,
    min: 1,
    idleTimeoutMillis: 30000
  }
};

let pool;
let poolPromise;

async function getPool() {
  if (pool && pool.connected) {
    return pool;
  }

  if (!poolPromise) {
    const nextPool = new sql.ConnectionPool(dbConfig);
    nextPool.on("error", () => {
      if (pool === nextPool) {
        pool = null;
      }
      poolPromise = null;
    });

    poolPromise = nextPool.connect()
      .then((connectedPool) => {
        pool = connectedPool;
        return connectedPool;
      })
      .catch((error) => {
        pool = null;
        poolPromise = null;
        throw error;
      });
  }

  return poolPromise;
}

async function query(sqlText) {
  const activePool = await getPool();
  return activePool.request().query(sqlText);
}

async function send(res, action) {
  try {
    const result = await action();
    res.json(result);
  } catch (error) {
    res.status(500).json({ error: error.message });
  }
}

app.use(express.static(path.join(__dirname, "public")));

async function dashboardPayload() {
  const sources = await query("SELECT COUNT(*) AS SourceCount FROM Sources");
  const breaches = await query("SELECT COUNT(*) AS BreachCount FROM Breaches");
  const leakedUsersCount = await query("SELECT COUNT(*) AS LeakedUserCount FROM Leaked_Users");
  const analysesCount = await query("SELECT COUNT(*) AS AnalysisCount FROM AI_Analysis");
  const alertsCount = await query("SELECT COUNT(*) AS AlertCount FROM Critical_Alert_Logs");
  const automationRuns = await query("SELECT COUNT(*) AS AutomationRunCount FROM Automation_Runs");
  const leakedUsers = await query(`
    SELECT TOP 20 leaked_user_id, breach_id, email, password_hash, created_at
    FROM Leaked_Users
    ORDER BY created_at DESC
  `);
  const analyses = await query(`
    SELECT TOP 20 analysis_id, breach_id, risk_level, category, summary, critical_domain_detected, analyzed_at
    FROM AI_Analysis
    ORDER BY analyzed_at DESC
  `);
  const alerts = await query(`
    SELECT TOP 20 alert_id, leaked_user_id, breach_id, email, reason, created_at
    FROM Critical_Alert_Logs
    ORDER BY created_at DESC
  `);

  return {
    stats: {
      SourceCount: sources.recordset[0].SourceCount,
      BreachCount: breaches.recordset[0].BreachCount,
      LeakedUserCount: leakedUsersCount.recordset[0].LeakedUserCount,
      AnalysisCount: analysesCount.recordset[0].AnalysisCount,
      AlertCount: alertsCount.recordset[0].AlertCount,
      AutomationRunCount: automationRuns.recordset[0].AutomationRunCount
    },
    leakedUsers: leakedUsers.recordset,
    analyses: analyses.recordset,
    alerts: alerts.recordset
  };
}

app.get("/api/dashboard", (req, res) => {
  send(res, dashboardPayload);
});

app.get("/api/stats", (req, res) => {
  send(res, async () => {
    const sources = await query("SELECT COUNT(*) AS SourceCount FROM Sources");
    const breaches = await query("SELECT COUNT(*) AS BreachCount FROM Breaches");
    const leakedUsers = await query("SELECT COUNT(*) AS LeakedUserCount FROM Leaked_Users");
    const analyses = await query("SELECT COUNT(*) AS AnalysisCount FROM AI_Analysis");
    const alerts = await query("SELECT COUNT(*) AS AlertCount FROM Critical_Alert_Logs");
    const automationRuns = await query("SELECT COUNT(*) AS AutomationRunCount FROM Automation_Runs");

    return {
      SourceCount: sources.recordset[0].SourceCount,
      BreachCount: breaches.recordset[0].BreachCount,
      LeakedUserCount: leakedUsers.recordset[0].LeakedUserCount,
      AnalysisCount: analyses.recordset[0].AnalysisCount,
      AlertCount: alerts.recordset[0].AlertCount,
      AutomationRunCount: automationRuns.recordset[0].AutomationRunCount
    };
  });
});

app.get("/api/leaked-users", (req, res) => {
  send(res, async () => {
    const result = await query(`
      SELECT TOP 20 leaked_user_id, breach_id, email, password_hash, created_at
      FROM Leaked_Users
      ORDER BY created_at DESC
    `);
    return result.recordset;
  });
});

app.get("/api/ai-analysis", (req, res) => {
  send(res, async () => {
    const result = await query(`
      SELECT TOP 20 analysis_id, breach_id, risk_level, category, summary, critical_domain_detected, analyzed_at
      FROM AI_Analysis
      ORDER BY analyzed_at DESC
    `);
    return result.recordset;
  });
});

app.get("/api/alerts", (req, res) => {
  send(res, async () => {
    const result = await query(`
      SELECT TOP 20 alert_id, leaked_user_id, breach_id, email, reason, created_at
      FROM Critical_Alert_Logs
      ORDER BY created_at DESC
    `);
    return result.recordset;
  });
});

app.listen(port, () => {
  console.log(`Dashboard running on http://localhost:${port}`);
});
