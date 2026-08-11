using Unity.Entities;

namespace FourCorners.Scripts.Components.Connection
{
    /// <summary>
    /// Marks the single client world that owns the managed scene loader and the UI —
    /// i.e. <see cref="Unity.NetCode.ClientServerBootstrap.ClientWorld"/>.
    ///
    /// Secondary full clients and ThinClient worlds (Multiplayer Play Mode) do NOT carry this tag:
    /// nothing drives their SubScene streaming, so they must not block on it.
    ///
    /// This exists so unmanaged ISystems can discriminate worlds with a plain entity query
    /// instead of probing for an attached managed service, which is not Burst-compatible and
    /// was silently true in every client world at once.
    /// </summary>
    public struct PresentationClientTag : IComponentData { }
}
