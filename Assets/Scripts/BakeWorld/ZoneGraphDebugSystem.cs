using Authoring;
using Runtime;
using Unity.Burst;
using Unity.Entities;
using UnityEngine;

namespace BakeWorld
{
    [WorldSystemFilter(WorldSystemFilterFlags.BakingSystem)]
    public partial struct ZoneGraphDebugSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var deltaTime = SystemAPI.Time.DeltaTime;
            foreach (var buffer in SystemAPI.Query<DynamicBuffer<ZoneShapePoint>>().WithAll<RegisteredShapeComponent>())
            {
                Debug.Log($"Drawing for points in count: {buffer.Length}");
                var shapes = buffer.ToNativeArray(state.WorldUpdateAllocator);
                for (var index = 1; index < shapes.Length; index++)
                {
                    var previous = shapes[index - 1];
                    var current = shapes[index];
                    Debug.DrawLine(previous.Position, current.Position, Color.magenta, 10);
                }
            }
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {

        }
    }
}