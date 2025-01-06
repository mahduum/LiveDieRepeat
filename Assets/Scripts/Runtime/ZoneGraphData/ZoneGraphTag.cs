using UnityEngine;

namespace Runtime.ZoneGraphData
{
    public struct ZoneHandle
    {
        public static readonly ZoneHandle Invalid = new ZoneHandle { Index = InvalidIndex };
    
        public uint Index;
    
        private const uint InvalidIndex = uint.MaxValue;
    
        public bool IsValid() => Index != InvalidIndex;
    
        public static bool operator ==(ZoneHandle lhs, ZoneHandle rhs) => lhs.Index == rhs.Index;
        public static bool operator !=(ZoneHandle lhs, ZoneHandle rhs) => lhs.Index != rhs.Index;
    
        // Overriding Equals and GetHashCode for proper comparison
        public override bool Equals(object obj) => obj is ZoneHandle other && other.Index == this.Index;
        public override int GetHashCode() => Index.GetHashCode();
    }
    
    public struct ZoneGraphTag
    {
        public static readonly ZoneGraphTag None = new ZoneGraphTag { Bit = NoneValue };
    
        private const byte NoneValue = byte.MaxValue;
    
        public byte Bit;
    
        public ZoneGraphTag(byte bit)
        {
            Debug.Assert(bit <= (byte)ZoneGraphTags.MaxTagIndex);
            Bit = bit;
        }
    
        public void Set(byte bit)
        {
            Debug.Assert(bit <= (byte)ZoneGraphTags.MaxTagIndex);
            Bit = bit;
        }
    
        public byte Get() => Bit;
    
        public void Reset() => Bit = NoneValue;
    
        public bool IsValid() => Bit != NoneValue;
    
        public static bool operator ==(ZoneGraphTag lhs, ZoneGraphTag rhs) => lhs.Bit == rhs.Bit;
        public static bool operator !=(ZoneGraphTag lhs, ZoneGraphTag rhs) => lhs.Bit != rhs.Bit;
    }
    
    public enum ZoneGraphTags
    {
        MaxTags = 32,
        MaxTagIndex = MaxTags - 1,
    }
    
    public struct ZoneGraphTagMask
    {
        public static readonly ZoneGraphTagMask All = new ZoneGraphTagMask { Mask = uint.MaxValue };
        public static readonly ZoneGraphTagMask None = new ZoneGraphTagMask { Mask = 0 };
    
        public uint Mask;
    
        public ZoneGraphTagMask(uint mask)
        {
            Mask = mask;
        }
    
        public void Add(ZoneGraphTagMask tags)
        {
            Mask |= tags.Mask;
        }
    
        public void Add(ZoneGraphTag tag)
        {
            if (tag.IsValid())
            {
                Mask |= (1u << tag.Bit);
            }
        }
    
        public void Remove(ZoneGraphTagMask tags)
        {
            Mask &= ~tags.Mask;
        }
    
        public void Remove(ZoneGraphTag tag)
        {
            if (tag.IsValid())
            {
                Mask &= ~(1u << tag.Bit);
            }
        }
    
        public bool ContainsAny(ZoneGraphTagMask tags)
        {
            return (Mask & tags.Mask) != 0;
        }
    
        public bool ContainsAll(ZoneGraphTagMask tags)
        {
            return (Mask & tags.Mask) == tags.Mask;
        }
    
        public bool Contains(ZoneGraphTag tag)
        {
            return tag.IsValid() && (Mask & (1u << tag.Bit)) != 0;
        }
    
        public uint GetValue() => Mask;
    
        public static bool operator ==(ZoneGraphTagMask lhs, ZoneGraphTagMask rhs) => lhs.Mask == rhs.Mask;
        public static bool operator !=(ZoneGraphTagMask lhs, ZoneGraphTagMask rhs) => lhs.Mask != rhs.Mask;
    
        // For bitwise operations
        public static ZoneGraphTagMask operator &(ZoneGraphTagMask lhs, ZoneGraphTagMask rhs) => new ZoneGraphTagMask(lhs.Mask & rhs.Mask);
        public static ZoneGraphTagMask operator |(ZoneGraphTagMask lhs, ZoneGraphTagMask rhs) => new ZoneGraphTagMask(lhs.Mask | rhs.Mask);
        public static ZoneGraphTagMask operator ~(ZoneGraphTagMask mask) => new ZoneGraphTagMask(~mask.Mask);
    }
    
    public struct ZoneGraphTagFilter
    {
        public ZoneGraphTagMask AnyTags;
        public ZoneGraphTagMask AllTags;
        public ZoneGraphTagMask NotTags;
    
        public bool Pass(ZoneGraphTagMask tags)
        {
            return (AnyTags == ZoneGraphTagMask.None || tags.ContainsAny(AnyTags))
                    && (AllTags == ZoneGraphTagMask.None || tags.ContainsAll(AllTags))
                    && (NotTags == ZoneGraphTagMask.None || !tags.ContainsAny(NotTags));
        }
    
        public static bool operator ==(ZoneGraphTagFilter lhs, ZoneGraphTagFilter rhs)
            => lhs.AnyTags == rhs.AnyTags && lhs.AllTags == rhs.AllTags && lhs.NotTags == rhs.NotTags;
    
        public static bool operator !=(ZoneGraphTagFilter lhs, ZoneGraphTagFilter rhs)
            => lhs.AnyTags != rhs.AnyTags || lhs.AllTags != rhs.AllTags || lhs.NotTags != rhs.NotTags;
    }
    
    public struct ZoneGraphTagInfo
    {
        public string Name;
        public Color Color;
        public ZoneGraphTag Tag;
    
        public bool IsValid() => !string.IsNullOrEmpty(Name);
    }
}