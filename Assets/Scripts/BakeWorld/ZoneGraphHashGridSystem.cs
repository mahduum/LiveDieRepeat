using DataUtilities;
using Runtime.ZoneGraphData;
using Unity.Entities;
using UnityEngine;

namespace BakeWorld
{
    [WorldSystemFilter(WorldSystemFilterFlags.BakingSystem)]
    public partial class ZoneGraphHashGridSystem : SystemBase
    {
        //register to this system, all shapee components are entitiees
        //since system works across the scenes it is the more important to distinguish components do actorstrorage connection by scenes
        //entity has some component - it is a scene entity, because we may have multiple scenes be opened
        //[BakingType] can be used to get data from baker to baking system
        //we may have a baker actor, that calls build on Builder, and authors this data?
        //or we may have a baker on each zone shape component that way each zone shape will be an entity available for baking system?
        //bounding box cleanup tracks entities parent and destroys the entity if it is destroyed and recalculates parent bounds
        //each entity with bounding box authoring gets bounds calculated to a bounding box component
        protected override void OnCreate()
        {
            RequireForUpdate<LookUpHashGrid2d>();
            RequireForUpdate<HashGrid2dTag>();
            var hashGridEntity = EntityManager.CreateEntity();
            EntityManager.AddComponent<HashGrid2dTag>(hashGridEntity);
            EntityManager.AddComponentObject(hashGridEntity, new LookUpHashGrid2d()
            {
                Grid = new NativeHierarchicalHashGrid2D<Entity>(4, 4, 100)
            });
        }

        protected override void OnUpdate()
        {
            foreach (var lookupHashGrid in SystemAPI.Query<LookUpHashGrid2d>().WithAll<HashGrid2dTag>())
            {
                lookupHashGrid.Grid.Reset();
                //do work on grid, register etc.
            }

            //use this if we should recreate grid while the system is running then we must reinitialize containers:
            // foreach (var lookUpHashGrid in SystemAPI.Query<LookUpHashGrid2d>().WithNone<HashGrid2dTag>())
            // {
            //     Debug.Log("Disposing grid containers in on update.");
            //     lookUpHashGrid.Grid.Dispose();
            // }
            //todo change the storage based only on the entities of points that have changed added to them
            //todo go through all entities and add them to grid
        }

        protected override void OnDestroy()
        {
            foreach (var lookUpHashGrid in SystemAPI.Query<LookUpHashGrid2d>().WithNone<HashGrid2dTag>())
            {
                lookUpHashGrid.Grid.Dispose();
            }
        }
    }

    public struct HashGrid2dTag : IComponentData
    {
        
    }
}