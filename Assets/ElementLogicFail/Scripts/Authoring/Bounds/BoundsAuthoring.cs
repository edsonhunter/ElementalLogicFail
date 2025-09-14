using Unity.Entities;
using UnityEngine;

namespace ElementLogicFail.Scripts.Authoring.Bounds
{
    public class BoundsAuthoring : MonoBehaviour
    {
        public Vector3 min = new Vector3(-10f, 0f, -10f);
        public Vector3 max = new Vector3(10f, 0f, 10f);
    }
}