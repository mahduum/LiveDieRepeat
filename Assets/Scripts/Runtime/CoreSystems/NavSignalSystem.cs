using System;
using System.Reflection;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using FieldInfo = System.Reflection.FieldInfo;

namespace Runtime.CoreSystems
{
    public partial class NavSignalSystem : OneSignalSystem<CurrentLaneChangedSignal>//for various additional TSignals make generic
    {
        /*
         * Two possible solutions:
         * - Each signal sender has its own buffer and clears it upon adding new signals.
         * - Signal receiver has its own one buffer to which other systems are adding, however for this to work it would need to have a method for it and swap/block additions.
         * - Both buffers must contain ranges and entities. For swapping it there would need to be two interchangeable entities each with its own pair of buffers per signal system.
         * - Both buffers are cleared after having been processed.
         * - There could be a "subsytem" one for all entity, that would contain all the signals and respective "OnSignalReceived" delegates? And OnCreate each system would subscribe,
         *      but this might be unncecessary, system can query signals directly?
         */

        protected override void OnCreate()
        {
            base.OnCreate();
            //todo: this is deprecated, now signal senders with create their buffers:
            //Entity entity = EntityManager.CreateEntity();
            var systemEntities = EntityManager.UniversalQueryWithSystems;
            Type systemHandleType = typeof(SystemHandle);

            // Get the FieldInfo for the internal m_Entity field
            FieldInfo entityField = systemHandleType.GetField("m_Entity", BindingFlags.NonPublic | BindingFlags.Instance);

            if (entityField != null)
            {
                // Get the value of the m_Entity field from the systemHandle instance
                Entity entity = (Entity)entityField.GetValue(SystemHandle);
                
                var buffer = EntityManager.AddBuffer<EntitySignalRange>(entity);

                buffer.Add(new EntitySignalRange());
                buffer.Add(new EntitySignalRange());
                buffer.Add(new EntitySignalRange());

                // Output the Entity (or use it for other operations)
                Console.WriteLine("Entity from SystemHandle: " + entity);
            }
            else
            {
                Console.WriteLine("m_Entity field not found.");
            }

            unsafe
            {
                int structSize = UnsafeUtility.SizeOf<SystemHandle>();

                // Allocate memory for the struct
                byte* structMemory = (byte*)UnsafeUtility.Malloc(structSize, 4, Unity.Collections.Allocator.Temp);
                
                try
                {
                    var systemHandle = SystemHandle;
                    // Copy the struct to unmanaged memory
                    UnsafeUtility.CopyStructureToPtr(ref systemHandle, structMemory);

                    // Access specific fields by offset
                    // Assuming the struct is laid out sequentially (default in Unity unless specified otherwise):
                    // Offset of m_Entity
                    FieldInfo handlePtrField = systemHandleType.GetField("m_Handle", BindingFlags.NonPublic | BindingFlags.Instance);
                    Entity* entityPtr = (Entity*)structMemory;
                    ushort* handlePtr = (ushort*)(structMemory + UnsafeUtility.GetFieldOffset(handlePtrField));

                    UnityEngine.Debug.Log($"Entity: Index={entityPtr->Index}, Version={entityPtr->Version}");
                    UnityEngine.Debug.Log($"Handle: {*handlePtr}");
                }
                finally
                {
                    // Free the allocated memory
                    UnsafeUtility.Free(structMemory, Unity.Collections.Allocator.Temp);
                }
                
            }
            
            var sys = SystemHandle;
            var asEntity = UnsafeUtility.As<SystemHandle, Entity>(ref sys);
            var buffer2 = EntityManager.AddBuffer<EntitySignalRange>(asEntity);

            buffer2.Add(new EntitySignalRange());
            buffer2.Add(new EntitySignalRange());
            buffer2.Add(new EntitySignalRange());

            var signalQuery = SystemAPI.QueryBuilder()
                .WithAll<EntitySignalRange, CurrentLaneChangedSignal>().Build();
        }

        //DEPRECATED
        protected NativeHashMap<TypeIndex, NativeArray<Entity>> GetSignalEntityMap()
        {
            //to be generic, derived class can return range with entities, range has signal typeindex
            //if there is a systemBase SignalSystemBase<TSignal> where TSignal : IBufferElementData, and we can have many signals: SignalSystemBase<T1, T2, ...>
            // then each template will have an abstract query that it can run on its own: 
            /*
             * SignalQuery = SystemAPI.QueryBuilder()
                .WithAll<EntitySignalRange, TSignal>().Build();
                
                and can have a reusable method for all derived systems
                
                it needs an array of all queries for each type. 
                
                the base will only have a hashmap of TypeIndex to query, 
             */
            int signalEntitiesCount = 0;
            foreach (var (signalRange, laneSignal) in SystemAPI
                         .Query<DynamicBuffer<EntitySignalRange>, DynamicBuffer<CurrentLaneChangedSignal>>())
            {
                //add each buffer content to the map by index, count all buffers
                //use range data and buffer 
                
                /*
                 * the base needs just the array of entities with specific signal index and signal rane so it can process further:
                 * - merge signal ranges, and process given signal
                 * - here we are grouping buffers for the same signal from potentially different sources, so each source must be kept with their own buffer
                 * - when merging ranges for the same signal:
                 * for (var (ranges, EntityArray) in allBuffers){
                 * 
                 * process each array separately with its range, and add to multihashmap by archetype, which will be processed by base
                 * it would be better if the base could simply receive entities from the buffer. system could be T - signal, like in ISignalSystem version? T is interface type IEntityContainer, GetEntity...
                 *
                 * what if there are many signals? we would need to group them by index just the same, each type index would need to have new merged range with it.
                 *
                 * on loop finish:
                 * var processedArrayCounts += EntityArray.Lenght
                 * }
                 *
                 * Temp: merge ranges for given buffer
                 */
                
                signalEntitiesCount += laneSignal.Length;//repeat for each signal.
            }

            foreach (var (signalRange, laneSignal) in SystemAPI
                         .Query<DynamicBuffer<EntitySignalRange>, DynamicBuffer<CurrentLaneChangedSignal>>())
            {
                
            }

            return new NativeHashMap<TypeIndex, NativeArray<Entity>>(0, Allocator.Temp);
        }

        protected override void SignalEntities(NativeArray<Entity> entities, TypeIndex signalType)
        {
            Debug.Log($"Signalling entities in count {entities.Length}, for signal type {TypeManager.GetType(signalType)}");
        }
    }
}