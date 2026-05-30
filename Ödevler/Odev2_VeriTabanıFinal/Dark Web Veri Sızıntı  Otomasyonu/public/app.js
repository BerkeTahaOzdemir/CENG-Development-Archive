const state = {
  lang: "en",
  stats: {},
  leakedUsers: [],
  analyses: [],
  alerts: [],
  search: "",
  risk: "ALL",
  errors: []
};

const labels = {
  en: {
    title: "Threat intelligence dashboard",
    subtitle: "Dark Web Breach Monitoring Automation",
    live: "Live monitor",
    sources: "Sources",
    breaches: "Breaches",
    leakedUsers: "Leaked Users",
    aiAnalyses: "AI Analyses",
    criticalAlerts: "Critical Alerts",
    automationRuns: "Automation Runs",
    filters: "Filters",
    search: "Search email",
    allRisks: "All risks",
    refresh: "Refresh Data",
    noRecords: "No records found",
    id: "ID",
    breachId: "Breach ID",
    leakedUserId: "Leaked User ID",
    email: "Email",
    passwordHash: "Password Hash",
    createdAt: "Created At",
    riskLevel: "Risk Level",
    category: "Category",
    summary: "Summary",
    criticalDomain: "Critical Domain",
    analyzedAt: "Analyzed At",
    reason: "Reason",
    records: "records",
    yes: "YES",
    no: "NO",
    dashboardError: "Dashboard error"
  },
  tr: {
    title: "Tehdit İstihbaratı Paneli",
    subtitle: "Dark Web Veri Sızıntısı İzleme Otomasyonu",
    live: "Canlı izleme",
    sources: "Kaynaklar",
    breaches: "Sızıntılar",
    leakedUsers: "Sızdırılan Kullanıcılar",
    aiAnalyses: "Yapay Zeka Analizleri",
    criticalAlerts: "Kritik Uyarılar",
    automationRuns: "Otomasyon Çalıştırmaları",
    filters: "Filtreler",
    search: "E-posta ara",
    allRisks: "Tüm riskler",
    refresh: "Veriyi Yenile",
    noRecords: "Kayıt bulunamadı",
    id: "ID",
    breachId: "Sızıntı ID",
    leakedUserId: "Kullanıcı ID",
    email: "E-posta",
    passwordHash: "Parola Hash",
    createdAt: "Oluşturulma",
    riskLevel: "Risk Seviyesi",
    category: "Kategori",
    summary: "Özet",
    criticalDomain: "Kritik Domain",
    analyzedAt: "Analiz Zamanı",
    reason: "Sebep",
    records: "kayıt",
    yes: "EVET",
    no: "HAYIR",
    dashboardError: "Panel hatası"
  }
};

const app = document.getElementById("app") || document.body.appendChild(document.createElement("main"));
const formatter = new Intl.NumberFormat();

function label(key) {
  return labels[state.lang][key];
}

function clear(node) {
  while (node.firstChild) {
    node.removeChild(node.firstChild);
  }
}

function textNode(value) {
  return document.createTextNode(value === null || value === undefined ? "" : String(value));
}

function el(tag, options = {}, children = []) {
  const node = document.createElement(tag);

  if (options.className) {
    node.className = options.className;
  }

  if (options.id) {
    node.id = options.id;
  }

  if (options.type) {
    node.type = options.type;
  }

  if (options.value !== undefined) {
    node.value = options.value;
  }

  if (options.placeholder) {
    node.placeholder = options.placeholder;
  }

  children.forEach((child) => {
    node.appendChild(typeof child === "string" || typeof child === "number" ? textNode(child) : child);
  });

  return node;
}

function formatNumber(value) {
  return formatter.format(Number(value || 0));
}

function formatDate(value) {
  if (!value) {
    return "";
  }

  const parsed = new Date(value);
  if (Number.isNaN(parsed.getTime())) {
    return String(value);
  }

  return parsed.toLocaleString();
}

async function getJson(url) {
  const response = await fetch(url, { cache: "no-store" });
  const payload = await response.json().catch(() => ({}));

  if (!response.ok) {
    throw new Error(payload.error || `${url} failed`);
  }

  return payload;
}

async function loadData() {
  state.errors = [];

  try {
    const dashboard = await getJson("/api/dashboard");
    state.stats = dashboard.stats || {};
    state.leakedUsers = Array.isArray(dashboard.leakedUsers) ? dashboard.leakedUsers : [];
    state.analyses = Array.isArray(dashboard.analyses) ? dashboard.analyses : [];
    state.alerts = Array.isArray(dashboard.alerts) ? dashboard.alerts : [];
  } catch (error) {
    state.stats = {};
    state.leakedUsers = [];
    state.analyses = [];
    state.alerts = [];
    state.errors.push(`/api/dashboard: ${error.message}`);
  }

  render();
}

function filteredLeakedUsers() {
  const query = state.search.trim().toLowerCase();
  return state.leakedUsers.filter((row) => String(row.email || "").toLowerCase().includes(query));
}

function filteredAlerts() {
  const query = state.search.trim().toLowerCase();
  return state.alerts.filter((row) => String(row.email || "").toLowerCase().includes(query));
}

function filteredAnalyses() {
  if (state.risk === "ALL") {
    return state.analyses;
  }
  return state.analyses.filter((row) => String(row.risk_level || "").toUpperCase() === state.risk);
}

function statCard(title, value, critical = false) {
  return el("article", { className: `stat-card${critical ? " critical" : ""}` }, [
    el("span", {}, [title]),
    el("strong", {}, [formatNumber(value)])
  ]);
}

