using Unity.Entities;
using UnityEngine;

namespace ElementLogicFail.Scripts.Authoring.Spawner
{
    public class SpawnerBaker : MonoBehaviour
    {
        private class SpawnerBakerBaker : Baker<SpawnerBaker>
        {
            public override void Bake(SpawnerBaker authoring)
            {
            }
        }
    }
}