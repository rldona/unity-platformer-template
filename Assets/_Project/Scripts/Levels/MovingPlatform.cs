using UnityEngine;

namespace Platformer.Levels
{
    /// <summary>
    /// Kinematic platform that travels back and forth between its start position and
    /// start + travel, pausing at each end. The player reads Velocity to ride it.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class MovingPlatform : MonoBehaviour
    {
        [Tooltip("Offset from the start position to the far end (u).")]
        [SerializeField] private Vector2 travel = new Vector2(4f, 0f);
        [Tooltip("Travel speed (u/s).")]
        [SerializeField, Min(0.01f)] private float speed = 2.5f;
        [Tooltip("Pause at each end (s).")]
        [SerializeField, Min(0f)] private float waitTime = 0.5f;

        private Rigidbody2D body;
        private Vector2 start;
        private float progress;
        private int direction = 1;
        private float waitTimer;

        /// <summary>Velocity over the last physics step (u/s).</summary>
        public Vector2 Velocity { get; private set; }

        public Vector2 Travel
        {
            get => travel;
            set => travel = value;
        }

        public float Speed
        {
            get => speed;
            set => speed = Mathf.Max(0.01f, value);
        }

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            start = body.position;
        }

        private void FixedUpdate()
        {
            var dt = Time.fixedDeltaTime;
            var length = travel.magnitude;
            if (length < 0.0001f)
                return;

            if (waitTimer > 0f)
            {
                waitTimer -= dt;
                Velocity = Vector2.zero;
                return;
            }

            progress += direction * speed * dt / length;
            if (progress >= 1f || progress <= 0f)
            {
                progress = Mathf.Clamp01(progress);
                direction = -direction;
                waitTimer = waitTime;
            }

            var target = start + travel * progress;
            Velocity = (target - body.position) / dt;
            body.MovePosition(target);
        }

        private void OnDrawGizmos()
        {
            var from = Application.isPlaying ? start : (Vector2)transform.position;
            Gizmos.color = new Color(1f, 1f, 1f, 0.6f);
            Gizmos.DrawLine(from, from + travel);
            Gizmos.DrawWireSphere(from + travel, 0.15f);
        }
    }
}
