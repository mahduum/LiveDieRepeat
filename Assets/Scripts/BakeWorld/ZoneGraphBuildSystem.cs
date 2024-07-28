using System.Collections.Generic;
using Authoring;
using Runtime;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace BakeWorld
{
    [WorldSystemFilter(WorldSystemFilterFlags.BakingSystem)]
    public partial struct ZoneGraphBuildSystem : ISystem
    {

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
            ZoneGraphData zoneGraphData = SystemAPI.GetSingleton<ZoneGraphData>();
            
            EntityQuery query = SystemAPI.QueryBuilder().WithAll<LaneProfileComponent>().Build();
            NativeArray<Entity> entities = query.ToEntityArray(state.WorldUpdateAllocator);
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
            
            Debug.Log($"Shape entities count: {entities.Length}");
            
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
                    ref storageBounds
                    );
            }
            
            //todo ConnectLanes(InternalLinks, OutZoneStorage);
                
            //todo: ConnectLanes(), add connectors to hash grid
                
            //todo build storage BVTree Blob asset Pointer?
            
            BlobBuilder blobBuilder = new BlobBuilder(Allocator.Temp);
            ref ZoneGraphStorage storage = ref blobBuilder.ConstructRoot<ZoneGraphStorage>();

            CopyToBlobArray(boundaryPoints, ref storage.BoundaryPoints, ref blobBuilder);
            CopyToBlobArray(zones, ref storage.Zones, ref blobBuilder);
            CopyToBlobArray(lanes, ref storage.Lanes, ref blobBuilder);
            CopyToBlobArray(lanePoints, ref storage.LanePoints, ref blobBuilder);
            CopyToBlobArray(laneTangentVectors, ref storage.LaneTangentVectors, ref blobBuilder);
            CopyToBlobArray(laneUpVectors, ref storage.LaneUpVectors, ref blobBuilder);
            CopyToBlobArray(lanePointProgressions, ref storage.LanePointProgressions, ref blobBuilder);
            storage.Bounds = storageBounds;
            
            var storageBlobAssetReference = blobBuilder.CreateBlobAssetReference<ZoneGraphStorage>(Allocator.Persistent);
            zoneGraphData.Storage = storageBlobAssetReference;

            Debug.Log($"Registered boundary points storage: ({storage.BoundaryPoints.Length}), boundary points asset ref: ({storageBlobAssetReference.Value.BoundaryPoints.Length}), total lane points: ({storage.LanePoints.Length})");
            blobBuilder.Dispose();
        }
        
        //todo make this extension method
        private static unsafe void CopyToBlobArray<T>(NativeList<T> source, ref BlobArray<T> destination, ref BlobBuilder blobBuilder) where T : unmanaged
        {
            BlobBuilderArray<T> arrayBuilder = blobBuilder.Allocate(
                ref destination,
                source.Length
            );

            // for (int i = 0; i < source.Length; i++)
            // {
            //     arrayBuilder[i] = source[i];
            // }
            void* destinationPtr = arrayBuilder.GetUnsafePtr();
            void* sourcePtr = source.GetUnsafePtr();
            UnsafeUtility.MemCpy(destinationPtr, sourcePtr, source.Length * UnsafeUtility.SizeOf<T>());
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