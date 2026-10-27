using Platformer.Player;
using UnityEngine;

namespace Platformer.Levels
{
    /// <summary>
    /// Touching it makes it the player's respawn point. The previous checkpoint goes back to idle.
    /// The respawn position is this transform (the feet).
    /// </summary>
    public class Checkpoint : PlayerTrigger
    {
        [Tooltip("Flag tinted with the colours below.")]
        [SerializeField] private SpriteRenderer flag;
        [SerializeField] private Color idleColor = new Color(0.55f, 0.55f, 0.6f);
        [SerializeField] private Color activeColor = new Color(0.35f, 0.9f, 0.45f);

        private static Checkpoint current;

        public SpriteRenderer Flag
        {
            get => flag;
            set => flag = value;
        }

        private void Awake() => SetActive(false);

        protected override void OnPlayerEnter(PlayerController player)
        {
            if (current == this || !player.TryGetComponent<PlayerRespawn>(out var respawn))
                return;
            if (current != null)
                current.SetActive(false);
            current = this;
            SetActive(true);
            respawn.SpawnPoint = transform;
        }

        private void SetActive(bool active)
        {
            if (flag != null)
                flag.color = active ? activeColor : idleColor;
        }
    }
}
