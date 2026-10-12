using UnityEngine;

namespace Nusantara.World
{
    // Physical bank colliders provide normal movement blocking. This also catches combat
    // teleports or unusually large movement steps that bypass the controller's collision.
    [DefaultExecutionOrder(9000)]
    public sealed class WorldWaterSafety : MonoBehaviour
    {
        public WorldGeographyProfile geography;
        public Transform player;
        public CharacterController controller;
        Vector3 lastDryPosition;
        void OnEnable()
        {
            if (player == null) return;
            lastDryPosition = player.position;
            if (geography != null && geography.IsForbiddenWater(new Vector2(player.position.x, player.position.z)))
            {
                lastDryPosition = geography.world.playerStart;
                if (controller != null) lastDryPosition -= Vector3.up * (controller.center.y - controller.height * .5f);
            }
        }
        void LateUpdate()
        {
            if (geography == null || player == null) return;
            if (!geography.IsForbiddenWater(new Vector2(player.position.x, player.position.z))&&!geography.IsBelowWalkingPlatform(player.position))
            { lastDryPosition = player.position; return; }
            bool enabledController = controller != null && controller.enabled;
            if (enabledController) controller.enabled = false;
            player.position = lastDryPosition;
            if (enabledController) controller.enabled = true;
        }
    }
}
