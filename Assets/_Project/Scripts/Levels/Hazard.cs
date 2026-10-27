using Platformer.Player;

namespace Platformer.Levels
{
    /// <summary>Spikes, pits and anything else that sends the player back to the last checkpoint.</summary>
    public class Hazard : PlayerTrigger
    {
        protected override void OnPlayerEnter(PlayerController player)
        {
            if (player.TryGetComponent<PlayerRespawn>(out var respawn))
                respawn.Die();
        }
    }
}
