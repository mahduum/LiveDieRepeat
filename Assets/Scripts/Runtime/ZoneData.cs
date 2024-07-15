using System;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Runtime
{
    public struct ZoneData
    {
        public int BoundaryPointsBegin;
        /*One past index*/
        public int BoundaryPointsEnd;
        /*Indices range to all lanes in, can this be made differently in a buffer? */
        public int LanesBegin;
        public int LanesEnd;
        public Bounds Bounds;
        //masktags
    }

    public struct ZoneLaneData
    {
        public float Width;
        /*Indices range to all LanePoints in storage*/
        public int PointsBegin;
        public int PointsEnd;
        public int LinksBegin;
        public int LinksEnd;
        public int ZoneIndex;

        //tags
    }
    
    [Flags]
    public enum ZoneLaneLinkType : uint
    {
        None				= 0,
        All					= uint.MaxValue,
        Outgoing			= 1 << 0,	// The lane is connected at the end of the current lane and going out.
        Incoming			= 1 << 1,	// The lane is connected at the beginning of the current lane and coming in.
        Adjacent			= 1 << 2,	// The lane is in same zone, immediately adjacent to the current lane, can be opposite direction or not, see EZoneLaneLinkFlags
    };
    
    [Flags]
    public enum ZoneLaneLinkFlags : uint
    {
        None				= 0,
        All					= uint.MaxValue,
        Left				= 1 << 0,	// Left of the current lane
        Right				= 1 << 1,	// Right of the current lane
        Splitting			= 1 << 2,	// Splitting from current lane at start
        Merging				= 1 << 3,	// Merging into the current lane at end
        OppositeDirection	= 1 << 4,	// Opposition direction than current lane
    };
    
    public struct ZoneLaneLinkData
    {
        public int DestinationLaneIndex;
        public ZoneLaneLinkType Type;
        public ZoneLaneLinkFlags Flags;
    }

    /* Link for a specified lane, used during building */
    public struct ZoneShapeLaneInternalLink
    {
        /*Lane index to which the link belongs to*/
        public int LaneIndex;
        /*Link details*/
        public ZoneLaneLinkData LinkData;
    }
    
    /*Point in a lane*/
    [BakingType]
    public struct ZoneShapePoint : IBufferElementData
    {
        public float3 Position;
        public float3 Tangent;
        public float3 Up;
        public float3 Right;
    }
}