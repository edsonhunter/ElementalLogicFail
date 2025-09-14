using Unity.Entities;
using UnityEngine;

namespace ElementLogicFail.Scripts.Authoring.Pool
{
    public class ElementPoolAuthoring : MonoBehaviour
    {
        private class ElementPoolAuthoringBaker : Baker<ElementPoolAuthoring>
        {
            public override void Bake(ElementPoolAuthoring authoring)
            {
            }
        }
    }
}