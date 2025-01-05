using Unity.Mathematics;

namespace Data.Quantization
{
    /** Float encoded in int16, 1cm accuracy. */
    public struct Int16Real
    {
        // 1cm accuracy in Unreal (converted to meters, 1cm = 0.01m in Unity)
        private const float Scale = 100.0f;
        
        private short _value;
    
        public Int16Real(float value)
        {
            _value = (short)math.clamp(math.round(value * Scale), short.MinValue, short.MaxValue);
        }
    
        public void Set(float value)
        {
            _value = (short)math.clamp(math.round(value * Scale), short.MinValue, short.MaxValue);
        }
    
        public float Get()
        {
            return _value * (1.0f/Scale);
        }
    
        public static bool operator <(Int16Real lhs, Int16Real rhs) => lhs._value < rhs._value;
        public static bool operator <=(Int16Real lhs, Int16Real rhs) => lhs._value <= rhs._value;
        public static bool operator >(Int16Real lhs, Int16Real rhs) => lhs._value > rhs._value;
        public static bool operator >=(Int16Real lhs, Int16Real rhs) => lhs._value >= rhs._value;
        public static bool operator ==(Int16Real lhs, Int16Real rhs) => lhs._value == rhs._value;
        public static bool operator !=(Int16Real lhs, Int16Real rhs) => lhs._value != rhs._value;
    }
    
    public struct Int16Real10
    {
        // 10cm accuracy in Unreal (converted to meters, 10cm = 0.1m in Unity)
        private const float Scale = 10.0f;

        private short _value;
    
        public Int16Real10(float value)
        {
            _value = (short)math.clamp(math.round(value * Scale), short.MinValue, short.MaxValue);
        }
    
        public void Set(float value)
        {
            _value = (short)math.clamp(math.round(value * Scale), short.MinValue, short.MaxValue);
        }
    
        public float Get()
        {
            return _value * (1.0f/Scale);
        }
    
        public static bool operator <(Int16Real10 lhs, Int16Real10 rhs) => lhs._value < rhs._value;
        public static bool operator <=(Int16Real10 lhs, Int16Real10 rhs) => lhs._value <= rhs._value;
        public static bool operator >(Int16Real10 lhs, Int16Real10 rhs) => lhs._value > rhs._value;
        public static bool operator >=(Int16Real10 lhs, Int16Real10 rhs) => lhs._value >= rhs._value;
        public static bool operator ==(Int16Real10 lhs, Int16Real10 rhs) => lhs._value == rhs._value;
        public static bool operator !=(Int16Real10 lhs, Int16Real10 rhs) => lhs._value != rhs._value;
    }
    
    /* Vector which components are in range [-1..1], encoded in signed bytes. */
    public struct Snorm8Vector
    {
        // Encodes a vector with components in the range [-1..1], represented as signed bytes (range [-128..127])
        private const float Scale = 127f; // Max int8

        private byte X;
        private byte Y;
        private byte Z;
    
        public Snorm8Vector(float3 vector)
        {
            X = (byte)math.clamp((int)math.round(vector.x * Scale), byte.MinValue, byte.MaxValue);
            Y = (byte)math.clamp((int)math.round(vector.y * Scale), byte.MinValue, byte.MaxValue);
            Z = (byte)math.clamp((int)math.round(vector.z * Scale), byte.MinValue, byte.MaxValue);
        }
    
        public void Set(float3 vector)
        {
            X = (byte)math.clamp((int)math.round(vector.x * Scale), byte.MinValue, byte.MaxValue);
            Y = (byte)math.clamp((int)math.round(vector.y * Scale), byte.MinValue, byte.MaxValue);
            Z = (byte)math.clamp((int)math.round(vector.z * Scale), byte.MinValue, byte.MaxValue);
        }
    
        public float3 Get()
        {
            return new float3(X / Scale, Y / Scale, Z / Scale);
        }
    }
    
