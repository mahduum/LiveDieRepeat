using Runtime.CoreSystems;
using Runtime.ZoneGraphNavigationData;
using Runtime.ZoneGraphData;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Transforms;

namespace Runtime.ZoneGraphNavigationSystems
{
    /*This system updates the transform given short path and signals other systems or entities (see what to choose)
     when the short path is done or lane has changed. These other systems/tasks need to replenish the short path fragment data
     for the further movement to be possible. */

    [UpdateBefore(typeof(TransformSystemGroup))]
    public partial struct ZoneGraphPathFollowSystem : ISystem, ISystemStartStop
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<LocalTransform>();
            state.RequireForUpdate<ZoneGraphShortPath>();
            state.RequireForUpdate<ZoneGraphLaneLocationFragment>();
            state.RequireForUpdate<MoveTarget>();
            state.RequireForUpdate<ZoneGraphDataSource>();
            
            //todo add buffer
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            //This will get the individual buffer for this system entity, if we want this system to be able to send signals this way, it must have this buffer on it.
            var signalEntities = new NativeList<CurrentLaneChangedSignal>(state.WorldUpdateAllocator);
            
            //TODO create buffer entity just for this system for each signal type that will have <SignalTypeComponent, AllComponentsFromGivenQuery, UniqueSystemComponent>???
            
            foreach (var entity in SystemAPI.Query<RefRO<ZoneGraphLaneLocationFragment>>().WithEntityAccess())
            {
                //entities signaled with given signal are expected to have components adequate to this signal actions, for example lane chaged signal will expect signaled entity to have ZoneLaneLocationFragment etc.
                //generally if something than signal:
                signalEntities.Add(
                    new CurrentLaneChangedSignal() { SignaledEntity = entity.Item2 }
                );
            }
            //acclaiming first archetype will commit given archetype range, if we use ToArchetypeChunks, then there is no need to keep trace of archetype ranges:
            SignalSystemBase.SignalEntities(ref state, signalEntities);
            
