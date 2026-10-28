using UnityEngine;

namespace Platformer.Player
{
    /// <summary>
    /// Keeps the sprite a whole number of pixels away from the moving platform being ridden.
    /// The game renders at 320x180, so every object is rounded to the pixel grid on its own;
    /// with a fractional gap, player and platform round on different frames and the sprite
    /// shakes 1 px back and forth. The camera locks onto RenderPosition for the same reason
    /// (see Platformer.Cameras.CameraPixelLock). The offset is under half a pixel and only visual.
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    [DisallowMultipleComponent]
    public class PlayerPixelSnap : MonoBehaviour
    {
        [Tooltip("Child that holds the sprite. Its local position is overwritten every frame.")]
        [SerializeField] private Transform visual;
        [SerializeField, Min(1f)] private float pixelsPerUnit = 16f;

        private PlayerController controller;

        /// <summary>Where the sprite's origin is drawn this frame (world space).</summary>
        public Vector2 RenderPosition
        {
            get
            {
                var position = (Vector2)transform.position;
                var carrier = controller != null ? controller.Carrier : null;
                if (carrier == null)
                    return position;
                var anchor = (Vector2)carrier.transform.position;
                return anchor + Snap(position - anchor);
            }
        }

        private void Awake() => controller = GetComponent<PlayerController>();

        // Positions are read on demand (the camera asks during its own LateUpdate), so this
        // only moves the sprite; both read the same interpolated transforms.
        private void LateUpdate()
        {
            if (visual != null)
                visual.localPosition = RenderPosition - (Vector2)transform.position;
        }

        private Vector2 Snap(Vector2 v) =>
            new Vector2(Mathf.Round(v.x * pixelsPerUnit), Mathf.Round(v.y * pixelsPerUnit)) / pixelsPerUnit;
    }
}
