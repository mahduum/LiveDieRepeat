using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using UnityEngine;

namespace Authoring
{
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class ECSSplineUIHandleAuthoring : MonoBehaviour
    {
        private class ECSSplineUIHandleBaker : Baker<ECSSplineUIHandleAuthoring>
        {
            public override void Bake(ECSSplineUIHandleAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent<ECSSplineHandleTagComponent>(entity); //we will create this entity when in mode
                AddComponent<DisableRendering>(entity);
                AddComponent<ECSSplineHandleVisibleComponent>(entity);
                AddComponent<PositionDelta>(entity);
                AddComponent<MoveDirection>(entity);
            }
        }
    }
    
    public struct ECSSplineHandleTagComponent : IComponentData
    {
    }

    public struct ECSSplineHandleVisibleComponent : IComponentData, IEnableableComponent
    {
        
    }
}