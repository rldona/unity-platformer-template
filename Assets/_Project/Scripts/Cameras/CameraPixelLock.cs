using Platformer.Player;
using Unity.Cinemachine;
using UnityEngine;

namespace Platformer.Cameras
{
    /// <summary>
    /// Cinemachine extension that keeps the camera a whole number of pixels away from what it
    /// follows. The Pixel Perfect Camera rounds the camera to the pixel grid and the target is
    /// rounded separately, so a fractional gap makes the followed sprite shake 1 px on screen
    /// while both move. Locking the gap removes it; the correction is under half a pixel.
    /// Locks to PlayerPixelSnap.RenderPosition when the target is (or belongs to) the player.
    /// Each axis is locked only while the camera moves on it: a camera held still (jumps,
    /// level bounds) would otherwise flip a pixel back and forth as the player moves.
    /// </summary>
    [AddComponentMenu("Cinemachine/Procedural/Extensions/Camera Pixel Lock")]
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class CameraPixelLock : CinemachineExtension
    {
        // Camera movement per frame below this counts as standing still (u).
        private const float StillEpsilon = 0.0001f;

        [SerializeField, Min(1f)] private float pixelsPerUnit = 16f;

        private Vector2 lastPosition;
        private bool hasLastPosition;

        protected override void PostPipelineStageCallback(
            CinemachineVirtualCameraBase vcam, CinemachineCore.Stage stage, ref CameraState state, float deltaTime)
        {
            // Finalize runs after the composer, damping and confiner have placed the camera.
            if (stage != CinemachineCore.Stage.Finalize || !Application.isPlaying)
                return;
            var target = vcam.Follow;
            if (target == null)
                return;

            // Movement is measured before this correction, on the position Cinemachine wants.
            var position = (Vector2)state.GetFinalPosition();
            var moved = hasLastPosition && deltaTime >= 0f ? position - lastPosition : Vector2.zero;
            lastPosition = position;
            hasLastPosition = true;

            // The target may be a child of the player (PlayerCameraTarget); lock to the sprite itself.
            var snap = target.GetComponentInParent<PlayerPixelSnap>();
            var anchor = snap != null ? snap.RenderPosition : (Vector2)target.position;
            var gap = position - anchor;
            var correction = Vector2.zero;
            if (Mathf.Abs(moved.x) > StillEpsilon)
                correction.x = Mathf.Round(gap.x * pixelsPerUnit) / pixelsPerUnit - gap.x;
            if (Mathf.Abs(moved.y) > StillEpsilon)
                correction.y = Mathf.Round(gap.y * pixelsPerUnit) / pixelsPerUnit - gap.y;
            state.PositionCorrection += (Vector3)correction;
        }
    }
}
