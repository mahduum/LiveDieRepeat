using System;
using System.Collections.Generic;
using System.Linq;
using Authoring;
using Data;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEngine;

namespace Runtime
{
    public static class ZoneShapeUtilities
    {
        /*todo don't need zone shape component, only need points*/
        public static BlobAssetReference<ZoneGraphStorage> AppendZoneShapeToStorage(ZoneShape shape, List<ZoneShapeLaneInternalLink>/*out list*/ internalLinks/*,zone tags*/)
        {
            //todo work on shape connectors...
            //...
            //todo get points, this is wrong since we will be adding to storage multiple times and we cant create it over and over
            //
            var blobBuilder = new BlobBuilder(Allocator.Temp);
            ref ZoneGraphStorage storage = ref blobBuilder.ConstructRoot<ZoneGraphStorage>();//todo this will be the passed in storage
            
            var subShapes = shape.GetShapesAsPoints();
            var totalPoints = subShapes.Sum(list => list.Count);
            var totalBoundaryPoints = totalPoints * 2;
            var laneProfileCount = shape.GetLaneProfile()._lanes.Length;
            var totalLanes = subShapes.Length * laneProfileCount;//subshapes * lanes in laneprofile
            var totalLanePoints = totalPoints * laneProfileCount;

            BlobBuilderArray<float3> boundaryPointsArrayBuilder = blobBuilder.Allocate(
                ref storage.BoundaryPoints,
                totalBoundaryPoints
            );

            BlobBuilderArray<ZoneData> zoneArrayBuilder = blobBuilder.Allocate(
                ref storage.Zones,
                subShapes.Length);

            BlobBuilderArray<ZoneLaneData> zoneLaneArrayBuilder = blobBuilder.Allocate(
                ref storage.Lanes,
                totalLanes
            );

            BlobBuilderArray<float3> lanePointsArrayBuilder = blobBuilder.Allocate(
                ref storage.LanePoints,
                totalLanePoints);
            
            BlobBuilderArray<float3> laneTangentVectorsArrayBuilder = blobBuilder.Allocate(
                ref storage.LaneTangentVectors,
                totalLanePoints);
            
            BlobBuilderArray<float3> laneUpVectorsArrayBuilder = blobBuilder.Allocate(
                ref storage.LaneUpVectors,
                totalLanePoints);
            
            BlobBuilderArray<float> lanePointProgressionsArrayBuilder = blobBuilder.Allocate(
                ref storage.LanePointProgressions,
                totalLanePoints);

            var currentBoundaryPointsCount = 0;
            var currentZoneCount = 0;
            var currentLaneCount = 0;
            var currentLanePointsCount = 0;
            
            foreach (var shapePoints in subShapes)
            {
                int zoneIndex = storage.Zones.Length;
                var zone = new ZoneData();
                //todo set zone tags
                
                //Build boundary points
                zone.BoundaryPointsBegin = currentBoundaryPointsCount;//storage.BoundaryPoints.Length;//current length
                ZoneLaneProfile laneProfile = shape.GetLaneProfile();
                float totalWidth = laneProfile.GetLanesTotalWidth();
                float halfWidth = totalWidth * 0.5f;
                
                for (int i = 0; i < shapePoints.Count; i++, currentBoundaryPointsCount++)
                {
                    ZoneShapePoint point = shapePoints[i];
                    boundaryPointsArrayBuilder[currentBoundaryPointsCount + i] = (point.Position - point.Right * halfWidth);
                }
                
                for (int i = shapePoints.Count - 1; i >= 0; i--, currentBoundaryPointsCount++)
                {
                    ZoneShapePoint point = shapePoints[i];
                    boundaryPointsArrayBuilder[currentBoundaryPointsCount] = (point.Position - point.Right * halfWidth);
                }

                zone.BoundaryPointsEnd = currentBoundaryPointsCount;//storage.BoundaryPoints.Count;//current lenght
                
                //Build lanes
                zone.LanesBegin = currentLaneCount;//storage.Lanes.Count;
                float currentWidth = 0.0f;
                var lanePoints = new List<ZoneShapePoint>(shapePoints.Count);
                for (int i = 0; i < laneProfile._lanes.Length; i++)
                {
                    ZoneLaneDesc laneDesc = laneProfile._lanes[i];

                    if (laneDesc._direction == ZoneLaneDirection.None)
                    {
                        currentWidth += laneDesc._width;
                        continue;
                    }

                    ZoneLaneData lane = new ZoneLaneData();

                    lane.ZoneIndex = zoneIndex;
                    lane.Width = laneDesc._width;
                    //lane.Tags = todo
                    //lane.StartEntryId = firstPointId;
                    //lane.EndEntryId = endPointId;
                    var currentLaneIndex = currentLaneCount;
                    
                    //todo AddAdjacentLaneLinks(currentLaneIndex, i, laneProfile._lanes, ref internalLinks);

                    float laneOffset/*offset from curve center*/ = halfWidth - (currentWidth + laneDesc._width * 0.5f);

                    lanePoints.Clear();
                    if (laneDesc._direction == ZoneLaneDirection.Forward)
                    {
                        for (int j = 0; j < shapePoints.Count; j++)
                        {
                            var shapePoint = shapePoints[j];
                            shapePoint.Position += shapePoint.Right * laneOffset;
                            lanePoints.Add(shapePoint);
                        }
                    }
                    else if (laneDesc._direction == ZoneLaneDirection.Backward)
                    {
                        for (int j = shapePoints.Count - 1; j >= 0; j--)
                        {
                            var shapePoint = shapePoints[j];
                            shapePoint.Position += shapePoint.Right * laneOffset;
                            lanePoints.Add(shapePoint);
                        }
                    }

                    //todo simplify lane shape defined by lanePoints (but won't be able to 
                    //SimplifyShape(lanePoints, float tolerance); 

                    lane.PointsBegin = currentLanePointsCount;//storage.LanePoints.Count;
                    foreach (var shapePoint in lanePoints)
                    {
                        //lane points are total number of points times total number of lanes per shape, all shapes have the same profile
                        //or make the storage reference only lane entities => NOTE: if we make lanes entities with dynamic buffer, we can edit them at runtime
                        lanePointsArrayBuilder[currentLanePointsCount] = (shapePoint.Position);
                        laneUpVectorsArrayBuilder[currentLanePointsCount] = (shapePoint.Up);
                        if (laneDesc._direction == ZoneLaneDirection.Forward)
                        {
                            laneTangentVectorsArrayBuilder[currentLanePointsCount] = (math.normalize(shapePoint.Tangent));
                        }
                        else if (laneDesc._direction == ZoneLaneDirection.Backward)
                        {
                            laneTangentVectorsArrayBuilder[currentLanePointsCount] = (math.normalize(-shapePoint.Tangent));
                        }

                        currentLanePointsCount++;
                    }

                    currentWidth += laneDesc._width;
                    
                    zoneLaneArrayBuilder[currentLaneCount++] = lane;
                }

                zone.LanesEnd = currentLaneCount;//storage.Lanes.Count;
                
                // Calculate progressions along lanes for each lane:
                for (int i = zone.LanesBegin; i < zone.LanesEnd; i++)
                {
                    var lane = storage.Lanes[i];
                    CalculateLaneProgressions(lane.PointsBegin, lane.PointsEnd, ref storage, lanePointProgressionsArrayBuilder);
                }

                //Calculate bounds:
                zone.Bounds = new MinMaxAABB();
                for (int i = zone.BoundaryPointsBegin; i < zone.BoundaryPointsEnd; i++)
                {
                    zone.Bounds.Encapsulate(storage.BoundaryPoints[i]);
                }

                zoneArrayBuilder[currentZoneCount++] = zone; //Add(zone);
            }
            
            foreach (var zone in storage.Zones.ToArray())
            {
                storage.Bounds.Encapsulate(zone.Bounds);    
            }
            
            //todo store built data
            var zoneStorageBlobAssetRef = blobBuilder.CreateBlobAssetReference<ZoneGraphStorage>(Allocator.Persistent);
            blobBuilder.Dispose();
            return zoneStorageBlobAssetRef;
        }

