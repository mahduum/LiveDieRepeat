using Runtime.ZoneGraphData;
using Unity.Entities;

namespace Runtime.ZoneGraphNavigationData
{
    
    // USTRUCT()
    // struct MASSZONEGRAPHNAVIGATION_API FMassZoneGraphNavigationParameters : public FMassSharedFragment
    // {
    // GENERATED_BODY()
    //
    // /** Filter describing which lanes can be used when spawned. */
    // UPROPERTY(EditAnywhere, Category="Navigation")
    // FZoneGraphTagFilter LaneFilter;
    //
    // /** Query radius when trying to find nearest lane when spawned. */
    // UPROPERTY(EditAnywhere, Category="Navigation", meta = (UIMin = 0.0, ClampMin = 0.0, ForceUnits="cm"))
    // float QueryRadius = 500.0f;
    // };
    
    public struct ZoneGraphLaneLocationFragment : IComponentData
    {
        /** Handle to current lane. */
        public ZoneGraphLaneHandle LaneHandle;
        
        /** Distance along current lane. */
        public float DistanceAlongLane;
	
        /** Cached lane length, used for clamping and testing if at end of lane. */
        public float LaneLength;
    }
    
    //todo cached lane and functions
}