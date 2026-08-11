using System;
using System.Threading.Tasks;
using FourCorners.Scripts.Services.Interface;
using Unity.NetCode;
using Unity.Networking.Transport.Relay;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using UnityEngine;
using Unity.Networking.Transport;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using Unity.Services.Relay.Models;
using Unity.Collections;
using Unity.Entities;

namespace FourCorners.Scripts.Services
{
    public class MultiplayerService : IMultiplayerService
    {
        public void Init() { }

        public async Task AuthenticateAsync()
        {
            await UnityServices.InitializeAsync();
            if (!AuthenticationService.Instance.IsSignedIn)
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
                Debug.Log($"[Matchmaking] Signed in as: {AuthenticationService.Instance.PlayerId}");
            }
        }

        public async Task<string> HostRelayGameAsync(int maxPlayers)
        {
            try
            {
                // Local fallback path for single-player or host-only mode:
                // If maxPlayers < 2, don't create a Relay allocation. Instead, start a local direct game
                // using IPC/Socket and return a sentinel indicating local host mode.
                if (maxPlayers < 2)
                {
                    Debug.Log("[Matchmaking] Starting local direct game as fallback (no Relay).");
                    // Choose a default port for local IPC/Socket host
                    const ushort localPort = 7777;
                    bool localHostStarted = await HostDirectGameAsync(localPort);
                    if (!localHostStarted)
                    {
                        Debug.LogError("[Matchmaking] Failed to start local direct game fallback.");
                        return string.Empty;
                    }
                    // Sentinel to indicate local direct host was started. Client side should handle accordingly.
                    return "LOCAL_DIRECT";
                }

                // 1. Create relay allocation for the server
                var allocation = await RelayService.Instance.CreateAllocationAsync(maxPlayers - 1);
                var joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

                // 2. Create lobby so others can discover the game
                var options = new CreateLobbyOptions
                {
                    IsPrivate = false,
                    Data = new System.Collections.Generic.Dictionary<string, DataObject>
                    {
                        { "JoinCode", new DataObject(DataObject.VisibilityOptions.Public, joinCode) }
                    }
                };
                await LobbyService.Instance.CreateLobbyAsync("FourCornersLobby", maxPlayers, options);
                Debug.Log($"[Matchmaking] Lobby created. Join Code: {joinCode}");

                // 3. Server uses the allocation relay data to LISTEN
                var serverRelayData = allocation.ToRelayServerData("dtls");

                // 4. The host's own local Client must JOIN its own relay via the join code.
                var joinAllocation = await RelayService.Instance.JoinAllocationAsync(joinCode);
                var clientRelayData = joinAllocation.ToRelayServerData("dtls");

                // 5. Set up Server driver with server relay data
                var serverWorld = ClientServerBootstrap.ServerWorld;
                var serverNetDebug = serverWorld.EntityManager.CreateEntityQuery(typeof(NetDebug)).GetSingleton<NetDebug>();
                var driverStore = new NetworkDriverStore();
                var serverRelayConstructor = new RelayDriverConstructor(serverRelayData, default, false);
                serverRelayConstructor.CreateServerDriver(serverWorld, ref driverStore, serverNetDebug);

                var serverDriverQuery = serverWorld.EntityManager.CreateEntityQuery(typeof(NetworkStreamDriver));
                serverDriverQuery.GetSingletonRW<NetworkStreamDriver>().ValueRW.ResetDriverStore(serverWorld.Unmanaged, ref driverStore);

                var serverListenEntity = serverWorld.EntityManager.CreateEntity();
                serverWorld.EntityManager.AddComponentData(serverListenEntity, new NetworkStreamRequestListen { Endpoint = NetworkEndpoint.AnyIpv4 });

                // 6. Set up Client driver with CLIENT relay data
                var clientWorld = ClientServerBootstrap.ClientWorld;
                var clientNetDebug = clientWorld.EntityManager.CreateEntityQuery(typeof(NetDebug)).GetSingleton<NetDebug>();
                var clientDriverStore = new NetworkDriverStore();
                var clientRelayConstructor = new RelayDriverConstructor(default, clientRelayData, false);
                clientRelayConstructor.CreateClientDriver(clientWorld, ref clientDriverStore, clientNetDebug);

                var clientDriverQuery = clientWorld.EntityManager.CreateEntityQuery(typeof(NetworkStreamDriver));
                clientDriverQuery.GetSingletonRW<NetworkStreamDriver>().ValueRW.ResetDriverStore(clientWorld.Unmanaged, ref clientDriverStore);

                var clientConnectEntity = clientWorld.EntityManager.CreateEntity();
                clientWorld.EntityManager.AddComponentData(clientConnectEntity, new NetworkStreamRequestConnect { Endpoint = clientRelayData.Endpoint });

                return joinCode;
            }
            catch (Exception e)
            {
                Debug.LogError($"[Matchmaking] Failed to Host Game: {e.Message}");
                return string.Empty;
            }
        }


