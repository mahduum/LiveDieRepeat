using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

public class SpawnPrefabsRegister : MonoBehaviour
{
    [SerializeField] private List<GameObject> _spawnPrefabs;
    
    class SpawnPrefabsRegisterBaker : Baker<SpawnPrefabsRegister>
    {
        public override void Bake(SpawnPrefabsRegister authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);
            var buffer = AddBuffer<SpawnObject>(entity);

            foreach (var go in authoring._spawnPrefabs)
            {
                var subEntity = GetEntity(go, TransformUsageFlags.Dynamic);
                buffer.Add(new SpawnObject()
                {
                    Prefab = subEntity
                });
            }
        }
    }
}

public struct SpawnObject : IBufferElementData
{
    public Entity Prefab;
}