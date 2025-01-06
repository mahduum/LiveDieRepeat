using Unity.Entities;
using Unity.Mathematics;

namespace Runtime.ZoneGraphNavigationData
{
    public struct MoveTarget : IComponentData
    {
        // Center of the move target.
        public float3 Center;
    
        // Forward direction of the movement target.
        public float3 Forward;
    
        // Distance remaining to the movement goal.
        public float DistanceToGoal;
    
        // Allowed deviation around the movement target.
        public float SlackRadius;
    
        // Requested movement speed.
        public float DesiredSpeed;
    
        // Intended movement action at the target.
        public MovementAction IntentAtGoal;
    
        // Current movement action.
        public MovementAction CurrentAction;
    
        // Previous movement action.
        public MovementAction PreviousAction;
    
        // True if the movement target is assumed to be outside navigation boundaries.
        public bool IsOffBoundaries;
    
        // True if the movement target is assumed to be falling behind in steering.
        public bool SteeringFallingBehind;
    
        // World time in seconds when the action started.
        public double CurrentActionWorldStartTime;
    
        // Server time in seconds when the action started.
        public double CurrentActionServerStartTime;
    
        // Number incremented each time new action (e.g., move, stand, animation) is started.
        public ushort CurrentActionID;
    
        // Convert to string for debugging or display purposes.
        public override string ToString()
        {
            return $"Move Target: {Center}, Forward: {Forward}, DistanceToGoal: {DistanceToGoal}, SlackRadius: {SlackRadius}";
        }
    }
}