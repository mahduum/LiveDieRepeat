using Runtime;
using Unity.Entities;
using UnityEngine;

namespace BakeWorld
{
    [WorldSystemFilter(WorldSystemFilterFlags.BakingSystem)]
    public partial class ZoneGraphBakingSystem : SystemBase
    {
        //register to this system, all shapee components are entitiees
        //since system works across the scenes it is the more important to distinguish components do actorstrorage connection by scenes
        //entity has some component - it is a scene entity, because we may have multiple scenes be opened
        //[BakingType] can be used to get data from baker to baking system
        //we may have a baker actor, that calls build on Builder, and authors this data?
        //or we may have a baker on each zone shape component that way each zone shape will be an entity available for baking system?
        //bounding box cleanup tracks entities parent and destroys the entity if it is destroyed and recalculates parent bounds
        //each entity with bounding box authoring gets bounds calculated to a bounding box component
        protected override void OnUpdate()
        {
            //todo change the storage based only on the entities of points that have changed added to them
            // foreach (var buffer in SystemAPI.Query<DynamicBuffer<ZoneShapePoint>>())//and profile lane
            // {
            //     Debug.Log($"Buffer count: {buffer.Length}");
            // }
        }
    }
}