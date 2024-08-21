using System.Collections.Generic;
using Data;
using Unity.Mathematics;
using UnityEngine;

namespace Runtime
{
    public class ZonePolygonShape : ZoneShape
    {
        /*
         * todo: must build lanes between points, may use external static function for that
         * 1. Tesselating polygon shape, create bezier between boundary point pairs
         * 2. The tesselation will produce a curve only if the point type is bezier (with control points) or lane profile
         * 3. Otherwise no tesselation, leave point as they are
         * 4. For each corner point known as source point (non LaneProfile point, todo: why?)
         *      make a connection with each other point src->dst if their profiles match
         * 5. OutgoingConnections and IncomingConnections have counts of connections per given index point of polygon.
         * 6. Use same lane begin logic in storage.Lanes
         * 7. BuildLanesBetweenPoints with DubinsPath, and use FConnectionEntry for that as source argument (end file)
         * NOTE:     Function takes in storage to append to, we should create points here so we can append this as regular shape
         * 8. Build lanes also appends connection candidates (connection src slot to dst slot, and tag mask) based on tags to selected destination points within the polygon
         *  - lerp between lanes based on profile data to find slot connection positions for all lanes and slot data:
         *      position, forward, up, lane desc, index of this slot in the array of slots for polygon, entry index (index within polygon), lane profile ptr,
         *      restrictions (what can be connected, extracted from source zone shape point, but is set only on destination slot),
         *      distance from lane profile edge, distance from far lane profile edge, inner turning radius
         *  - connect lanes source to each destination at a time:
         *      1. Iterate through all possible tags and collect src and dst slots with tags matching lane desc
         *      2. Append them as candidates, based on restrictions (like "one lane per destination" or "merge lanes to one destination")
         * 9. Remove candidates that lead to the same destination (based on flag if we want this, remove duplicate destination candidate that has also other connections, if both can be removed then keep the one that is more straight)
         * 10. Fill empty destinations that might've been produced when overlapping lanes were removed (for example while merging 4 to 2)
         * 11. Sort candidates by index (adjacency).
         * 12. Create lanes from candidates (Bezier or Dubin), each candidate is a lane that is added to storage.
         * 13. Get tangents and progressions and lane points.
         * 14. End Build lanes method. TODO: check if polygon in Unreal has separate profile for each point
         * 15. Add lanes, calculate progressions, boundaries as usual.
         *  

         */
        public override ZoneShapeType ShapeType => ZoneShapeType.Polygon;
        public override Component GetBakerDependency()
        {
            throw new System.NotImplementedException();
        }

        protected override void SubscribeOnShapeChanged()
        {
            throw new System.NotImplementedException();
        }

        protected override void UnsubscribeOnShapeChanged()
        {
            throw new System.NotImplementedException();
        }

        public override List<ZoneShapePoint>[] GetShapesAsPoints()
        {
            throw new System.NotImplementedException();
        }

        public override List<MinMaxAABB> GetShapesBounds()
        {
            throw new System.NotImplementedException();
        }

        public override IZoneLaneProfile GetZoneLaneProfile()
        {
            throw new System.NotImplementedException();
        }
    }
}
/*struct FConnectionEntry
{
	FConnectionEntry(const FZoneShapePoint& InPoint, const FZoneLaneProfile& InProfile, const uint16 InEntryID, const int32 InOutgoingConnections, const int32 InIncomingConnections)
		: Point(InPoint), Profile(InProfile), EntryID(InEntryID), OutgoingConnections(InOutgoingConnections), IncomingConnections(InIncomingConnections)
	{}
	
	const FZoneShapePoint& Point;
	const FZoneLaneProfile& Profile;
	const uint16 EntryID;//src point index
	const int32 OutgoingConnections;
	const int32 IncomingConnections;
};*/