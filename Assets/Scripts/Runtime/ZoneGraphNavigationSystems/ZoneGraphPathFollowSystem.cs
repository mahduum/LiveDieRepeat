using Runtime.ZoneGraphNavigationData;
using Runtime.ZoneGraphData;
using Unity.Burst;
using Unity.Entities;
using Unity.Transforms;

namespace Runtime.ZoneGraphNavigationSystems
{
    /*This system updates the transform given short path and signals other systems or entities (see what to choose)
     when the short path is done or lane has changed. These other systems/tasks need to replenish the short path fragment data
     for the further movement to be possible. */
    
    [UpdateBefore(typeof(TransformSystemGroup))]
    public partial struct ZoneGraphPathFollowSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<LocalTransform>();
            state.RequireForUpdate<ZoneGraphShortPath>();
            state.RequireForUpdate<ZoneGraphLaneLocationFragment>();
            state.RequireForUpdate<MoveTarget>();
            state.RequireForUpdate<ZoneGraphDataSource>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {

        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {

        }
    }
    
    /*
     * TODO add Signal system `MassSignalProcessorBase` for entities with used up short path:
     * var archetypeQuery = EntityManager.CreateEntityQuery(typeof(SomeComponent));
    using var chunks = archetypeQuery.CreateArchetypeChunkArray(Allocator.Temp);
    foreach (var chunk in chunks)
    {
    // Iterate through all entities of the same archetype.
    }
    
    Unreal solution: 
    - entities are grouped by signals, and by archetypes
    - SignalEntities ultimate method is virtual, it is overriden by specific specialized system (it is called in Execute, indirection penalty)
    - OnSignalReceived has default implementation, but also can be derived, it mainly needs to adhere to delegate signal signature
        principally is should pass on the signal name and add entities (in a buffer) that subscribed to given signal
        it is flexible because different archetypes may subscribe to same signal and then be sorted.
        In Unity this buffer must be thread safe, it will be one of the command buffers
    - Signals are convenient ways to select and act upon selected singular entities in scene
    - Each signal system can subscribe to unlimited amount of signals, then OnSignalReceived will put signalled entities in a safe command buffer
    - In Unreal the signal dispatcher has reference to signal subsystem -> in Unity this can be a storage entity; this subsystem is also
        references by all signal systems (processors), and all of them use it to subscribe to specific signals; when a signal is sent
        then the signalled entity/entities are added in thread safe way to this system's entity frame buffer (Execute method blocks
        current frame buffer index in a scope lock `ReceivedSignalLock`, and so does the `OnSignalReceived`). What would be the analogous thread
        safe way of doing it in Unity - when signal processor system runs in some group it must not block incoming signals from being added
        to the buffer, and at the same time it needs a buffer of entities ready for processing, it does not as usual perform an entity query,
        signaled entities should be ready in some way - I don't know what way - for processing. DYNAMIC BUFFER?

     */
}