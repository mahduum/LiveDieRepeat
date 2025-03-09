using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;

namespace Runtime.CoreSystems
{
    /*
     * This is system is dynamically created consider instead of sending directly
     */
    public partial class SignalSenderBase<TSignal> : SystemBase where TSignal : unmanaged, IBufferElementData
    {
        protected override void OnCreate()
        {
            var sys = SystemHandle;
            var asEntity = UnsafeUtility.As<SystemHandle, Entity>(ref sys);
            var rangesBuffer = EntityManager.AddBuffer<EntitySignalRange>(asEntity);
            var signalBuffer = EntityManager.AddBuffer<TSignal>(asEntity);
        }

        protected override void OnUpdate()
        {
            throw new System.NotImplementedException();
        }
        
        //todo make generic call
        //public void SignalEntities()
    }
}