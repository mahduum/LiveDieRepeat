using Rand = UnityEngine.Random;
using Authoring;
using Runtime;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace BakeWorld
{
    [WorldSystemFilter(WorldSystemFilterFlags.BakingSystem)]
    [UpdateAfter(typeof(ZoneGraphBuildSystem))]
    public partial struct ZoneGraphDebugSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            //state.RequireForUpdate<LaneProfileComponent>();
            state.RequireForUpdate<ZoneGraphData>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var deltaTime = SystemAPI.Time.DeltaTime;
            //just for the main spline debug:
            foreach (var buffer in SystemAPI.Query<DynamicBuffer<ZoneShapePoint>>().WithAll<RegisteredShapeComponent, LaneProfileComponent>())
            {
                Debug.Log($"Drawing for points in count: {buffer.Length}");
                var shapes = buffer.ToNativeArray(state.WorldUpdateAllocator);
                for (var index = 1; index < shapes.Length; index++)
                {
                    var previous = shapes[index - 1];
                    var current = shapes[index];
                    Debug.DrawLine(previous.Position, current.Position, Color.magenta, 10); //todo use lane profiles or other buffer
                    // var arrowDashDirRight = math.mul(quaternion.AxisAngle(current.Up, 0.785f), current.Right);
                    // var arrowRightArmEnd = current.Position + arrowDashDirRight;
                    // var arrowDashDirLeft = math.mul(quaternion.AxisAngle(current.Up, 2.355f), current.Right);
                    // var arrowLeftArmEnd = current.Position + arrowDashDirLeft;
                    // Debug.DrawLine(current.Position, arrowRightArmEnd, Color.green, 10);
                    // Debug.DrawLine(current.Position, arrowLeftArmEnd, Color.green, 10);
                }
            }
            //todo configure lane profile, on bake retrieve the data
            //on bake should read the interface
            //for all lanes:
            var zoneGraphData = SystemAPI.GetSingleton<ZoneGraphData>();
            var allLanePointsLength = zoneGraphData.Storage.Value.LanePoints.Length;
            Debug.Log($"All lane points count: {allLanePointsLength}");

            ref var allLanePoints = ref zoneGraphData.Storage.Value.LanePoints;
            ref var allLaneTangents = ref zoneGraphData.Storage.Value.LaneTangentVectors;
            ref var allLaneUps = ref zoneGraphData.Storage.Value.LaneUpVectors;

            if (allLaneTangents.Length != allLanePointsLength || allLaneUps.Length != allLanePointsLength)
            {
                Debug.LogError("Data arrays should be of equal length!");
            }
            
            if (allLanePointsLength < 1) return;
            for (int i = 1; i < allLanePointsLength; i++)//todo will get better drawing if drawing per lane, then lanes won't be connected and arrows will be inside bounds
            {
                var previous = allLanePoints[i - 1];
                var current = allLanePoints[i];
                var randomColor = new Color(Rand.value, Rand.value, Rand.value);
                Debug.DrawLine(previous, current, randomColor, 10);
                var up = allLaneUps[i];
                var forward = allLaneTangents[i];
                var rightArrowArmDir = math.mul(quaternion.AxisAngle(up, math.radians(135f)), forward);
                Debug.DrawLine(current, rightArrowArmDir + current, randomColor, 10);//todo scale arrow
                var leftArrowArmDir = math.mul(quaternion.AxisAngle(up, math.radians(225f)), forward);
                Debug.DrawLine(current, leftArrowArmDir + current, randomColor, 10);//todo scale arrow
            }

            ref var boundaryPoints = ref zoneGraphData.Storage.Value.BoundaryPoints;
            var boundaryPointsLength = boundaryPoints.Length;
            if (boundaryPointsLength < 1) return;
            for (int i = 1; i < boundaryPointsLength; i++)
            {
                var previous = boundaryPoints[i - 1];
                var current = boundaryPoints[i];
                Debug.DrawLine(previous, current, Color.cyan, 10);//todo there is only internal boundary
            }
            
            //close bounds
            var start = boundaryPoints[0];
            var end = boundaryPoints[boundaryPointsLength - 1];
            Debug.DrawLine(start, end, Color.cyan, 10);

        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {

        }
    }
}