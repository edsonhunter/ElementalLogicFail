using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;
using FourCorners.Scripts.Components.Combat;
using FourCorners.Scripts.Components.Minion;
using FourCorners.Scripts.Components.Team;

namespace FourCorners.Scripts.Authoring.Minion
{
    public class MinionAuthoring : MonoBehaviour
    {
        [Tooltip("Fallback team. Overwritten at spawn with the owning PlayerBase's team.")]
        public TeamNumber Type;

        [Tooltip("Movement speed in units/second. Read by PathFollowSystem and WanderSystem.")]
        public float speed = 2f;

        [Tooltip("Seconds between attacks. Also the delay before this unit's first attack.")]
        public float Cooldown = 2f;

        [Header("Combat")]
        [Tooltip("Hit points. The unit is destroyed when these reach zero.")]
        public int maxHealth = 10;

        [Tooltip("Hit points removed per landed attack.")]
        public int attackDamage = 2;

        [Tooltip("How close an enemy must be to be hit. Fights break off at twice this distance.")]
        public float attackRange = 1.5f;

        public class MinionBaker : Baker<MinionAuthoring>
        {
            public override void Bake(MinionAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);

                // RandomSeed is deliberately left at 0 here. It used to be
                // UnityEngine.Random.Range(...), which made bake output differ per bake — server
                // and client entity scenes must be bit-identical in a Netcode project.
                // MinionSpawningSystem assigns the real per-instance seed at spawn time.
                AddComponent(entity, new MinionData
                {
                    TeamNumber = authoring.Type,
                    Speed = authoring.speed,
                    Target = float3.zero,
                    RandomSeed = 0
                });

                AddComponent(entity, new Health
                {
                    Current = authoring.maxHealth,
                    Max = authoring.maxHealth
                });

                AddComponent(entity, new AttackStats
                {
                    Damage = authoring.attackDamage,
                    Interval = authoring.Cooldown,
                    Range = authoring.attackRange
                });

                // Seeded to the full interval so a unit cannot land a free hit the instant it
                // touches an enemy — the first blow costs the same wind-up as every other.
                AddComponent(entity, new AttackCooldown { Remaining = authoring.Cooldown });

                // Minions are deleted when they die. Bases will carry Health without this tag,
                // because a dead corner has to stay in the world as a deactivated ghost.
                AddComponent<DestroyOnDeath>(entity);
            }
        }
    }
}
