using NUnit.Framework;
using Unity.Collections;
using DataUtilities;
using UnityEngine;
using System;
using System.Linq;

namespace Tests
{
    [TestFixture]
    public class NativeSparseArrayTests
    {
        [Test]
        public void MultiHashMapResizeTest()
        {
            NativeParallelMultiHashMap<int, int>
                map = new NativeParallelMultiHashMap<int, int>(1, Allocator.Persistent);
            map.Add(1, 2);
            Assert.AreEqual(1, map.Count());
            Debug.Log($"Capacity: {map.Capacity}");
            map.Add(1, 3);
            Assert.AreEqual(map.Count(), 2);
            Debug.Log($"Capacity: {map.Capacity}");

            map.Dispose();
        }
    }
}