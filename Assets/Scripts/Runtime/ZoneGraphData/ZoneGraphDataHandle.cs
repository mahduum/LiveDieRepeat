using System;

namespace Runtime.ZoneGraphData
{
    public struct ZoneGraphDataHandle : IEquatable<ZoneGraphDataHandle>
    {
        public static readonly uint InvalidGeneration = 0;

        public uint Index { get; private set; }
        public uint Generation { get; private set; }

        public ZoneGraphDataHandle(uint index, uint generation)
        {
            Index = index;
            Generation = generation;
        }

        public static ZoneGraphDataHandle Default => new ZoneGraphDataHandle(0, InvalidGeneration);
        
        public bool Equals(ZoneGraphDataHandle other)
        {
            return Index == other.Index && Generation == other.Generation;
        }

        public override bool Equals(object obj)
        {
            return obj is ZoneGraphDataHandle other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Index, Generation);
        }
        
        public static bool operator ==(ZoneGraphDataHandle left, ZoneGraphDataHandle right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(ZoneGraphDataHandle left, ZoneGraphDataHandle right)
        {
            return !(left == right);
        }

        public void Reset()
        {
            Index = 0;
            Generation = InvalidGeneration;
        }

        public bool IsValid()
        {
            return Generation != InvalidGeneration;
        }
    }
    
    
    public struct ZoneGraphLaneHandle : IEquatable<ZoneGraphLaneHandle>
    {
        public int Index { get; private set; }
        public ZoneGraphDataHandle DataHandle { get; private set; }
        
        public ZoneGraphLaneHandle(int index, ZoneGraphDataHandle dataHandle)
        {
            Index = index;
            DataHandle = dataHandle;
        }
        
        public static ZoneGraphLaneHandle Default => new ZoneGraphLaneHandle(-1, ZoneGraphDataHandle.Default);

        public bool Equals(ZoneGraphLaneHandle other)
        {
            return Index == other.Index && DataHandle.Equals(other.DataHandle);
        }

        public override bool Equals(object obj)
        {
            return obj is ZoneGraphLaneHandle other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Index, DataHandle);
        }
        
        public static bool operator ==(ZoneGraphLaneHandle left, ZoneGraphLaneHandle right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(ZoneGraphLaneHandle left, ZoneGraphLaneHandle right)
        {
            return !(left == right);
        }
        
        public void Reset()
        {
            Index = -1;
            DataHandle = ZoneGraphDataHandle.Default;
        }

        public override string ToString()
        {
            return $"[{DataHandle.Index}/{Index}]";
        }

        public bool IsValid()
        {
            return Index != -1 && DataHandle.IsValid();
        }
    }
}