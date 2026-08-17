using Unity.Entities;

namespace FourCorners.Scripts.Components.Command
{
    /// <summary>
    /// Client-side record that the server refused one of our commands, waiting to be handed to the
    /// UI by BridgeNotificationSystem.
    ///
    /// An instant rather than a state, so it is consumed on delivery — the same treatment
    /// <c>ClientDisconnectedTag</c> gets, and the opposite of <c>MatchStartedTag</c>. Several may
    /// exist at once: a player mashing an upgrade button they cannot afford earns one rejection per
    /// press, and collapsing them would make the HUD look like it had missed some.
    /// </summary>
    public struct BaseCommandRejectedTag : IComponentData
    {
        public BaseCommandType Type;
        public BaseCommandRejection Reason;
    }
}
