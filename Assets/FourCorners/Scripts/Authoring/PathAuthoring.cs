using System.Collections.Generic;
using FourCorners.Scripts.Components.Path;
using Unity.Entities;
using UnityEngine;

namespace FourCorners.Scripts.Authoring
{
    public class PathAuthoring : MonoBehaviour
    {
        public List<Transform> Waypoints;

        public class PathAuthoringBaker : Baker<PathAuthoring>
        {
            public override void Bake(PathAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);

                AddComponent(entity, new PathFollower { CurrentIndex = 0 });

                var buffer = AddBuffer<PathWaypoint>(entity);
                if (authoring.Waypoints != null)
                {
                    foreach (var wp in authoring.Waypoints)
                    {
                        if (wp != null)
                        {
                            buffer.Add(new PathWaypoint { Position = wp.position });
                        }
                    }
                }
            }
        }
    }
}
