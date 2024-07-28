using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;

namespace Runtime
{
    public struct ZoneGraphStorage//todo make dynamic buffer version, then the storage can be entity with various component buffers
    {
        public BlobArray<ZoneData> Zones;//look up by bvtree, entities? make ranges of entities, can be just an element
        public BlobArray<ZoneLaneData> Lanes;//entities?
        public BlobArray<float3> BoundaryPoints;//for debug drawings and calculating bounds
        public BlobArray<float3> LanePoints;
        public BlobArray<float3> LaneUpVectors;
        public BlobArray<float3> LaneTangentVectors;
        /*total distance between positions, not `t, todo but maybe can be inferred from `t`?`*/
        public BlobArray<float> LanePointProgressions;
        //public List<ZoneLaneLinkData> LaneLinks;
        /*All zones combined bounds*/
        public MinMaxAABB Bounds;
        //ZoneGraphBVTree ZoneBVTree; todo
        //public ZoneGraphDataHandle DataHandle;//for lookup by zone graph subsytem
    }

    public struct ZoneGraphDataHandle
    {
        public uint Index;
        public uint Generation;
    }
    
    //todo make a blob of zonegraphstorage? entity that moves may have pointer to a lane
}