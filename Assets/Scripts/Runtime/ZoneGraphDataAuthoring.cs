using Unity.Entities;
using UnityEngine;

namespace Runtime
{
    /*will create entity that will then get processed by the builder system*/
    public class ZoneGraphDataAuthoring : MonoBehaviour//authoring?//todo systemBase for global large data
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
        private class ZoneGraphDataBaker : Baker<ZoneGraphDataAuthoring>
        {
            public override void Bake(ZoneGraphDataAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new ZoneGraphData());
            }
        }
    }

    public struct ZoneGraphData : IComponentData
    {
        public BlobAssetReference<ZoneGraphStorage> Storage;
    }
    
    //todo add scene tag
}