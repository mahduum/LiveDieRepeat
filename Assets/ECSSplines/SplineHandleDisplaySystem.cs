using Authoring;
using Unity.Entities;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;

namespace ECSSplines
{
    [WorldSystemFilter(WorldSystemFilterFlags.Default | WorldSystemFilterFlags.Editor)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateBefore(typeof(EntitiesGraphicsSystem))]
    [CreateAfter(typeof(EntitiesGraphicsSystem))]
    public partial struct SplineHandleDisplaySystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<BeginPresentationEntityCommandBufferSystem.Singleton>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var ecb = SystemAPI.GetSingleton<BeginPresentationEntityCommandBufferSystem.Singleton>();
            var buff = ecb.CreateCommandBuffer(state.WorldUnmanaged);
            //with show and with disabled rendering etc
            //todo remove only the ones that are 
            foreach (var (c, e) //if we add disable this will not pass
                     in SystemAPI.Query<RefRO<ECSSplineHandleVisibleComponent>>().WithAll<DisableRendering>().WithChangeFilter<LocalTransform>().WithEntityAccess())
            {
                //enable rendering, and enable spline component
                //Debug.Log("Enabling rendering");
                buff.RemoveComponent<DisableRendering>(e);
            }

            foreach (var (c, e) in SystemAPI.Query<RefRO<ECSSplineHandleTagComponent>>().WithNone<DisableRendering>().WithDisabled<ECSSplineHandleVisibleComponent>().WithEntityAccess())
            {
                //Debug.Log("Disabling rendering");
                //we add disable rendering once and the previous loop won't run anyway because local transform hasn't changed
                buff.AddComponent<DisableRendering>(e);
            }
            //when to stop rendering, we must have entities without disable rendering, but they also need some flag
        }
    }
}