        public async Task<bool> JoinRelayGameAsync(string joinCode)
        {
            try
            {
                var joinAllocation = await RelayService.Instance.JoinAllocationAsync(joinCode);
                var relayServerData = joinAllocation.ToRelayServerData("dtls");

                if (ClientServerBootstrap.ServerWorld != null && ClientServerBootstrap.ServerWorld.IsCreated)
                {
                    ClientServerBootstrap.ServerWorld.Dispose();
                }

                var clientWorld = ClientServerBootstrap.ClientWorld;
                var clientNetDebug = clientWorld.EntityManager.CreateEntityQuery(typeof(NetDebug)).GetSingleton<NetDebug>();
                
                var relayConstructor = new RelayDriverConstructor(default, relayServerData, false);
                var clientDriverStore = new NetworkDriverStore();
                relayConstructor.CreateClientDriver(clientWorld, ref clientDriverStore, clientNetDebug);
                
                var clientDriverQuery = clientWorld.EntityManager.CreateEntityQuery(typeof(NetworkStreamDriver));
                clientDriverQuery.GetSingletonRW<NetworkStreamDriver>().ValueRW.ResetDriverStore(clientWorld.Unmanaged, ref clientDriverStore);

                var clientConnectEntity = clientWorld.EntityManager.CreateEntity();
                clientWorld.EntityManager.AddComponentData(clientConnectEntity, new NetworkStreamRequestConnect { Endpoint = relayServerData.Endpoint });

                Debug.Log($"[Matchmaking] Joined Game with Code: {joinCode}");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[Matchmaking] Failed to Join Game: {e.Message}");
                return false;
            }
        }

        public Task<bool> HostDirectGameAsync(ushort port)
        {
            try
            {
                var serverWorld = ClientServerBootstrap.ServerWorld;
                var serverNetDebug = serverWorld.EntityManager.CreateEntityQuery(typeof(NetDebug)).GetSingleton<NetDebug>();
                var driverStore = new NetworkDriverStore();
                
                var serverConstructor = new IPCAndSocketDriverConstructor();
                serverConstructor.CreateServerDriver(serverWorld, ref driverStore, serverNetDebug);

                var serverDriverQuery = serverWorld.EntityManager.CreateEntityQuery(typeof(NetworkStreamDriver));
                serverDriverQuery.GetSingletonRW<NetworkStreamDriver>().ValueRW.ResetDriverStore(serverWorld.Unmanaged, ref driverStore);

                var serverListenEntity = serverWorld.EntityManager.CreateEntity();
                serverWorld.EntityManager.AddComponentData(serverListenEntity, new NetworkStreamRequestListen { Endpoint = NetworkEndpoint.AnyIpv4.WithPort(port) });

                var clientWorld = ClientServerBootstrap.ClientWorld;
                var clientNetDebug = clientWorld.EntityManager.CreateEntityQuery(typeof(NetDebug)).GetSingleton<NetDebug>();
                var clientDriverStore = new NetworkDriverStore();
                
                var clientConstructor = new IPCAndSocketDriverConstructor();
                clientConstructor.CreateClientDriver(clientWorld, ref clientDriverStore, clientNetDebug);

                var clientDriverQuery = clientWorld.EntityManager.CreateEntityQuery(typeof(NetworkStreamDriver));
                clientDriverQuery.GetSingletonRW<NetworkStreamDriver>().ValueRW.ResetDriverStore(clientWorld.Unmanaged, ref clientDriverStore);

                var clientConnectEntity = clientWorld.EntityManager.CreateEntity();
                clientWorld.EntityManager.AddComponentData(clientConnectEntity, new NetworkStreamRequestConnect { Endpoint = NetworkEndpoint.LoopbackIpv4.WithPort(port) });

                Debug.Log($"[Matchmaking] Hosted Direct Game on Port: {port}");
                return Task.FromResult(true);
            }
            catch (Exception e)
            {
                Debug.LogError($"[Matchmaking] Failed to Host Direct Game: {e.Message}");
                return Task.FromResult(false);
            }
        }

