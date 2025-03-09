using System;
using System.Linq;
using Runtime.ZoneGraphNavigationSystems;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Entities.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Jobs;
using UnityEngine.Scripting;

namespace Runtime.CoreSystems
{
    /*
     * can use singleton buffers for signals, don't need to be on systems:
     * public partial struct SingletonBufferSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        if (!SystemAPI.HasSingleton<SingletonBufferElement>())
        {
            Entity singletonEntity = state.EntityManager.CreateEntity();
            state.EntityManager.AddBuffer<SingletonBufferElement>(singletonEntity);
        }
    }
}

then signalTypeQueryPair.Value.GetSingletonBuffer<EntitySignalRange>().AsParallel().
     */

    public struct SignalData
    {
        public int EntityCount;
        public int RangeCount;
    }
    
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
        //TODO: MAKE THIS AS ROSLYN GENERATED CODE UTILITY TO USE INSIDE SIGNAL SENDING SYSTEMS:
        public static void SignalEntities<TSignal>(ref SystemState state, NativeList<Entity> entities) where TSignal : unmanaged, IBufferElementData
        {
            var signalBuffer = state.EntityManager.GetBuffer<TSignal>(state.SystemHandle).Reinterpret<Entity>();
            signalBuffer.AddRange(
                entities.AsArray()
            );

            var rangesBuffer = state.EntityManager.GetBuffer<EntitySignalRange>(state.SystemHandle);
            rangesBuffer.Add(new EntitySignalRange() {Begin = 0, End = signalBuffer.Length, IsProcessed = false});//this is incorrect.
        }
        
        public static void SignalEntities<TSignal>(ref SystemState state, NativeList<TSignal> entities) where TSignal : unmanaged, IBufferElementData
        {
            var signalBuffer = state.EntityManager.GetBuffer<TSignal>(state.SystemHandle);
            signalBuffer.AddRange(
                entities.AsArray()
            );

            var rangesBuffer = state.EntityManager.GetBuffer<EntitySignalRange>(state.SystemHandle);
            rangesBuffer.Add(new EntitySignalRange() {Begin = 0, End = signalBuffer.Length, IsProcessed = false});//this is incorrect.
        }
        
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

        /* All queries must contain buffer entities for ranges and some signal. Each derived system will apply one operation characteristic to that system to all signals it was created for.
         This component may be simply assumed to exist with that signal buffer always, or we could do: Query<EntityRangeSignal, TSignal>*/
        protected abstract NativeHashMap<TypeIndex, EntityQuery> GetEntitySignalQueries();

        protected override void OnCreate()
        {
            //EntityManager.CreateEntity() todo create entity and check whether the system will be able to access as associated entity.
        }

