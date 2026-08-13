using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using Unity.Networking.Transport;

namespace FourCorners.Scripts.Systems.Connection
{
    /// <summary>
    /// Diagnostic mirror of every transport-level connect/disconnect, in every world.
    ///
    /// This is the ground truth for "did NetCode ever notice the drop?". ServerDisconnectSystem
    /// only reacts to <see cref="ConnectionState"/>, which the project adds itself — so a silent
    /// failure there is indistinguishable from the transport never raising the event at all.
    /// <see cref="NetworkStreamDriver.ConnectionEventsForTick"/> is the package's own disconnect
    /// API and reports connections the gameplay layer has never seen.
    ///
    /// The events list is refilled by NetworkGroupCommandBufferSystem, which runs OrderLast in
    /// NetworkReceiveSystemGroup (inside InitializationSystemGroup). Any SimulationSystemGroup
    /// system therefore reads the current tick; polling earlier would always be one tick stale.
    ///
    /// The one-shot driver dump answers the other half of the question: whether a given world is
    /// actually talking over Relay/UDP or IPC, and what timeout it was built with. Both are
    /// decided at runtime by which button the player pressed, so neither can be read off the
    /// project settings.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ServerSimulation |
                       WorldSystemFilterFlags.ThinClientSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderFirst = true)]
    public partial struct ConnectionEventLogSystem : ISystem
    {
        private bool _driversLogged;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NetworkStreamDriver>();
        }

        public void OnUpdate(ref SystemState state)
        {
            // Fetched RW: NetworkStreamDriver.DriverStore is a ref property into unmanaged
            // memory and the package requires RW access for any driver-store read.
            ref var networkStreamDriver = ref SystemAPI.GetSingletonRW<NetworkStreamDriver>().ValueRW;

            // Bound to a ref local: NetworkDriverStore is a large struct behind a ref-returning
            // property, which cannot be passed straight through as a ref argument.
            ref var driverStore = ref networkStreamDriver.DriverStore;

            // The store stays empty until MultiplayerService swaps in a real one on host/join,
            // so the dump cannot be done in OnCreate.
            if (!_driversLogged && driverStore.DriversCount > 0)
            {
                _driversLogged = true;
                LogDrivers(state.WorldUnmanaged.Name, ref driverStore);
            }

            foreach (var connectionEvent in networkStreamDriver.ConnectionEventsForTick)
            {
                UnityEngine.Debug.Log(
                    $"[ConnEvent][{state.WorldUnmanaged.Name}] {connectionEvent.ToFixedString()}");
            }
        }

        private static void LogDrivers(FixedString128Bytes worldName, ref NetworkDriverStore driverStore)
        {
            int lastDriverId = NetworkDriverStore.FirstDriverId + driverStore.DriversCount - 1;

            for (int driverId = NetworkDriverStore.FirstDriverId; driverId <= lastDriverId; driverId++)
            {
                // Copied to a local: GetNetworkConfigParameters takes `ref this`, and
                // CurrentSettings is a by-value property that cannot be passed by reference.
                var settings = driverStore.GetDriverRO(driverId).CurrentSettings;
                var config = settings.GetNetworkConfigParameters();

                UnityEngine.Debug.Log(
                    $"[ConnEvent][{worldName}] Driver {driverId}: " +
                    $"transport={driverStore.GetDriverType(driverId)}, " +
                    $"disconnectTimeoutMS={config.disconnectTimeoutMS}, " +
                    $"heartbeatTimeoutMS={config.heartbeatTimeoutMS}, " +
                    $"maxFrameTimeMS={config.maxFrameTimeMS}");
            }
        }
    }
}
