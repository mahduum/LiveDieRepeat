using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using Utilities;

namespace DataUtilities
{
    //why sparse array?
    //to be able to access fast by index abd then next item is also accessed by index using next index of the first item
    //we can replace this system with a bucket of multihashmap, we avoid next link to random point within array
    //what makes item to be added outside capacity?
    // - it grows array if number of added uninitialized indices is greater than capacity
    // - if item is removed
    //todo struct?
    //put it on a game object?
    public class NativeHierarchicalHashGrid2D<T> : IDisposable where T : unmanaged//T for example entity
    {
        protected NativeArray<float> _cellSizesPerLevel;
        protected NativeArray<float> _inverseCellSizesPerLevel;
        protected NativeArray<int> _itemCountPerLevel;
        /*Essentially Cell represents a bucket of linked items, instead of using First and linked random items we can use a value of all items in multihash map*/
        //protected Dictionary<uint, Cell> _cells;//hash buckets to locate items - can be replaced by multihashmap
        /*The First in Cell is an index to the first item of that cell in linked items, these items can be simply values in multihashmap,
         - but the drawbacks: multihashmap resizing? removing and moving of items -> check how efficient it is with traditional vs multihash*/
        //protected List<BucketItem> _items;//todo implement as ISparseArray which will have Native or GC implementation defined in U

        private NativeParallelMultiHashMap<uint, BucketItem> _hashGridCellItems;//change uint for custom key, maybe that key can contain the child count data?
        private NativeParallelHashMap<uint, Cell> _hashGridCells;

        private int _spillListIndex = -1; //linked list of items that didn't fit in any of the levels
        public int LevelCount { get; }
        public int LevelRatio { get; } /*values multiplier when going level up*/

        public void Dispose()
        {
            _cellSizesPerLevel.Dispose();
            _inverseCellSizesPerLevel.Dispose();
            _itemCountPerLevel.Dispose();
            _hashGridCellItems.Dispose();
            _hashGridCells.Dispose();
        }

        public NativeHierarchicalHashGrid2D(int levelCount, int levelRatio, float cellSize, int initialAverageBucketCapacity = 1)
        {
            LevelCount = levelCount;
            LevelRatio = levelRatio;
            _cellSizesPerLevel = new NativeArray<float>(levelCount, Allocator.Persistent);
            _inverseCellSizesPerLevel = new NativeArray<float>(levelCount, Allocator.Persistent);
            _itemCountPerLevel = new NativeArray<int>(levelCount, Allocator.Persistent);
            var initialCellCount = levelCount * levelRatio * levelRatio;
            _hashGridCellItems = new NativeParallelMultiHashMap<uint, BucketItem>(initialCellCount * initialAverageBucketCapacity, Allocator.Persistent);
            _hashGridCells = new NativeParallelHashMap<uint, Cell>(initialCellCount, Allocator.Persistent);
            Initialize(cellSize);
        }

        public void Initialize(float cellSize)//todo resize capacity
        {
            Reset();
            float currentCellSize = cellSize;
            for (int i = 0; i < LevelCount; i++)
            {
                _cellSizesPerLevel[i] = currentCellSize;
                _inverseCellSizesPerLevel[i] = 1.0f / currentCellSize;
                currentCellSize *= LevelRatio;
            }
        }

        public int AllItemsCount()
        {
            var count = 0;
            for (int i = 0; i < LevelCount; i++)
            {
                count += _itemCountPerLevel[i];
            }
            return count;
        }

