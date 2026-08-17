using FourCorners.Scripts.Components.Command;
using FourCorners.Scripts.Components.Connection;
using FourCorners.Scripts.Components.Request;
using FourCorners.Scripts.Components.Spawner;
using FourCorners.Scripts.Components.Team;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;

namespace FourCorners.Scripts.Systems.Command
{
    /// <summary>
    /// The server's front door for gameplay commands, and the only place a client's word is turned
    /// into something the simulation will act on.
    ///
    /// It answers exactly one question — *may this connection command a base, and which one* — and
    /// then gets out of the way. Whether the player can afford the thing, whether the building is
    /// already at its cap, what the command actually does: none of that is here. Those depend on
    /// systems that do not exist yet (economy in 1.2, buildings in 1.3), and folding them in would
    /// make every new command type an edit to this file. Instead an accepted command becomes a
    /// <see cref="BaseCommand"/> intent entity, and a handler per command type consumes it. Adding
    /// a command means adding an enum member and a system; it never means touching this one.
    ///
    /// The security property worth stating plainly: <see cref="BaseCommandRequest"/> contains no
    /// base, no team and no player id. The corner is derived from
    /// <c>ReceiveRpcCommandRequest.SourceConnection</c> matched against
    /// <c>TeamStatusElement.OccupyingPlayer</c>, which is server-owned state a client cannot
    /// influence. A malicious client can therefore send nonsense about its own corner and nothing
    /// else — the interesting attack, commanding someone else's base, is not expressible.
    ///
    /// Deliberately not Burst-compiled, on the same reasoning as HostStartGameSystem: it runs only
    /// on the frames a player actually presses something, and legible rejection logs are worth far
    /// more than Burst on a system that idles.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct ServerBaseCommandSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();

            var rpcQuery = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<BaseCommandRequest, ReceiveRpcCommandRequest>();
            state.RequireForUpdate(state.GetEntityQuery(rpcQuery));

            var matchQuery = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<MatchStateTag, MatchState, TeamStatusElement>();
            state.RequireForUpdate(state.GetEntityQuery(matchQuery));
        }

        public void OnUpdate(ref SystemState state)
        {
            var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);

            var matchState = SystemAPI.GetSingleton<MatchState>();
            var teamBuffer = SystemAPI.GetSingletonBuffer<TeamStatusElement>(isReadOnly: true);

            // Resolved once up front rather than per command. Iterating the bases through
            // SystemAPI.Query keeps the main-thread read safe without a ComponentLookup and its
            // CompleteDependency, and there are only ever four of them.
            var basesByTeam = new NativeArray<Entity>(Teams.Count, Allocator.Temp);
            foreach (var (playerBase, baseEntity) in
                     SystemAPI.Query<RefRO<PlayerBase>>().WithEntityAccess())
            {
                if (!playerBase.ValueRO.IsActive) continue;

                int team = (int)playerBase.ValueRO.TeamNumber;
                if (team < 0 || team >= basesByTeam.Length) continue;

                basesByTeam[team] = baseEntity;
            }

            foreach (var (request, receive, rpcEntity) in
                     SystemAPI.Query<RefRO<BaseCommandRequest>, RefRO<ReceiveRpcCommandRequest>>()
                         .WithEntityAccess())
            {
                ecb.DestroyEntity(rpcEntity);

                var sender = receive.ValueRO.SourceConnection;
                var type = request.ValueRO.Type;

                // Before anything else: a reply aimed at a connection that has already gone throws
                // during ECB playback, and one throw there discards the commands of every system
                // that recorded into the same buffer this frame. Nobody to answer means nothing to
                // do — the sender is not around to care either way.
                if (!state.EntityManager.Exists(sender)) continue;

                if (type == BaseCommandType.None)
                {
                    Reject(ref ecb, sender, type, BaseCommandRejection.MalformedCommand);
                    continue;
                }

                if (matchState.Phase != MatchPhase.Active)
                {
                    Reject(ref ecb, sender, type, BaseCommandRejection.MatchNotActive);
                    continue;
                }

                int ownedTeam = ResolveOwnedTeam(teamBuffer, sender);
                if (ownedTeam == -1)
                {
                    UnityEngine.Debug.LogWarning(
                        $"[ServerBaseCommandSystem] {type} from connection {sender}, which occupies " +
                        "no corner. Rejected.");
                    Reject(ref ecb, sender, type, BaseCommandRejection.NotYourBase);
                    continue;
                }

                if (teamBuffer[ownedTeam].IsEliminated)
                {
                    Reject(ref ecb, sender, type, BaseCommandRejection.Eliminated);
                    continue;
                }

                var target = basesByTeam[ownedTeam];
                if (target == Entity.Null)
                {
                    // Ordinary for the few frames between the slot being granted and
                    // BaseAllocationSystem activating the corner.
                    Reject(ref ecb, sender, type, BaseCommandRejection.BaseUnavailable);
                    continue;
                }

                var accepted = ecb.CreateEntity();
                ecb.AddComponent(accepted, new BaseCommand
                {
                    BaseEntity = target,
                    Team = (TeamNumber)ownedTeam,
                    SourceConnection = sender,
                    Type = type,
                    TargetSlot = request.ValueRO.TargetSlot
                });

                UnityEngine.Debug.Log(
                    $"[ServerBaseCommandSystem] {type} slot={request.ValueRO.TargetSlot} accepted for " +
                    $"Team {(TeamNumber)ownedTeam}.");
            }

            basesByTeam.Dispose();
        }

        /// <summary>
        /// Finds the corner this connection occupies, or -1.
        ///
        /// A slot held for an absent owner (occupied with a null connection, see
        /// <see cref="TeamStatusElement"/>) matches nobody — which is what we want. Commands are
        /// live input; a corner playing on without its owner keeps fighting but stops building.
        /// </summary>
        private static int ResolveOwnedTeam(in DynamicBuffer<TeamStatusElement> teamBuffer, Entity connection)
        {
            if (connection == Entity.Null) return -1;

            for (int i = 0; i < teamBuffer.Length; i++)
            {
                if (!teamBuffer[i].IsOccupied) continue;
                if (teamBuffer[i].OccupyingPlayer != connection) continue;

                return i;
            }

            return -1;
        }

        private static void Reject(
            ref EntityCommandBuffer ecb,
            Entity sender,
            BaseCommandType type,
            BaseCommandRejection reason)
        {
            var rpc = ecb.CreateEntity();
            ecb.AddComponent(rpc, new BaseCommandRejectedRpc { Type = type, Reason = reason });
            ecb.AddComponent(rpc, new SendRpcCommandRequest { TargetConnection = sender });
        }
    }
}
