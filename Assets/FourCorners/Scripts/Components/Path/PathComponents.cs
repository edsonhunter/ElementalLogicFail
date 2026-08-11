using Unity.Entities;
using Unity.Mathematics;

namespace FourCorners.Scripts.Components.Path
{
    /// <summary>
    /// Position along the owning lane. PathFollowSystem advances CurrentIndex and wraps to 0 at
    /// the end — since every lane's last waypoint is its own base, that wrap *is* the
    /// "walk home and start over" behaviour.
    ///
    /// An IsReverse flag used to sit here from an earlier design where minions retraced their
    /// lane backwards. It was written once at bake time and never read.
    /// </summary>
    public struct PathFollower : IComponentData
    {
        public int CurrentIndex;
    }

    [InternalBufferCapacity(8)]
    public struct PathWaypoint : IBufferElementData
    {
        public float3 Position;
    }
}
