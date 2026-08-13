using System.Threading.Tasks;

namespace FourCorners.Scripts.Services.Interface
{
    public interface IMultiplayerService : IService
    {
        Task AuthenticateAsync();

        // Direct IP/Port
        Task<bool> HostDirectGameAsync(ushort port);
        Task<bool> JoinDirectGameAsync(string ip, ushort port);

        // Unity Relay
        Task<string> HostRelayGameAsync(int maxPlayers);
        Task<bool> JoinRelayGameAsync(string joinCode);

        /// <summary>Closes every stream connection in the client and server worlds.</summary>
        void Disconnect();

        /// <summary>
        /// As <see cref="Disconnect()"/>, but optionally pumps each world once so the disconnect
        /// packet leaves before the process stops ticking. Only needed on application shutdown —
        /// otherwise the server has to wait out the connection timeout to notice the departure.
        /// </summary>
        void Disconnect(bool flushImmediately);
    }
}
