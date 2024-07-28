using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace ECSSplines
{
    public struct BezierSegment : IBufferElementData
    {
        public float3 P0;
        public float3 P1;
        public float3 P2;
        public float3 P3;
    }

    public struct SplineComponent : IComponentData//will have attached buffer of segments to it
    {
    }
}