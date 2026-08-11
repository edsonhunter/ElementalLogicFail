using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;
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

        [Tooltip("Initial cooldown in seconds.")]
        public float Cooldown = 2f;

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
                    RandomSeed = 0,
                    Cooldown = authoring.Cooldown
                });
            }
        }
    }
}
