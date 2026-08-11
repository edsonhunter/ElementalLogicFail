using Unity.Entities;

namespace FourCorners.Scripts.Components.Connection
{
    /// <summary>
    /// Client-world singleton raised when the server refuses the join because every corner is
    /// taken. Observed by BridgeNotificationSystem, which surfaces it to the UI.
    ///
    /// Previously TeamRejectedRpc was sent but consumed by nobody, so an extra player simply
    /// hung on the connection screen with no explanation.
    /// </summary>
    public struct JoinRejectedTag : IComponentData { }
}
