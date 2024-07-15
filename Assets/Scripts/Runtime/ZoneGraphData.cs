using UnityEngine;

namespace Runtime
{
    public class ZoneGraphData : MonoBehaviour//authoring?
    {
        //todo!
        
        //search for all zone shapes in this scene
        //can be done by accessing static system, but the static system is only available in the scene playmode
        
        //test solutions:
        //create blob ref asset and attach it to this as and entity
        /*
         * 1. Everything in ZoneGraphStorage except for BVTree and Bounds can be packed inside Blob Asset
         * 2.
         */
        public ZoneGraphStorage Storage;

        private void Awake()
        {
            Storage = new ZoneGraphStorage();
        }

        public ref ZoneGraphStorage GetStorageByRef()
        {
            return ref Storage;
        }
    }
}