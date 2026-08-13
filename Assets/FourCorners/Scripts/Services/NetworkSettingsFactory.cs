using Unity.NetCode;
using Unity.Networking.Transport;

namespace FourCorners.Scripts.Services
{
    /// <summary>
    /// Builds the transport settings for every driver this project creates.
    ///
    /// The only thing it changes from NetCode's defaults is <see cref="DisconnectTimeoutMS"/>.
    /// A client that is killed rather than closed — a window shut, a process ended, a cable
    /// pulled — never gets to send a disconnect packet, so the server can only notice by timing
    /// the connection out. Transport's default is 30 s, which reads as "leaving does nothing"
    /// while testing and leaves a corner occupied by a player who is long gone.
    ///
    /// Applied by mutating the existing <see cref="NetworkConfigParameter"/> rather than calling
    /// WithNetworkConfigParameters: every argument of that method has a default, so passing only
    /// the timeout would silently reset the rest — including the editor-only maxFrameTimeMS clamp
    /// that keeps a breakpoint from disconnecting everyone.
    ///
    /// The alternative route is a global NetCodeConfig asset, which is deliberately not used:
    /// it also feeds ClientServerTickRate, ClientTickRate, GhostSendSystemData and the bootstrap
    /// toggle into world creation, so it is a much wider blast radius than one timeout.
    /// </summary>
    public static class NetworkSettingsFactory
    {
        /// <summary>
        /// Long enough to survive an ordinary hitch, short enough that a departed player's corner
        /// is reusable while you are still looking at the screen.
        /// </summary>
        public const int DisconnectTimeoutMS = 5000;

        public static NetworkSettings ClientSettings()
        {
            var settings = DefaultDriverBuilder.GetNetworkClientSettings();
            ApplyDisconnectTimeout(ref settings);
            return settings;
        }

        public static NetworkSettings ServerSettings()
        {
            var settings = DefaultDriverBuilder.GetNetworkServerSettings();
            ApplyDisconnectTimeout(ref settings);
            return settings;
        }

        private static void ApplyDisconnectTimeout(ref NetworkSettings settings)
        {
            var config = settings.GetNetworkConfigParameters();
            config.disconnectTimeoutMS = DisconnectTimeoutMS;
            settings.AddRawParameterStruct(ref config);
        }
    }
}
