using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using UnityEngine.Scripting;

namespace Runtime.CoreSystems
{
    /*This system has its own dynamic buffers for signals and ranges, it will enqueue signals additions in command buffer and
     play them out at the end of simulation stage, and process them in update at the beginning of the simulation stage next frame.
     
     This one adds indiscriminately signals
     - buffer a: signals converted to entities
     - buffer b: size of addition and signal type of the addition
     
    On its own update it sorts buffer into subunits per signal and archetype and sends them to systems that subscribed for given signal?
    It would effectively need to mirror the Unreal's solution with delegates*/
    [UpdateBefore(typeof(BeginSimulationEntityCommandBufferSystem))]
    [RequireDerived]
    public abstract partial class SignalConductorSystemBase : SystemBase
    {
        /*Todo */
        protected abstract void SignalEntities(NativeArray<Entity> entities, TypeIndex signalType);
        
        protected override void OnUpdate()
        {
            //DynamicBuffer<int> intBuffer
                //= EntityManager.GetBuffer<MyBufferElement>(entity).Reinterpret<int>();
            //todo sort buffers by archetypes, and send signals by archetypes:
        }
        
        public void AddSignals(NativeArray<Entity> signalEntities, TypeIndex signalType, SystemHandle sender)
        {
            //NOTE: can be called from outside by: 
            //World.GetExistingSystemManaged<SignalSystemBase>().AddSignals(signalEntities, signalType, sender);
            
            //Many external systems would add to this 
            var ecbBufferSystem = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
            var writer = ecbBufferSystem.CreateCommandBuffer(EntityManager.WorldUnmanaged).AsParallelWriter();//don't need this to be parallel if this has not multiple jobs writing to it within this system or other systems jobs, each call from another system will create another pending buffer and add it to list
            var bufferEntitySystemHandle = SystemHandle;
            
            //Next when the buffer it played at the end of the simulation stage, all signals are effectively added and can be sorted and sent at the next begin simulation stage (or earlier?)
            
            //todo maybe don't need to use a job, sort index in command buffer will be enough
            //but it may be called from a different thread, for example a system can perform a job, 
            //and in this job it will write to the same signals, but this won't happen as it is supposed to add all singals of given type at once
            //it could have its own command buffer, where it will collect all entities to signal and then perform the signaling 
            //on playback of the buffer
            
            //here adding happens in a command buffer end simulation. reading on begin simulation (for example)
            //in unreal there is a simple lock, can write any time, but has to wait if there is a lock, and something else is adding
            //and the buffer is swapped on write.
            //Q: is there a double dynamic buffer in unity?
            // lock (expression)
            // {
            //     
            // }
            
            //
            var addSignalsJob = new AddSignalsJob()
            {
                ecb = writer,
                entities = signalEntities,
                bufferEntity =
                    UnsafeUtility.As<SystemHandle, Entity>(
                        ref bufferEntitySystemHandle), //todo it is this system's assocated entity
                signalSender = sender
            };
            
            addSignalsJob.Schedule(signalEntities.Length, 64, Dependency);//todo what if job is scheduled outside update?
            //add queue with job handles
        }
    }
    
    //parallel for entity that owns the bufferS
    public partial struct AddSignalsJob : IJobParallelFor//how?
    {
        public EntityCommandBuffer.ParallelWriter ecb;
        public NativeArray<Entity> entities;
        public Entity bufferEntity;
        public SystemHandle signalSender;
        public void Execute(int index)//for all entities with this buffer, set lengts to passed entities
        {
            var sender = signalSender;
            //sort key must be unique per thread, so it can be id of a system that schedules adding signals, NOTE: for ecb there is no need to use EntityIndexInQuery
            ecb.AppendToBuffer(UnsafeUtility.As<SystemHandle, Entity>(ref sender).Index/*with version?*/, bufferEntity, new CurrentLaneChangedSignal(){SignaledEntity = entities[index]});
            //TODO set ranges, they will be sorted too, but then I need to pass only the spans of the ranges, without begin/end, end then iterate the spans and increment the start index of consecutive portions
            //... and other parallel buffer for the range (count) of this addition here: ...
        }
    }
}