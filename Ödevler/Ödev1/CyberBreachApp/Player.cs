using System;
using System.Drawing;

namespace CyberBreachApp
{
    /// <summary>
    /// The DataPacket — controlled by the player via keyboard.
    /// Supports full 8-directional movement with diagonal normalization.
    /// Inherits from GameObject and overrides Move() (Polymorphism).
    /// </summary>
    public class Player : GameObject
    {
        // ── Input State (tracked per-axis) ───────────────────────────
        public bool MoveLeft { get; set; }
        public bool MoveRight { get; set; }
        public bool MoveUp { get; set; }
        public bool MoveDown { get; set; }

        /// <summary>
        /// Arena dimensions for boundary clamping.
        /// </summary>
        public int ArenaWidth { get; set; }
        public int ArenaHeight { get; set; }

        // ── Diagonal normalization factor: 1 / √2 ───────────────────
        private const double DiagonalFactor = 0.7071;

        // ── Constructor ──────────────────────────────────────────────
        public Player(int x, int y, int speed, int arenaWidth, int arenaHeight)
            : base(x, y, speed, width: 40, height: 40, Color.FromArgb(0, 255, 200))
        {
            ArenaWidth = arenaWidth;
            ArenaHeight = arenaHeight;
        }

        // ── Polymorphic Move (8-way with normalization) ──────────────
        /// <summary>
        /// Moves the player based on current input flags.
        /// When moving diagonally, speed is multiplied by 1/√2
        /// to keep consistent velocity in all directions.
        /// </summary>
        public override void Move()
        {
            // Calculate raw direction vector
            int dx = 0;
            int dy = 0;

            if (MoveLeft) dx -= 1;
            if (MoveRight) dx += 1;
            if (MoveUp) dy -= 1;
            if (MoveDown) dy += 1;

            // No input → no movement
            if (dx == 0 && dy == 0) return;

            // Normalize diagonal speed
            bool isDiagonal = dx != 0 && dy != 0;
            double effectiveSpeed = isDiagonal ? Speed * DiagonalFactor : Speed;

            X_Position += (int)Math.Round(dx * effectiveSpeed);
            Y_Position += (int)Math.Round(dy * effectiveSpeed);

            // Clamp to arena boundaries
            if (X_Position < 0) X_Position = 0;
            if (Y_Position < 0) Y_Position = 0;
            if (X_Position + Width > ArenaWidth) X_Position = ArenaWidth - Width;
            if (Y_Position + Height > ArenaHeight) Y_Position = ArenaHeight - Height;
        }

        // ── Custom Draw (diamond / packet shape) ─────────────────────
        public override void Draw(Graphics g)
        {
            var cx = X_Position + Width / 2;
            var cy = Y_Position + Height / 2;

            Point[] diamond =
            {
                new(cx, Y_Position),              // top
                new(X_Position + Width, cy),       // right
                new(cx, Y_Position + Height),      // bottom
                new(X_Position, cy)                // left
            };

            using var brush = new SolidBrush(ObjectColor);
            g.FillPolygon(brush, diamond);

            // Glow outline
            using var pen = new Pen(Color.FromArgb(120, 0, 255, 200), 2);
            g.DrawPolygon(pen, diamond);
        }
    }
}
