using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace Runtime
{
    public struct ZoneGraphStorage//todo should this be a buffer? cana this be a direct mono actor?
    {
        public List<ZoneData> Zones;//look up by bvtree
        public List<ZoneLaneData> Lanes;
        public List<float3> BoundaryPoints;
        public List<float3> LanePoints;
        public List<float3> LaneUpVectors;
        public List<float3> LaneTangentVectors;
        /*total distance between positions, not `t, todo but maybe can be inferred from `t`?`*/
        public List<float> LanePointsProgressions;
        //public List<ZoneLaneLinkData> LaneLinks;
        /*All zones combined bounds*/
        public Bounds Bounds;
        //ZoneGraphBVTree ZoneBVTree; todo
        public ZoneGraphDataHandle DataHandle;//for lookup by zone graph subsytem
    }

    public struct ZoneGraphDataHandle
    {
        public uint Index;
        public uint Generation;
    }
}