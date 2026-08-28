using FourCorners.Scripts.Components.Building;
using FourCorners.Scripts.Components.Combat;
using FourCorners.Scripts.Components.Economy;
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

        [Header("Economy")]
        [Tooltip("Gold the corner opens the match with.")]
        public int startingGold = 100;

        [Tooltip("Gold earned per second while the corner is alive. Upgradeable later.")]
        public int incomePerSecond = 5;

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

                // Baked rather than added when a player claims the corner: a ghost's component
                // composition is fixed at bake time, so a replicated component that appears at
                // runtime is not replicated at all. BaseAllocationSystem resets the values instead.
                AddComponent(entity, new PlayerEconomy
                {
                    Gold = authoring.startingGold,
                    StartingGold = authoring.startingGold,
                    IncomePerSecond = authoring.incomePerSecond,
                    BaseIncomePerSecond = authoring.incomePerSecond
                });

                // The base entity *is* the central building — no separate entity, because it would
                // duplicate the team, the transform and the lifetime of one that already exists.
                AddComponent(entity, new BuildingData
                {
                    Type = BuildingType.Central,
                    Level = 0
                });
            }
        }
    }
}
