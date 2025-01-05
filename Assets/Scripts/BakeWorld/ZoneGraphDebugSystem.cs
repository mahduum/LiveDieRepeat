using Rand = UnityEngine.Random;
using Authoring;
using Runtime.ZoneGraphData;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using Random = Unity.Mathematics.Random;

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
            //todo: how often to update? this will run only on editor updates, what if I need it constantly? how are beziers made in splines example?
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
            ref var lanes = ref zoneGraphData.Storage.Value.Lanes;
            ref var laneLinks = ref zoneGraphData.Storage.Value.LaneLinks;
            Debug.Log($"All lane links count: {laneLinks.Length}");

            if (allLaneTangents.Length != allLanePointsLength || allLaneUps.Length != allLanePointsLength)
            {
                Debug.LogError("Data arrays should be of equal length!");
            }
            
            if (allLanePointsLength < 1) return;

            Random random = new Random();
            random.InitState((uint)allLanePoints.Length);
            
            for (int i = 0; i < lanes.Length; i++)
            {
                var currentLane = lanes[i];
                if (currentLane.PointsEnd - currentLane.PointsBegin < 2)
                {
                    continue;
                }
                
                var laneColor= new Color(random.NextFloat(0, 1), random.NextFloat(0, 1), random.NextFloat(0, 1));
                
                for (int j = currentLane.PointsBegin + 1; j < currentLane.PointsEnd; j++)
                {
                    var previous = allLanePoints[j - 1];
                    var current = allLanePoints[j];
                    
                    var up = allLaneUps[j];
                    var forward = allLaneTangents[j];
                    DrawDebugArrow(previous, current, up, forward, laneColor);
                }

                if (currentLane.LinksEnd - currentLane.LinksBegin < 1)
                {
                    continue;
                }
                
                for (int j = currentLane.LinksBegin; j < currentLane.LinksEnd; j++)
                {
                    var linkData = laneLinks[j];
                    if ((linkData.Type & (ZoneLaneLinkType.Incoming | ZoneLaneLinkType.Outgoing)) == 0)
                    {
                       continue; 
                    }
                    
                    Debug.Log($"Lane index ({i}) links to destination lane: ({linkData.DestinationLaneIndex}), link type: ({(int)linkData.Type})");
                    //todo draw/connect intersections for lanes of the same type from different containers (are these links but I forgot this?)
                }
                
                //todo get logic for finding next portion of data
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
            
            //close bounds, for now it will not connect all ends but only last and first boundary point for each container.
            var start = boundaryPoints[0];
            var end = boundaryPoints[boundaryPointsLength - 1];
            Debug.DrawLine(start, end, Color.cyan, 10);

            // for (int i = 0; i < laneLinks.Length; i++)
            // {
            //     var linkData = laneLinks[i];
            //     Debug.Log($"Destination lane index for current lane link index ({i}): ({linkData.DestinationLaneIndex})");
            //     var destinationLaneIndex = linkData.DestinationLaneIndex;
            //     if (destinationLaneIndex >= 0 && destinationLaneIndex < laneLinks.Length)
            //     {
            //         var destinationLaneLink = laneLinks[destinationLaneIndex];
            //         Debug.Log($"Lane link index from destination lane link index ({destinationLaneIndex}): ({destinationLaneLink.DestinationLaneIndex})");
            //
            //     }
            //     else
            //     {
            //         Debug.LogError($"Lane link index out of bounds. No matching link!");
            //     }
            //
            // }

        }

        private static void DrawDebugArrow(float3 from, float3 to, float3 up, float3 forward, Color color)
        {
            Debug.DrawLine(from, to, color, 10);
            var rightArrowArmDir = math.mul(quaternion.AxisAngle(up, math.radians(135f)), forward);
            Debug.DrawLine(to, rightArrowArmDir + to, color, 10);//todo scale arrow
            var leftArrowArmDir = math.mul(quaternion.AxisAngle(up, math.radians(225f)), forward);
            Debug.DrawLine(to, leftArrowArmDir + to, color, 10);//todo scale arrow
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {

        }
    }
}