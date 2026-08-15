using Unity.Entities;

namespace FourCorners.Scripts.Components.Combat
{
    /// <summary>
    /// This entity is locked in a fight and has stopped travelling.
    ///
    /// Presence is the signal, not the contents: PathFollowSystem excludes anything carrying this,
    /// which is what makes "minions meet, fight, survivor walks on" fall out of the existing lane
    /// loop without a state machine.
    ///
    /// Acquired by EngagementAcquisitionSystem on contact and released by EngagementSystem when the target
    /// dies or gets too far away. It is added and removed structurally through the
    /// EndSimulation ECB, so it is always one frame behind the event that caused it — every
    /// consumer must therefore re-validate <see cref="Target"/> rather than trust it.
    /// </summary>
    public struct Engagement : IComponentData
    {
        /// <summary>
        /// Who we are fighting. May already be destroyed or out of range by the time it is read.
        ///
        /// Safe to store as an Entity because this is a component: entity references in
        /// components and buffers are remapped on entity-scene load. (A BlobAsset would not be.)
        /// </summary>
        public Entity Target;
    }
}
