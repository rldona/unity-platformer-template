using Platformer.Levels;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Platformer.Player
{
    /// <summary>
    /// Sends the player back to the spawn point (the level start, then the last checkpoint)
    /// with a short fade. Hazards call Die(); R on the keyboard or View/Select on a gamepad
    /// does the same for debugging, as does the component's context menu.
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public class PlayerRespawn : MonoBehaviour
    {
        [Tooltip("Where to respawn. Checkpoints replace it. If empty, the position at scene start is used.")]
        [SerializeField] private Transform spawnPoint;

        private PlayerController controller;
        private Vector2 startPosition;

        public Transform SpawnPoint
        {
            get => spawnPoint;
            set => spawnPoint = value;
        }

        private Vector2 RespawnPosition => spawnPoint != null ? (Vector2)spawnPoint.position : startPosition;

        private void Awake()
        {
            controller = GetComponent<PlayerController>();
            startPosition = transform.position;
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            var gamepad = Gamepad.current;
            if ((keyboard != null && keyboard.rKey.wasPressedThisFrame) ||
                (gamepad != null && gamepad.selectButton.wasPressedThisFrame))
                Die();
        }

        [ContextMenu("Respawn")]
        public void Die()
        {
            if (!Application.isPlaying)
                return;
            if (LevelFlow.Instance != null)
                LevelFlow.Instance.Respawn(controller, RespawnPosition);
            else
                controller.Respawn(RespawnPosition);
        }
    }
}
