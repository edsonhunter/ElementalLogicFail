using FourCorners.Scripts.Components.Connection;
using FourCorners.Scripts.Components.Request;
using Unity.Entities;
using Unity.NetCode;

namespace FourCorners.Scripts.Systems.Connection
{
    /// <summary>
    /// Shared lobby-state fan-out. Both the accept path and the disconnect path must tell every
    /// remaining player the same thing; duplicating the loop is how the two drift apart.
    /// </summary>
    internal static class LobbyBroadcast
    {
        /// <summary>
        /// Queues a LobbyStateUpdateRpc to every connected player, marking as host whichever
        /// player matches <paramref name="hostNetworkId"/>.
        /// </summary>
        public static void SendToAll(
            ref EntityCommandBuffer ecb,
            in DynamicBuffer<ConnectedPlayerElement> players,
            int hostNetworkId)
        {
            for (int i = 0; i < players.Length; i++)
            {
                var rpc = ecb.CreateEntity();
                ecb.AddComponent(rpc, new LobbyStateUpdateRpc
                {
                    IsHost = players[i].NetworkId == hostNetworkId,
                    PlayerCount = players.Length
                });
                ecb.AddComponent(rpc, new SendRpcCommandRequest
                {
                    TargetConnection = players[i].ConnectionEntity
                });
            }
        }
    }
}
