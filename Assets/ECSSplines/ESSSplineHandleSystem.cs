using System.Linq;
using Authoring;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;

namespace Physics.Systems
{
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [UpdateAfter(typeof(PhysicsSystemGroup))]
    public partial struct ESSSplineHandleSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ECSSplineHandleTagComponent>();
            state.RequireForUpdate<ScreenPointToRayComponent>();
            //state.RequireForUpdate<ShowSplineHandleComponent>();
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
            state.RequireForUpdate<PhysicsWorldSingleton>();
        }

        //[BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            //update depending on input mode
            PhysicsWorldSingleton physicsWorldSingleton = SystemAPI.GetSingleton<PhysicsWorldSingleton>();

            foreach (var screenPointToRay in SystemAPI.Query<RefRO<ScreenPointToRayComponent>>().WithChangeFilter<ScreenPointToRayComponent>().WithAll<ShowSplineHandleComponent>())//this is data for spline only
            {
                RaycastInput ray = screenPointToRay.ValueRO.Value;
                var splineHandleEntity = SystemAPI.GetSingletonEntity<ECSSplineHandleTagComponent>();
                bool visibilityEnabled =
                    state.EntityManager.IsComponentEnabled<ECSSplineHandleVisibleComponent>(splineHandleEntity);

                if (physicsWorldSingleton.CastRay(ray, out var closestHit))
                {
                    var surfaceNormal = math.normalize(closestHit.SurfaceNormal);
                    var position = closestHit.Position;
                    var hitTransform = SystemAPI.GetComponent<LocalTransform>(closestHit.Entity);
                    var localTransform = SystemAPI.GetComponent<LocalTransform>(splineHandleEntity);
                    var positionDelta = SystemAPI.GetComponent<PositionDelta>(splineHandleEntity);

                    if (visibilityEnabled == false)
                    {
                        state.EntityManager.SetComponentEnabled<ECSSplineHandleVisibleComponent>(splineHandleEntity, true);//todo visibility switch on will be taken care of just by filter
                    }
                
                    LocalTransform transform = LocalTransform.FromPositionRotation(
                        position,
                        quaternion.LookRotation(hitTransform.Forward(), surfaceNormal));

                    float3 moveDirection;
                    if (math.length(position - positionDelta.Current) > 0.05f)//smooth by lerping
                    {
                        positionDelta.Previous = positionDelta.Current;
                        positionDelta.Current = position;
                        
                        SystemAPI.SetComponent(splineHandleEntity, new PositionDelta()
                        {
                            Current = positionDelta.Current,
                            Previous = positionDelta.Previous
                        });
                        
                        moveDirection = math.normalize(positionDelta.Current - positionDelta.Previous);
                        SystemAPI.SetComponent(splineHandleEntity, new MoveDirection()
                        {
                            Value = moveDirection//smooth
                        });
                    }
                    else if (SystemAPI.GetComponent<MoveDirection>(splineHandleEntity).Value is {} direction && direction.x != 0 && direction.y != 0 && direction.z != 0)
                    {
                        moveDirection = direction;
                    }
                    else
                    {
                        moveDirection = new float3(0, 1, 0);
                    }
                    
                    float3 projectionOntoSurfaceNormal = math.project(moveDirection, surfaceNormal);
                    float3 surfaceForward = math.normalize(moveDirection - projectionOntoSurfaceNormal);
                    float3 rightVector = math.normalize(math.cross(surfaceForward, surfaceNormal));
                    float3x3 lookRotation = float3x3.LookRotation(-surfaceNormal, surfaceForward);//forward and normal are switched because of quad orientation
                        //or: new float3x3(-rightVector, surfaceForward, -surfaceNormal);
                    float4x4 rotationMatrix =
                        new float4x4(lookRotation, position);
                    //Debug.Log($"Look rotation: {lookRotation.ToString()}, my rotation matrix: {rotationMatrix.ToString()}");
                    //make delta from previous position
                
                    state.EntityManager.SetComponentData(splineHandleEntity, new LocalTransform()//todo use component data lookup
                    {
                        Position = position + surfaceNormal * 0.01f,//transform.Position,
                        Scale = 1f,
                        Rotation = new quaternion(rotationMatrix)
                    });
                }
                else if (visibilityEnabled)
                {
                    //state.EntityManager.SetComponentEnabled<ECSSplineHandleVisibleComponent>(splineHandleEntity, false);//no change on local transform, should leave last valid location?
                }
            }

            //todo must write only once
            foreach (var (showSpline, entity) in SystemAPI.Query<RefRO<ShowSplineHandleComponent>>().WithDisabled<ScreenPointToRayComponent>().WithEntityAccess())
            {
                var splineHandleEntity = SystemAPI.GetSingletonEntity<ECSSplineHandleTagComponent>();
                state.EntityManager.SetComponentEnabled<ShowSplineHandleComponent>(entity, false);
                state.EntityManager.SetComponentEnabled<ECSSplineHandleVisibleComponent>(splineHandleEntity, false);
            }
            
            //make use of change filter if it was not processed
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {

        }
    }
}