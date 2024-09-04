using System;
using System.Collections.Generic;
using Data;
using DataUtilities;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Runtime.ZoneGraphData
{
    public struct ZoneData
    {
        public int BoundaryPointsBegin;//it is only needed to build storage and later for debug
        /*One past index*/
        public int BoundaryPointsEnd;
        /*Indices range to all lanes in, can this be made differently in a buffer? */
        public int LanesBegin;//used for queries the range in which to look for the nearest lanes
        public int LanesEnd;
        public MinMaxAABB Bounds;//for spacial sorting in BVTree or Grid
        //masktags
    }

    public struct ZoneLaneData
    {
        public float Width;
        /*Indices range to all LanePoints in storage*/
        public int PointsBegin;
        public int PointsEnd;//blob pointer?
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
        /*Lane index within given zone to which the link belongs to*/
        public int LaneIndex;
        /*Link details*/
        public ZoneLaneLinkData LinkData;
    }

    public class ZoneShapeLaneInternalLinkComparer : IComparer<ZoneShapeLaneInternalLink>
    {
        public int Compare(ZoneShapeLaneInternalLink x, ZoneShapeLaneInternalLink y)
        {
            return x.LaneIndex.CompareTo(y.LaneIndex);
        }
    }
    
    /*Point in a lane, consider using it not only as a baking type*/
    //[BakingType]
    public struct ZoneShapePoint : IBufferElementData
    {
        public float3 Position;
        public float3 Tangent;
        public float3 Up;
        public float3 Right;
    }

    public class LookUpHashGrid2d : ICleanupComponentData
    {
        public NativeHierarchicalHashGrid2D<Entity> Grid;
    }

    /*Represents a location where shapes can be connected together.
     Belongs to array on a single ZoneShape. In Spline type only start and end point
     are connectors, but in Polygon type each point is a connector.*/
    public struct ZoneShapeConnector
    {
        /*Reference to the profile, used for compatibility comparisons (to connect to connectors their profiles must be identical.*/
        public IZoneLaneProfile Profile;
        public Vector3 Position;
        public Vector3 Normal;
        public Vector3 Up;
        /*Point index of ZoneShape Mono, as in list returned by GetShapesAsPoints, is needed only to access shape points
         in editor for blending the connections (lookup Unreal ZoneShapeComponent class)*/
        public int PointIndex;
        public bool IsLaneProfileReversed;
        public ZoneShapeType ShapeType;//polygon or spline

    }

    /*Represents a connection between two shape connectors.
     Is ZoneShapeComponent property, has a reference to other connected shape 
     and index of a connector on that shape.*/
    public struct ZoneShapeConnection
    {
        public WeakReference<ZoneShape> ConnectedShape;
        /*Connector index at the connected shape*/
        public int ConnectorIndex;
    }
    
    
    /*what shape entity needs:
     1. Points.
     2. Bounds.
     3. Lanes.
     */
    
}