        public Task<bool> JoinDirectGameAsync(string ip, ushort port)
        {
            try
            {
                if (!NetworkEndpoint.TryParse(ip, port, out NetworkEndpoint endpoint))
                {
                    Debug.LogError($"[Matchmaking] Invalid IP/Port: {ip}:{port}");
                    return Task.FromResult(false);
                }

                if (ClientServerBootstrap.ServerWorld != null && ClientServerBootstrap.ServerWorld.IsCreated)
                {
                    ClientServerBootstrap.ServerWorld.Dispose();
                }

                var clientWorld = ClientServerBootstrap.ClientWorld;
                var clientNetDebug = clientWorld.EntityManager.CreateEntityQuery(typeof(NetDebug)).GetSingleton<NetDebug>();
                var clientDriverStore = new NetworkDriverStore();
                
                var clientConstructor = new IPCAndSocketDriverConstructor();
                clientConstructor.CreateClientDriver(clientWorld, ref clientDriverStore, clientNetDebug);

                var clientDriverQuery = clientWorld.EntityManager.CreateEntityQuery(typeof(NetworkStreamDriver));
                clientDriverQuery.GetSingletonRW<NetworkStreamDriver>().ValueRW.ResetDriverStore(clientWorld.Unmanaged, ref clientDriverStore);

                var clientConnectEntity = clientWorld.EntityManager.CreateEntity();
                clientWorld.EntityManager.AddComponentData(clientConnectEntity, new NetworkStreamRequestConnect { Endpoint = endpoint });

                Debug.Log($"[Matchmaking] Requesting Direct Join to {endpoint}");
                return Task.FromResult(true);
            }
            catch (Exception e)
            {
                Debug.LogError($"[Matchmaking] Failed to Join Direct Game: {e.Message}");
                return Task.FromResult(false);
            }
        }
        public void Disconnect()
        {
            Debug.Log("[MultiplayerService] Disconnecting from session.");

            RequestDisconnect(ClientServerBootstrap.ClientWorld);
            RequestDisconnect(ClientServerBootstrap.ServerWorld);
        }

        /// <summary>
        /// Asks Netcode to close every stream connection in the given world.
        ///
        /// The previous version also allocated a NativeArray of connections and immediately
        /// disposed it, commented as "flush pending connections before tear-down" — that does
        /// nothing at all, and the real work was then done by a second identical query.
        /// </summary>
        private static void RequestDisconnect(World world)
        {
            if (world is not { IsCreated: true }) return;

            var query = world.EntityManager.CreateEntityQuery(typeof(NetworkStreamConnection));
            if (query.IsEmpty) return;

            using var connections = query.ToEntityArray(Allocator.Temp);
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var connection in connections)
                ecb.AddComponent<NetworkStreamRequestDisconnect>(connection);

            ecb.Playback(world.EntityManager);
            ecb.Dispose();
        }
    }
}
