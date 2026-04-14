using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace CyberBreachApp
{
    public partial class Form1 : Form
    {
        // ── Game Constants ───────────────────────────────────────────
        private const int ArenaWidth = 600;
        private const int ArenaHeight = 700;
        private const int PlayerSpeed = 8;
        private const int FirewallCount = 6;
        private const int TickInterval = 16;            // ~60 FPS
        private const int BlastSpeed = 12;
        private const int BlastCooldownMs = 250;        // min ms between shots

        // ══════════════════════════════════════════════════════════════
        //  POLYMORPHIC COLLECTION — holds Player, Firewalls, DataBlasts
        // ══════════════════════════════════════════════════════════════
        private List<GameObject> _gameObjects = new();

        // ── Typed references for convenience ─────────────────────────
        private Player _player = null!;
        private readonly Random _rng = new();

        // ── Game State ───────────────────────────────────────────────
        private System.Windows.Forms.Timer _gameTimer = null!;
        private int _score;
        private int _highScore;
        private bool _isGameOver;
        private DateTime _lastBlastTime = DateTime.MinValue;

        // ── Fonts (cached) ───────────────────────────────────────────
        private Font _hudFont = null!;
        private Font _titleFont = null!;
        private Font _subtitleFont = null!;

        // ══════════════════════════════════════════════════════════════
        //  Initialization
        // ══════════════════════════════════════════════════════════════
        public Form1()
        {
            InitializeComponent();
            SetupForm();
            SetupFonts();
            InitGame();
        }

        private void SetupForm()
        {
            Text = "CyberBreach — DataPacket vs Firewalls";
            ClientSize = new Size(ArenaWidth, ArenaHeight);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            DoubleBuffered = true;
            BackColor = Color.FromArgb(10, 10, 25);

            KeyDown += OnKeyDown;
            KeyUp += OnKeyUp;
            Paint += OnPaint;
        }

        private void SetupFonts()
        {
            _hudFont = new Font("Consolas", 14, FontStyle.Bold);
            _titleFont = new Font("Consolas", 32, FontStyle.Bold);
            _subtitleFont = new Font("Consolas", 12, FontStyle.Regular);
        }

        private void InitGame()
        {
            _score = 0;
            _isGameOver = false;

            // ── Build the polymorphic collection ─────────────────────
            _gameObjects.Clear();

            // Player (bottom-center)
            _player = new Player(
                x: ArenaWidth / 2 - 20,
                y: ArenaHeight - 70,
                speed: PlayerSpeed,
                arenaWidth: ArenaWidth,
                arenaHeight: ArenaHeight
            );
            _gameObjects.Add(_player);

            // Firewalls
            for (int i = 0; i < FirewallCount; i++)
                _gameObjects.Add(CreateFirewall(initialSpawn: true));

            // Game timer
            _gameTimer?.Stop();
            _gameTimer = new System.Windows.Forms.Timer { Interval = TickInterval };
            _gameTimer.Tick += GameLoop;
            _gameTimer.Start();
        }

        private Firewall CreateFirewall(bool initialSpawn)
        {
            int x = _rng.Next(0, ArenaWidth - 50);
            int y = initialSpawn ? _rng.Next(-ArenaHeight, -20) : _rng.Next(-200, -20);
            int speed = _rng.Next(3, 8);
            return new Firewall(x, y, speed, ArenaHeight);
        }

        // ══════════════════════════════════════════════════════════════
        //  Game Loop — Polymorphism In Action
        // ══════════════════════════════════════════════════════════════
        private void GameLoop(object? sender, EventArgs e)
        {
            if (_isGameOver) return;

            // ── 1. POLYMORPHIC MOVE on every GameObject ──────────────
            foreach (var obj in _gameObjects)
            {
                if (obj.IsActive)
                    obj.Move();
            }

            // ── 2. Collision: DataBlast ↔ Firewall ───────────────────
            ResolveBlastFirewallCollisions();

            // ── 3. Collision: Player ↔ Firewall ──────────────────────
            foreach (var obj in _gameObjects)
            {
                if (obj is Firewall fw && fw.IsActive && _player.CollidesWith(fw))
                {
                    _isGameOver = true;
                    _gameTimer.Stop();
                    if (_score > _highScore) _highScore = _score;
                    Invalidate();
                    return;
                }
            }

            // ── 4. Recycle off-screen firewalls & score ──────────────
            foreach (var obj in _gameObjects)
            {
                if (obj is Firewall fw && fw.IsActive && fw.IsOffScreen)
                {
                    _score += 10;
                    fw.Recycle(
                        newX: _rng.Next(0, ArenaWidth - fw.Width),
                        newSpeed: _rng.Next(3, 8 + _score / 100)
                    );
                }
            }

            // ── 5. Remove dead objects (off-screen blasts, etc.) ─────
            _gameObjects.RemoveAll(obj => !obj.IsActive);

            // ── 6. Redraw ────────────────────────────────────────────
            Invalidate();
        }

        // ══════════════════════════════════════════════════════════════
        //  Collision Detection — DataBlast vs Firewall
        // ══════════════════════════════════════════════════════════════
        /// <summary>
        /// Checks every active DataBlast against every active Firewall.
        /// If they intersect, both are destroyed and the player earns points.
        /// </summary>
        private void ResolveBlastFirewallCollisions()
        {
            // Snapshot lists to avoid issues with mutation during iteration
            var blasts = _gameObjects.OfType<DataBlast>().Where(b => b.IsActive).ToList();
            var firewalls = _gameObjects.OfType<Firewall>().Where(f => f.IsActive).ToList();

            foreach (var blast in blasts)
            {
                foreach (var fw in firewalls)
                {
                    if (!blast.IsActive || !fw.IsActive) continue;

                    if (blast.CollidesWith(fw))
                    {
                        // Destroy both
                        blast.IsActive = false;
                        fw.IsActive = false;

                        // Award points for destroying a firewall
                        _score += 25;

                        // Spawn a replacement firewall at the top
                        _gameObjects.Add(CreateFirewall(initialSpawn: false));
                    }
                }
            }
        }

        // ══════════════════════════════════════════════════════════════
        //  Firing Mechanism
        // ══════════════════════════════════════════════════════════════
        private void FireDataBlast()
        {
            if (_isGameOver) return;

            // Cooldown to prevent spam
            if ((DateTime.Now - _lastBlastTime).TotalMilliseconds < BlastCooldownMs)
                return;

            _lastBlastTime = DateTime.Now;

            // Spawn blast from the center-top of the player
            var blast = new DataBlast(
                x: _player.X_Position + _player.Width / 2 - 3,
                y: _player.Y_Position - 18,
                speed: BlastSpeed
            );

            _gameObjects.Add(blast);
        }

        // ══════════════════════════════════════════════════════════════
        //  Rendering
        // ══════════════════════════════════════════════════════════════
        private void OnPaint(object? sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            DrawGrid(g);

            if (_isGameOver)
            {
                DrawGameOver(g);
                return;
            }

            // ── Draw all active game objects polymorphically ──────────
            foreach (var obj in _gameObjects)
            {
                if (obj.IsActive)
                    obj.Draw(g);
            }

            DrawHUD(g);
        }

        /// <summary>
        /// Draws a subtle cyberpunk grid background.
        /// </summary>
        private void DrawGrid(Graphics g)
        {
            using var pen = new Pen(Color.FromArgb(20, 0, 255, 200), 1);
            for (int x = 0; x < ArenaWidth; x += 40)
                g.DrawLine(pen, x, 0, x, ArenaHeight);
            for (int y = 0; y < ArenaHeight; y += 40)
                g.DrawLine(pen, 0, y, ArenaWidth, y);
        }

        /// <summary>
        /// Draws the score and high-score HUD.
        /// </summary>
        private void DrawHUD(Graphics g)
        {
            // Current score (top-left)
            string scoreText = $"SCORE: {_score}";
            using var cyanBrush = new SolidBrush(Color.FromArgb(200, 0, 255, 200));
            g.DrawString(scoreText, _hudFont, cyanBrush, 10, 10);

            // High score (top-right)
            string hiText = $"HIGH: {_highScore}";
            var hiSize = g.MeasureString(hiText, _hudFont);
            using var goldBrush = new SolidBrush(Color.FromArgb(200, 255, 215, 0));
            g.DrawString(hiText, _hudFont, goldBrush, ArenaWidth - hiSize.Width - 10, 10);

            // Controls hint (bottom-center)
            string controls = "[WASD] Move   [SPACE] Fire";
            var ctrlSize = g.MeasureString(controls, _subtitleFont);
            using var dimBrush = new SolidBrush(Color.FromArgb(80, 0, 255, 200));
            g.DrawString(controls, _subtitleFont, dimBrush,
                (ArenaWidth - ctrlSize.Width) / 2, ArenaHeight - 30);
        }

        /// <summary>
        /// Draws the Game Over overlay.
        /// </summary>
        private void DrawGameOver(Graphics g)
        {
            // Dimmed background
            using var overlay = new SolidBrush(Color.FromArgb(180, 0, 0, 0));
            g.FillRectangle(overlay, 0, 0, ArenaWidth, ArenaHeight);

            // Title
            string title = "BREACH DETECTED";
            var titleSize = g.MeasureString(title, _titleFont);
            using var redBrush = new SolidBrush(Color.FromArgb(255, 60, 60));
            g.DrawString(title, _titleFont, redBrush,
                (ArenaWidth - titleSize.Width) / 2,
                ArenaHeight / 2 - 80);

            // Final score
            string scoreLine = $"Final Score: {_score}";
            var scoreSize = g.MeasureString(scoreLine, _hudFont);
            using var whiteBrush = new SolidBrush(Color.White);
            g.DrawString(scoreLine, _hudFont, whiteBrush,
                (ArenaWidth - scoreSize.Width) / 2,
                ArenaHeight / 2 - 20);

            // High score
            string hiLine = $"Session High: {_highScore}";
            var hiSize = g.MeasureString(hiLine, _hudFont);
            using var goldBrush = new SolidBrush(Color.FromArgb(255, 215, 0));
            g.DrawString(hiLine, _hudFont, goldBrush,
                (ArenaWidth - hiSize.Width) / 2,
                ArenaHeight / 2 + 15);

            // Restart hint
            string hint = "Press [R] to Reboot";
            var hintSize = g.MeasureString(hint, _subtitleFont);
            using var cyanBrush = new SolidBrush(Color.FromArgb(0, 255, 200));
            g.DrawString(hint, _subtitleFont, cyanBrush,
                (ArenaWidth - hintSize.Width) / 2,
                ArenaHeight / 2 + 55);
        }

        // ══════════════════════════════════════════════════════════════
        //  Input — 8-way movement + Spacebar firing
        // ══════════════════════════════════════════════════════════════
        private void OnKeyDown(object? sender, KeyEventArgs e)
        {
            if (_isGameOver && e.KeyCode == Keys.R)
            {
                InitGame();
                return;
            }

            switch (e.KeyCode)
            {
                case Keys.Left:
                case Keys.A:
                    _player.MoveLeft = true;
                    break;
                case Keys.Right:
                case Keys.D:
                    _player.MoveRight = true;
                    break;
                case Keys.Up:
                case Keys.W:
                    _player.MoveUp = true;
                    break;
                case Keys.Down:
                case Keys.S:
                    _player.MoveDown = true;
                    break;
                case Keys.Space:
                    FireDataBlast();
                    break;
            }
        }

        private void OnKeyUp(object? sender, KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.Left:
                case Keys.A:
                    _player.MoveLeft = false;
                    break;
                case Keys.Right:
                case Keys.D:
                    _player.MoveRight = false;
                    break;
                case Keys.Up:
                case Keys.W:
                    _player.MoveUp = false;
                    break;
                case Keys.Down:
                case Keys.S:
                    _player.MoveDown = false;
                    break;
            }
        }
    }
}
