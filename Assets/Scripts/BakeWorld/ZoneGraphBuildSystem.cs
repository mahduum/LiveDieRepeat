using System.Collections.Generic;
using Authoring;
using Runtime;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace BakeWorld
{
    [WorldSystemFilter(WorldSystemFilterFlags.BakingSystem)]// | WorldSystemFilterFlags.Editor)]
    //[UpdateBefore(typeof(ZoneGraphDebugSystem))]
    public partial struct ZoneGraphBuildSystem : ISystem
    {
        private BlobAssetReference<ZoneGraphStorage> _storageBlobAssetReference;
        
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<LaneProfileComponent>();
            state.RequireForUpdate<ZoneGraphData>();
            //example of changed version:
            //m_ImageGeneratorEntitiesQuery.SetChangedVersionFilter(ComponentType.ReadOnly<ImageGeneratorEntity>());
            //to rerun system only on changed entities, todo unfortunately in current state we can't modify just a part of the graph
            //unless we change the structure to reference entities instead of packing them into a single blob asset
            //todo to enable this kind of incremental build keep entities for shapes and put all the data 
            //directly on them in dynamic buffer, with profile etc. - todo make another version of append to storage (or we won't need a storage? maybe just a tree?
            //the problem is that we must build zones and lanes.
        }

        public void OnUpdate(ref SystemState state)
        {
            /*todo get current scene and get zonegraph data from that scene and other entities also from that scene*/
            EntityQuery query = SystemAPI.QueryBuilder().WithAll<LaneProfileComponent>().Build();
            NativeArray<Entity> entities = query.ToEntityArray(state.WorldUpdateAllocator);
            Debug.Log($"Updating zone graph data in build system, profile entities count: {entities.Length}");

            //todo lets register shapes to hash grid first and then build the graph with storage here
            /*TODO each of these can be singleton buffer also, there can be many buffers attached to a single entity (like ZoneGraphStorage)*/
            NativeList<float3> boundaryPoints = new NativeList<float3>(Allocator.Temp);
            NativeList<ZoneData> zones = new NativeList<ZoneData>(Allocator.Temp);
            NativeList<ZoneLaneData> lanes = new NativeList<ZoneLaneData>(Allocator.Temp);
            NativeList<float3> lanePoints = new NativeList<float3>(Allocator.Temp);
            NativeList<float3> laneTangentVectors = new NativeList<float3>(Allocator.Temp);
            NativeList<float3> laneUpVectors = new NativeList<float3>(Allocator.Temp);
            NativeList<float> lanePointProgressions = new NativeList<float>(Allocator.Temp);
            MinMaxAABB storageBounds = new MinMaxAABB();
            NativeList<ZoneShapeLaneInternalLink> internalLinks =
                new NativeList<ZoneShapeLaneInternalLink>(Allocator.Temp);
            
            Debug.Log($"Shape entities count: {entities.Length}");
            
            //each lane profile is added to storage with its complete data per shape
            //what entities have the same profile? we do not know it is irrelevant, but we cannot access them by profile filter
            //because they are not entities (yet todo, better to convert to dynamic buffers?) it is only stacked data by zones 
            foreach (Entity entity in entities)//todo entities per scene only. 
            {
                ref LaneProfileBlobAsset laneProfile = ref state.EntityManager.GetComponentData<LaneProfileComponent>(entity).LaneProfile.Value;
                NativeArray<ZoneShapePoint> points = state.EntityManager.GetBuffer<ZoneShapePoint>(entity).ToNativeArray(Allocator.Temp);
                ZoneShapeUtilities.AddShapeZoneData(
                    ref laneProfile,
                    points,
                    boundaryPoints,
                    zones,
                    lanes,
                    lanePoints,
                    laneTangentVectors,
                    laneUpVectors,
                    lanePointProgressions,
                    ref storageBounds,
                    internalLinks
                    );
            }
            
            //todo ConnectLanes(InternalLinks, OutZoneStorage);//uses hash grid to create links (zone link data etc. between lanes)
                
            //todo: ConnectLanes(), add connectors to hash grid
                
            //todo build storage BVTree Blob asset Pointer?
            
            BlobBuilder blobBuilder = new BlobBuilder(Allocator.Temp);
            ref ZoneGraphStorage storage = ref blobBuilder.ConstructRoot<ZoneGraphStorage>();

            ZoneShapeUtilities.CopyToBlobArray(boundaryPoints, ref storage.BoundaryPoints, ref blobBuilder);
            ZoneShapeUtilities.CopyToBlobArray(zones, ref storage.Zones, ref blobBuilder);
            ZoneShapeUtilities.CopyToBlobArray(lanes, ref storage.Lanes, ref blobBuilder);
            ZoneShapeUtilities.CopyToBlobArray(lanePoints, ref storage.LanePoints, ref blobBuilder);
            ZoneShapeUtilities.CopyToBlobArray(laneTangentVectors, ref storage.LaneTangentVectors, ref blobBuilder);
            ZoneShapeUtilities.CopyToBlobArray(laneUpVectors, ref storage.LaneUpVectors, ref blobBuilder);
            ZoneShapeUtilities.CopyToBlobArray(lanePointProgressions, ref storage.LanePointProgressions, ref blobBuilder);
            storage.Bounds = storageBounds;
            
            //todo COPY INTERNAL LINKS!
            ZoneShapeUtilities.ConnectLanes(internalLinks, ref storage);
            
            /*
             * todo use this:
             * var blobAssetStore = state.World.GetExistingSystemManaged<BakingSystem>().BlobAssetStore;
             * // Collect the BlobAssets that
            // - haven't already been processed in this run
            // - aren't already known to the BlobAssetStore from previous runs (if they are known, save the BlobAssetReference for later)
            foreach (var (rawMesh, entity) in
                     SystemAPI.Query<RefRO<RawMesh>>().WithAll<MeshBB>()
                         .WithEntityAccess())
            {
                if (m_BlobAssetReferences.TryAdd(rawMesh.ValueRO.Hash, BlobAssetReference<MeshBBBlobAsset>.Null))
                {
                    if (blobAssetStore.TryGet<MeshBBBlobAsset>(rawMesh.ValueRO.Hash,
                            out BlobAssetReference<MeshBBBlobAsset> blobAssetReference))
                    {
                        m_BlobAssetReferences[rawMesh.ValueRO.Hash] = blobAssetReference;
                    }
                    else
                    {
                        m_EntitiesToProcess.Add(entity);
                    }
                }
            }
            
            - add some unique hash to the baked component
             */
            //_storageBlobAssetReference.Dispose();
            _storageBlobAssetReference = blobBuilder.CreateBlobAssetReference<ZoneGraphStorage>(Allocator.Persistent);
            
            var zoneGraphData = SystemAPI.GetSingletonRW<ZoneGraphData>();
            zoneGraphData.ValueRW.Storage = _storageBlobAssetReference;
            
            Debug.Log($"Registered boundary points storage: ({storage.BoundaryPoints.Length}), boundary points asset ref: ({_storageBlobAssetReference.Value.BoundaryPoints.Length}), total lane points: ({storage.LanePoints.Length})");
            blobBuilder.Dispose();
        }

        public void OnDestroy(ref SystemState state)
        {
            if (_storageBlobAssetReference.IsCreated)
                _storageBlobAssetReference.Dispose();
        }
        
        //private //todo to hash grid registration. 
        //NOTE: registered component has its cell location!
        //todo for now only one data object
        public static void Build(ZoneGraphDataAuthoring zoneGraphDataAuthoring)//zone graph data will have asset reference
        {
            HashSet<ZoneShape> _registeredShapeComponents = new HashSet<ZoneShape>();
            foreach (ZoneShape registeredShapeComponent in _registeredShapeComponents)//todo make solution when system builds it at runtime, make entity versions of zone datas
            {
                //if (zoneGraphData.gameObject.scene != registeredShapeComponent.gameObject.scene) continue;//todo group by levels/streamable loadable sections or scenes
                List<ZoneShapeLaneInternalLink> internalLinks = new List<ZoneShapeLaneInternalLink>();
                BlobAssetReference<ZoneGraphStorage> zoneStorageRef =
                    ZoneShapeUtilities.AppendZoneShapeToStorage(registeredShapeComponent, internalLinks);
                
                //todo ConnectLanes(InternalLinks, OutZoneStorage);
                
                //todo: ConnectLanes(), add connectors to hash grid
                
                //todo build storage BVTree Blob asset Pointer?
                //NOTE: node has positive index that is index to the ZoneData, negative index is sibling index in a tree
                /*
                 * BlobPtr<Node> Node; -> set each node to point to a specific element in array:
                 * BlobArray<
                 * flatten the built tree while setting pointers:
                 * var arrayBuilder = builder.Allocate(ref nodes, 10);
                 * builder.SetPointer(ref Node, ref arrayBuilder[2]);//set a particular node to point to specific array element.
                 * struct FriendList
                    {
                        public BlobPtr<Node> BestNode;
                        public BlobArray<Node> Nodes;
                    }
                    or simply array of nodes that are sorted in specific order
                 */
            }
        }
            
    }
}