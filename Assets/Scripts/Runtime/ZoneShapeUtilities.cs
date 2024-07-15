using System;
using System.Collections.Generic;
using Data;
using Unity.Mathematics;
using UnityEngine;

namespace Runtime
{
    public static class ZoneShapeUtilities
    {
        /*todo don't need zone shape component, only need points*/
        public static void AppendZoneShapeToStorage(ZoneShape shape, ref ZoneGraphStorage storage, List<ZoneShapeLaneInternalLink>/*out list*/ internalLinks/*,zone tags*/)
        {
            //todo work on shape connectors...
            //...
            //get points
            var subShapes = shape.GetShapesAsPoints();
            foreach (var shapePoints in subShapes)
            {
                int zoneIndex = storage.Zones.Count;
                var zone = new ZoneData();
                //todo set zone tags
                
                //Build boundary points
                zone.BoundaryPointsBegin = storage.BoundaryPoints.Count;
                ZoneLaneProfile laneProfile = shape.GetLaneProfile();
                float totalWidth = laneProfile.GetLanesTotalWidth();
                float halfWidth = totalWidth * 0.5f;
                
                for (int i = 0; i < shapePoints.Count; i++)
                {
                    var point = shapePoints[i];
                    storage.BoundaryPoints.Add(point.Position - point.Right * halfWidth);
                }
                
                for (int i = shapePoints.Count - 1; i >= 0; i--)
                {
                    var point = shapePoints[i];
                    storage.BoundaryPoints.Add(point.Position - point.Right * halfWidth);
                }

                zone.BoundaryPointsEnd = storage.BoundaryPoints.Count;
                
                //Build lanes
                zone.LanesBegin = storage.Lanes.Count;
                float currentWidth = 0.0f;
                var lanePoints = new List<ZoneShapePoint>(shapePoints.Count);
                for (int i = 0; i < laneProfile._lanes.Length; i++)
                {
                    var laneDesc = laneProfile._lanes[i];

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
                    var currentLaneIndex = storage.Lanes.Count;
                    
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
                    
                    lane.PointsBegin = storage.LanePoints.Count;
                    foreach (var shapePoint in lanePoints)
                    {
                        storage.LanePoints.Add(shapePoint.Position);
                        storage.LaneUpVectors.Add(shapePoint.Up);
                        if (laneDesc._direction == ZoneLaneDirection.Forward)
                        {
                            storage.LaneTangentVectors.Add(math.normalize(shapePoint.Tangent));
                        }
                        else if (laneDesc._direction == ZoneLaneDirection.Backward)
                        {
                            storage.LaneTangentVectors.Add(math.normalize(-shapePoint.Tangent));
                        }
                    }

                    currentWidth += laneDesc._width;
                    
                    storage.Lanes.Add(lane);
                }

                zone.LanesEnd = storage.Lanes.Count;
                
                // Calculate progressions along lanes for each lane:
                for (int i = zone.LanesBegin; i < zone.LanesEnd; i++)
                {
                    var lane = storage.Lanes[i];
                    CalculateLaneProgressions(storage.LanePoints, lane.PointsBegin, lane.PointsEnd, storage.LanePointsProgressions);
                }
                
                //Calculate bounds:
                zone.Bounds = new Bounds();
                for (int i = zone.BoundaryPointsBegin; i < zone.BoundaryPointsEnd; i++)
                {
                    zone.Bounds.Expand(storage.BoundaryPoints[i]);
                }

                storage.Zones.Add(zone);
            }
            
            //todo store built data
        }

        public static void CalculateLaneProgressions(List<float3> lanePoints, int pointsBegin, int pointsEnd, List<float> lanePointProgressions)
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