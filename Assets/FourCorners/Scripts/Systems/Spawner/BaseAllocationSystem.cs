using FourCorners.Scripts.Components.Combat;
using FourCorners.Scripts.Components.Spawner;
using FourCorners.Scripts.Systems.Connection;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;

namespace FourCorners.Scripts.Systems.Spawner
{
    /// <summary>
    /// Activates the PlayerBase and all of its owned Spawners for a newly connected player.
    ///
    /// Linking strategy (no TeamNumber on spawners):
    ///   1. Find the PlayerBase whose TeamNumber == PendingBaseAllocation.ApprovedTeam.
    ///   2. Set PlayerBase.IsActive = true, PlayerBase.NetworkId = playerId.
    ///   3. Scan all SpawnerData for entries where spawner.PlayerBaseEntity == baseEntity.
    ///      Set their NetworkId = playerId and IsActive = true (ghost field for client replication).
    ///      SpawnerSystem independently gates on PlayerBase.IsActive, so IsActive here is
    ///      a derived mirror — not the authority.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(ServerStreamReadySystem))]
    public partial struct BaseAllocationSystem : ISystem
    {
        private const int YieldLogInterval = 120;

        private EntityQuery _baseQuery;
        private EntityQuery _spawnerQuery;
        private int _framesYielded;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
            var builder = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<NetworkId, NetworkStreamInGame, PendingBaseAllocation>();
            state.RequireForUpdate(state.GetEntityQuery(builder));

            _baseQuery = state.GetEntityQuery(ComponentType.ReadWrite<PlayerBase>());
            _spawnerQuery = state.GetEntityQuery(ComponentType.ReadWrite<SpawnerData>());

            state.RequireForUpdate(_baseQuery);
        }

        public void OnUpdate(ref SystemState state)
        {
            // This system mutates PlayerBase and SpawnerData from the MAIN THREAD via
            // ComponentLookup, while MinionSpawningSystem's ProcessSpawningJob reads
            // PlayerBase from a scheduled job. Writing without completing that job first
            // throws InvalidOperationException from the safety system every frame.
            //
            // The sync point is acceptable here specifically because RequireForUpdate
            // <PendingBaseAllocation> means this runs only for the frame or two around a
            // player claiming a corner — not every frame.
            state.CompleteDependency();

            var bases = _baseQuery.ToEntityArray(Allocator.Temp);
            var spawners = _spawnerQuery.ToEntityArray(Allocator.Temp);

            // ComponentLookup is the correct ECS pattern for random-access reads/writes
            // on entities that are NOT part of the current SystemAPI.Query iteration.
            // Using EntityManager.GetComponentData/SetComponentData inside a SystemAPI.Query
            // loop violates ECS safety handles and causes silent failures in the editor.
            var baseLookup = SystemAPI.GetComponentLookup<PlayerBase>(isReadOnly: false);
            var spawnerLookup = SystemAPI.GetComponentLookup<SpawnerData>(isReadOnly: false);
            var healthLookup = SystemAPI.GetComponentLookup<Health>(isReadOnly: false);

            var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);

            foreach (var (networkId, allocation, connectionEntity) in
                     SystemAPI.Query<RefRO<NetworkId>, RefRO<PendingBaseAllocation>>()
                         .WithAll<NetworkStreamInGame>()
                         .WithEntityAccess())
            {
                int playerId = networkId.ValueRO.Value;
                var approvedTeam = allocation.ValueRO.ApprovedTeam;
                bool assigned = false;
                Entity baseEntity = Entity.Null;

                // --- Phase 1: Activate the PlayerBase ---
                foreach (var candidate in bases)
                {
                    if (!baseLookup.TryGetComponent(candidate, out var baseData)) continue;

                    if (baseData.TeamNumber == approvedTeam && !baseData.IsActive)
                    {
                        baseData.IsActive = true;
                        baseData.NetworkId = playerId;
                        baseData.Race = allocation.ValueRO.ApprovedRace;
                        baseLookup[candidate] = baseData;

                        // Full health on claim. A corner released by a disconnect keeps whatever
                        // damage it took, so without this the next occupant inherits a wreck.
                        if (healthLookup.TryGetComponent(candidate, out var baseHealth))
                        {
                            baseHealth.Current = baseHealth.Max;
                            healthLookup[candidate] = baseHealth;
                        }

                        // Marks the corner as live so CornerTeardownSystem can spot it going
                        // inactive later, whether that is a disconnect or a demolished base.
                        ecb.AddComponent<ActiveCorner>(candidate);

                        baseEntity = candidate;
                        assigned = true;

                        UnityEngine.Debug.Log(
                            $"[BaseAllocationSystem] Activated PlayerBase Team={approvedTeam} " +
                            $"Race={baseData.Race} for NetworkId={playerId}");
                        break;
                    }
                }

                // --- Phase 2: Activate owned Spawners by entity reference ---
                // No TeamNumber scan — each spawner carries its owning base entity directly.
                if (assigned)
                {
                    foreach (var spawnerEntity in spawners)
                    {
                        if (!spawnerLookup.TryGetComponent(spawnerEntity, out var spawnerData)) continue;

                        if (spawnerData.PlayerBaseEntity == baseEntity)
                        {
                            spawnerData.NetworkId = playerId;
                            spawnerData.IsActive = true; // mirror for client ghost replication
                            spawnerLookup[spawnerEntity] = spawnerData;
                        }
                    }

                    // Only consume the allocation request once the base was successfully activated
                    ecb.RemoveComponent<PendingBaseAllocation>(connectionEntity);
                    _framesYielded = 0;
                }
                else
                {
                    // We yield and retry next frame — the SubScene map may still be streaming.
                    // Throttled: this used to fire every frame, drowning the real cause.
                    // Reports which bases actually exist so a missing/duplicate corner or a
                    // stale baked TeamNumber is visible instead of just "nothing happened".
                    _framesYielded++;
                    if (_framesYielded % YieldLogInterval == 1)
                    {
                        var available = new FixedString128Bytes();
                        foreach (var candidate in bases)
                        {
                            if (!baseLookup.TryGetComponent(candidate, out var baseData)) continue;
                            available.Append((int)baseData.TeamNumber);
                            available.Append(baseData.IsActive ? '*' : ' ');
                        }

                        UnityEngine.Debug.LogWarning(
                            $"[BaseAllocationSystem] Yielding allocation for Team={approvedTeam} NetworkId={playerId} " +
                            $"after {_framesYielded} frames. Bases present (team, '*'=already active): [{available}]. " +
                            "If this never resolves, the corner's baked TeamNumber does not match, " +
                            "or the SubScene has not streamed into the server world.");
                    }
                }
            }

            bases.Dispose();
            spawners.Dispose();
        }
    }
}