using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Transforms;
using UnityEngine;
using RaycastHit = Unity.Physics.RaycastHit;

namespace Physics.Systems
{
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [UpdateAfter(typeof(PhysicsSystemGroup))]
    public partial struct PhysicsRaycastSpawnSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
            state.RequireForUpdate<PhysicsWorldSingleton>();
            state.RequireForUpdate<SpawnObject>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            PhysicsWorldSingleton physicsWorldSingleton = SystemAPI.GetSingleton<PhysicsWorldSingleton>();
            NativeList<RaycastHit> hits = new NativeList<RaycastHit>(state.WorldUpdateAllocator);
            var spawnObjectBuffer = SystemAPI.GetSingletonBuffer<SpawnObject>();
            var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);

            foreach (var placementInput in SystemAPI.Query<DynamicBuffer<PlacementInput>>())
            {
                for (var index = 0; index < placementInput.Length; index++)
                {
                    var placement = placementInput[index];
                    if (physicsWorldSingleton.CastRay(placement.Value, out RaycastHit hit))
                    {
                        hits.Add(hit);
                        
                        if (index < placementInput.Length - 1) continue;
                        
                        Debug.Log($"Instantiating entity for index: {index}, buffer length: {placementInput.Length}");
                        var entity = ecb.Instantiate(spawnObjectBuffer[placement.SpawnObjectIndex].Prefab);
                        ecb.SetComponent(entity, new LocalToWorld()
                        {
                            Value = float4x4.TRS(
                                hit.Position,
                                quaternion.identity,
                                new float3(1, 1, 1))
                        });

                        // ecb.SetComponent(entity, new LocalTransform()
                        // {
                        //     
                        //        Position = hit.Position,
                        //        Rotation = quaternion.identity, 
                        //        Scale = 1f
                        // });
                    }
                }

                if (hits.Length > 1)
                {
                    for (int i = 1; i < hits.Length; i++)
                    {
                        Debug.DrawLine(hits[i-1].Position, hits[i].Position, Color.magenta);
                    }
                }

                //if (placementInput.Length > 5)
                //{
                    placementInput.Clear();
                //}
            }
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
            
        }
    }
}