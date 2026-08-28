using FourCorners.Scripts.Components.Building;
using FourCorners.Scripts.Components.Command;
using FourCorners.Scripts.Components.Economy;
using FourCorners.Scripts.Systems.Command;
using Unity.Entities;

namespace FourCorners.Scripts.Systems.Building
{
    /// <summary>
    /// Buys a level on a corner's central building, raising its passive income.
    ///
    /// One of the two systems that finally consume a <see cref="BaseCommand"/>. Note what it does
    /// not do: it never asks who sent anything. ServerBaseCommandSystem settled that before this
    /// intent existed, and <see cref="BaseCommand.BaseEntity"/> is a server-derived answer rather
    /// than a client's claim — so this can spend that corner's gold without a second thought about
    /// authorisation. What it must check is everything ownership does not cover: can they afford
    /// it, and is there anywhere left to go.
    ///
    /// It filters commands by type in the loop rather than through a query, which looks like the
    /// branch this design set out to avoid but is the opposite. Splitting on type in a *query*
    /// would need the dispatcher to attach a per-type tag, which would mean the dispatcher
    /// switching on type — reintroducing exactly the coupling that was removed. A handler
    /// recognising its own command is knowledge it is entitled to have.
    /// </summary>
    /// <remarks>
    /// Not Burst-compiled: it runs only on the frames a player presses something, and it logs
    /// purchases by name.
    /// </remarks>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct UpgradeCentralSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
            state.RequireForUpdate<BaseCommand>();
        }

        public void OnUpdate(ref SystemState state)
        {
            // Required before writing a ComponentLookup from the main thread. Paid only on frames
            // that carry a command at all, thanks to RequireForUpdate<BaseCommand> — this is not a
            // per-frame stall.
            state.CompleteDependency();

            var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);

            var economyLookup = SystemAPI.GetComponentLookup<PlayerEconomy>(isReadOnly: false);
            var buildingLookup = SystemAPI.GetComponentLookup<BuildingData>(isReadOnly: false);

            foreach (var command in SystemAPI.Query<RefRW<BaseCommand>>())
            {
                if (command.ValueRO.Type != BaseCommandType.UpgradeCentral) continue;

                // Claimed up front, on every path below. A rejection is still this system having
                // handled the command; leaving the flag clear would have BaseCommandCleanupSystem
                // report a perfectly well-handled refusal as an unimplemented command type.
                command.ValueRW.Handled = true;

                var baseEntity = command.ValueRO.BaseEntity;
                var sender = command.ValueRO.SourceConnection;

                // The intent is a frame old, so the corner may have been destroyed since. Same
                // staleness rule as Engagement.Target: re-check, never assume.
                if (!buildingLookup.TryGetComponent(baseEntity, out var building) ||
                    building.Type != BuildingType.Central)
                {
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

                int cost = BuildingUpgrade.CostFor(BuildingType.Central, building.Level);
                if (economy.Gold < cost)
                {
                    CommandRejection.Send(ref ecb, state.EntityManager, sender,
                        command.ValueRO.Type, BaseCommandRejection.InsufficientFunds);
                    continue;
                }

                economy.Gold -= cost;
                economyLookup[baseEntity] = economy;

                building.Level++;
                buildingLookup[baseEntity] = building;

                UnityEngine.Debug.Log(
                    $"[UpgradeCentralSystem] Team {command.ValueRO.Team} central building → level " +
                    $"{building.Level} for {cost} gold.");
            }
        }
    }
}
