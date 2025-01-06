using Data.Quantization;
using Runtime.ZoneGraphData;
using Unity.Mathematics;

namespace Runtime.ZoneGraphNavigationData
{
    public enum MovementAction
    {
        Stand,
        Move,
        Animate
    }
    
    public struct ZoneGraphShortPathRequest
    {
        public override string ToString()
        {
            return $"{(ShouldMoveReverse == 0 ? "forward" : "reverse")} to distance {TargetDistance:F1} Next lane: {(NextLaneHandle.IsValid() ? NextLaneHandle.ToString() : "unset")} of type {(NextLaneHandle.IsValid() ? NextExitLinkType.ToString() : "Unset")} End of path intent: {EndOfPathIntent}";
        }

        public float3 StartPosition; // FVector is typically represented as a 3D float vector (float3)
        public float3 EndOfPathPosition;
        public ZoneGraphLaneHandle NextLaneHandle;
        public float TargetDistance;
        public Snorm8Vector EndOfPathDirection;
        public Int16Real AnticipationDistance;
        public Int16Real EndOfPathOffset;
        public MovementAction EndOfPathIntent;
        public ZoneLaneLinkType NextExitLinkType;
        public byte ShouldMoveReverse; // Bitfield representation using byte for flags
        public byte IsEndOfPathPositionSet; // Bitfield representation using byte for flags
        public byte IsEndOfPathDirectionSet; // Bitfield representation using byte for flags
    }
}