        public static void CalculateLaneProgressions(int pointsBegin, int pointsEnd, ref ZoneGraphStorage storage, BlobBuilderArray<float> lanePointProgressionsArrayBuilder)
        {
            ref var lanePoints = ref storage.LanePoints;
            float totalDistance = 0.0f;
            for (int i = pointsBegin; i < pointsEnd - 1; i++)
            {
                lanePointProgressionsArrayBuilder[i] = totalDistance;
                totalDistance += math.distance(lanePoints[i], lanePoints[i + 1]);
            }
            lanePointProgressionsArrayBuilder[pointsEnd - 1] = totalDistance;
        }
        
        public static void AddShapeZoneData(
            ref LaneProfileBlobAsset laneProfile,
            in NativeArray<ZoneShapePoint> shapePoints,
            NativeList<float3> boundaryPoints,
            NativeList<ZoneData> zones,
            NativeList<ZoneLaneData> lanes,
            NativeList<float3> lanePoints,
            NativeList<float3> laneTangentVectors,
            NativeList<float3> laneUpVectors,
            NativeList<float> lanePointProgressions,
            ref MinMaxAABB storageBounds)// List<ZoneShapeLaneInternalLink>/*out list*/ internalLinks/*,zone tags*/)
        {
            int zoneIndex = zones.Length;
            var zone = new ZoneData();
            //todo set zone tags
            
            //Build boundary points
            zone.BoundaryPointsBegin = boundaryPoints.Length;//storage.BoundaryPoints.Length;//current length
            
            float totalWidth = laneProfile.LanesTotalWidth;
            float halfWidth = totalWidth * 0.5f;
            
            for (int i = 0; i < shapePoints.Length; i++)
            {
                ZoneShapePoint point = shapePoints[i];
                boundaryPoints.Add(point.Position - point.Right * halfWidth);
            }
            
            for (int i = shapePoints.Length - 1; i >= 0; i--)
            {
                ZoneShapePoint point = shapePoints[i];
                boundaryPoints.Add(point.Position - point.Right * halfWidth);
            }

            zone.BoundaryPointsEnd = boundaryPoints.Length;//storage.BoundaryPoints.Count;//current lenght
            
            //Build lanes
            zone.LanesBegin = lanes.Length;//storage.Lanes.Count;
            float currentWidth = 0.0f;
            var lanePointsTemp = new NativeList<ZoneShapePoint>(shapePoints.Length, Allocator.Temp);
            for (int i = 0; i < laneProfile.LaneDescriptions.Length; i++)
            {
                ZoneLaneDesc laneDesc = laneProfile.LaneDescriptions[i];

                if (laneDesc._direction == ZoneLaneDirection.None)
                {
                    currentWidth += laneDesc._width;
                    continue;
                }

                ZoneLaneData lane = new ZoneLaneData();

                lane.ZoneIndex = zoneIndex;
                lane.Width = laneDesc._width;
                //lane.Tags = todo
                //lane.StartEntryId = firstPointId;
                //lane.EndEntryId = endPointId;
                var currentLaneIndex = lanes.Length;
                
                //todo AddAdjacentLaneLinks(currentLaneIndex, i, laneProfile._lanes, ref internalLinks);

                float laneOffset/*offset from curve center*/ = halfWidth - (currentWidth + laneDesc._width * 0.5f);

                lanePointsTemp.Clear();
                if (laneDesc._direction == ZoneLaneDirection.Forward)
                {
                    for (int j = 0; j < shapePoints.Length; j++)
                    {
                        var shapePoint = shapePoints[j];
                        shapePoint.Position += shapePoint.Right * laneOffset;
                        lanePointsTemp.Add(shapePoint);
                    }
                }
                else if (laneDesc._direction == ZoneLaneDirection.Backward)
                {
                    for (int j = shapePoints.Length - 1; j >= 0; j--)
                    {
                        var shapePoint = shapePoints[j];
                        shapePoint.Position += shapePoint.Right * laneOffset;
                        lanePointsTemp.Add(shapePoint);
                    }
                }

                //todo simplify lane shape defined by lanePoints (but won't be able to 
                //SimplifyShape(lanePoints, float tolerance); 

                lane.PointsBegin = lanes.Length;//storage.LanePoints.Count;
                foreach (var shapePoint in lanePointsTemp)
                {
                    //lane points are total number of points times total number of lanes per shape, all shapes have the same profile
                    //or make the storage reference only lane entities => NOTE: if we make lanes entities with dynamic buffer, we can edit them at runtime
                    lanePoints.Add(shapePoint.Position);
                    laneUpVectors.Add(shapePoint.Up);
                    if (laneDesc._direction == ZoneLaneDirection.Forward)
                    {
                        laneTangentVectors.Add(math.normalize(shapePoint.Tangent));
                    }
                    else if (laneDesc._direction == ZoneLaneDirection.Backward)
                    {
                        laneTangentVectors.Add(math.normalize(-shapePoint.Tangent));
                    }
                }

                currentWidth += laneDesc._width;
                
                lanes.Add(lane);
            }

            zone.LanesEnd = lanes.Length;//storage.Lanes.Count;
            
            // Calculate progressions along lanes for each lane:
            for (int i = zone.LanesBegin; i < zone.LanesEnd; i++)
            {
                var lane = lanes[i];
                CalculateLaneProgressions(lane.PointsBegin, lane.PointsEnd, lanePoints, lanePointProgressions);
            }

            //Calculate bounds:
            zone.Bounds = new MinMaxAABB();
            for (int i = zone.BoundaryPointsBegin; i < zone.BoundaryPointsEnd; i++)
            {
                zone.Bounds.Encapsulate(boundaryPoints[i]);
            }

            zones.Add(zone);
            
            foreach (var zoneData in zones)
            {
                storageBounds.Encapsulate(zoneData.Bounds);    
            }

            lanePointsTemp.Dispose();
        }
        
          
        public static void CalculateLaneProgressions(int pointsBegin, int pointsEnd, in NativeList<float3> lanePoints, NativeList<float> lanePointProgressions)
        {
            float totalDistance = 0.0f;
            for (int i = pointsBegin; i < pointsEnd - 1; i++)
            {
                lanePointProgressions.Add(totalDistance);
                totalDistance += math.distance(lanePoints[i], lanePoints[i + 1]);
            }
            lanePointProgressions.Add(totalDistance);
        }
    }
}