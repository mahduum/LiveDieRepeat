using Runtime.ZoneGraphNavigationData;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using UnityEngine;

namespace Runtime.CoreSystems
{
    /*Signal processor is tied to a particular buffer? no, each system can process the same signal in a different way.
     In fact, one single system processes various signals in the same way.*/
    public interface ISignalProcessor
    {
        void OnSignalReceived(NativeArray<Entity> signalledEntities);
        void SignalEntities(NativeArray<Entity> signalledEntities);
    }
    
    public interface ISignalSystem<T> : ISystem where T : unmanaged, ISignalProcessor
    {
        ISignalProcessor SignalProcessor { get; }
    }

    public struct NavigationSignalProcessor : ISignalProcessor
    {
        public void OnSignalReceived(NativeArray<Entity> signalledEntities)
        {
            throw new System.NotImplementedException();
        }

        public void SignalEntities(NativeArray<Entity> signalledEntities)
        {
            throw new System.NotImplementedException();
        }
    }
    
    [BurstCompile]
    public partial struct NavigationSignalSystem : ISignalSystem<NavigationSignalProcessor>
    {
        public ISignalProcessor SignalProcessor => new NavigationSignalProcessor();
        
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            //state.EntityManager.AddBuffer<CurrentLaneChangedSignal>(state.SystemHandle);
            //UnsafeUtility.As<SystemHandle, Entity>(state.SystemHandleUntyped);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            return;
            var buffer = state.EntityManager.GetBuffer<CurrentLaneChangedSignal>(state.SystemHandle).ToNativeArray(state.WorldUpdateAllocator);

            SignalProcessor.SignalEntities(new NativeArray<Entity>(1, Allocator.Temp));//todo maybe it would be better with a base system, for real inheritance solution

            foreach (var signal in buffer)
            {
                //Debug.Log(signal.SignalledEntity.Index);
            }

            foreach (var lane in SystemAPI.Query<RefRO<ZoneGraphCachedLaneFragment>>())
            {
                Debug.Log(lane.ToString());
            }
            //todo check for reference DynamicBuffer<Waypoint> waypoints = state.EntityManager.AddBuffer<Waypoint>(entity);
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {

        }
    }
}