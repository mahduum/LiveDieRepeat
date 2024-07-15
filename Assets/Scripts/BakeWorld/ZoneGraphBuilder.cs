using System.Collections.Generic;
using Runtime;
using UnityEngine;

namespace BakeWorld
{
    public class ZoneGraphBuilder
    {
        private HashSet<ZoneShape> _registeredShapeComponents = new HashSet<ZoneShape>();
        
        //todo for now only one data object
        public void Build(ZoneGraphData zoneGraphData)
        {
            foreach (var registeredShapeComponent in _registeredShapeComponents)
            {
                //if (zoneGraphData.gameObject.scene != registeredShapeComponent.gameObject.scene) continue;//todo group by levels/streamable loadable sections or scenes
                ref ZoneGraphStorage storage = ref zoneGraphData.Storage;
                var internalLinks = new List<ZoneShapeLaneInternalLink>();
                ZoneShapeUtilities.AppendZoneShapeToStorage(registeredShapeComponent, ref storage, internalLinks);
                //todo ConnectLanes(InternalLinks, OutZoneStorage);
                foreach (var zone in storage.Zones)
                {
                    storage.Bounds.Encapsulate(zone.Bounds);    
                }
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