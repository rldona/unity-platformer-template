using Platformer.Player;
using UnityEngine;

namespace Platformer.Cameras
{
    /// <summary>
    /// Point the camera follows instead of the player. It tracks the player horizontally, but
    /// vertically it stays at the height of the last ground while airborne, so jumps don't drag
    /// the camera up and down. It follows a fall below that height (pits, ledges) and a rise far
    /// above it, and recentres after landing at a new height: fast while far, then at a steady
    /// minimum speed, so it never crawls the last pixels one by one.
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    [DefaultExecutionOrder(-100)] // before CinemachineBrain reads the target in LateUpdate
    [DisallowMultipleComponent]
    public class PlayerCameraTarget : MonoBehaviour
    {
        [Tooltip("Child transform the Cinemachine camera follows. Its position is overwritten every frame.")]
        [SerializeField] private Transform target;
        [Tooltip("How far above the last ground the player may go before the camera follows up (u).")]
        [SerializeField, Min(0f)] private float maxRise = 4f;
        [Tooltip("Vertical catch-up rate: fraction of the remaining distance covered per second.")]
        [SerializeField, Min(0f)] private float catchUp = 6f;
        [Tooltip("Slowest vertical catch-up speed (u/s), so the camera finishes instead of crawling.")]
        [SerializeField, Min(0.01f)] private float minSpeed = 3f;
        [Tooltip("A move larger than this in one frame is a teleport (respawn): the target jumps with it (u).")]
        [SerializeField, Min(0f)] private float teleportDistance = 3f;

        private PlayerController controller;
        private float groundY;
        private float y;
        private Vector2 lastPosition;

        public Transform Target => target;

        private void Awake() => controller = GetComponent<PlayerController>();

        private void OnEnable() => Snap();

        private void LateUpdate()
        {
            if (target == null)
                return;

            var position = (Vector2)transform.position;
            if ((position - lastPosition).sqrMagnitude > teleportDistance * teleportDistance)
                Snap();
            lastPosition = position;

            if (controller.IsGrounded || position.y < groundY)
                groundY = position.y;
            else if (position.y > groundY + maxRise)
                groundY = position.y - maxRise;

            var distance = Mathf.Abs(groundY - y);
            y = Mathf.MoveTowards(y, groundY, Mathf.Max(minSpeed, distance * catchUp) * Time.deltaTime);
            target.position = new Vector3(position.x, y, target.position.z);
        }

        private void Snap()
        {
            lastPosition = transform.position;
            groundY = y = lastPosition.y;
            if (target != null)
                target.position = new Vector3(lastPosition.x, y, target.position.z);
        }
    }
}
