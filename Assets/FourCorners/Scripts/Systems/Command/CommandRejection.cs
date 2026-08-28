using FourCorners.Scripts.Components.Command;
using FourCorners.Scripts.Components.Request;
using Unity.Entities;
using Unity.NetCode;

namespace FourCorners.Scripts.Systems.Command
{
    /// <summary>
    /// The one way to tell a player their command was refused.
    ///
    /// Extracted the moment there was a second sender: the dispatcher rejects on identity, and each
    /// handler rejects on its own rules, so without this the "does the connection still exist"
    /// check would be copied into every one of them. That check is not optional politeness —
    /// recording an RPC aimed at a destroyed connection throws during ECB playback, and a throw
    /// there discards every command every system buffered that frame, surfacing as some completely
    /// unrelated system appearing to fail. Making it impossible to forget is worth a static class.
    /// </summary>
    internal static class CommandRejection
    {
        /// <summary>
        /// Sends a rejection, or does nothing if there is nobody left to send it to.
        ///
        /// Silence on a departed connection is the correct behaviour rather than a swallowed error:
        /// the player is gone, and they were not waiting for an answer.
        /// </summary>
        public static void Send(
            ref EntityCommandBuffer ecb,
            EntityManager entityManager,
            Entity sender,
            BaseCommandType type,
            BaseCommandRejection reason)
        {
            if (sender == Entity.Null) return;
            if (!entityManager.Exists(sender)) return;

            var rpc = ecb.CreateEntity();
            ecb.AddComponent(rpc, new BaseCommandRejectedRpc { Type = type, Reason = reason });
            ecb.AddComponent(rpc, new SendRpcCommandRequest { TargetConnection = sender });
        }
    }
}
