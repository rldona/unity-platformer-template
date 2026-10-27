using Platformer.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Platformer.Debugging
{
    /// <summary>
    /// On-screen readout of the controller (F1 toggles it) and editor gizmos for the body
    /// collider and the ground probe. In-game collider outlines are drawn by <see cref="ColliderVisualizer"/>.
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public class PlayerDebugOverlay : MonoBehaviour
    {
        [Tooltip("Show the overlay when entering play mode.")]
        [SerializeField] private bool visible = true;
        [Tooltip("Overlay scale relative to a 360 px tall screen.")]
        [SerializeField, Min(0.5f)] private float scale = 1f;

        [Header("Gizmos")]
        [SerializeField] private Color groundedColor = new Color(0.3f, 1f, 0.4f);
        [SerializeField] private Color airborneColor = new Color(1f, 0.85f, 0.2f);
        [SerializeField] private Color probeColor = new Color(0.3f, 0.8f, 1f);

        private PlayerController controller;
        private float smoothedDeltaTime;
        private GUIStyle style;
        private readonly OutlineBuffer gizmoOutlines = new OutlineBuffer();

        private void Awake()
        {
            controller = GetComponent<PlayerController>();
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.f1Key.wasPressedThisFrame)
                visible = !visible;

            smoothedDeltaTime = Mathf.Lerp(smoothedDeltaTime, Time.unscaledDeltaTime, 0.1f);
        }

        private void OnGUI()
        {
            if (!visible || controller == null)
                return;

            style ??= new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.UpperLeft,
                fontSize = 11,
                richText = false,
                padding = new RectOffset(6, 6, 4, 4),
            };

            var factor = Screen.height / 360f * scale;
            GUI.matrix = Matrix4x4.Scale(new Vector3(factor, factor, 1f));

            var velocity = controller.Velocity;
            var fps = smoothedDeltaTime > 0f ? 1f / smoothedDeltaTime : 0f;
            var text =
                $"state     {controller.CurrentState}\n" +
                $"vx        {velocity.x,7:F2}\n" +
                $"vy        {velocity.y,7:F2}\n" +
                $"grounded  {controller.IsGrounded}\n" +
                $"ground by {controller.GroundSource}\n" +
                $"coyote    {controller.CoyoteTimeRemaining,7:F3}\n" +
                $"buffer    {controller.JumpBufferRemaining,7:F3}\n" +
                $"platform  {controller.CarryVelocity.x,5:F1},{controller.CarryVelocity.y,5:F1}\n" +
                $"fps       {fps,7:F0}\n" +
                "F1 hide   R respawn\n" +
                "F2 colliders";
            GUI.Box(new Rect(4f, 4f, 150f, 160f), text, style);
            GUI.matrix = Matrix4x4.identity;
        }

        private void OnDrawGizmos()
        {
            var pc = controller != null ? controller : GetComponent<PlayerController>();
            if (pc == null)
                return;

            var body = pc.BodyCollider != null ? pc.BodyCollider : GetComponent<BoxCollider2D>();
            DrawGizmo(body, pc.IsGrounded ? groundedColor : airborneColor);

            gizmoOutlines.Clear();
            DebugShapes.GroundProbe(pc, gizmoOutlines);
            Gizmos.color = probeColor;
            DebugShapes.DrawGizmos(gizmoOutlines);
        }

        private void DrawGizmo(Collider2D collider, Color color)
        {
            if (collider == null)
                return;
            gizmoOutlines.Clear();
            DebugShapes.Collect(collider, gizmoOutlines);
            Gizmos.color = color;
            DebugShapes.DrawGizmos(gizmoOutlines);
        }
    }
}
