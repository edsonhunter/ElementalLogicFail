using FourCorners.Scripts.Components.Combat;
using FourCorners.Scripts.Components.Minion;
using FourCorners.Scripts.Components.Path;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace FourCorners.Scripts.Systems.Path
{
    /// <summary>
    /// Walks minions along their lane — unless they are fighting.
    ///
    /// "Minions meet, fight, and the survivor walks on" is not a state machine here: it is the
    /// absence of <see cref="Engagement"/>. A minion in a fight simply stops matching this query,
    /// and starts matching it again the moment EngagementSystem releases it, resuming from the
    /// waypoint index it had already reached.
    /// </summary>
    [BurstCompile]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    public partial struct PathFollowSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var deltaTime = SystemAPI.Time.DeltaTime;
            var elapsedTime = (float)SystemAPI.Time.ElapsedTime;

            var job = new PathFollowJob
            {
                DeltaTime = deltaTime,
                ElapsedTime = elapsedTime
            };
            
            state.Dependency = job.ScheduleParallel(state.Dependency);
        }
    }

    [BurstCompile]
    [WithNone(typeof(Engagement))]
    public partial struct PathFollowJob : IJobEntity
    {
        public float DeltaTime;
        public float ElapsedTime;

        private void Execute(RefRW<LocalTransform> transform, RefRW<PathFollower> follower, RefRW<MinionData> minion, DynamicBuffer<PathWaypoint> buffer)
        {
            if (buffer.IsEmpty) return;

            var followerRW = follower.ValueRW;
            var currentTarget = buffer[followerRW.CurrentIndex].Position;
            
            minion.ValueRW.Target = currentTarget;
            
            float3 forwardDirection = currentTarget - transform.ValueRO.Position;
            forwardDirection.y = 0;
            
            float distanceToTarget = math.length(forwardDirection);
            if (distanceToTarget > 0.001f)
            {
                forwardDirection /= distanceToTarget;
            }
            
            // Calculate a perpendicular Right Vector for the wandering sway
            float3 rightDirection = math.cross(new float3(0, 1, 0), forwardDirection);
            
            // Generate a smooth Perlin Noise value based on the simulation elapsed time and the unit's unique random seed
            float noiseValue = noise.cnoise(new float2(ElapsedTime * 2f, minion.ValueRO.RandomSeed * 0.001f));
            
            // Combine Forward and Right directions, scaled by the Noise
            // This produces a snaking/wandering trajectory while still progressing towards the True Forward line.
            float wanderStrength = 0.8f; 
            float3 finalDirection = math.normalizesafe(forwardDirection + (rightDirection * noiseValue * wanderStrength));
            
            // Apply rotation to face movement direction
            if (math.lengthsq(finalDirection) > 0.001f)
            {
                transform.ValueRW.Rotation = quaternion.LookRotationSafe(finalDirection, math.up());
            }

            // Apply drift
            transform.ValueRW.Position += finalDirection * minion.ValueRO.Speed * DeltaTime;

            // Check distance
            if (math.distancesq(transform.ValueRO.Position, currentTarget) < 0.2f * 0.2f)
            {
                // Reached waypoint
                followerRW.CurrentIndex++;
                if (followerRW.CurrentIndex >= buffer.Length)
                {
                    followerRW.CurrentIndex = 0;
                }
            }
            
            follower.ValueRW = followerRW;
        }
    }
}
