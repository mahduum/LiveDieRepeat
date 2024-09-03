using System;
using System.Collections.Generic;
using System.Linq;
using Authoring;
using Data;
using DataUtilities;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
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
            var laneProfileCount = shape.GetZoneLaneProfile().GetLaneDescriptions().Length;
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
                IZoneLaneProfile laneProfile = shape.GetZoneLaneProfile();
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
                    boundaryPointsArrayBuilder[currentBoundaryPointsCount] = (point.Position + point.Right * halfWidth);
                }

                zone.BoundaryPointsEnd = currentBoundaryPointsCount;//storage.BoundaryPoints.Count;//current lenght
                
                //Build lanes
                zone.LanesBegin = currentLaneCount;//storage.Lanes.Count;
                float currentWidth = 0.0f;
                var lanePoints = new List<ZoneShapePoint>(shapePoints.Count);
                for (int i = 0; i < laneProfile.GetLaneDescriptions().Length; i++)
                {
                    ZoneLaneDesc laneDesc = laneProfile.GetLaneDescriptions()[i];

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
        
        /*NOTE: This seems to be shape type (polygon or bezier) agnostic because it does not perform any tesselation*/
        public static void AddShapeZoneData(
            ZoneShapeType shapeType,
            ref LaneProfileBlobAsset laneProfile,
            in NativeArray<ZoneShapePoint> shapePoints,
            NativeList<float3> boundaryPoints,
            NativeList<ZoneData> zones,
            NativeList<ZoneLaneData> lanes,
            NativeList<float3> lanePoints,
            NativeList<float3> laneTangentVectors,
            NativeList<float3> laneUpVectors,
            NativeList<float> lanePointProgressions,
            ref MinMaxAABB storageBounds, NativeList<ZoneShapeLaneInternalLink>/*out list*/ internalLinks/*,zone tags*/)
        {
            //todo somewhere points should be adjusted for snapped connections... here or before
            //since this only appends data, keep the corrections out of it
            //todo reserve snapping logic for later
            
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
            
            for (int i = shapePoints.Length - 1; i >= 0; i--)//adding points in reverse so they follow each other
            {
                ZoneShapePoint point = shapePoints[i];
                boundaryPoints.Add(point.Position + point.Right * halfWidth);
            }

            zone.BoundaryPointsEnd = boundaryPoints.Length;//storage.BoundaryPoints.Count;//current length
            
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
                //laneProfile.LaneDescriptions.

                if (shapeType != ZoneShapeType.Polygon)//todo see if I can resolve it universally
                {
                    AddAdjacentLaneLinks(currentLaneIndex, i, ref laneProfile.LaneDescriptions, internalLinks);
                }

                float laneOffset/*offset from curve center*/ = halfWidth - (currentWidth + laneDesc._width * 0.5f);

                lanePointsTemp.Clear();
                if (laneDesc._direction == ZoneLaneDirection.Forward)
                {
                    for (int j = 0; j < shapePoints.Length; j++)
                    {
                        var shapePoint = shapePoints[j];
                        shapePoint.Position += math.normalize(shapePoint.Right) * laneOffset;
                        lanePointsTemp.Add(shapePoint);
                    }
                }
                else if (laneDesc._direction == ZoneLaneDirection.Backward)
                {
                    for (int j = shapePoints.Length - 1; j >= 0; j--)//adding points in reverse so they follow each other
                    {
                        var shapePoint = shapePoints[j];
                        shapePoint.Position += shapePoint.Right * laneOffset;
                        lanePointsTemp.Add(shapePoint);
                    }
                }

                //todo simplify lane shape defined by lanePoints (but won't be able to 
                //SimplifyShape(lanePoints, float tolerance); 

                lane.PointsBegin = lanePoints.Length;//lanes.Length;//storage.LanePoints.Count;//TODO lane points count instead!!!!!
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
                
                lane.PointsEnd = lanePoints.Length;
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

        /*Links to lanes within the same zone (spline shape), that is why they are internal*/
        public static void AddAdjacentLaneLinks(int currentLaneIndex,
            int laneDescIndex, ref BlobArray<ZoneLaneDesc> laneDescriptions, NativeList<ZoneShapeLaneInternalLink> internalLinks)
        {
            var lanesCount = laneDescriptions.Length;
            var laneDescription = laneDescriptions[laneDescIndex]; 
            
            // Assign left/right based on current lane direction. Lanes are later arranged so that they all point forward.
            ZoneLaneLinkFlags previousLinkFlags = ZoneLaneLinkFlags.None;
            ZoneLaneLinkFlags nextLinkFlags = ZoneLaneLinkFlags.None;

            if (laneDescription._direction == ZoneLaneDirection.Forward)
            {
                nextLinkFlags = ZoneLaneLinkFlags.Left;//going to the left, so next lane will be to the left
                previousLinkFlags = ZoneLaneLinkFlags.Right;//and previous to the right
            }
            else if (laneDescription._direction == ZoneLaneDirection.Backward)
            {
                nextLinkFlags = ZoneLaneLinkFlags.Right;//going to the left, so next lane will be to the left
                previousLinkFlags = ZoneLaneLinkFlags.Left;//and previous to the right
            }
            else if (laneDescription._direction == ZoneLaneDirection.None)
            {
                Debug.LogError("This lane direction shouldn't be implemented!");
            }
            else
            {
                Debug.LogError("This lane direction is not implemented!");
            }

            if ((laneDescIndex + 1) < lanesCount) //next lane to the left:
            {
                var nextLaneDescription = laneDescriptions[laneDescIndex + 1];
                if (nextLaneDescription._direction !=
                    ZoneLaneDirection.None) //if it is not a simple division empty space
                {
                    if (laneDescription._direction != nextLaneDescription._direction)
                    {
                        nextLinkFlags |= ZoneLaneLinkFlags.OppositeDirection;
                    }
                    
                    internalLinks.Add(new ZoneShapeLaneInternalLink
                    {
                        LaneIndex = currentLaneIndex,
                        LinkData = new ZoneLaneLinkData()
                        {
                            DestinationLaneIndex = currentLaneIndex + 1,
                            Type = ZoneLaneLinkType.Adjacent,
                            Flags = nextLinkFlags
                        }
                    });
                }
            }

            if ((laneDescIndex - 1) >= 0)
            {
                var previousLaneDescription = laneDescriptions[laneDescIndex - 1];
                if (laneDescription._direction != previousLaneDescription._direction)
                {
                    previousLinkFlags |= ZoneLaneLinkFlags.OppositeDirection;
                }
                
                internalLinks.Add(new ZoneShapeLaneInternalLink
                {
                    LaneIndex = currentLaneIndex,
                    LinkData = new ZoneLaneLinkData()
                    {
                        DestinationLaneIndex = currentLaneIndex - 1,
                        Type = ZoneLaneLinkType.Adjacent,
                        Flags = previousLinkFlags
                    }
                });
            }
        }
        
        public struct LanePointID
        {
            public enum LaneExtremity : uint
            {
                Start = 0,
                End = 1,
            }
            
            public int LaneIndex;
            public LaneExtremity Extremity;
            
            public LanePointID(int laneIndex, LaneExtremity extremity)
            {
                LaneIndex = laneIndex;
                Extremity = extremity;
            }
        }
        
        public static NativeList<ZoneLaneLinkData> ConnectLanes(NativeList<ZoneShapeLaneInternalLink> internalLinks,
            NativeList<ZoneLaneData> lanes,
            NativeList<float3> lanePoints,
            NativeList<float3> laneTangentVectors,
            NativeList<float3> laneUpVectors)
        {
            //need zone storage for reading and writing... instead pass only arrays that will
            //go into storage, but we will use then for data to facilitate copying:
            //Lanes, LanePoints, lane tangent and up vectors LaneLinks to fill
            var zoneLaneLinks = new NativeList<ZoneLaneLinkData>(Allocator.Temp);
            internalLinks.Sort(new ZoneShapeLaneInternalLinkComparer());
            
            //lookup for first link by lane
            NativeHashMap<int, int> firstLinkByLane = new NativeHashMap<int, int>(internalLinks.Length, Allocator.Temp);

            int previousLaneIndex = -1;
            for (int linkIndex = 0; linkIndex < internalLinks.Length; linkIndex++)
            {
                var link = internalLinks[linkIndex];
                if (link.LaneIndex != previousLaneIndex /*take just first link*/)
                {
                    Debug.Log($"Adding link by lane, lane index: {link.LaneIndex}");
                    firstLinkByLane.Add(link.LaneIndex, linkIndex);
                    previousLaneIndex = link.LaneIndex;
                }
            }

            NativeHierarchicalHashGrid2D<LanePointID> linkGrid =
                new NativeHierarchicalHashGrid2D<LanePointID>(1, 1, 10.0f);

            const float connectionTolerance = 0.2f;
            float connectionToleranceSq = math.pow(connectionTolerance, 2);
            float3 connectionToleranceExtent =
                new float3(connectionTolerance, connectionTolerance, connectionTolerance);

            //add lanes extreme points to grid
            for (int laneIndex = 0; laneIndex < lanes.Length; laneIndex++)
            {
                var zoneLaneData = lanes[laneIndex];
                linkGrid.Add(new LanePointID(laneIndex, LanePointID.LaneExtremity.Start),
                    new AABB()
                    {
                        Center = lanePoints[zoneLaneData.PointsBegin],
                        Extents = float3.zero
                    });
                
                linkGrid.Add(new LanePointID(laneIndex, LanePointID.LaneExtremity.End),
                    new AABB()
                    {
                        Center = lanePoints[zoneLaneData.PointsEnd - 1],
                        Extents = float3.zero
                    });
            }
            
            Debug.Log($"Link grid all items count: {linkGrid.AllItemsCount()}");

            //Build lane connections:
            NativeList<LanePointID> tempQueryResults = new NativeList<LanePointID>(Allocator.Temp);
            Debug.Log($"Searching links, all lanes count: {lanes.Length}");
            for (int laneIndex = 0; laneIndex < lanes.Length; laneIndex++)
            {
                var sourceLane = lanes[laneIndex];
                sourceLane.LinksBegin = zoneLaneLinks.Length;
                
                //add internal links
                int adjacentLaneCount = 0;
                if (firstLinkByLane.TryGetValue(laneIndex, out var firstLink))
                {
                    for (int linkIndex = firstLink; linkIndex < internalLinks.Length; linkIndex++)
                    {
                        var link = internalLinks[linkIndex];
                        if (link.LaneIndex != laneIndex)
                        {
                            break;
                        }
                        
                        zoneLaneLinks.Add(link.LinkData);
                        if (link.LinkData.Type == ZoneLaneLinkType.Adjacent)
                        {
                            adjacentLaneCount++;
                        }
                    }
                }
                
                // Add links to connected lanes
                var sourceStartPosition = lanePoints[sourceLane.PointsBegin];
                var sourceEndPosition = lanePoints[sourceLane.PointsEnd - 1];
                
                // Lanes touching the source lane start point
                tempQueryResults.Clear();
                
                linkGrid.Query(
                    new AABB()
                    {
                        Center = sourceStartPosition,
                        Extents = connectionToleranceExtent, 
                    },
                    tempQueryResults);

                string res = tempQueryResults.Length > 0 ? tempQueryResults[0].LaneIndex.ToString() : "-1";
                Debug.Log(
                    $"Touching source lane start point count: {tempQueryResults.Length}, lane index for first point: {res}");
                // all the points close enough to current lane extremity that can be liked against
                foreach (var laneID in tempQueryResults)
                {
                    if (laneID.LaneIndex == laneIndex)
                    {
                        //skip self
                        Debug.LogWarning($"Skipped self lane index: {laneID.LaneIndex}");
                        continue;
                    }

                    var destinationLane = lanes[laneID.LaneIndex];
                    var destinationStartPosition = lanePoints[destinationLane.PointsBegin];
                    var destinationEndPosition = lanePoints[destinationLane.PointsEnd - 1];

                    bool srcLaneDataTagContainsAnyDestLaneTags = true;//todo implement tag masks and LaneConnectionMask setting
                    if (srcLaneDataTagContainsAnyDestLaneTags)
                    {
                        if (sourceLane.ZoneIndex != destinationLane.ZoneIndex &&
                            laneID.Extremity == LanePointID.LaneExtremity.End &&
                            math.distancesq(sourceStartPosition, destinationEndPosition) < connectionToleranceSq)
                        {
                            // if our point is end extremity then we measure against end point position
                            // Incoming lane, connects to start with its end and has the same direction
                            var laneLink = new ZoneLaneLinkData()
                            {
                                DestinationLaneIndex = laneID.LaneIndex,
                                Type = ZoneLaneLinkType.Incoming,
                                Flags = ZoneLaneLinkFlags.None,
                            };
                            
                            //adding to the current source lane slice section
                            zoneLaneLinks.Add(laneLink);
                        }
                        else if
                            (sourceLane.ZoneIndex ==
                             destinationLane
                                 .ZoneIndex /*same zone, for example splitting in polygon or adjacent in spline*/
                             && laneID.Extremity == LanePointID.LaneExtremity.Start
                             && math.distancesq(sourceStartPosition, destinationStartPosition) < connectionToleranceSq)
                        {
                            // Splitting lane
                            var laneLink = new ZoneLaneLinkData()
                            {
                                DestinationLaneIndex = laneID.LaneIndex,
                                Type = ZoneLaneLinkType.Adjacent,
                                Flags = ZoneLaneLinkFlags.Splitting,//two starts diverging at common point
                            };
                            
                            //adding to the current source lane slice section
                            zoneLaneLinks.Add(laneLink);
                        }
                    }
                }
                
                // Lanes touching the source lane end point
                tempQueryResults.Clear();
                linkGrid.Query(
                    new AABB()
                    {
                        Center = sourceEndPosition,
                        Extents = connectionToleranceExtent, 
                    },
                    tempQueryResults);
                
                res = tempQueryResults.Length > 0 ? tempQueryResults[0].LaneIndex.ToString() : "-1";
                Debug.Log(
                    $"Touching source lane end point count: {tempQueryResults.Length}, lane index for first point: {res}");
                
                // all the points close enough to current lane extremity that can be liked against
                foreach (var laneID in tempQueryResults)
                {
                    if (laneID.LaneIndex == laneIndex)
                    {
                        //skip self
                        continue;
                    }

                    var destinationLane = lanes[laneID.LaneIndex];
                    var destinationStartPosition = lanePoints[destinationLane.PointsBegin];
                    var destinationEndPosition = lanePoints[destinationLane.PointsEnd - 1];

                    bool srcLaneDataTagContainsAnyDestLaneTags = true;//todo implement tag masks and LaneConnectionMask setting
                    if (srcLaneDataTagContainsAnyDestLaneTags)
                    {
                        if (sourceLane.ZoneIndex != destinationLane.ZoneIndex &&
                            laneID.Extremity == LanePointID.LaneExtremity.Start &&
                            math.distancesq(sourceEndPosition, destinationStartPosition) < connectionToleranceSq)
                        {
                            // if our point is end extremity then we measure against end point position
                            // Outgoing lane, connects to end with its start and has the same direction
                            var laneLink = new ZoneLaneLinkData()
                            {
                                DestinationLaneIndex = laneID.LaneIndex,
                                Type = ZoneLaneLinkType.Outgoing,
                                Flags = ZoneLaneLinkFlags.None,
                            };
                            
                            //adding to the current source lane slice section
                            zoneLaneLinks.Add(laneLink);
                        }
                        else if
                            (sourceLane.ZoneIndex ==
                             destinationLane
                                 .ZoneIndex /*same zone, for example splitting in polygon or adjacent in spline*/
                             && laneID.Extremity == LanePointID.LaneExtremity.End
                             && math.distancesq(sourceEndPosition, destinationEndPosition) < connectionToleranceSq)
                        {
                            // Splitting lane
                            var laneLink = new ZoneLaneLinkData()
                            {
                                DestinationLaneIndex = laneID.LaneIndex,
                                Type = ZoneLaneLinkType.Adjacent,
                                Flags = ZoneLaneLinkFlags.Merging,//two ends coming to common point
                            };
                            
                            //adding to the current source lane slice section
                            zoneLaneLinks.Add(laneLink);
                        }
                    }
                }
                
                //continue;//todo

                
                // Potentially adjacent lanes in a polygon shape, we don't add adjacent links for polygon shape, or 
                // we shouldn't add them in a way that is used for spline shapes todo: check if they can be added and debug whether adding them should be disabled in append shapes for polygons
                if (adjacentLaneCount == 0)//it is assumed for now that polygons should have no adjacent internal links added to them in earlier code
                {
                    float adjacentRadius = sourceLane.Width + connectionTolerance;// assumes adjacent lanes have same width
                    float adjacentRadiusSq = math.pow(adjacentRadius, 2f);
                    float3 adjacentExtent = new float3(adjacentRadius, adjacentRadius, adjacentRadius);
                    
                    tempQueryResults.Clear();
                    linkGrid.Query(
                        new AABB()
                        {
                            Center = sourceEndPosition,
                            Extents = adjacentExtent
                        },
                        tempQueryResults);

                    float3 sourceStartSide /*to the left*/ = math.cross(laneTangentVectors[sourceLane.PointsBegin], laneUpVectors[sourceLane.PointsBegin]);
                    float3 sourceEndSide /*to the left*/ = math.cross(laneTangentVectors[sourceLane.PointsEnd - 1], laneUpVectors[sourceLane.PointsEnd - 1]);
                    
                    //todo
                    foreach (var laneID in tempQueryResults)
                    {
                        //skip self
                        if (laneID.LaneIndex == laneIndex)
                        {
                            continue;
                        }
                        
                        var destinationLane = lanes[laneID.LaneIndex];
                        if (sourceLane.ZoneIndex == destinationLane.ZoneIndex
                            &&
                            true /*todo SourceLane.Tags.ContainsAny(DestLane.Tags & BuildSettings.LaneConnectionMask)*/)
                        {
                            // If the link already exists, do not create a duplicate one.
                            bool linkExists = false;
                            for (int linkIndex = sourceLane.LinksBegin; linkIndex < zoneLaneLinks.Length; linkIndex++)
                            {
                                var link = zoneLaneLinks[linkIndex];
                                if (link.DestinationLaneIndex == laneID.LaneIndex)
                                {
                                    linkExists = true;
                                    break;
                                }
                            }

                            if (linkExists)
                            {
                                continue;
                            }
                            
                            var destinationStartPosition = lanePoints[destinationLane.PointsBegin];
                            var destinationEndPosition = lanePoints[destinationLane.PointsEnd - 1];
                            
                            // Using range checks since we assume that the points should not be overlapping:
                            if (InRange(math.distancesq(sourceStartPosition, destinationStartPosition),
                                    connectionToleranceSq, adjacentRadiusSq)
                                && InRange(math.distancesq(sourceEndPosition, destinationEndPosition),
                                    connectionToleranceSq, adjacentRadiusSq))
                            {
                                // Same direction adjacent lanes
                                bool startIsLeft = math.dot(sourceStartSide, destinationStartPosition - sourceStartPosition) > 0.0f;
                                bool endIsLeft = math.dot(sourceEndSide, destinationEndPosition - sourceEndPosition) > 0.0f;
                                
                                // Expect the adjacent lane points to be same side of the lane at start and end.
                                if (startIsLeft == endIsLeft)
                                {
                                    var link = new ZoneLaneLinkData()
                                    {
                                        DestinationLaneIndex = laneID.LaneIndex,
                                        Type = ZoneLaneLinkType.Adjacent,
                                        Flags = (startIsLeft ? ZoneLaneLinkFlags.Left : ZoneLaneLinkFlags.Right)
                                    };
                                    
                                    zoneLaneLinks.Add(link);
                                }
                            }
                            else if (InRange(math.distancesq(sourceStartPosition, destinationEndPosition),
                                         connectionToleranceSq, adjacentRadiusSq)
                                     && InRange(math.distancesq(sourceEndPosition, destinationStartPosition),
                                         connectionToleranceSq, adjacentRadiusSq))
                            {
                                // Opposite direction adjacent lanes
                                bool startIsLeft = math.dot(sourceStartSide, destinationEndPosition - sourceStartPosition) > 0.0f;
                                bool endIsLeft = math.dot(sourceEndSide, destinationStartPosition - sourceEndPosition) > 0.0f;
                                
                                // Expect the adjacent lane points to be same side of the lane at start and end.
                                if (startIsLeft == endIsLeft)
                                {
                                    var link = new ZoneLaneLinkData()
                                    {
                                        DestinationLaneIndex = laneID.LaneIndex,
                                        Type = ZoneLaneLinkType.Adjacent,
                                        Flags = (startIsLeft ? ZoneLaneLinkFlags.Left : ZoneLaneLinkFlags.Right) | ZoneLaneLinkFlags.OppositeDirection
                                    };
                                    
                                    zoneLaneLinks.Add(link);
                                }
                            }
                        }
                    }
                }

                sourceLane.LinksEnd = zoneLaneLinks.Length;
                
                // when all link data is set swap the updated lane data
                lanes[laneIndex] = sourceLane;
            }

            return zoneLaneLinks;
        }

        public static bool InRange(float value, float min, float max)
        {
            return (value >= min && value <= max);
        }
        
        //todo make this extension method
        public static unsafe void CopyToBlobArray<T>(NativeList<T> source, ref BlobArray<T> destination, ref BlobBuilder blobBuilder) where T : unmanaged
        {
            BlobBuilderArray<T> arrayBuilder = blobBuilder.Allocate(
                ref destination,
                source.Length
            );

            // for (int i = 0; i < source.Length; i++)
            // {
            //     arrayBuilder[i] = source[i];
            // }
            void* destinationPtr = arrayBuilder.GetUnsafePtr();
            void* sourcePtr = source.GetUnsafePtr();
            UnsafeUtility.MemCpy(destinationPtr, sourcePtr, source.Length * UnsafeUtility.SizeOf<T>());
        }
    }
}