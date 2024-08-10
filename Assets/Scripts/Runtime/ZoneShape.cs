using System;
using System.Collections.Generic;
using Data;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

namespace Runtime
{
    public enum ZoneShapeType
    {
        Bezier,
        Polygon
    }
    
    //todo this may register itself to baking
    //system together with zonegraph data
    //use shared component filter to filter by scenes
    //make registered zoneShape an entity with data.
    public abstract class ZoneShape : MonoBehaviour//this can be authoring component
    {
        [SerializeField] protected float _pointsSpacing = 0.01f;
        
        public ZoneShapeType ShapeType { get; set; }//deduce from points provider
        
        public event Action<Spline> OnShapeChangedEvent;//todo for debug, to delete

        public abstract Component GetBakerDependency();
        
        //todo editor mode to set points provider
        //todo zone tags separate from lane tags
        
        //todo shape connectors
        //todo shape connections
        
        //reference to a profile, that is kept in settings, all the profiles can be in settings but for now keep it separately

        /*todo it registers itself to the system and that system creates authoring data
         transforms data provided by this component to be available in correct for to the baking world*/
        private void OnEnable()
        {
            //register itself to system//system must be global? static?
            Debug.Log("Shape component enabled.");
        }

        private void OnDisable()
        {
            //unregister itself from system
            Debug.Log("Shape component disabled.");

        }

        public abstract List<ZoneShapePoint>[] GetShapesAsPoints(); //todo make interface method
        public abstract List<MinMaxAABB> GetShapesBounds();

        //todo we need it to provide ZoneShapePoints for tesselation
        //but it can directly return tesselated points
        //we only need this to be able to provide shape points for lanes
        //lane profile is stored in zone shape component, has description for all
        //the lanes within the shape FZoneLaneDesc: direction, width, tags (who can walk it)
        //
        //builder will need curve points complete with normals etc., distanced by tollerance
        //can have internal tollerance to be overriden
        public abstract IZoneLaneProfile GetZoneLaneProfile();

        /*Autor it with points and profile, shape point should be, each entity will have a set of its points?
         it will be a blob asset for this entity? is it worth creating?*/
    }
}