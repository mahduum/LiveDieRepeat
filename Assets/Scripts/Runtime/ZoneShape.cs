using System;
using System.Collections.Generic;
using Data;
using Unity.Mathematics;
using Unity.Physics.Authoring;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Splines;

namespace Runtime
{
    public enum ZoneShapeType
    {
        Spline,
        Polygon
    }
    
    //todo this may register itself to baking
    //system together with zonegraph data
    //use shared component filter to filter by scenes
    //make registered zoneShape an entity with data.
    [ExecuteAlways]
    public abstract class ZoneShape: MonoBehaviour//this can be authoring component
    {
        [SerializeField] protected float _pointsSpacing = 0.01f;
        public abstract ZoneShapeType ShapeType { get; }//todo make abstract and implement in derived classes 

        public abstract Component GetBakerDependency();
        
        //todo editor mode to set points provider
        //todo zone tags separate from lane tags
        
        //todo shape connectors
        //todo shape connections
        //it probably serves only for editor manipulation and snapping 
        private List<ZoneShapeConnector> _shapeConnectors = new List<ZoneShapeConnector>();
        private List<ZoneShapeConnection> _shapeConnections = new List<ZoneShapeConnection>();
        public IReadOnlyList<ZoneShapeConnector> ShapeConnectors => _shapeConnectors;
        public IReadOnlyList<ZoneShapeConnection> ShapeConnections => _shapeConnections;

        private bool _updateRelatedData;
        
        //reference to a profile, that is kept in settings, all the profiles can be in settings but for now keep it separately

        /*todo it registers itself to the system and that system creates authoring data
         transforms data provided by this component to be available in correct for to the baking world*/
        private void OnEnable()
        {
            //register itself to system//system must be global? static?
            SubscribeOnShapeChanged();
            Debug.Log("Shape component enabled.");
        }

        private void OnDisable()
        {
            //unregister itself from system
            UnsubscribeOnShapeChanged();
            Debug.Log("Shape component disabled.");
        }

        public bool IsShapeClosed() => ShapeType != ZoneShapeType.Spline;

        protected abstract void SubscribeOnShapeChanged();
        protected abstract void UnsubscribeOnShapeChanged();

        //todo list of lists maybe problematic for connectors
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
        
        //need universal update shape method, when underlying shape changes
        //todo must enforce derived classes to call appropriate update methods
        
        public void OnShapeChanged()
        {
            _updateRelatedData = true;
        }

        public void UpdateRelatedData()
        {
            if (_updateRelatedData)
            {
                _updateRelatedData = false;
                UpdateConnectedShapes();
            }
        }
        
        private void UpdateConnectors()
        {
            //for each sub shape, separate list
        }

        private void UpdateConnections()
        {
            //for each sub shape
        }

        private void UpdateConnectedShapes()
        {
            // Store which shapes were previously connected to, refresh their potentially mutual connections later.
            /*
             * TSet<UZoneShapeComponent*> AffectedComponents;
    for (const FZoneShapeConnection& Conn : ConnectedShapes)
    {
        AffectedComponents.Add(Conn.ShapeComponent.Get());
    }
             */
            // Update connectors and find connections.
            UpdateConnectors();
            UpdateConnections();
            /*
             * // Store which shapes were got connected to, refresh their potentially mutual connections.
    for (const FZoneShapeConnection& Conn : ConnectedShapes)
    {
        AffectedComponents.Add(Conn.ShapeComponent.Get());
    }

    // Connection may alter the shape, request previous and current connections to update their connections visuals too.
    for (UZoneShapeComponent* AffectedComponent : AffectedComponents)
    {
        if (AffectedComponent)
        {
            AffectedComponent->UpdateConnectedShapes();
            AffectedComponent->MarkRenderStateDirty();
        }
    }
             */
        }
    }
}