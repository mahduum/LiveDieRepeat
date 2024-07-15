using System.Diagnostics;
using System;
using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Collections;
using Unity.Mathematics;

namespace Utilities
{
    // Marks our struct as a NativeContainer.
    // If ENABLE_UNITY_COLLECTIONS_CHECKS is enabled,
    // it is required that m_Safety with exactly this name.
    [NativeContainer]
    // The [NativeContainerSupportsMinMaxWriteRestriction] enables
    // a common jobification pattern where an IJobParallelFor is split into ranges
    // And the job is only allowed to access the index range being Executed by that worker thread.
    // Effectively limiting access of the array to the specific index passed into the Execute(int index) method
    // This attribute requires m_MinIndex & m_MaxIndex to exist.
    // and the container is expected to perform out of bounds checks against it.
    // m_MinIndex & m_MaxIndex will be set by the job scheduler before Execute is called on the worker thread.
    [NativeContainerSupportsMinMaxWriteRestriction]
    // It is recommended to always implement a Debugger proxy
    // to visualize the contents of the array in VisualStudio and other tools.
    [DebuggerDisplay("Length = {Length}")]
    [DebuggerTypeProxy(typeof(NativeCustomArrayDebugView<>))]
    public unsafe struct NativeSparseArray<T> : IDisposable where T : unmanaged
    {
        internal void* m_Buffer;
        internal int m_Length;
        //todo must add something like a linked list of previous/next free index, like a one sized array element with link to previous and next?
        //it can also be achieved with unsafe list for non blittable elements?
        internal NativeReference<int2> m_previousNextFreeIndex;//we don't need linked list for this or a loose reference because we will jump in memory
        //todo: question: are free links indesciminate of level? we need to access them randomly
    
        /*todo I need list and links to free indices,
         - the number of elements in free list
         - first free index of an unallocated element - head of linked list of free elements
         - index is a pointer to an array of elements of free list links
         - the linked element has NextFreeIndex and PrevFreeIndex
         - registered component version (maybe an entity one) has also its cell location info, as when it is 
			being registered it is also added to hash grid and created the cell location info
		 - when querying for cell location (to see what else is nearby), we find cell by cell location
			that is transformed to x, y and level and we return the cell with info about items count
		 - system gets access to hash grid entity and in sparse array of all items by cell->first
			and from then on each item has .next pointer - results are added to "outCloseEntities"
		 - if item is removed then its index is assinged to existing (if one exists) free index.previousFreeIndex, and first free index is set to be the removed index
         NOTE: use layout union trick
         template<typename ElementType>
union TSparseArrayElementOrFreeListLink
{
	/** If the element is allocated, its value is stored here. * /
	ElementType ElementData;

	struct
	{
		/** If the element isn't allocated, this is a link to the previous element in the array's free list. * /
		int32 PrevFreeIndex;

		/** If the element isn't allocated, this is a link to the next element in the array's free list. * /
		int32 NextFreeIndex;
	};
};

FSparseArrayAllocationInfo InsertUninitialized(int32 Index)
	{
		FElementOrFreeListLink* DataPtr;

		// Enlarge the array to include the given index.
		if(Index >= Data.Num())
		{
			Data.AddUninitialized(Index + 1 - Data.Num());

			// Defer getting the data pointer until after a possible reallocation
			DataPtr = (FElementOrFreeListLink*)Data.GetData();
NOTE: add free indices between far allocation and last valid index, DataPtr is ptr to linked list of free indices
			while(AllocationFlags.Num() < Data.Num())
			{
				const int32 FreeIndex = AllocationFlags.Num();
				DataPtr[FreeIndex].PrevFreeIndex = -1;
				DataPtr[FreeIndex].NextFreeIndex = FirstFreeIndex;
				if(NumFreeIndices)
				{
					DataPtr[FirstFreeIndex].PrevFreeIndex = FreeIndex;
				}
				FirstFreeIndex = FreeIndex;
				verify(AllocationFlags.Add(false) == FreeIndex);
				++NumFreeIndices;
			};
		}
		else
		{
			DataPtr = (FElementOrFreeListLink*)Data.GetData();
		}

		// Verify that the specified index is free.
		check(!AllocationFlags[Index]);

		// Remove the index from the list of free elements.
		--NumFreeIndices;
		const int32 PrevFreeIndex = DataPtr[Index].PrevFreeIndex;
		const int32 NextFreeIndex = DataPtr[Index].NextFreeIndex;
		if(PrevFreeIndex != -1)
		{
			DataPtr[PrevFreeIndex].NextFreeIndex = NextFreeIndex;
		}
		else
		{
			FirstFreeIndex = NextFreeIndex;
		}
		if(NextFreeIndex != -1)
		{
			DataPtr[NextFreeIndex].PrevFreeIndex = PrevFreeIndex;
		}

		return AllocateIndex(Index);
	}
         */
    #if ENABLE_UNITY_COLLECTIONS_CHECKS
        internal int m_MinIndex;
        internal int m_MaxIndex;
        internal AtomicSafetyHandle m_Safety;
        internal static readonly SharedStatic<int> s_staticSafetyId = SharedStatic<int>.GetOrCreate<NativeSparseArray<T>>();
    #endif
    
