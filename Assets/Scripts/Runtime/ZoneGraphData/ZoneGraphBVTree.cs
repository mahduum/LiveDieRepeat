using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace Runtime
{
    public struct ZoneGraphBvTree
    {
        /*Quantization origin*/
        private float3 _origin;
        private float _quantizationScale;

        private List<ZoneGraphBVNode> _nodes; //todo change to native array, maybe dynamic buffer? Zonegraph tree is an entity?

        private const float MaxQuantizedDimension = (float) (ushort.MaxValue - 1);

        public void Build(List<Bounds> bounds)
        {
            _nodes ??= new List<ZoneGraphBVNode>();
            _nodes.Clear();
            _origin = float3.zero;
            _quantizationScale = 0.0f;
            if (bounds.Count == 0)
            {
                return;
            }

            Bounds totalBounds = new Bounds();
            foreach (var bound in bounds)
            {
                totalBounds.Encapsulate(bound);
            }

            var size = totalBounds.size;
            var maxDimension = math.max(size.x, math.max(size.y, size.z));
            _quantizationScale = MaxQuantizedDimension / maxDimension;
            _origin = totalBounds.min;

            //quantize all bounds
            List<ZoneGraphBVNode> items = new List<ZoneGraphBVNode>(bounds.Count);
            int index = 0;
            foreach (var bound in bounds)
            {
                var item = CreateNodeFromBounds(bound);
                item.Index = index++;
                items.Add(item);
            }

            //build tree
            _nodes.Capacity =
                items.Count * 2 - 1; //leaf nodes are the count of all items, branch nodes are the number of splits n log(n) - 1
            Subdivide(items, 0, items.Count, _nodes);
        }
        
        /*Function will be called on each node that ends up overlapping the query bounds*/
        public void Query(Bounds queryBounds, Action<ZoneGraphBVNode> function)
        {
            var bounds = CreateNodeFromBounds(queryBounds);
            int limitIndex = _nodes.Count;
            int nodeIndex = 0;

            while (nodeIndex < limitIndex)
            {
                var node = _nodes[nodeIndex];
                bool overlap = node.DoesOverlap(bounds);//method for job, receive data
                bool isLeafNode = (node.Index >= 0);

                if (isLeafNode && overlap)
                {
                    function(node);
                }

                nodeIndex += (overlap || isLeafNode) ? 1 : -node.Index/*if there is no overlap skip by the offset to sibling*/;
            }
        }

        /*For debugging*/
        Bounds CalcWorldBounds(ZoneGraphBVNode node)
        {
            float unquantizedScale = _quantizationScale > float.Epsilon ? 1.0f / _quantizationScale : 0.0f; // Scale from quantized to unquantized coordinates.
            return new Bounds(_origin + new float3(node.MinX * unquantizedScale, node.MinY * unquantizedScale, node.MinZ* unquantizedScale), 
                new float3(node.MaxX * unquantizedScale, node.MaxY * unquantizedScale, node.MaxZ * unquantizedScale) - _origin);
        }

        public ZoneGraphBVNode CreateNodeFromBounds(Bounds bounds)
        {
            float3 quantizedBoxSize = MaxQuantizedDimension;
            float3 localMin = math.clamp(((float3) bounds.min - _origin) * _quantizationScale, float3.zero,
                quantizedBoxSize);
            float3 localMax = math.clamp(((float3) bounds.max - _origin) * _quantizationScale, float3.zero,
                quantizedBoxSize);
            return new ZoneGraphBVNode((ushort) localMin.x, (ushort) localMin.y, (ushort) localMin.z,
                (ushort) (localMax.x + 1), (ushort) (localMax.y + 1), (ushort) (localMax.z + 1));
        }
        
        
        // Calculate bounds of items in range [BeginIndex, EndIndex] (EndIndex non-inclusive).
        static ZoneGraphBVNode CreateNode(List<ZoneGraphBVNode> items, int beginIndex, int endIndex)
        {
            if (endIndex > beginIndex == false)
            {
                throw new IndexOutOfRangeException("Begin index must be smaller than end index!");
            }
    
            ZoneGraphBVNode result = items[beginIndex];
        
            for (int index = beginIndex + 1; index < endIndex; ++index)
            {
                ZoneGraphBVNode node = items[index];
                result.MinX = Math.Min(result.MinX, node.MinX);
                result.MinY = Math.Min(result.MinY, node.MinY);
                result.MinZ = Math.Min(result.MinZ, node.MinZ);
                result.MaxX = Math.Max(result.MaxX, node.MaxX);
                result.MaxY = Math.Max(result.MaxY, node.MaxY);
                result.MaxZ = Math.Max(result.MaxZ, node.MaxZ);
            }
        
            return result;
        }
        
        static int GetLongestAxis(ZoneGraphBVNode node)
        {
            ushort dimX = (ushort) (node.MaxX - node.MinX);
            ushort dimY = (ushort) (node.MaxY - node.MinY);
            ushort dimZ = (ushort) (node.MaxZ - node.MinZ);
    
            if (dimX > dimY && dimX > dimZ)
            {
                return 0;
            }
            if (dimY > dimZ)
            {
                return 1;
            }
            return 2;
        }
    
        // Creates subtree of nodes in range [BeginIndex, EndIndex] (EndIndex non-inclusive),
        // by sorting the nodes along the longest axis of all items, and splitting them equally (in count) in two subtrees.
        static void Subdivide(List<ZoneGraphBVNode> inItems, int beginIndex, int endIndex,
            List<ZoneGraphBVNode> outNodes)
        {
            int count = endIndex - beginIndex;
            int currentNodeIndex = outNodes.Count;//an offset of all the nodes added to this point
    
            ZoneGraphBVNode node;
            if (count == 1)
            {
                //single leaf node
                node = inItems[beginIndex];//preserved the index of ZoneData
                outNodes.Add(node);
            }
            else
            {
                // split, get total bounds for node:
                node = CreateNode(inItems, beginIndex, endIndex);
                outNodes.Add(node);//todo check with fiddle//what are we a
                var longestAxisIndex = GetLongestAxis(node);
                //sort by the longest axis this slice
                inItems.Sort(beginIndex, endIndex, new ZoneGraphBVNodeComparer(longestAxisIndex));
                var splitIndex = beginIndex + count / 2;
                //left
                Subdivide(inItems, beginIndex, splitIndex, outNodes);
                //right
                Subdivide(inItems, splitIndex, endIndex, outNodes);
                
                //negative index means skip the subtree to next sibling
                int nextSiblingIndexOffset = outNodes.Count - currentNodeIndex;//todo outNodes.Count at this point is direct index of the next sibling, maybe this would be less complex to use?
                //this index is used to traverse flattened array as though it was a b-tree
                node.Index = -nextSiblingIndexOffset;
            }
        }

    }
    
    public struct ZoneGraphBVNode
    {
        public ushort MinX;
        public ushort MinY;
        public ushort MinZ;
        public ushort MaxX;
        public ushort MaxY;
        public ushort MaxZ;
        public int Index;

        public ZoneGraphBVNode(ushort minX, ushort minY, ushort minZ, ushort maxX, ushort maxY, ushort maxZ)
        {
            MinX = minX;
            MinY = minY;
            MinZ = minZ;
            MaxX = maxX;
            MaxY = maxY;
            MaxZ = maxZ;
            Index = -1;
        }
        
        public bool DoesOverlap(ZoneGraphBVNode other)
        {
            if (MinX > other.MaxX || MinY > other.MaxY || MinZ > other.MaxZ) return false;
            if (MaxX < other.MinX || MaxY < other.MinY || MaxZ < other.MinZ) return false;
            return true;
        }
    }

    public class ZoneGraphBVNodeComparer : IComparer<ZoneGraphBVNode>
    {
        private readonly int _componentIndex;
            
        public ZoneGraphBVNodeComparer(int componentIndex)
        {
            _componentIndex = componentIndex;
        }
            
        public int Compare(ZoneGraphBVNode x, ZoneGraphBVNode y)
        {
            var (a, b) = _componentIndex switch
            {
                0 => (x.MinX, y.MinX),
                1 => (x.MinY, y.MinY),
                2 => (x.MinZ, y.MinZ),
                _ => throw new ArgumentOutOfRangeException()
            };

            return a.CompareTo(b);
        }
    }
}