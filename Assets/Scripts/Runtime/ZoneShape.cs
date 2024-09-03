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
            _shapeConnectors.Clear();
            var shapes = GetShapesAsPoints();

            //for each sub shape, separate list
            // if (ShapeType == ZoneShapeType.Spline)
            // {
                int offset = 0;
                foreach (var zoneShapePoints in shapes)
                {
                    if (zoneShapePoints.Count < 2)
                    {
                        continue;
                    }

                    //set start connector and end connector
                    var startShapeConnector = new ZoneShapeConnector()
                    {
                        ShapeType = ZoneShapeType.Spline,
                        Position = zoneShapePoints[0].Position,
                        Normal = -zoneShapePoints[0].Tangent, //always point away from shape
                        Up = zoneShapePoints[0].Up,
                        //splines will only have one shape so we can point to the first or last index,
                        //polygons for now will have multiple splines that connect lanes from other shapes,
                        //therefore polygon should keep all the splines as one, or just their border points
                        //we need point index just to blend the connections in editor (not supported for now, todo)
                        PointIndex = offset + 0,
                        IsLaneProfileReversed = false, //todo
                        Profile = GetZoneLaneProfile()
                    };

                    _shapeConnectors.Add(startShapeConnector);

                    var endShapeConnector = new ZoneShapeConnector()
                    {
                        ShapeType = ZoneShapeType.Spline,
                        Position = zoneShapePoints[^1].Position,
                        Normal = zoneShapePoints[^1].Tangent, //always point away from shape
                        Up = zoneShapePoints[^1].Up,
                        PointIndex = offset + zoneShapePoints.Count - 1,
                        IsLaneProfileReversed = false, //todo
                        Profile = GetZoneLaneProfile()
                    };

                    _shapeConnectors.Add(endShapeConnector);

                    offset += zoneShapePoints.Count;
                }
            // }
            // else if (ShapeType == ZoneShapeType.Polygon)
            // {
            //     foreach (var zoneShapePoints in shapes)
            //     {
            //         if (zoneShapePoints.Count < 2)
            //         {
            //             continue;
            //         }
            //         
            //         //for polygons create a connector for each lane segment point if 
            //         //the lanes are generated procedurally, otherwise that the first and 
            //         //last one for each connecting shape, for now we iterate the same way like in splines
            //         //connecting points will have point type of LaneProfile (as opposed to sharp, bezier or auto bezier)
            //
            //         for (var i = 0; i < zoneShapePoints.Count; i++)
            //         {
            //             var point = zoneShapePoints[i];
            //             //todo polygon should not have fixed directions?
            //             //we need to go around polygon with the indicies
            //         }
            //     }
            // }
        }

        private void UpdateConnections()
        {
            //for each sub shape
            _shapeConnections.Clear();
            //find connected shapes with builder, must have access to builder hash grid where all registered shapes are for shapes lookup
            //only hash grid is necessary for this, after the connectors are updated, then the system that localizes the connections should run
            //find overlapping shapes and retrieve the connectors from those shapes
            
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