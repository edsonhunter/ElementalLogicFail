using FourCorners.Scripts.Components.Combat;
using FourCorners.Scripts.Components.Team;
using Unity.Entities;
using UnityEngine;

namespace FourCorners.Scripts.Authoring.Spawner
{
    public class PlayerBaseAuthoring : MonoBehaviour
    {
        public TeamNumber teamNumber;

        [Tooltip("Hit points of the central building. At zero the owning player is eliminated.")]
        public int maxHealth = 300;

        public class PlayerBaseAuthoringBaker : Baker<PlayerBaseAuthoring>
        {
            public override void Bake(PlayerBaseAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);

                AddComponent(entity, new Components.Spawner.PlayerBase
                {
                    TeamNumber = authoring.teamNumber,
                    IsActive = false,
                    NetworkId = 0
                });

                // Deliberately no DestroyOnDeath. A destroyed corner has to survive as a
                // deactivated ghost — the team slot, the visuals and the replicated identity all
                // outlive the building. BaseDestructionSystem handles it instead of DeathSystem.
                AddComponent(entity, new Health
                {
                    Current = authoring.maxHealth,
                    Max = authoring.maxHealth
                });
            }
        }
    }
}
