using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

namespace Authoring
{
    public class SpawnObjectAuthoring : MonoBehaviour
    {
        private class SpawnObjectBaker : Baker<SpawnObjectAuthoring>
        {
            public override void Bake(SpawnObjectAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent<RayCastSpawned>(entity);
            }
        }
    }
    
    [WriteGroup(typeof(LocalToWorld))]
    public struct RayCastSpawned : IComponentData
    {
    
    }
}