            //todo there may be same signal for another archetype here:
            //... (can a method detect whether to add another range)?
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
        }

        public void OnStartRunning(ref SystemState state)
        {
            var systemHandle = state.SystemHandle;
            var systemEntity = UnsafeUtility.As<SystemHandle, Entity>(ref systemHandle);
            state.EntityManager.AddBuffer<CurrentLaneChangedSignal>(systemEntity);
        }

        public void OnStopRunning(ref SystemState state)
        {
            var systemHandle = state.SystemHandle;
            var systemEntity = UnsafeUtility.As<SystemHandle, Entity>(ref systemHandle);
            state.EntityManager.RemoveComponent<CurrentLaneChangedSignal>(systemEntity);
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

    - override function is ok if it is called only once per update, `SignalEntities` would receive the list of entities to signal
    with specific logic, which is usually choosing the entities that should be signalled, and then calling them from
    a hash map by delegate or overriden system function (OnSignalReceived), blob asset or dynamic buffer keeps hash map.
    - hash map: signal name or id mapped to given system entity
     */

    //Sequence/structure:
    //sub to signal in on create; when entities are being signalled externally, by means of a subsystem (blob asset or other entity here)
    //entities are mapped to a particular signal, that signal might be another component data marker, and it can be linked to a given subsystem,
    //the signaller must get that entity and write to it, the single system's entity may have many different such components
    //Execute sorts entities by archetype and calls signal entities by archetype set IMPORTANT: it acts on entities collection from delegate OnReceviedSignal which are currently in the buffer!
    //then overriden SignalEntities is called, examples:
    /*
     * CrowdLaneTrackingSignalProcessor:
     * - Calls OnEntityLaneChanged event on a subsytem with entity index
     * - Assigns to FMassCrowdLaneTrackingFragment a new handle to lane location.
     *
     *  UMassStateTreeProcessor:
     * - Ticks the tree execution context for given entity with adjusted time and adds entities to be signalled again if needed to postpone then at the end signals them again.
     */

    /*
     * Adding component data to a system:
     * public class CameraFromEntitySync : MonoBehaviour
    {
        [SerializeField] private Camera _camera;

        private CameraSystem _cameraSystem;
        private EntityManager _entityManager;

        private bool _velocityChanged;
        private bool _rotationChanged;

        private void Awake()
        {
            _entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
            _cameraSystem = World.DefaultGameObjectInjectionWorld.GetOrCreateSystemManaged<CameraSystem>();
            _entityManager.AddComponentData(_cameraSystem.SystemHandle, new CameraWrapperComponent()
            {
                Camera = _camera,
            });
        }
    }

    and get component data from system:

    protected override void OnUpdate()
        {
            var cameraWrapper = EntityManager.GetComponentData<CameraWrapperComponent>(SystemHandle);
            var camera = cameraWrapper.Camera;
            var player = SystemAPI.GetSingletonEntity<PlayerComponent>();
            var playerTransform = SystemAPI.GetComponentRO<LocalTransform>(player);

            var offsetToPlayer = new float3 (0, 0, -30f);
            camera.transform.position = playerTransform.ValueRO.Position + offsetToPlayer;
        }

    or:

    [UpdateInGroup(typeof(InitializationSystemGroup))]
public partial struct SignalSystemInitialization : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        // Get the entity associated with this system
        var systemEntity = state.SystemHandle.GetEntity();

        // Check if the entity already has a SignalSubscription
        if (!state.EntityManager.HasComponent<SignalSubscription>(systemEntity))
        {
            // Add the SignalSubscription component
            state.EntityManager.AddComponentData(systemEntity, new SignalSubscription
            {
                SignalName = "ExampleSignal"
            });

            // Add a DynamicBuffer<SignalBuffer> for processing signals
            state.EntityManager.AddBuffer<SignalBuffer>(systemEntity);
        }
    }
}
     */

    /*
     * Generated example:
     *
public struct Signal : IBufferElementData
{
    public FixedString64Bytes Name; // Signal name
}

public struct SignalEntity : IBufferElementData
{
    public Entity Entity; // Entity subscribing to the signal
}

public struct SignalSubscription : IBufferElementData
{
    public FixedString64Bytes SignalName; // Name of the signal
    public Entity SignalBufferEntity;    // Reference to the entity holding the dynamic buffer
}

public struct SignalManager : IComponentData { }

[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial struct SignalDispatchSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        // Query SignalManager to find active subscriptions
        Entities.WithAll<SignalManager>().ForEach(
            (Entity manager, ref DynamicBuffer<SignalSubscription> subscriptions) =>
            {
                foreach (var subscription in subscriptions)
                {
                    // Example: Dispatch a signal to specific entities
                    var signalBuffer = state.EntityManager.GetBuffer<SignalEntity>(subscription.SignalBufferEntity);
                    signalBuffer.Add(new SignalEntity { Entity = /* Example: signaled entity * / });
                }
            }).Run();
    }
}

[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial struct SignalProcessorSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        // Query SignalManager and process signals
        Entities.WithAll<SignalManager>().ForEach(
            (Entity manager, ref DynamicBuffer<SignalSubscription> subscriptions) =>
            {
                foreach (var subscription in subscriptions)
                {
                    var signalBuffer = state.EntityManager.GetBuffer<SignalEntity>(subscription.SignalBufferEntity);

                    // Process each entity in the signal's buffer
                    foreach (var signalEntity in signalBuffer)
                    {
                        var entity = signalEntity.Entity;
                        // Execute custom processing logic for the signaled entity
                    }

                    // Clear the buffer after processing
                    signalBuffer.Clear();
                }
            }).Run();
    }
}

public partial struct SignalRegistrationSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        var ecb = new EntityCommandBuffer(Allocator.Temp);

        Entities.WithAll<SignalManager>().ForEach(
            (Entity manager, ref DynamicBuffer<SignalSubscription> subscriptions) =>
            {
                // Example: Register a signal
                var signalBufferEntity = ecb.CreateEntity();
                ecb.AddBuffer<SignalEntity>(signalBufferEntity);

                subscriptions.Add(new SignalSubscription
                {
                    SignalName = "ExampleSignal",
                    SignalBufferEntity = signalBufferEntity
                });
            }).Run();

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}

public class DynamicBufferExample : MonoBehaviour
{
    // Reference to the EntityManager (usually done via World.DefaultGameObjectInjectionWorld)
    private EntityManager entityManager;

    // Entity you want to modify
    private Entity myEntity;

    // Buffer element data you want to add
    private int newElementData = 42;

    private void Start()
    {
        // Initialize EntityManager
        entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;

        // Create the entity (or get it from somewhere)
        myEntity = entityManager.CreateEntity(typeof(DynamicBuffer<int>));

        // Adding a new element to the DynamicBuffer from MonoBehaviour using ECB
        AddToDynamicBuffer();
    }

    private void AddToDynamicBuffer()
    {
        // Create an EntityCommandBuffer (ECB) to safely modify entities from MonoBehaviour
        EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.Temp);

        // Schedule the operation of adding a new element to the DynamicBuffer
        ecb.AppendToBuffer(myEntity, newElementData);

        // Playback the ECB to apply the changes
        ecb.Playback(entityManager);
        ecb.Dispose();
    }
}

Signal system can inherit


public class Program
{
    public interface IProc
    {
        void Do();
    }

    public class ProcA : IProc
    {
        //public ProcA(){}

        public void Do()
        {
            Console.WriteLine("Doing is better than not.");
        }
    }

    public class System<T> where T : IProc, new()
    {
        protected T t = new T();
        public void DoMyWay()
        {
            t.Do();
        }
    }

    public class SystemA : System<ProcA>
    {

    }


    public static void Main()
    {
        var sysA = new SystemA();
        sysA.DoMyWay();
    }
}
     */
}