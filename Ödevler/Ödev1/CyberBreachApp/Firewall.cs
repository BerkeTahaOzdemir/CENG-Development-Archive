using System.Drawing;

namespace CyberBreachApp
{
    /// <summary>
    /// A falling obstacle the player must dodge.
    /// Inherits from GameObject and overrides Move() (Polymorphism).
    /// </summary>
    public class Firewall : GameObject
    {
        /// <summary>
        /// Whether this firewall has left the screen and should be recycled.
        /// </summary>
        public bool IsOffScreen { get; private set; }

        /// <summary>
        /// The height of the game arena, used to detect off-screen state.
        /// </summary>
        public int ArenaHeight { get; set; }

        // ── Constructor ──────────────────────────────────────────────
        public Firewall(int x, int y, int speed, int arenaHeight)
            : base(x, y, speed, width: 50, height: 18, Color.FromArgb(255, 60, 60))
        {
            ArenaHeight = arenaHeight;
            IsOffScreen = false;
        }

        // ── Polymorphic Move ─────────────────────────────────────────
        /// <summary>
        /// Moves the firewall downward each tick.
        /// Sets IsOffScreen when it leaves the bottom edge.
        /// </summary>
        public override void Move()
        {
            Y_Position += Speed;

            if (Y_Position > ArenaHeight)
                IsOffScreen = true;
        }

        /// <summary>
        /// Resets the firewall to the top with a new random X position.
        /// </summary>
        public void Recycle(int newX, int newSpeed)
        {
            X_Position = newX;
            Y_Position = -Height;
            Speed = newSpeed;
            IsOffScreen = false;
        }

        // ── Custom Draw (brick-wall look) ────────────────────────────
        public override void Draw(Graphics g)
        {
            // Body
            using var brush = new SolidBrush(ObjectColor);
            g.FillRectangle(brush, GetBounds());

            // Brick lines for a "wall" effect
            using var pen = new Pen(Color.FromArgb(180, 80, 0), 1);
            int midY = Y_Position + Height / 2;
            g.DrawLine(pen, X_Position, midY, X_Position + Width, midY);

            int sliceW = Width / 3;
            g.DrawLine(pen, X_Position + sliceW, Y_Position, X_Position + sliceW, midY);
            g.DrawLine(pen, X_Position + sliceW * 2, midY, X_Position + sliceW * 2, Y_Position + Height);

            // Border glow
            using var border = new Pen(Color.FromArgb(120, 255, 80, 80), 1);
            g.DrawRectangle(border, GetBounds());
        }
    }
}
