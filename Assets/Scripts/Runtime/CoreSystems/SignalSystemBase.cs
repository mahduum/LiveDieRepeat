using System;
using System.Linq;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Entities.LowLevel.Unsafe;
using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Scripting;

namespace Runtime.CoreSystems
{
    public struct SignalArchetypeKey : IEquatable<SignalArchetypeKey>
    {
        public EntityArchetype Archetype;
        public TypeIndex SignalType;

        public SignalArchetypeKey(EntityArchetype archetype, TypeIndex signalType)
        {
            Archetype = archetype;
            SignalType = signalType;
        }

        public bool Equals(SignalArchetypeKey other)
        {
            return Archetype.Equals(other.Archetype) && SignalType.Equals(other.SignalType);
        }

        public override bool Equals(object obj)
        {
            return obj is SignalArchetypeKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Archetype, SignalType);
        }
    }
    
    [RequireDerived]
    public abstract partial class SignalSystemBase : SystemBase
    {
        public static void RetrieveComponents(EntityManager entityManager, EntityQuery query, SystemBase system)
        {
            // Create ArchetypeChunks from the query
            using var chunks = query.ToArchetypeChunkArray(Allocator.Temp);

            foreach (var chunk in chunks)
            {
                // Get all component types in the archetype, but I can use mapped type index. 
                NativeArray<ComponentType> componentTypes = chunk.Archetype.GetComponentTypes();

                foreach (var componentType in componentTypes)
                {
                    if (componentType.IsZeroSized)
                        continue;
                    
                    // if (componentType.TypeIndex != my typeindex)
                    //     continue;

                    //or get from type index if it is what we have:
                    var th = ComponentType.FromTypeIndex(componentType.TypeIndex);
                    DynamicComponentTypeHandle typeHandle = system.GetDynamicComponentTypeHandle(componentType);

                    // If the chunk has this component type
                    if (chunk.Has(typeHandle))
                    {
                        // Get raw component data, when I don't know the type, 
                        var componentDataArray = chunk.GetDynamicComponentDataArrayReinterpret<Entity>(
                            ref typeHandle, UnsafeUtility.SizeOf<Entity>());

                        foreach (var componentData in componentDataArray)
                        {
                            // Process the raw component data
                            Debug.Log($"Component Data: {componentData}");
                        }
                    }
                }
            }
        }
        //override configure query - unimportant, it operates on buffer, just get the buffer?
        //associated entity will have potentially many signal buffers and as many range buffers.
        //each range buffer must be mapped to singal buffer.
        //start processing a signal buffer, find range buffer to go with it.
        //it would be best if one data component could hold reference to dynamic buffer.
        
        //NOTE: Can be updated manually if it doesn't change entity data by SystemHandle.Update(world), and set Enabled = false to prevent auto update
        //TODO: who clears the buffer?
        //TODO-Note: or each signal sender can create particular signal buffer and then other systems can read it? But each buffer creation will clear all other buffers...
        //NOTE: so each system that can send signals will create its own entity buffer (and will clear it only after receiving some kind of
        //confirmation that the data was used?
        //NOTE: buffers can be spawned as singletons on first update and after that the system can be disabled
        protected abstract NativeHashMap<TypeIndex, NativeArray<Entity>> GetSignalEntityMap();//todo other method to clear the buffer? check in unreal
        protected abstract EntityQuery SignalQuery { get; set; }

        /* All queries must contain buffer entities for ranges and some signal. Each derived system will apply one operation characteristic to that system to all signals it was created for.
         This component may be simply assumed to exist with that signal buffer always, or we could do: Query<EntityRangeSignal, TSignal>*/
        protected abstract NativeHashMap<TypeIndex, EntityQuery> GetEntitySignalQueries();

        protected override void OnCreate()
        {
            //EntityManager.CreateEntity() todo create entity and check whether the system will be able to access as associated entity.
        }

        protected sealed override void OnUpdate()
        {
            var signaledEntitiesCount = 0;
            var signaledEntitiesCountBySignal = new NativeHashMap<TypeIndex, int>(GetEntitySignalQueries().Count, Allocator.Temp);
            
            //count entities to be signaled
            foreach (var signalTypeQueryPair in GetEntitySignalQueries()) //each query is per signal so there is no overlap, signal buffer may have a tag "Signal", but each buffer is from different sender
            {
                using var chunks = signalTypeQueryPair.Value.ToArchetypeChunkArray(Allocator.Temp);//archetype of dynamic buffer, all buffers of signaled entities sharing the same buffer type
                var signalType = ComponentType.FromTypeIndex(signalTypeQueryPair.Key);//query contains buffers of elements of given type, what if there should be entities in query queried for double buffers, for singals and ranges?
                var signalTypeHandle = GetDynamicComponentTypeHandle(signalType);
                var prevSignalEntitiesCount = signaledEntitiesCount;
                
                foreach (var chunk in chunks)
                {
                    UnsafeUntypedBufferAccessor buffer = chunk.GetUntypedBufferAccessor(ref signalTypeHandle);
                    signaledEntitiesCount += buffer.Length;
                }
                
                //add count for each type index separately
                signaledEntitiesCountBySignal.Add(signalTypeQueryPair.Key, signaledEntitiesCount - prevSignalEntitiesCount);
            }

            /*
             * Each signal type series in a buffer was added as the same archetype, for each series of elements in that buffer an Entity range was added to mark the series of one archetype
             */
            foreach (var signalTypeQueryPair in GetEntitySignalQueries())//each query is per signal so there is no overlap, signal buffer may have a tag "Signal"
            {
                var signalEntitiesCount = signaledEntitiesCountBySignal[signalTypeQueryPair.Key];
                var signalArchetypesMap = new NativeParallelMultiHashMap<EntityArchetype, Entity>(signaledEntitiesCount, Allocator.Temp);
                var signalEntities = new NativeArray<Entity>(signalEntitiesCount, Allocator.Temp);
                
                using var chunks = signalTypeQueryPair.Value.ToArchetypeChunkArray(Allocator.Temp);
                var signalType = ComponentType.FromTypeIndex(signalTypeQueryPair.Key);
                var signalTypeHandle = GetDynamicComponentTypeHandle(signalType);

                var signalsBegin = 0;
                
                foreach (var chunk in chunks)
                {
                    unsafe
                    {
                        UnsafeUntypedBufferAccessor buffer = chunk.GetUntypedBufferAccessor(ref signalTypeHandle);
                        Entity* bufferPtr = (Entity*)buffer.GetUnsafeReadOnlyPtrAndLength(0, out int length);
                        
                        for (int i = 0; i < length; i++)
                        {
                            signalEntities[signalsBegin + i] = bufferPtr[i];
                        }
                        
                        signalsBegin += length;
                    }
                }
                
                var rangesBegin = 0;
                var signalBuffersEntities = signalTypeQueryPair.Value.ToEntityArray(Allocator.Temp);
                
                foreach (var entity in signalBuffersEntities)
                {
                    //there is buffer for each signal, but each one is from different signal sender, but we are still in the same query, so the order is preserved. 
                    var rangesBuffer = EntityManager.GetBuffer<EntitySignalRange>(entity).ToNativeArray(Allocator.Temp);//this one will work only for first part of array, last value is the offset
                    for (int i = 0; i < rangesBuffer.Length; i++)
                    {
                        var range = rangesBuffer[i];
                        var begin = rangesBegin + range.Begin;
                        var end = rangesBegin + range.End;
                        var archetype = GetEntityStorageInfoLookup()[signalEntities[range.Begin]].Chunk.Archetype;

                        while (begin != end)
                        {
                            signalArchetypesMap.Add(archetype, signalEntities[begin]);
                            begin++;
                        }

                        range.IsProcessed = true;
                    }
                    
                    rangesBegin += rangesBuffer[^1].End;
                }
                
                //signal by archetype collections, todo debug: check if we are still signaling the same types
                foreach (var archetypeEntitiesPair in signalArchetypesMap)
                {
                    var archetypeCount = signalArchetypesMap.CountValuesForKey(archetypeEntitiesPair.Key);
                    var signalEntitiesByArchetype = new NativeArray<Entity>(archetypeCount, Allocator.Temp);
                    var valuesForKey = signalArchetypesMap.GetValuesForKey(archetypeEntitiesPair.Key);
                    var index = 0;
                    while (valuesForKey.MoveNext())
                    {
                        signalEntitiesByArchetype[index++] = valuesForKey.Current;
                    }
                    
                    SignalEntities(signalEntitiesByArchetype, signalTypeQueryPair.Key);
                    //TODO: mark ranges as used so another system (supposedly signal sender) will know to clear the buffers
                    //TODO: for sending signals, may use an entity with the buffer, and when it gets the array of entities to signal with the signal, it creates the range element by itself
                    //this way system that sends the signal does not need to have its own buffers... but then how we sync adding to same one buffer? we may loose the ranges, because we dont know the order and we fill ranges based on current buffer size
                    //TODO: do test code
                }
                
                //clear all!!!
                signalArchetypesMap.Clear();
            }
        }
        
        protected abstract void SignalEntities(NativeArray<Entity> entities, TypeIndex signalType);
    }

    //Used when signals are being sent to indicate consistent range of same archetype entities
    //need ranges of the same archetype
    //make entity responsible for adding signals, get the entity components
    public struct EntitySignalRange : IBufferElementData
    {
        public bool IsProcessed;//todo remove field?
        public int Begin;
        public int End;
    }
    
    public struct CurrentLaneChangedSignal : IBufferElementData//may have entities of different archetypes
    {
        public Entity SignaledEntity;
    }
}