        //NOTE: this solution goes over the all buffer entities that belong to the senders.
        protected sealed override void OnUpdate()
        {
            /*TODO only one archetype can exist in a buffer.
            */
                
            var signaledEntitiesCount = 0;
            var rangesCount = 0;
            var signaledEntitiesCountBySignal =
                new NativeHashMap<TypeIndex, SignalData>(GetEntitySignalQueries().Count, Allocator.Temp);//query will have both buffers in order

            //count entities to be signaled
            foreach (var signalTypeQueryPair in
                     GetEntitySignalQueries()) //each query is per signal so there is no overlap, signal buffer may have a tag "Signal", but each buffer is from different sender
            {
                using var
                    chunks = signalTypeQueryPair.Value
                        .ToArchetypeChunkArray(Allocator
                            .Temp); //archetype of dynamic buffer, all buffers of signaled entities sharing the same buffer type
                var signalType =
                    ComponentType.FromTypeIndex(signalTypeQueryPair
                        .Key); //query contains buffers of elements of given type, what if there should be entities in query queried for double buffers, for singals and ranges?
                var signalTypeHandle = GetDynamicComponentTypeHandle(signalType);
                var prevSignalEntitiesCount = signaledEntitiesCount;
                var prevRangesCount = rangesCount;

                foreach (var chunk in chunks)//these are chunks of dynamic buffers
                {
                    UnsafeUntypedBufferAccessor buffer = chunk.GetUntypedBufferAccessor(ref signalTypeHandle);//in each buffer there will be exactly one of any archetype contiguously, two different buffers can have repeating archetype, there is more archetypes than signals
                    signaledEntitiesCount += buffer.Length;//counting all entities in that dyn buffer

                    var rangesHandle = GetBufferTypeHandle<EntitySignalRange>();
                    var rangesBuffer = chunk.GetBufferAccessor(ref rangesHandle);
                    
                    rangesCount += rangesBuffer.Length;
                }

                //add count for each type index separately //todo this might fail later, because the order of archetypes after ToArchetypeChunkArray is not guaranteed to remain the same.
                signaledEntitiesCountBySignal.Add(signalTypeQueryPair.Key,
                    new SignalData()
                    {
                        EntityCount = signaledEntitiesCount - prevSignalEntitiesCount,
                        RangeCount = rangesCount - prevRangesCount,
                    });
            }

            /*
             * Each signal type series in a buffer was added as the same archetype, for each series of elements in that buffer an Entity range was added to mark the series of one archetype
             */
            foreach (var signalTypeQueryPair in
                     GetEntitySignalQueries()) //each query is per signal so there is no overlap, signal buffer may have a tag "Signal"
            {
                var signalsCount = signaledEntitiesCountBySignal[signalTypeQueryPair.Key].EntityCount;
                var signalRangesCount = signaledEntitiesCountBySignal[signalTypeQueryPair.Key].RangeCount;
                var signalArchetypesMap =
                    new NativeParallelMultiHashMap<EntityArchetype, Entity>(signaledEntitiesCount, Allocator.Temp);
                var signalEntities = new NativeArray<Entity>(signalsCount, Allocator.Temp);
                var signalRanges = new NativeArray<EntitySignalRange>(signalRangesCount, Allocator.Temp);
                
                using var chunks = signalTypeQueryPair.Value.ToArchetypeChunkArray(Allocator.Temp);
                ComponentType signalType = ComponentType.FromTypeIndex(signalTypeQueryPair.Key);
                DynamicComponentTypeHandle signalTypeHandle = GetDynamicComponentTypeHandle(signalType);

                var signalsBegin = 0;
                var rangesChunksBegin = 0;

                //each run within higher loop is for system that has buffer and sent signals
                foreach (var chunk in chunks)
                {
                    unsafe
                    {
                        UnsafeUntypedBufferAccessor signalsBuffer = chunk.GetUntypedBufferAccessor(ref signalTypeHandle);

                        for (int i = 0; i < signalsBuffer.Length; ++i)
                        {
                            var ptr = (Entity*) signalsBuffer.GetUnsafeReadOnlyPtrAndLength(i, out int length);
                            for (int j = 0; j < length; j++)
                            {
                                var entity = ptr[j];
                                signalEntities[signalsBegin + j] = entity;
                            }
                            signalsBegin += length;
                        }

                        var rangeBufferTypeHandle = GetBufferTypeHandle<EntitySignalRange>();//returns list of buffers
                        var rangesAccessor = chunk.GetBufferAccessor<EntitySignalRange>(ref rangeBufferTypeHandle);
                        for (int i = 0; i < rangesAccessor.Length; i++)
                        {
                            var rangesBuff = rangesAccessor[i];
                            for (int j = 0; j < rangesBuff.Length; j++)
                            {
                                signalRanges[rangesChunksBegin + j] = rangesBuff[j];
                            }
                            rangesChunksBegin += rangesBuff.Length;
                        }
                    }
                }

                var rangesBegin = 0;
                var signalBuffersEntities = signalTypeQueryPair.Value.ToEntityArray(Allocator.Temp);

                //todo make another version of base since we have already called on chunks array, we could've sorted it there into different archetypes
                foreach (var entity in signalBuffersEntities)
                {
                    //there is buffer for each signal, but each one is from different signal sender, but we are still in the same query, so the order is preserved. 
                    var rangesBuffer = EntityManager.GetBuffer<EntitySignalRange>(entity)
                        .ToNativeArray(Allocator
                            .Temp); //this one will work only for first part of array, last value is the offset
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
        
        private void OnUpdate2()
        {
            /*TODO consider but after measure performance changes since it is already called once I could have another system that used this instead of ranges.: 
            NativeList<ArchetypeChunk> archetypeChunks = SystemAPI.QueryBuilder().WithAll<ZoneGraphLaneLocationFragment>().Build()
                .ToArchetypeChunkListAsync(state.WorldUpdateAllocator, out var jobHandle);
            sort by: archetypeChunks[0].Archetype
            */
                
            var signaledEntitiesCount = 0;
            var signaledEntitiesCountBySignal =
                new NativeHashMap<TypeIndex, int>(GetEntitySignalQueries().Count, Allocator.Temp);

            //count entities to be signaled
            foreach (var signalTypeQueryPair in
                     GetEntitySignalQueries()) //each query is per signal so there is no overlap, signal buffer may have a tag "Signal", but each buffer is from different sender
            {
                using var
                    chunks = signalTypeQueryPair.Value
                        .ToArchetypeChunkArray(Allocator.Temp); //archetype of dynamic buffer, all buffers of signaled entities sharing the same buffer type
                
                var signalType =
                    ComponentType.FromTypeIndex(signalTypeQueryPair
                        .Key); //query contains buffers of elements of given type, what if there should be entities in query queried for double buffers, for singals and ranges?
                var signalTypeHandle = GetDynamicComponentTypeHandle(signalType);

                foreach (var chunk in chunks)
                {
                    unsafe
                    {
                        UnsafeUntypedBufferAccessor buffer = chunk.GetUntypedBufferAccessor(ref signalTypeHandle);
                        signaledEntitiesCount += buffer.Length;
                        
                        NativeArray<Entity> array = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<Entity>(buffer.GetUnsafePtr(0),
                            buffer.Length, WorldUpdateAllocator);
                        array.toar
                    }
                }
                
                var allSignaledEntitiesForSignal = new NativeArray<Entity>(signaledEntitiesCount, Allocator.Temp); 
                
                
                var signalArchetypesMap =
                    new NativeParallelMultiHashMap<EntityArchetype, Entity>(signaledEntitiesCount, Allocator.Temp);
                
                
                
                
                
            }

            /*
             * Each signal type series in a buffer was added as the same archetype, for each series of elements in that buffer an Entity range was added to mark the series of one archetype
             */
            foreach (var signalTypeQueryPair in
                     GetEntitySignalQueries()) //each query is per signal so there is no overlap, signal buffer may have a tag "Signal"
            {
                var signalEntitiesCount = signaledEntitiesCountBySignal[signalTypeQueryPair.Key];
                var signalArchetypesMap =
                    new NativeParallelMultiHashMap<EntityArchetype, Entity>(signaledEntitiesCount, Allocator.Temp);
                var signalEntities = new NativeArray<Entity>(signalEntitiesCount, Allocator.Temp);
                
                using var chunks = signalTypeQueryPair.Value.ToArchetypeChunkArray(Allocator.Temp);
                var signalType = ComponentType.FromTypeIndex(signalTypeQueryPair.Key);
                var signalTypeHandle = GetDynamicComponentTypeHandle(signalType);

                var signalsBegin = 0;

                foreach (var chunk in chunks)//attention, chunks may have different sorting than the original query and later reconstruction with ranges might fail
                {
                    unsafe
                    {
                        UnsafeUntypedBufferAccessor buffer = chunk.GetUntypedBufferAccessor(ref signalTypeHandle);
                        Entity* bufferPtr = (Entity*) buffer.GetUnsafeReadOnlyPtrAndLength(0, out int length);

                        for (int i = 0; i < length; i++)
                        {
                            signalEntities[signalsBegin + i] = bufferPtr[i];//this is based on the same sequence of adding entities by a given sender, by 
                        }

                        signalsBegin += length;
                    }
                }

                var rangesBegin = 0;
                var signalBuffersEntities = signalTypeQueryPair.Value.ToEntityArray(Allocator.Temp);

                //todo make another version of base since we have already called on chunks array, we could've sorted it there into different archetypes
                foreach (var entity in signalBuffersEntities)
                {
                    //there is buffer for each signal, but each one is from different signal sender, but we are still in the same query, so the order is preserved. 
                    var rangesBuffer = EntityManager.GetBuffer<EntitySignalRange>(entity)
                        .ToNativeArray(Allocator
                            .Temp); //this one will work only for first part of array, last value is the offset
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

        /*Todo */
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