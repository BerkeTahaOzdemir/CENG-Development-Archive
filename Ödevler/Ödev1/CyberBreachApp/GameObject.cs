using System.Drawing;

namespace CyberBreachApp
{
    /// <summary>
    /// Abstract base class for all game objects.
    /// Demonstrates Abstraction and Encapsulation (OOP).
    /// </summary>
    public abstract class GameObject
    {
        // ── Encapsulated Fields ──────────────────────────────────────
        private int _xPosition;
        private int _yPosition;
        private int _speed;

        // ── Public Properties (Encapsulation) ────────────────────────
        public int X_Position
        {
            get => _xPosition;
            set => _xPosition = value;
        }

        public int Y_Position
        {
            get => _yPosition;
            set => _yPosition = value;
        }

        public int Speed
        {
            get => _speed;
            set
            {
                if (value < 0)
                    throw new ArgumentOutOfRangeException(nameof(Speed), "Speed cannot be negative.");
                _speed = value;
            }
        }

        // ── Visual Properties ────────────────────────────────────────
        public int Width { get; set; }
        public int Height { get; set; }
        public Color ObjectColor { get; set; }

        // ── Lifecycle (Encapsulated) ─────────────────────────────────
        private bool _isActive;

        /// <summary>
        /// Whether this object is alive and should be updated / drawn.
        /// Set to false to mark for removal.
        /// </summary>
        public bool IsActive
        {
            get => _isActive;
            set => _isActive = value;
        }

        // ── Constructor ──────────────────────────────────────────────
        protected GameObject(int x, int y, int speed, int width, int height, Color color)
        {
            X_Position = x;
            Y_Position = y;
            Speed = speed;
            Width = width;
            Height = height;
            ObjectColor = color;
            IsActive = true;
        }

        // ── Abstract Method (Polymorphism) ───────────────────────────
        /// <summary>
        /// Every derived class must provide its own movement logic.
        /// </summary>
        public abstract void Move();

        // ── Shared Behaviour ─────────────────────────────────────────
        /// <summary>
        /// Returns the bounding rectangle used for rendering and collision.
        /// </summary>
        public Rectangle GetBounds()
        {
            return new Rectangle(X_Position, Y_Position, Width, Height);
        }

        /// <summary>
        /// Checks axis-aligned bounding-box collision with another object.
        /// </summary>
        public bool CollidesWith(GameObject other)
        {
            return GetBounds().IntersectsWith(other.GetBounds());
        }

        /// <summary>
        /// Draws the object on the provided Graphics surface.
        /// Can be overridden for custom visuals.
        /// </summary>
        public virtual void Draw(Graphics g)
        {
            using var brush = new SolidBrush(ObjectColor);
            g.FillRectangle(brush, GetBounds());
        }
    }
}