function hero() {
  const refreshButton = el("button", { className: "language-toggle", type: "button" }, [label("refresh")]);
  const languageButton = el("button", { className: "language-toggle", type: "button" }, ["TR / EN"]);

  refreshButton.addEventListener("click", loadData);
  languageButton.addEventListener("click", () => {
    state.lang = state.lang === "en" ? "tr" : "en";
    render();
  });

  return el("section", { className: "hero" }, [
    el("div", {}, [
      el("p", { className: "kicker" }, [label("subtitle")]),
      el("h1", {}, [label("title")])
    ]),
    el("div", { className: "hero-actions" }, [
      refreshButton,
      languageButton,
      el("div", { className: "status" }, [
        el("span"),
        el("strong", {}, [label("live")])
      ])
    ])
  ]);
}

function statsGrid() {
  return el("section", { className: "stats-grid" }, [
    statCard(label("sources"), state.stats.SourceCount),
    statCard(label("breaches"), state.stats.BreachCount),
    statCard(label("leakedUsers"), state.stats.LeakedUserCount),
    statCard(label("aiAnalyses"), state.stats.AnalysisCount),
    statCard(label("criticalAlerts"), state.stats.AlertCount, true),
    statCard(label("automationRuns"), state.stats.AutomationRunCount)
  ]);
}

function filtersPanel() {
  const searchInput = el("input", { type: "search", value: state.search, placeholder: label("search") });
  const riskFilter = el("select", { value: state.risk });

  [
    ["ALL", label("allRisks")],
    ["LOW", "LOW"],
    ["MEDIUM", "MEDIUM"],
    ["HIGH", "HIGH"],
    ["CRITICAL", "CRITICAL"]
  ].forEach(([value, text]) => {
    const option = el("option", { value }, [text]);
    option.value = value;
    option.selected = value === state.risk;
    riskFilter.appendChild(option);
  });

  searchInput.addEventListener("input", (event) => {
    state.search = event.target.value;
    render();
  });

  riskFilter.addEventListener("change", (event) => {
    state.risk = event.target.value;
    render();
  });

  return el("section", { className: "panel" }, [
    el("div", { className: "panel-heading" }, [
      el("h2", {}, [label("filters")]),
      el("small", {}, [label("search")])
    ]),
    el("div", { className: "panel-heading" }, [
      searchInput,
      riskFilter
    ])
  ]);
}

function errorsPanel() {
  if (!state.errors.length) {
    return null;
  }

  return el("section", { className: "panel" }, [
    el("div", { className: "panel-heading" }, [
      el("h2", {}, [label("dashboardError")]),
      el("small", {}, [state.errors.join(" | ")])
    ])
  ]);
}

function tablePanel(title, rows, headers, cells) {
  const table = el("table");
  const thead = el("thead");
  const tbody = el("tbody");
  const headerRow = el("tr");

  headers.forEach((header) => {
    headerRow.appendChild(el("th", {}, [header]));
  });

  thead.appendChild(headerRow);

  if (rows.length === 0) {
    const emptyCell = el("td", { className: "empty" }, [label("noRecords")]);
    emptyCell.colSpan = headers.length;
    tbody.appendChild(el("tr", {}, [emptyCell]));
  } else {
    rows.forEach((row) => {
      const tr = el("tr");
      cells(row).forEach((value) => {
        tr.appendChild(el("td", {}, [value]));
      });
      tbody.appendChild(tr);
    });
  }

  table.appendChild(thead);
  table.appendChild(tbody);

  return el("section", { className: "panel" }, [
    el("div", { className: "panel-heading" }, [
      el("h2", {}, [title]),
      el("small", {}, [`${formatNumber(rows.length)} ${label("records")}`])
    ]),
    el("div", { className: "table-wrap" }, [table])
  ]);
}

function leakedUsersTable(rows) {
  return tablePanel(
    label("leakedUsers"),
    rows,
    [label("id"), label("breachId"), label("email"), label("passwordHash"), label("createdAt")],
    (row) => [row.leaked_user_id, row.breach_id, row.email, row.password_hash, formatDate(row.created_at)]
  );
}

function analysesTable(rows) {
  return tablePanel(
    label("aiAnalyses"),
    rows,
    [label("id"), label("breachId"), label("riskLevel"), label("category"), label("summary"), label("criticalDomain"), label("analyzedAt")],
    (row) => [
      row.analysis_id,
      row.breach_id,
      row.risk_level,
      row.category,
      row.summary,
      row.critical_domain_detected ? label("yes") : label("no"),
      formatDate(row.analyzed_at)
    ]
  );
}

function alertsTable(rows) {
  return tablePanel(
    label("criticalAlerts"),
    rows,
    [label("id"), label("leakedUserId"), label("breachId"), label("email"), label("reason"), label("createdAt")],
    (row) => [row.alert_id, row.leaked_user_id, row.breach_id, row.email, row.reason, formatDate(row.created_at)]
  );
}

function render() {
  const leakedUsers = filteredLeakedUsers();
  const analyses = filteredAnalyses();
  const alerts = filteredAlerts();
  const shell = el("main", { className: "shell" }, [
    hero(),
    ...(errorsPanel() ? [errorsPanel()] : []),
    statsGrid(),
    filtersPanel(),
    leakedUsersTable(leakedUsers),
    analysesTable(analyses),
    alertsTable(alerts)
  ]);

  document.documentElement.lang = state.lang;
  clear(app);
  app.appendChild(shell);
}

render();
loadData();
