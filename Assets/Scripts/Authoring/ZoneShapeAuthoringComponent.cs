using System;
using Runtime;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Splines;

namespace Authoring
{
    //make similar to "Changed" component when we edit the curve
    public class ZoneShapeAuthoringComponent : MonoBehaviour
    {
        [SerializeField] private ZoneShape _zoneShape;
        //todo get indicies of entites to bake, baking system can update changes...
        //add temporary baking type to 
        
        class Baker : Baker<ZoneShapeAuthoringComponent>
        {
            public override void Bake(ZoneShapeAuthoringComponent authoring)
            {
                var parentEntity = GetEntity(TransformUsageFlags.None);
                
                DependsOn(authoring._zoneShape.GetDependency());
                
                AddBuffer<ZoneShapeChangesComponent>(parentEntity);

                var shapes = authoring._zoneShape.GetShapesAsPoints();//todo make native array?

                for (var index = 0; index < shapes.Length; index++)//check if we have data
                {
                    var shape = shapes[index];
                    //todo if shape has data, set key and entity of this shape
                    //there may be other components like this one, we should update storage incrementally, build will filter only the "changed" elements
                    //to reconstruct the tree?

                    //todo use only when editing is finished and only proceed with the single shape?
                    var additionalEntity = CreateAdditionalEntity(TransformUsageFlags.None);
                    var buffer = AddBuffer<ZoneShapePoint>(additionalEntity);
                    buffer.AddRange(
                        shape.ToNativeArray(Allocator.Temp)); //the contents is copied, make all temp from get shapes
                    Debug.Log("baked shape");
                    //todo the rest will be managed by the system
                }

                //todo how to attach changes to main entity? base system? get base system
                //authoring adds it?
            }
        }
    }

    
    public struct ZoneShapeChangesComponent : IBufferElementData
    {
        public Entity ChangedEntity;
    }
    //make baking type with points and profile, it could be a dynamic buffer as we may edit the spline
    //and this buffer will be specific for this entity only!
    // [BakingType]
    // public struct ZoneShapePointsComponent : IBufferElementData
    // {
    //     public float3 Position;
    //     public float3 Tangent;
    //     public float3 Up;
    //     public float3 Right;
    // }
}