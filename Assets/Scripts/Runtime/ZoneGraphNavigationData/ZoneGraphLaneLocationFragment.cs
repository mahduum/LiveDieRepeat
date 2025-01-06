using System;
using Data.Quantization;
using Runtime.ZoneGraphData;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Runtime.ZoneGraphNavigationData
{
    // TODO: translate tags for lanes too
    // Shared Component for FMassZoneGraphNavigationParameters
    // public struct FMassZoneGraphNavigationParameters : ISharedComponentData
    // {
    //     // Replace Unreal's FZoneGraphTagFilter with a Unity equivalent (custom type)
    //     public ZoneGraphTagFilter LaneFilter;  // Assuming you have a corresponding type for this in Unity
    //
    //     // Query radius when trying to find the nearest lane
    //     public float QueryRadius;
    //
    //     // Constructor to initialize values
    //     public FMassZoneGraphNavigationParameters(ZoneGraphTagFilter laneFilter, float queryRadius)
    //     {
    //         LaneFilter = laneFilter;
    //         QueryRadius = queryRadius;
    //     }
    // }

    public struct FMassZoneGraphPathRequestFragment : IComponentData
    {
        // Short path request handle to current lane
        public ZoneGraphShortPathRequest PathRequest;

        public FMassZoneGraphPathRequestFragment(ZoneGraphShortPathRequest pathRequest)
        {
            PathRequest = pathRequest;
        }
    }
    
    public struct ZoneGraphLaneLocationFragment : IComponentData
    {
        /** Handle to current lane. */
        public ZoneGraphLaneHandle LaneHandle;
        
        /** Distance along current lane. */
        public float DistanceAlongLane;
	
        /** Cached lane length, used for clamping and testing if at end of lane. */
        public float LaneLength;
    }
    
    //todo cached lane and functions
    
    public struct ZoneGraphCachedLaneFragment : IComponentData
    {
        public const int MaxPoints = 5;//6?
    
        public float LaneLength;
        public Int16Real LaneWidth;
        public Int16Real LaneLeftSpace;
        public Int16Real LaneRightSpace;
        public ushort CacheID;
        public byte NumPoints;
    
        public ZoneGraphLaneHandle LaneHandle;
        public NativeArray<float3> LanePoints; // Equivalent to TStaticArray<FVector, MaxPoints>
        public NativeArray<Snorm8Vector2D> LaneTangentVectors; // Equivalent to TStaticArray<Snorm8Vector2D, MaxPoints>
        public NativeArray<Int16Real10> LanePointProgressions; // Equivalent to TStaticArray<Int16Real10, MaxPoints>
    
        public void Reset()
        {
            LaneHandle.Reset();
            LaneLength = 0.0f;
            LaneWidth = new Int16Real(0.0f);
            NumPoints = 0;
        }
    
        public void CacheLaneData(ref ZoneGraphStorage zoneGraphStorage, ZoneGraphLaneHandle currentLaneHandle,
                                   float currentDistanceAlongLane, float targetDistanceAlongLane, float inflateDistance)
        {
            ZoneLaneData lane = zoneGraphStorage.Lanes[currentLaneHandle.Index];
    
            float startDistance = math.min(currentDistanceAlongLane, targetDistanceAlongLane);
            float endDistance = math.max(currentDistanceAlongLane, targetDistanceAlongLane);
            float currentLaneLength = zoneGraphStorage.LanePointProgressions[lane.PointsEnd - 1];
    
            // If cached data contains the request part of the lane, early out.
            float inflatedStartDistance = math.max(0.0f, startDistance - inflateDistance);
            float inflatedEndDistance = math.min(endDistance + inflateDistance, currentLaneLength);
            
            if (LaneHandle.Equals(currentLaneHandle) && NumPoints > 0
                && inflatedStartDistance >= LanePointProgressions[0].Get()
                && inflatedEndDistance <= LanePointProgressions[NumPoints - 1].Get())
            {
                return;
            }
    
            Reset();
            CacheID++;
    
            LaneHandle = currentLaneHandle;
            LaneWidth = new Int16Real(lane.Width);
            LaneLength = currentLaneLength;
    
            int laneNumPoints = lane.PointsEnd - lane.PointsBegin;
            if (laneNumPoints <= MaxPoints)
            {
                // If we can fit all the lane's points, just do a copy.
                NumPoints = (byte)laneNumPoints;
                for (int index = 0; index < NumPoints; index++)
                {
                    LanePoints[index] = zoneGraphStorage.LanePoints[lane.PointsBegin + index];

                    var tangentVector = zoneGraphStorage.LaneTangentVectors[lane.PointsBegin + index];
                    LaneTangentVectors[index] = new Snorm8Vector2D(new float2(tangentVector.x, tangentVector.y));
                    LanePointProgressions[index] = new Int16Real10(zoneGraphStorage.LanePointProgressions[lane.PointsBegin + index]);
                }
            }
            else
            {
                // Find the segment of the lane that is important and copy that.
                ZoneGraphQuery.CalculateLaneSegmentIndexAtDistance(ref zoneGraphStorage, currentLaneHandle.Index, startDistance, out var startSegmentIndex);
                ZoneGraphQuery.CalculateLaneSegmentIndexAtDistance(ref zoneGraphStorage, currentLaneHandle.Index, endDistance, out var endSegmentIndex);
    
                // Expand if close to start of a segment start.
                if ((startSegmentIndex - 1) >= lane.PointsBegin && (startDistance - inflateDistance) < zoneGraphStorage.LanePointProgressions[startSegmentIndex])
                {
                    startSegmentIndex--;
                }
                // Expand if close to end segment end.
                if ((endSegmentIndex + 1) < (lane.PointsEnd - 2) && (endDistance + inflateDistance) > zoneGraphStorage.LanePointProgressions[endSegmentIndex + 1])
                {
                    endSegmentIndex++;
                }
    
                NumPoints = (byte)math.min((endSegmentIndex - startSegmentIndex) + 2, MaxPoints);
    
                for (int index = 0; index < NumPoints; index++)
                {
                    if ((startSegmentIndex + index) >= lane.PointsBegin && (startSegmentIndex + index) < lane.PointsEnd)
                    {
                        throw new IndexOutOfRangeException("Segment index is outside points range.");
                    }

                    LanePoints[index] = zoneGraphStorage.LanePoints[startSegmentIndex + index];

                    var tangentVector = zoneGraphStorage.LaneTangentVectors[startSegmentIndex + index];
                    LaneTangentVectors[index] = new Snorm8Vector2D(new float2(tangentVector.x, tangentVector.y));
                    LanePointProgressions[index] = new Int16Real10(zoneGraphStorage.LanePointProgressions[startSegmentIndex + index]);
                }
            }
    
            // Calculate extra space around the lane on adjacent lanes.
            NativeList<ZoneGraphLinkedLane> linkedLanes = new NativeList<ZoneGraphLinkedLane>(0, Allocator.Temp); // Temporary, replace with actual query.
            ZoneGraphQuery.GetLinkedLanes(ref zoneGraphStorage, currentLaneHandle.Index, ZoneLaneLinkType.Adjacent, ZoneLaneLinkFlags.Left | ZoneLaneLinkFlags.Right, ZoneLaneLinkFlags.None, linkedLanes);
    
            float adjacentLeftWidth = 0.0f;
            float adjacentRightWidth = 0.0f;
            foreach (var linkedLane in linkedLanes)
            {
                if (linkedLane.HasFlags(ZoneLaneLinkFlags.Left))
                {
                    var adjacentLane = zoneGraphStorage.Lanes[linkedLane.DestinationLaneHandle.Index];
                    adjacentLeftWidth += adjacentLane.Width;
                }
                else if (linkedLane.HasFlags(ZoneLaneLinkFlags.Right))
                {
                    var adjacentLane = zoneGraphStorage.Lanes[linkedLane.DestinationLaneHandle.Index];
                    adjacentRightWidth += adjacentLane.Width;
                }
            }
            LaneLeftSpace = new Int16Real(adjacentLeftWidth);
            LaneRightSpace = new Int16Real(adjacentRightWidth);
        }
    
        public int FindSegmentIndexAtDistance(float distanceAlongPath)
        {
            int segmentIndex = 0;
            while (segmentIndex < (NumPoints - 2))
            {
                if (distanceAlongPath < LanePointProgressions[segmentIndex + 1].Get())
                {
                    break;
                }
                segmentIndex++;
            }
    
            return segmentIndex;
        }
    
        public float GetInterpolationTimeOnSegment(int segmentIndex, float distanceAlongPath)
        {
            // Perform interpolation logic similar to Unreal's GetInterpolationTimeOnSegment
            float startDistance = LanePointProgressions[segmentIndex].Get();
            float endDistance = LanePointProgressions[segmentIndex + 1].Get();
            float segLength = endDistance - startDistance;
            float invSegLength = (segLength > math.EPSILON) ? 1.0f / segLength : 0.0f;
            return math.clamp((distanceAlongPath - startDistance) * invSegLength, 0.0f, 1.0f);
        }
    
        public void InterpolatePointAndTangentOnSegment(int segmentIndex, float distanceAlongPath, out float3 outPoint, out float3 outTangent)
        {
            float t = GetInterpolationTimeOnSegment(segmentIndex, distanceAlongPath);
            outPoint = math.lerp(LanePoints[segmentIndex], LanePoints[segmentIndex + 1], t);
            outTangent = new float3(math.lerp(LaneTangentVectors[segmentIndex].Get(), LaneTangentVectors[segmentIndex + 1].Get(), t), 0.0f);
        }
    
        public float3 InterpolatePointOnSegment(int segmentIndex, float distanceAlongPath)
        {
            float t = GetInterpolationTimeOnSegment(segmentIndex, distanceAlongPath);
            return math.lerp(LanePoints[segmentIndex], LanePoints[segmentIndex + 1], t);
        }
    
        public void GetPointAndTangentAtDistance(float distanceAlongPath, out float3 outPoint, out float3 outTangent)
        {
            if (NumPoints == 0)
            {
                outPoint = float3.zero;
                outTangent = new float3(0, 0, 1);
                return;
            }
            if (NumPoints == 1)
            {
                outPoint = LanePoints[0];
                outTangent = new float3(LaneTangentVectors[0].Get(), 0.0f);
                return;
            }
    
            int segmentIndex = FindSegmentIndexAtDistance(distanceAlongPath);
            InterpolatePointAndTangentOnSegment(segmentIndex, distanceAlongPath, out outPoint, out outTangent);
        }
    
        public float3 GetPointAtDistance(float distanceAlongPath)
        {
            if (NumPoints == 0)
            {
                return float3.zero;
            }
            if (NumPoints == 1)
            {
                return LanePoints[0];
            }
    
            int segmentIndex = FindSegmentIndexAtDistance(distanceAlongPath);
            return InterpolatePointOnSegment(segmentIndex, distanceAlongPath);
        }
    
        public bool IsDistanceAtLaneExtrema(float distance)
        {
            const float epsilon = 0.1f;
            return distance <= epsilon || (distance - LaneLength) >= -epsilon;
        }
    }
    
    public struct MassZoneGraphPathPoint
    {
        public float3 Position;
        public Snorm8Vector2D Tangent;
        public Int16Real10 DistanceAlongLane;
        public Int16Real Distance;
        public byte bOffLane;
        public byte bIsLaneExtrema;
    }
    
    public struct MassZoneGraphShortPathFragment : IComponentData
    {
        public const int MaxPoints = 3;
    
        public ZoneGraphLaneHandle DebugLaneHandle;
        public ZoneGraphLaneHandle NextLaneHandle;
        public float ProgressDistance;
        public NativeArray<MassZoneGraphPathPoint> Points;
        public ZoneLaneLinkType NextExitLinkType;
        public byte NumPoints;
        //public EMassMovementAction EndOfPathIntent;
        public byte bMoveReverse;
        public byte bPartialResult;
        public byte bDone;
    
        public void Reset()
        {
            // Empty body
        }
    
        public bool RequestPath(ZoneGraphCachedLaneFragment cachedLane, ZoneGraphShortPathRequest request, float currentDistanceAlongLane, float agentRadius)
        {
            return false;
        }
    
        public bool RequestStand(ZoneGraphCachedLaneFragment cachedLane, float currentDistanceAlongLane, float3 currentPosition)
        {
            return false;
        }
    
        public bool IsDone()
        {
            return NumPoints == 0 || bDone != 0;
        }
    }
}