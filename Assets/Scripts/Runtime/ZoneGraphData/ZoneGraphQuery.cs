using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Runtime.ZoneGraphData
{
    public static class ZoneGraphQuery
    {
        public static float ClosestTimeOnSegment(float3 point, float3 startPoint, float3 endPoint)
        {
            float3 segment = endPoint - startPoint;
            float3 vectToPoint = point - startPoint;

            // Check if the closest point is before the start point
            float dot1 = math.dot(vectToPoint, segment);
            if (dot1 <= 0.0f)
            {
                return 0.0f;
            }

            // Check if the closest point is beyond the end point
            float dot2 = math.dot(segment, segment);
            if (dot2 <= dot1)
            {
                return 1.0f;
            }

            // Closest point is within the segment
            return dot1 / dot2;
        }
        
        public static bool GetLaneLength(ref ZoneGraphStorage storage, uint laneIndex, out float outLength)
        {
            // Access the lane data for the specified lane index
            ref var lane = ref storage.Lanes[(int)laneIndex];

            // Get the length of the lane from the last progression point
            outLength = storage.LanePointProgressions[lane.PointsEnd - 1];

            return true;
        }
        
        /*Used inside follow path system, when data on entities must be replenished.*/
        public static bool FindNearestLocationOnLane(ref ZoneGraphStorage storage, ZoneGraphLaneHandle laneHandle,
            float3 center, float rangeSqr, out ZoneGraphLaneLocation outLaneLocation, out float outDistanceSqr)
        {
            //todo ensure storage and handle - it may be handled differently in dots? there are ready solutions for it?
            //check if storage is created etc
            
            var lane = storage.Lanes[laneHandle.Index];

            float nearestDistanceSqr = rangeSqr;
            int nearestLaneSegment = 0;
            float nearestLaneSegmentT = 0.0f;
            float3 nearestLanePosition = float3.zero;
            bool result = false;
        
            for (int i = lane.PointsBegin; i < lane.PointsEnd - 1; i++)
            {
                float3 segStart = storage.LanePoints[i];
                float3 segEnd = storage.LanePoints[i + 1];
                
                // ClosestTimeOnSegment finds the parametric t (0 to 1) on the segment closest to the point
                float segT = ClosestTimeOnSegment(center, segStart, segEnd);
                float3 closestPt = math.lerp(segStart, segEnd, segT);
                float distSqr = math.distancesq(center, closestPt);
                
                if (distSqr < nearestDistanceSqr)
                {
                    nearestDistanceSqr = distSqr;
                    nearestLaneSegment = i;
                    nearestLaneSegmentT = segT;
                    nearestLanePosition = closestPt;
                    result = true;
                }
            }
        
            if (result)
            {
                outLaneLocation = new ZoneGraphLaneLocation
                {
                    LaneHandle = new ZoneGraphLaneHandle(
                        laneHandle.Index, storage.DataHandle
                    ),
                    LaneSegment = nearestLaneSegment,
                    DistanceAlongLane = math.lerp(
                        storage.LanePointProgressions[nearestLaneSegment],
                        storage.LanePointProgressions[nearestLaneSegment + 1],
                        nearestLaneSegmentT),
                    Position = nearestLanePosition,
                    Direction = math.normalize(
                        storage.LanePoints[nearestLaneSegment + 1] - 
                        storage.LanePoints[nearestLaneSegment]),
                    Tangent = math.normalize(
                        math.lerp(
                            storage.LaneTangentVectors[nearestLaneSegment],
                            storage.LaneTangentVectors[nearestLaneSegment + 1],
                            nearestLaneSegmentT)),
                    Up = math.normalize(
                        math.lerp(
                            storage.LaneUpVectors[nearestLaneSegment],
                            storage.LaneUpVectors[nearestLaneSegment + 1],
                            nearestLaneSegmentT))
                };
                outDistanceSqr = nearestDistanceSqr;
            }
            else
            {
                outLaneLocation = default;
                outLaneLocation.Reset();
                outDistanceSqr = 0.0f;
            }
            return false;
        }
        
        public static bool CalculateLaneSegmentIndexAtDistance(ref ZoneGraphStorage storage, int laneIndex, float distance, out int outSegmentIndex)
        {
            // Access the lane data for the specified lane index
            ref var lane = ref storage.Lanes[laneIndex];
            int numLanePoints = lane.PointsEnd - lane.PointsBegin;
            if (numLanePoints < 2)
                throw new InvalidOperationException("A lane must have at least two points.");
            
            // Handle out of range cases
            if (distance <= storage.LanePointProgressions[lane.PointsBegin])
            {
                // Distance is before the first point
                outSegmentIndex = lane.PointsBegin;
            }
            else if (distance >= storage.LanePointProgressions[lane.PointsEnd - 1])
            {
                // Distance is after the last point
                outSegmentIndex = lane.PointsEnd - 2;
            }
            else
            {
                // Binary search for the correct segment
                int low = 0;
                int high = numLanePoints - 1;
                int result = 0;

                // Perform binary search to find the segment
                while (low <= high)
                {
                    int mid = (low + high) / 2;
                    float midValue = storage.LanePointProgressions[lane.PointsBegin + mid];

                    if (midValue < distance)
                    {
                        result = mid; // Save potential segment start index
                        low = mid + 1;
                    }
                    else
                    {
                        high = mid - 1;
                    }
                }

                // Clamp the result to ensure valid range
                outSegmentIndex = math.clamp(result, 0, numLanePoints - 2) + lane.PointsBegin;
            }

            return true;
        }
        
        public static bool GetLinkedLanes(
            ref ZoneGraphStorage storage,
            int laneIndex,
            ZoneLaneLinkType types,
            ZoneLaneLinkFlags includeFlags,
            ZoneLaneLinkFlags excludeFlags,
            NativeList<ZoneGraphLinkedLane> outLinkedLanes)
        {
            // Access the lane data.
            var lane = storage.Lanes[laneIndex];
            outLinkedLanes.Clear();

            // Iterate through the lane links.
            for (int i = lane.LinksBegin; i < lane.LinksEnd; i++)
            {
                var link = storage.LaneLinks[i];

                // Check if the link matches the provided types and flags.
                if (LinkMatches(link, types, includeFlags, excludeFlags))
                {
                    // Create a destination lane handle using the destination lane index and data handle.
                    var destLaneHandle = new ZoneGraphLaneHandle(link.DestinationLaneIndex, storage.DataHandle);

                    // Add the linked lane to the output list.
                    outLinkedLanes.Add(new ZoneGraphLinkedLane()
                    {
                        DestinationLaneHandle = destLaneHandle, 
                        Type = link.Type,
                        Flags = link.Flags
                    });
                }
            }

            return true;
        }
        
        public static bool LinkMatches
        (
            in ZoneLaneLinkData link,
            ZoneLaneLinkType types,
            ZoneLaneLinkFlags includeFlags,
            ZoneLaneLinkFlags excludeFlags)
        {
            // Check if the link type matches the provided types.
            if ((link.Type & types) != ZoneLaneLinkType.None)
            {
                // If the link type is "Adjacent", check flags.
                if (link.Type == ZoneLaneLinkType.Adjacent)
                {
                    return link.HasFlags(includeFlags) && !link.HasFlags(excludeFlags);
                }

                // For other link types, return true.
                return true;
            }

            // Link does not match the specified types.
            return false;
        }
        
        public struct ZoneGraphQueryResult
        {
            public float NearestDistanceSqr;
            public int NearestLaneIdx;
            public int NearestLaneSegment;
            public float NearestLaneSegmentT;
            public float3 NearestLanePosition;
            public bool bValid;
        }
        
        public static bool FindNearestLane(ref ZoneGraphStorage storage, AABB bounds, ZoneGraphTagFilter tagFilter, ref ZoneGraphLaneLocation outLaneLocation, ref float outDistanceSqr)
        {
            // Initialize the result struct
            var result = new ZoneGraphQueryResult
            {
                NearestDistanceSqr = math.lengthsq(bounds.Extents), // Initial guess of max distance
                bValid = false
            };
    
            var center = bounds.Center;
    
            // Example: Iterate through zones and lanes (this would need to be replaced with actual entity data)
            foreach (var zone in storage.Zones.ToArray()/*TODO array*/) // Example, needs to iterate over zone data
            {
                for (int laneIdx = zone.LanesBegin; laneIdx < zone.LanesEnd; laneIdx++)
                {
                    ZoneLaneData lane = storage.Lanes[laneIdx];
                    if (tagFilter.Pass(lane.Tags)) // Apply tag filter
                    {
                        for (int i = lane.PointsBegin; i < lane.PointsEnd - 1; i++)
                        {
                            var segStart = storage.LanePoints[i];
                            var segEnd = storage.LanePoints[i + 1];
                            float segT = ClosestTimeOnSegment(center, segStart, segEnd); // Find closest point on the segment
                            float3 closestPt = math.lerp(segStart, segEnd, segT);
    
                            if (bounds.Contains(closestPt)) // Check if the point is inside the bounds
                            {
                                float distSqr = math.lengthsq(center - closestPt);
                                if (distSqr < result.NearestDistanceSqr)
                                {
                                    result.NearestDistanceSqr = distSqr;
                                    result.NearestLaneIdx = laneIdx;
                                    result.NearestLaneSegment = i;
                                    result.NearestLaneSegmentT = segT;
                                    result.NearestLanePosition = closestPt;
                                    result.bValid = true;
                                }
                            }
                        }
                    }
                }
            }
    
            if (result.bValid)
            {
                // Populate outLaneLocation based on result
                outLaneLocation.LaneHandle.DataHandle = storage.DataHandle;
                outLaneLocation.LaneHandle.Index = result.NearestLaneIdx;
                outLaneLocation.LaneSegment = result.NearestLaneSegment;
                outLaneLocation.DistanceAlongLane = math.lerp(storage.LanePointProgressions[result.NearestLaneSegment],
                                                               storage.LanePointProgressions[result.NearestLaneSegment + 1],
                                                               result.NearestLaneSegmentT);
                outLaneLocation.Position = result.NearestLanePosition;
                outLaneLocation.Direction = math.normalize(storage.LanePoints[result.NearestLaneSegment + 1] - storage.LanePoints[result.NearestLaneSegment]);
                outLaneLocation.Tangent = math.normalize(math.lerp(storage.LaneTangentVectors[result.NearestLaneSegment],
                                                                    storage.LaneTangentVectors[result.NearestLaneSegment + 1],
                                                                    result.NearestLaneSegmentT));
                outLaneLocation.Up = math.normalize(math.lerp(storage.LaneUpVectors[result.NearestLaneSegment],
                                                               storage.LaneUpVectors[result.NearestLaneSegment + 1],
                                                               result.NearestLaneSegmentT));
                outDistanceSqr = result.NearestDistanceSqr;
            }
            else
            {
                outLaneLocation.Reset();
                outDistanceSqr = 0f;
            }

            if (result.bValid == false)
            {
                Debug.LogError("No lane found, probably missing tag data.");
            }
            
            return result.bValid;
        }
    }
}