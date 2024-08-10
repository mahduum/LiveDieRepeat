using System;
using Data;
using Runtime;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using Utilities;

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
                var laneProfile = authoring._zoneShape.GetZoneLaneProfile();

                if (laneProfile == null)
                {
                    return;
                }
                
                var parentEntity = GetEntity(TransformUsageFlags.None);
                
                DependsOn(authoring._zoneShape.GetBakerDependency());
                DependsOn(laneProfile.GetSourceInstance());//if get instance, underlying object
                DependsOn(authoring._zoneShape);
                
                AddBuffer<ZoneShapeChangesComponent>(parentEntity);

                var shapes = authoring._zoneShape.GetShapesAsPoints();//todo make native array?
                var shapesBounds = authoring._zoneShape.GetShapesBounds();

                var blobBuilder = new BlobBuilder(Allocator.Temp);
                ref var laneProfileBlobAsset = ref blobBuilder.ConstructRoot<LaneProfileBlobAsset>();
                
                laneProfileBlobAsset.LanesTotalWidth = laneProfile.GetLanesTotalWidth();
                
                var laneDescArrayBuilder = blobBuilder.Allocate(
                    ref laneProfileBlobAsset.LaneDescriptions,
                    laneProfile.GetLaneDescriptions().Length);
                
                for (int i = 0; i < laneProfile.GetLaneDescriptions().Length; i++)
                {
                    var laneDesc = laneProfile.GetLaneDescriptions()[i];
                    laneDescArrayBuilder[i] = laneDesc;
                }
                
                var laneProfileBlobAssetRef = blobBuilder.CreateBlobAssetReference<LaneProfileBlobAsset>(Allocator.Persistent);
                AddBlobAsset(ref laneProfileBlobAssetRef, out var hash);//de-duplicate for baking systems, but this way new ref needs to be created before adding to store https://docs.unity3d.com/Packages/com.unity.entities@1.0/manual/blob-assets-create.html
                
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
                    //calculate bounds for shape and add it to component registered shape, this will be entity index
                    AddComponent(additionalEntity, new RegisteredShapeComponent());
                    SetComponentEnabled<RegisteredShapeComponent>(additionalEntity, true);//todo true temp for tests
                    AddComponent(additionalEntity, new HashGrid2dBoundsComponent()
                    {
                        Bounds = shapesBounds[index],//these bounds are not including lane profile width, but it is enough for hash grid 2d
                    });
                    
                    AddComponent(additionalEntity, new CellLocationComponent());
                    AddComponent(additionalEntity, new LaneProfileComponent()
                    {
                        LaneProfile = laneProfileBlobAssetRef
                    });
                }
                
                //laneProfileBlobAssetRef.Dispose();
                //todo how to attach changes to main entity? base system? get base system
                //authoring adds it?
                //we have these entities for shapes with points, they should also have their cell location?
                //TODO!!!!!:
                //get system with RegisteredshapeComponent disabled and use bounds to register to grid, and set cell location
                //reference: void FZoneGraphBuilder::RegisterZoneShapeComponent(UZoneShapeComponent& ShapeComp)
                //we can add entity to the grid or we can add component index (whatever index it has in builder
                //NOTE: make system that will create entity hash grid and disable system, or each time the scene is baked
                blobBuilder.Dispose();
            }
        }
    }

    public struct RegisteredShapeComponent : IComponentData, IEnableableComponent//todo enableable??
    {
    }

    public struct HashGrid2dBoundsComponent : IComponentData
    {
        public MinMaxAABB Bounds;
    }

    public struct CellLocationComponent : IComponentData
    {
        public HierarchicalHashGrid2D<Entity>.CellLocation CellLocation;
    }

    public struct LaneProfileComponent : IComponentData
    {
        public BlobAssetReference<LaneProfileBlobAsset> LaneProfile;
    }

    public struct LaneProfileBlobAsset
    {
        public float LanesTotalWidth;
        public BlobArray<ZoneLaneDesc> LaneDescriptions;
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