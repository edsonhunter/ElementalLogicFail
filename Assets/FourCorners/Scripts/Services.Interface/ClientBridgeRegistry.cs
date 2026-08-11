namespace FourCorners.Scripts.Services.Interface
{
    /// <summary>
    /// Hand-off point for the one <see cref="ISystemBridgeService"/> that drives this process's UI.
    ///
    /// Why a static: ECS worlds are created by the Netcode bootstrap at application start, but the
    /// service is only registered later, from the MainMenu scene. BridgeNotificationSystem
    /// therefore cannot be pushed the service in OnCreate — it pulls from here lazily instead.
    ///
    /// This is presentation-layer state, not simulation state. Exactly one full client world
    /// exists per process (<see cref="Unity.NetCode.ClientServerBootstrap.ClientWorld"/>), so
    /// process scope is the correct scope. Do NOT use this pattern for anything a system
    /// simulates: per-world simulation state belongs in a singleton component, as
    /// LocalPlayerSelection now is.
    /// </summary>
    public static class ClientBridgeRegistry
    {
        public static ISystemBridgeService Service { get; set; }
    }
}