        public void Reset()
        {
            _hashGridCellItems.Clear();

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
            BucketItem bucketItem = new BucketItem()//todo eliminate and leave only item, no wrapper?
            {
                Item = item
            };

            if (location.Level == -1)//for objects without bounds separate container
            {
                bucketItem.Next = _spillListIndex;//we won't be setting this item if the location does not fit any of the grids, instead we redirect to the previous spill list
                _spillListIndex = _hashGridCellItems.Count();//todo???
            }
            else
            {
                //Adding a cell at specific level
                var hashKey = location.ToHashKey();
                
                _hashGridCellItems.Add(hashKey, bucketItem);
                
                //register this cell if we ever would need to check only items count on some finer level:
                //...

                _itemCountPerLevel[location.Level]++;
                
                //update child counts
                CellLocation parentLocation = location;
                //go towards higher levels as they contain larger items and contain lower levels
                while (parentLocation.Level < LevelCount - 1)
                {
                    parentLocation.LevelUp(LevelRatio);
                    /*If we eliminate Cell we loose the ability to check if there is anything in lower, fine-grained levels,
                     having the count we can create rect iterator that will gather objects from all the rects included in higher
                     larger rect, upper levels there are no item counts, we need another container for this.
                     
                     The query is for all the objects in given location
                     */
                    var key = parentLocation.ToHashKey();
                    IncrementCellChildItemsCount(key, parentLocation);
                }
            }

            void IncrementCellChildItemsCount(uint key, CellLocation parentLocation)
            {
                if (_hashGridCells.TryGetValue(key, out var cell))
                {
                    cell.ChildItemsCount++;
                    _hashGridCells[key] = cell;
                }
                else
                {
                    _hashGridCells[key] = new Cell(parentLocation.X, parentLocation.Y, parentLocation.Level, 1);
                }
            }
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

            if (level == LevelCount)
            {
                //too big to fit in any of the levels, will go into spill list
                x = 0;
                y = 0;
                level = -1;
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

       
        /** Returns items that potentially touch the bounds. Operates on grid level, can have false positives.
          * @param Bounds - Query bounding box.
          * @param OutResults - Result of the query, IDs of potentially overlapping items.
          */
        public void Query(AABB bounds, NativeList<T> results)
        {
            NativeArray<CellRect> rects = new NativeArray<CellRect>(LevelCount, Allocator.Temp);
            NativeArray<CellRectIteratorState> iterators =
                new NativeArray<CellRectIteratorState>(LevelCount, Allocator.Temp);
            int iteratorLevelIndex = 0;
            
            // Calculate cell bounds for each level, keep track of the coarsest level that has any items, we'll start from that
            for (int level = 0; level < LevelCount; level++)
            {
                rects[level] = CalculateQueryBounds(bounds, level);
            }
            
            // The idea of the iterator below is that it iterates over rectangle cells recursively towards finer levels, depth first.
            // The previous level's iterator is kept in the Iters stack, and we can pop and continue that once the finer level is completed.
            // Finer iterator rectangles is clamped against that levels tight bounds so that unnecessary cells are not visited.
            // Coarser levels of the grid also store data if the finer levels under them has any items. This is used to skip iterating
            // lower levels at certain locations completely. This can be big advantage in larger query boxes, compared to iterating all cells as in QuerySmall().

            // Init coarsest iterator, the biggest cells, look for the largest objects first, only then check if smaller cells contain anything
            int startLevel = LevelCount - 1;
            var iterator = new CellRectIteratorState()
            {
                Level = startLevel,
                Rect = rects[startLevel],
                X = rects[startLevel].MinX,//todo it is in rect already, make properties?
                Y = rects[startLevel].MinY
            };

            iterators[iteratorLevelIndex] = iterator;//initial iterator
            iteratorLevelIndex++;

            int safeGuardCounter = 0;
            
            while (iteratorLevelIndex > 0 && safeGuardCounter++ < 100)//one iterator per level todo must be higher but then will be empty...
            {
                var levelIteratorState = iterators[iteratorLevelIndex - 1];
                Debug.Log($"Loop start: iterator level index: {iteratorLevelIndex}, iterator state X: {levelIteratorState.X}, Y: {levelIteratorState.Y}, guard count: {safeGuardCounter}");

                // Check if the iterator has finished
                if (levelIteratorState.X > levelIteratorState.Rect.MaxX)//finished for X
                {
                    levelIteratorState.X = levelIteratorState.Rect.MinX;//reset X
                    levelIteratorState.Y++;//get next row
                    iterators[iteratorLevelIndex - 1] = levelIteratorState;
                    if (levelIteratorState.Y > levelIteratorState.Rect.MaxY)
                    {
                        //all rows done, that means that we are finished with this level, and
                        //we take up again on the coarser level where we left off when proceeding to finer levels
                        iteratorLevelIndex--;
                        continue;
                    }
                }

                //cell has info about total items at all levels
                var cellLocation = new CellLocation(levelIteratorState.X, levelIteratorState.Y, levelIteratorState.Level);
                //if we access come random hash, we don't know if there are items on other levels, so we start with cell that has register of item count per level
                //for example we may only access a coarse level with no other items on finer levels, if we don't add a cell for it, we may not check for bucket item
                //or we can check items first on bucket and if there none, we check for cell and see if there are any below, 
                //first find if we have any items at this level:
                if (_hashGridCellItems.TryGetFirstValue(cellLocation.ToHashKey(), out BucketItem bucketItem, out NativeParallelMultiHashMapIterator<uint> it))
                {
                    results.Add(bucketItem.Item);
                    while (_hashGridCellItems.TryGetNextValue(out var nextBucketItem, ref it))
                    {
                        results.Add(nextBucketItem.Item);
                    }
                }
                
                //check if we have items at finer levels, add iterator for the next loop
                if (FindCell(cellLocation) is {ChildItemsCount: > 0} && levelIteratorState.Level > 0)
                {
                    int finerLevel = levelIteratorState.Level - 1;
                    CellRect finerRect = rects[finerLevel];
                    int xMin = levelIteratorState.X * LevelRatio;
                    int yMin = levelIteratorState.Y * LevelRatio;
                    int step = LevelRatio - 1;
                    CellRect currentRect = new CellRect(xMin, yMin, xMin + step, yMin + step);
                    CellRect intersection = IntersectRect(currentRect, finerRect);

                    if (intersection.MaxX >= intersection.MinX && intersection.MaxY >= intersection.MinY)
                    {
                        //set the next iterator if it is not empty
                        iterators[iteratorLevelIndex] = new CellRectIteratorState()
                        {
                            Rect = intersection,
                            X = intersection.MinX,
                            Y = intersection.MinY,
                            Level = finerLevel
                        };
                        
                        iteratorLevelIndex++;
                    }
                }

                //if there weren't any items at finer levels then we simply advance iterator state and stay on the same level
                //else we raise the coarser iterator state, but we will continue with finer state until there are items
                levelIteratorState.X++;
                iterators[iteratorLevelIndex - 1] = levelIteratorState;
            }
            
            //at the end include all stuff from the spill list
            //todo add to results what spilled
        }
        
        /** Returns intersection of the two cell bounding rectangles.
          * @param Left - left hand side rectangle
          * @param Right - right hand side rectangle
          * @return Intersecting are between left and right.
          */
        CellRect IntersectRect(CellRect left, CellRect right)
        {
            return new CellRect()
            {
                MinX = math.max(left.MinX, right.MinX),
                MinY = math.max(left.MinY, right.MinY),
                MaxX = math.min(left.MaxX, right.MaxX),
                MaxY = math.min(left.MaxY, right.MaxY),
            };
        }
        
        /** Returns a cell for specific location and level.
          * @param X - Cell X coordinate.
          * @param Y - Cell Y coordinate.
          * @param Level - Grid Level.
          * @return Pointer to cell at specified location, or return nullptr if the cell does not exist.
          */
        private Cell FindCell(CellLocation cellLocation)
        {
            _hashGridCells.TryGetValue(cellLocation.ToHashKey(), out var found);
            return found;
        }

        /** Calculates cell based query rectangle. The bounds are expanded by half grid cell size because the items are stored for only one cell
          * based on their center and side. For that reason the items can overlap the neighbor cells by half the cell size.
          * @param Bounds - Query bounding box to quantize.
          * @param Level - Which level of the tree the to calculate the bounds for
          * @return Quantized rectangle representing the cell bounds at specific level of the tree, coordinates inclusive.
          */
        private CellRect CalculateQueryBounds(AABB bounds, int level)
        {
            MinMaxAABB minMaxAABB = bounds;
            return new CellRect()
            {
                MinX = (int) math.floor(minMaxAABB.Min.x * _inverseCellSizesPerLevel[level] - 0.5f),
                MinY = (int) math.floor(minMaxAABB.Min.y * _inverseCellSizesPerLevel[level] - 0.5f),
                MaxX = (int) math.floor(minMaxAABB.Max.x * _inverseCellSizesPerLevel[level] - 0.5f),
                MaxY = (int) math.floor(minMaxAABB.Max.y * _inverseCellSizesPerLevel[level] - 0.5f),
            };
        }

        public struct BucketItem
        {
            public T Item;//in particular case of zone graph shape components this will be a blittable index to array of registered shape components
            public int Next;//index to the next item in lined list (index to Items sparse list)
        }

        public struct Cell : IEquatable<Cell>
        {
            public readonly int X, Y, Level;
            /*first item in linked list where are all items under the same location in finer grid levels (which will be the lower levels)
             upon adding new LinkedItem, the First is set to the index to Items assigned for this new item, while this LinkedItem.Next
             is assigned to what was previously the First (but consider it to be called "last" as we follow the list from the back
             when retrieving items in the cell.*/
            public int ItemsCount;
            public int ChildItemsCount;

            public Cell(int level, int y, int x, int childItemsCount = 0)
            {
                Level = level;
                Y = y;
                X = x;
                ItemsCount = -1;
                ChildItemsCount = childItemsCount;
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

            public bool Equals(Cell other)
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
            
            public CellLocation(int x, int y, int level)
            {
                X = x;
                Y = y;
                Level = level;
            }

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
                    coordinateComponent -= (levelRatio - 1);/*default flooring in division is towards 0, subtract 1, so we get consistent results also for negatives and the flooring is toward smaller values always*/
                }
                return coordinateComponent / levelRatio; /*simply divide as it was multiplied before when value was assigned*/
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
            
            public uint ToHashKey()//todo use math.hash
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