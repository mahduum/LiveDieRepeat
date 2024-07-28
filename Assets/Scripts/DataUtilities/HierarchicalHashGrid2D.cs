using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine.Rendering;

namespace Utilities
{
    
    //why sparse array?
    //to be able to access fast by index abd then next item is also accessed by index using next index of the first item
    //we can replace this system with a bucket of multihashmap, we avoid next link to random point within array
    //what makes item to be added outside capacity?
    // - it grows array if number of added uninitialized indices is greater than capacity
    // - if item is removed
    public class HierarchicalHashGrid2D<T> where T : new()
    {
        protected float[] _cellSizesPerLevel;
        protected float[] _inverseCellSizesPerLevel;
        protected int[] _itemCountPerLevel;
        /*Essentially Cell represents a bucket of linked items, instead of using First and linked random items we can use a value of all items in multihash map*/
        protected Dictionary<uint, Cell> _cells;//hash buckets to locate items - can be replaced by multihashmap
        /*The First in Cell is an index to the first item of that cell in linked items, these items can be simply values in multihashmap,
         - but the drawbacks: multihashmap resizing? removing and moving of items -> check how efficient it is with traditional vs multihash*/
        protected List<LinkedItem> _items;//todo implement as ISparseArray which will have Native or GC implementation defined in U
        private int _spillListIndex = -1; //linked list of items that didn't fit in any of the levels
        public int LevelCount { get; }
        public int LevelRatio { get; } /*values multiplier when going level up*/
        
        public HierarchicalHashGrid2D(int levelCount, int levelRatio, float cellSize)
        {
            LevelCount = levelCount;
            LevelRatio = levelRatio;
            _cellSizesPerLevel = new float[levelCount];
            _inverseCellSizesPerLevel = new float[levelCount];
            _itemCountPerLevel = new int[levelCount];
            
            Initialize(cellSize);
        }

        public void Initialize(float cellSize)
        {
            float currentCellSize = cellSize;
            for (int i = 0; i < LevelCount; i++)
            {
                _cellSizesPerLevel[i] = currentCellSize;
                _inverseCellSizesPerLevel[i] = 1.0f / currentCellSize;
                currentCellSize *= LevelRatio;
            }
        }

        public void Reset()
        {
            _cells.Clear();
            _items.Clear();

            for (var index = 0; index < _itemCountPerLevel.Length; index++)
            {
                _itemCountPerLevel[index] = 0;
            }
        }

        public CellLocation Add(T item, AABB itemBounds)
        {
            CellLocation location = CalculateCellLocation(itemBounds);
            Add(item, location);
            return location;
        }

        /*An example how to proceed with it, but after changes Cell will be the key to multihashmap and
         _items will be the bucket of that key*/
        private void Add(T item, CellLocation location)
        {
            int index = _items.Count;//for the sparsed list this should return first free index if available otherwise adds next position uninitialised
            LinkedItem linkedItem = new LinkedItem()
            {
                Item = item
            };

            if (location.Level == -1)
            {
                linkedItem.Next = _spillListIndex;//we won't be setting this item if the location does not fit any of the grids, instead we redirect to the previous spill list
                _spillListIndex = index;//and the current spill list we set to this item
            }
            else
            {
                //Adding a cell at specific level
                //Find or add cell, this will be find or add a bucket
                var cellData = GetOrAddCell(location);

                linkedItem.Next = cellData.cell.First;//next on new item will point to the previous first in cell
                cellData.cell.First = index;//first in cell is now the most recently added item.

                UpdateCell(cellData);

                _itemCountPerLevel[location.Level]++;
                
                //update child counts
                CellLocation parentLocation = location;
                //go towards higher levels as they contain larger items and contain lower levels
                while (parentLocation.Level < LevelCount - 1)
                {
                    parentLocation.LevelUp(LevelRatio);
                    var data = GetOrAddCell(parentLocation);
                    data.cell.ChildItemsCount++;
                    UpdateCell(cellData);
                }
            }
            
            _items.Add(linkedItem);
        }

        private void UpdateCell((uint hashKey, Cell cell) cellData)
        {
            _cells[cellData.hashKey] = cellData.cell;
        }

        private (uint key, Cell cell) GetOrAddCell(CellLocation location)
        {
            Cell cellAtLocation;
            var hashKey = location.ToHashKey();
            if (_cells.TryGetValue(hashKey, out var found))
            {
                cellAtLocation = found;
            }
            else
            {
                cellAtLocation = new Cell(location.X, location.Y, location.Level);
            }

            return (hashKey, cellAtLocation);
        }

        private int LevelUpCoordComponent(int coordinateComponent)
        {
            if (coordinateComponent < 0)
            {
                coordinateComponent -= (LevelRatio - 1);/*default flooring in division is towards 0, subtract 1, so we get consistent results also for negatives and the flooring is toward smaller values always*/
            }

            return coordinateComponent / LevelRatio; /*simply divide as it was multiplied before when value was assigned*/
        }