        internal Allocator m_AllocatorLabel;
    
        public NativeSparseArray(int length, Allocator allocator)
        {
            long totalSize = UnsafeUtility.SizeOf<T>() * length;
    
    #if ENABLE_UNITY_COLLECTIONS_CHECKS
            // Native allocation is only valid for Temp, Job and Persistent
            if (allocator <= Allocator.None)
                throw new ArgumentException("Allocator must be Temp, TempJob or Persistent", "allocator");
            if (length < 0)
                throw new ArgumentOutOfRangeException("length", "Length must be >= 0");
            if (!UnsafeUtility.IsBlittable<T>())
                throw new ArgumentException(string.Format("{0} used in NativeCustomArray<{0}> must be blittable", typeof(T)));
    #endif
    
            m_Buffer = UnsafeUtility.Malloc(totalSize, UnsafeUtility.AlignOf<T>(), allocator);
            UnsafeUtility.MemClear(m_Buffer, totalSize);
    
            m_Length = length;
            m_AllocatorLabel = allocator;
    
    #if ENABLE_UNITY_COLLECTIONS_CHECKS
            m_MinIndex = 0;
            m_MaxIndex = length - 1;
            m_Safety = CollectionHelper.CreateSafetyHandle(allocator);
            CollectionHelper.SetStaticSafetyId<NativeSparseArray<T>>(ref m_Safety, ref s_staticSafetyId.Data);
    #endif
	        m_previousNextFreeIndex = default;//todo
        }
    
        public int Length { get { return m_Length; } }
    
        public unsafe T this[int index]
        {
            get
            {
    #if ENABLE_UNITY_COLLECTIONS_CHECKS
                // If the container is currently not allowed to read from the buffer
                // then this will throw an exception.
                // This handles all cases, from already disposed containers
                // to safe multithreaded access.
                AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
    
                // Perform out of range checks based on
                // the NativeContainerSupportsMinMaxWriteRestriction policy
                if (index < m_MinIndex || index > m_MaxIndex)
                    FailOutOfRangeError(index);
    #endif
                // Read the element from the allocated native memory
                return UnsafeUtility.ReadArrayElement<T>(m_Buffer, index);
            }
    
            set
            {
    #if ENABLE_UNITY_COLLECTIONS_CHECKS
                // If the container is currently not allowed to write to the buffer
                // then this will throw an exception.
                // This handles all cases, from already disposed containers
                // to safe multithreaded access.
                AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
    
                // Perform out of range checks based on
                // the NativeContainerSupportsMinMaxWriteRestriction policy
                if (index < m_MinIndex || index > m_MaxIndex)
                    FailOutOfRangeError(index);
    #endif
                // Writes value to the allocated native memory
                UnsafeUtility.WriteArrayElement(m_Buffer, index, value);
            }
        }
    
        public T[] ToArray()
        {
    #if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
    #endif
    
            var array = new T[Length];
            for (var i = 0; i < Length; i++)
                array[i] = UnsafeUtility.ReadArrayElement<T>(m_Buffer, i);
            return array;
        }
    
        public bool IsCreated
        {
            get { return m_Buffer != null; }
        }
    
        public void Dispose()
        {
    #if ENABLE_UNITY_COLLECTIONS_CHECKS
            CollectionHelper.DisposeSafetyHandle(ref m_Safety);
    #endif
    
	        //Free all links
            UnsafeUtility.Free(m_Buffer, m_AllocatorLabel);
            m_Buffer = null;
            m_Length = 0;
        }
    
    #if ENABLE_UNITY_COLLECTIONS_CHECKS
        private void FailOutOfRangeError(int index)
        {
            if (index < Length && (m_MinIndex != 0 || m_MaxIndex != Length - 1))
                throw new IndexOutOfRangeException(string.Format(
                    "Index {0} is out of restricted IJobParallelFor range [{1}...{2}] in ReadWriteBuffer.\n" +
                    "ReadWriteBuffers are restricted to only read & write the element at the job index. " +
                    "You can use double buffering strategies to avoid race conditions due to " +
                    "reading & writing in parallel to the same elements from a job.",
                    index, m_MinIndex, m_MaxIndex));
    
            throw new IndexOutOfRangeException(string.Format("Index {0} is out of range of '{1}' Length.", index, Length));
        }
    
    #endif
    }
    
    // Visualizes the custom array in the C# debugger
    internal sealed class NativeCustomArrayDebugView<T> where T : unmanaged
    {
        private NativeSparseArray<T> m_Array;
    
        public NativeCustomArrayDebugView(NativeSparseArray<T> array)
        {
            m_Array = array;
        }
    
        public T[] Items
        {
            get { return m_Array.ToArray(); }
        }
    }
}