using Unity.Entities;
using UnityEngine;

namespace ElementLogicFail.Scripts.Authoring.Bounds
{
    public class BoundsAuthoring : MonoBehaviour
    {
        private class BoundsAuthoringBaker : Baker<BoundsAuthoring>
        {
            public override void Bake(BoundsAuthoring authoring)
            {
            }
        }
    }
}