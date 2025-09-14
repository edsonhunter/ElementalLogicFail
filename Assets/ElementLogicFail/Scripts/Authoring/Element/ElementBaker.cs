using Unity.Entities;
using UnityEngine;

namespace ElementLogicFail.Scripts.Authoring.Element
{
    public class ElementBaker : MonoBehaviour
    {
        private class ElementBakerBaker : Baker<ElementBaker>
        {
            public override void Bake(ElementBaker authoring)
            {
            }
        }
    }
}