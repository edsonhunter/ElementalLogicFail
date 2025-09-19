using Unity.Entities;

namespace ElementLogicFail.Scripts.Components.Particles
{
    public struct ParticleEffectData : IComponentData
    {
        public float Lifetime;
        public float Timer;
    }
}