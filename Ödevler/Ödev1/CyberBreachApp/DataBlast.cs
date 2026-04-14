using System.Drawing;

namespace CyberBreachApp
{
    /// <summary>
    /// A projectile fired upward by the player to destroy Firewalls.
    /// Inherits from GameObject and overrides Move() (Polymorphism).
    /// </summary>
    public class DataBlast : GameObject
    {
        // ── Constructor ──────────────────────────────────────────────
        public DataBlast(int x, int y, int speed)
            : base(x, y, speed, width: 6, height: 18, Color.FromArgb(0, 200, 255))
        {
        }

        // ── Polymorphic Move ─────────────────────────────────────────
        /// <summary>
        /// DataBlasts travel upward each tick.
        /// Deactivates itself when it leaves the top edge of the screen.
        /// </summary>
        public override void Move()
        {
            Y_Position -= Speed;

            if (Y_Position + Height < 0)
                IsActive = false;
        }

        // ── Custom Draw (laser bolt) ─────────────────────────────────
        public override void Draw(Graphics g)
        {
            // Core bolt
            using var brush = new SolidBrush(ObjectColor);
            g.FillRectangle(brush, GetBounds());

            // Inner bright line
            using var brightBrush = new SolidBrush(Color.FromArgb(200, 180, 255, 255));
            int coreX = X_Position + Width / 2 - 1;
            g.FillRectangle(brightBrush, coreX, Y_Position, 2, Height);

            // Subtle glow
            using var pen = new Pen(Color.FromArgb(60, 0, 200, 255), 1);
            g.DrawRectangle(pen, X_Position - 1, Y_Position - 1, Width + 2, Height + 2);
        }
    }
}