    /* Vector2D which components are in range [-1..1], encoded in signed bytes. */
    public struct Snorm8Vector2D
    {
        // Encodes a 2D vector with components in the range [-1..1], represented as signed bytes (range [-128..127])
        private const float Scale = 127f; // Max int8

        private byte X;
        private byte Y;
    
        public Snorm8Vector2D(float2 vector)
        {
            X = (byte)math.clamp((int)math.round(vector.x * Scale), byte.MinValue, byte.MaxValue);
            Y = (byte)math.clamp((int)math.round(vector.y * Scale), byte.MinValue, byte.MaxValue);
        }
    
        public void Set(float2 vector)
        {
            X = (byte)math.clamp((int)math.round(vector.x * Scale), byte.MinValue, byte.MaxValue);
            Y = (byte)math.clamp((int)math.round(vector.y * Scale), byte.MinValue, byte.MaxValue);
        }
    
        public float2 Get()
        {
            return new float2(X / Scale, Y / Scale);
        }
    
        public float3 GetVector(float z = 0.0f)
        {
            return new float3(X / Scale, Y / Scale, z);
        }
    }
    
    /* Real in range [0..1], encoded in signed bytes. */
    public struct Unorm8Real
    {
        // Real value encoded in unsigned byte, range [0..1]
        private const float Scale = 255f; // Max uint8

        private byte Value;
    
        public Unorm8Real(float value)
        {
            Value = (byte)math.clamp((int)math.round(value * Scale), (int)byte.MinValue, (int)byte.MaxValue);
        }
    
        public void Set(float value)
        {
            Value = (byte)math.clamp((int)math.round(value * Scale), (int)byte.MinValue, (int)byte.MaxValue);
        }
    
        public float Get()
        {
            return Value / Scale;
        }
    }
    
    public struct Int16Vector
    {
        // Vector encoded in int16, 1cm accuracy (converted to meters, 1cm = 0.01m in Unity)
        private const float Scale = 100f;

        private short X;
        private short Y;
        private short Z;
    
        public Int16Vector(float3 vector)
        {
            X = (short)math.clamp(math.round(vector.x * Scale), short.MinValue, short.MaxValue);
            Y = (short)math.clamp(math.round(vector.y * Scale), short.MinValue, short.MaxValue);
            Z = (short)math.clamp(math.round(vector.z * Scale), short.MinValue, short.MaxValue);
        }
    
        public void Set(float3 vector)
        {
            X = (short)math.clamp(math.round(vector.x * Scale), short.MinValue, short.MaxValue);
            Y = (short)math.clamp(math.round(vector.y * Scale), short.MinValue, short.MaxValue);
            Z = (short)math.clamp(math.round(vector.z * Scale), short.MinValue, short.MaxValue);
        }
    
        public float3 Get()
        {
            return new float3(X * (1.0f / Scale), Y * (1.0f / Scale), Z * (1.0f / Scale));
        }
    }
    
    /* Vector encoded in int16, 1cm accuracy. */
    public struct Int16Vector2D
    {
        // Vector2D encoded in int16, 1cm accuracy (converted to meters, 1cm = 0.01m in Unity)
        private const float Scale = 100f;

        private short X;
        private short Y;
    
        public Int16Vector2D(float2 vector)
        {
            X = (short)math.clamp(math.round(vector.x * Scale), short.MinValue, short.MaxValue);
            Y = (short)math.clamp(math.round(vector.y * Scale), short.MinValue, short.MaxValue);
        }
    
        public void Set(float2 vector)
        {
            X = (short)math.clamp(math.round(vector.x * Scale), short.MinValue, short.MaxValue);
            Y = (short)math.clamp(math.round(vector.y * Scale), short.MinValue, short.MaxValue);
        }
    
        public float2 Get()
        {
            return new float2(X * (1.0f / Scale), Y * (1.0f / Scale));
        }
    
        public float3 GetVector(float z = 0.0f)
        {
            return new float3(X * (1.0f / Scale), Y * (1.0f / Scale), z);
        }
    }
}