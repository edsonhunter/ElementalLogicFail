using FourCorners.Scripts.Components.Building;
using FourCorners.Scripts.Components.Command;
using FourCorners.Scripts.Components.Economy;
using FourCorners.Scripts.Components.Spawner;
using FourCorners.Scripts.Components.Team;
using FourCorners.Scripts.Systems.Command;
using Unity.Collections;
using Unity.Entities;

namespace FourCorners.Scripts.Systems.Building
{
    /// <summary>
    /// Buys a level on one of a corner's three barracks, making it spawn more and faster.
    ///
    /// This is where <see cref="BaseCommand.TargetSlot"/> — the one piece of client input the
    /// dispatcher deliberately does not validate — finally gets checked. The dispatcher cannot: it
    /// has no idea what a slot addresses for any given command type. Here it names one of the
    /// corner's three barracks, and a slot naming none of them is refused rather than clamped —
    /// clamping would silently upgrade a building the player did not ask for.
    ///
    /// Two different components answer two different questions here, and keeping them apart is the
    /// point. <c>SpawnerData.PlayerBaseEntity</c> says who owns a barracks — server-only, because
    /// ownership is never a client's business. <c>BuildingData.Slot</c> says how it is addressed —
    /// replicated, because a client cannot build an upgrade UI for buildings it cannot name.
    /// </summary>
    /// <remarks>Not Burst-compiled, for the same reason as UpgradeCentralSystem.</remarks>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct UpgradeBarracksSystem : ISystem
    {
        /// <summary>A spawner and the two facts needed to address it, snapshotted once.</summary>
        private struct BarracksSlot
        {
            public Entity Entity;
            public Entity OwningBase;
            public byte Slot;
        }

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
            state.RequireForUpdate<BaseCommand>();
        }

        public void OnUpdate(ref SystemState state)
        {
            state.CompleteDependency();

            var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);

            var economyLookup = SystemAPI.GetComponentLookup<PlayerEconomy>(isReadOnly: false);
            var buildingLookup = SystemAPI.GetComponentLookup<BuildingData>(isReadOnly: false);

            // Snapshotted before the command loop rather than searched per command: nesting a
            // second SystemAPI.Query inside the first is not something to rely on, and there are
            // only twelve spawners in the entire match.
            var barracks = new NativeList<BarracksSlot>(Teams.Count * Buildings.BarracksPerBase, Allocator.Temp);
            foreach (var (spawner, building, spawnerEntity) in
                     SystemAPI.Query<RefRO<SpawnerData>, RefRO<BuildingData>>().WithEntityAccess())
            {
                barracks.Add(new BarracksSlot
                {
                    Entity = spawnerEntity,
                    OwningBase = spawner.ValueRO.PlayerBaseEntity,
                    Slot = building.ValueRO.Slot
                });
            }

            foreach (var command in SystemAPI.Query<RefRW<BaseCommand>>())
            {
                if (command.ValueRO.Type != BaseCommandType.UpgradeBarracks) continue;

                // Set on every path, rejections included — see UpgradeCentralSystem for why.
                command.ValueRW.Handled = true;

                var baseEntity = command.ValueRO.BaseEntity;
                var sender = command.ValueRO.SourceConnection;

                var target = Resolve(barracks, baseEntity, command.ValueRO.TargetSlot);
                if (target == Entity.Null ||
                    !buildingLookup.TryGetComponent(target, out var building) ||
                    building.Type != BuildingType.Barracks)
                {
                    UnityEngine.Debug.LogWarning(
                        $"[UpgradeBarracksSystem] Team {command.ValueRO.Team} addressed barracks slot " +
                        $"{command.ValueRO.TargetSlot}, which does not exist. Rejected.");
                    CommandRejection.Send(ref ecb, state.EntityManager, sender,
                        command.ValueRO.Type, BaseCommandRejection.NoSuchBuilding);
                    continue;
                }

                if (building.Level >= BuildingUpgrade.MaxLevel)
                {
                    CommandRejection.Send(ref ecb, state.EntityManager, sender,
                        command.ValueRO.Type, BaseCommandRejection.LevelCapped);
                    continue;
                }

                if (!economyLookup.TryGetComponent(baseEntity, out var economy))
                {
                    CommandRejection.Send(ref ecb, state.EntityManager, sender,
                        command.ValueRO.Type, BaseCommandRejection.NoSuchBuilding);
                    continue;
                }

                int cost = BuildingUpgrade.CostFor(BuildingType.Barracks, building.Level);
                if (economy.Gold < cost)
                {
                    CommandRejection.Send(ref ecb, state.EntityManager, sender,
                        command.ValueRO.Type, BaseCommandRejection.InsufficientFunds);
                    continue;
                }

                economy.Gold -= cost;
                economyLookup[baseEntity] = economy;

                building.Level++;
                buildingLookup[target] = building;

                UnityEngine.Debug.Log(
                    $"[UpgradeBarracksSystem] Team {command.ValueRO.Team} barracks " +
                    $"{command.ValueRO.TargetSlot} → level {building.Level} for {cost} gold.");
            }

            barracks.Dispose();
        }

        /// <summary>
        /// The barracks of <paramref name="owningBase"/> occupying <paramref name="slot"/>, or
        /// Entity.Null.
        ///
        /// Matching on the owning base as well as the slot is what stops slot 0 resolving to
        /// somebody else's barracks — every corner has a slot 0.
        /// </summary>
        private static Entity Resolve(in NativeList<BarracksSlot> barracks, Entity owningBase, byte slot)
        {
            for (int i = 0; i < barracks.Length; i++)
            {
                if (barracks[i].OwningBase != owningBase) continue;
                if (barracks[i].Slot != slot) continue;

                return barracks[i].Entity;
            }

            return Entity.Null;
        }
    }
}