        public CellLocation CalculateCellLocation(AABB bounds)
        {
            int x = 0, y = 0;
            int level = -1;
            
            float3 boundsCenter = bounds.Center;
            float maxDimension = math.max(bounds.Size.x, bounds.Size.y);

            for (level = 0; level < LevelCount; level++)
            {
                /*the bigger the bounds the higher the level it will be assigned to as greater cells will have smaller inverse*/
                int scalingFactor = (int) (math.ceil(maxDimension * _inverseCellSizesPerLevel[level]));
                if (scalingFactor <= 1)
                {
                    x = (int) math.floor(boundsCenter.x * _inverseCellSizesPerLevel[level]);/*bucket for this level*/
                    y = (int) math.floor(boundsCenter.y * _inverseCellSizesPerLevel[level]);/*bucket for this level*/
                    break;
                }
            }

            return new CellLocation(x, y, level);
        }

        MinMaxAABB CalculateCellBounds(CellLocation cellLocation)
        {
            float size = _cellSizesPerLevel[cellLocation.Level];
            float x = cellLocation.X * size;
            float y = cellLocation.Y * size;
            return new MinMaxAABB
            {
                Min = new float3(x, y, 0f),
                Max = new float3(x + size, y + size, 0f)
            };
        }

        public struct LinkedItem
        {
            public T Item;//in particular case of zone graph shape components this will be a blittable index to array of registered shape components
            public int Next;//index to the next item in lined list (index to Items sparse list)
        }

        public struct Cell
        {
            public readonly int X, Y, Level;
            /*first item in linked list where are all items under the same location in finer grid levels (which will be the lower levels)
             upon adding new LinkedItem, the First is set to the index to Items assigned for this new item, while this LinkedItem.Next
             is assigned to what was previously the First (but consider it to be called "last" as we follow the list from the back
             when retrieving items in the cell.*/
            public int First;
            public int ChildItemsCount;

            public Cell(int level, int y, int x)
            {
                Level = level;
                Y = y;
                X = x;
                First = -1;
                ChildItemsCount = 0;
            }

            public override int GetHashCode()
            {
                const uint h1 = 0x8da6b343;	// Arbitrary big primes.
                const uint h2 = 0xd8163841;
                const uint h3 = 0xcb1ab31f;
                return (int)(h1 * (uint)X + h2 * (uint)Y + h3 * (uint)Level);
            }
            
            public override bool Equals(object obj)
            {
                return obj is Cell location && Equals(location);
            }

            private bool Equals(Cell other)
            {
                return X == other.X && Y == other.Y && Level == other.Level;
            }
            
            public static bool operator==(Cell lHs, Cell rHs)
            {
                return lHs.Equals(rHs);
            }

            public static bool operator !=(Cell lHs, Cell rHs)
            {
                return !lHs.Equals(rHs);
            }
        }
        
        /*Specifies location within the grid at specific level - local to level coords*/
        public struct CellLocation
        {
            public int X, Y, Level;
            
            public CellLocation(int level, int y, int x)
            {
                Level = level;
                Y = y;
                X = x;
            }

            //todo make these methods static for data friendliness?
            public void LevelUp(int levelRatio)
            {
                X = LevelUpComponent(X, levelRatio);
                Y = LevelUpComponent(Y, levelRatio);
                Level += 1;
            }
            
            public void LevelDown(int levelRatio)
            {
                X = LevelDownComponent(X, levelRatio);
                Y = LevelDownComponent(Y, levelRatio);
                Level -= 1;
            }

            private int LevelUpComponent(int coordinateComponent, int levelRatio)
            {
                if (coordinateComponent < 0)
                {
                    coordinateComponent -= (levelRatio - 1);
                }
                return coordinateComponent / levelRatio;
            }
            
            private int LevelDownComponent(int coordinateComponent, int levelRatio)
            {
                if (coordinateComponent < 0)
                {
                    coordinateComponent -= (levelRatio - 1);
                }
                return coordinateComponent * levelRatio;
            }

            public override bool Equals(object obj)
            {
                return obj is CellLocation location && Equals(location);
            }

            private bool Equals(CellLocation other)
            {
                return X == other.X && Y == other.Y && Level == other.Level;
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(X, Y, Level);
            }

            public static bool operator==(CellLocation lHs, CellLocation rHs)
            {
                return lHs.Equals(rHs);
            }

            public static bool operator !=(CellLocation lHs, CellLocation rHs)
            {
                return !lHs.Equals(rHs);
            }
            
            public uint ToHashKey()
            {
                const uint h1 = 0x8da6b343;	// Arbitrary big primes.
                const uint h2 = 0xd8163841;
                const uint h3 = 0xcb1ab31f;
                return (h1 * (uint)X + h2 * (uint)Y + h3 * (uint)Level);
            }
        }
        
        //todo missing CellRect and CellRectIterator -> iterates over what is in the rect
        /*Bounds with position*/
        public struct CellRect
        {
            public int MinX, MinY, MaxX, MaxY;
            public CellRect(int minX, int minY, int maxX, int maxY)
            {
                MinX = minX;
                MinY = minY;
                MaxX = maxX;
                MaxY = maxY;
            }
        }

        public struct CellRectIteratorState
        {
            public int X, Y, Level;
            public CellRect Rect;//area to iterate over
        }
    }
}