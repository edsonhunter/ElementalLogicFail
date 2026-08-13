using Unity.Entities;
using Unity.NetCode;
using Unity.Networking.Transport.Relay;

namespace FourCorners.Scripts.Services
{
    public struct RelayDriverConstructor : INetworkStreamDriverConstructor
    {
        private RelayServerData _relayServerData;
        private RelayServerData _relayClientData;
        private bool _isServer;

        public RelayDriverConstructor(RelayServerData relayServerData, RelayServerData relayClientData, bool isServer)
        {
            _relayServerData = relayServerData;
            _relayClientData = relayClientData;
            _isServer = isServer;
        }

        public void CreateClientDriver(World world, ref NetworkDriverStore driverStore, NetDebug netDebug)
        {
            var settings = NetworkSettingsFactory.ClientSettings();
            if (!_isServer || ClientServerBootstrap.RequestedPlayType == ClientServerBootstrap.PlayType.Client)
            {
                settings.WithRelayParameters(ref _relayClientData);
                DefaultDriverBuilder.RegisterClientUdpDriver(world, ref driverStore, netDebug, settings);
            }
            else
            {
                DefaultDriverBuilder.RegisterClientIpcDriver(world, ref driverStore, netDebug, settings);
            }
        }

        public void CreateServerDriver(World world, ref NetworkDriverStore driverStore, NetDebug netDebug)
        {
            // IPC for local clients
            var ipcSettings = NetworkSettingsFactory.ServerSettings();
            DefaultDriverBuilder.RegisterServerIpcDriver(world, ref driverStore, netDebug, ipcSettings);

            // UDP + Relay for remote clients
            var relaySettings = NetworkSettingsFactory.ServerSettings();
            relaySettings.WithRelayParameters(ref _relayServerData);
            DefaultDriverBuilder.RegisterServerUdpDriver(world, ref driverStore, netDebug, relaySettings);
        }
    }
}
