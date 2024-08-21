using System;
using System.Collections.Generic;
using Data;
using Dreamteck.Splines;
using DreamTeckExtensions;
using Unity.Mathematics;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Runtime
{
    public class ZoneDreamteckSplineShape : ZoneShape
    {
        [SerializeField] private DreamTeckSplineLaneProfileProvider _laneProfileProvider;
        [SerializeField] private SplineComputer _splineComputer;
        [SerializeField] private GameObject _segmentPrefab;
        //todo make channels configurable, make a middle interface that introduces the lane profile
        //and with that it automatically sets channels, offsets etc.
        //override mesh offset
        //add interface ILaneProfile, the shape will provide the profile, width, spacing etc. the problem is direction
        //todo make a partial class that will provide zone graph setting
        //consider adding additional data from spline mesh, within one channel there can be multiple lanes
        //how many lanes a single channel has etc.
        
        //todo optionally update shape other relative data, have OnShapeUpdated, for example profile width etc.

        public override ZoneShapeType ShapeType => ZoneShapeType.Spline;

        public override Component GetBakerDependency()//todo should be profile provider instead?
        {
            return _splineComputer;//todo or lane profile
        }

        protected override void SubscribeOnShapeChanged()
        {
            _splineComputer.onRebuild += OnShapeChanged;
        }

        protected override void UnsubscribeOnShapeChanged()
        {
            _splineComputer.onRebuild -= OnShapeChanged;
        }

        public override List<ZoneShapePoint>[] GetShapesAsPoints()
        {
            return new List<ZoneShapePoint>[1] { GetShapeAsPoints() };
        }
        
        public List<ZoneShapePoint> GetShapeAsPoints()
        {
            var sampleCount = _splineComputer.sampleCount;
            List<ZoneShapePoint> curves = new List<ZoneShapePoint>(sampleCount);
            
            var lastIndex = _splineComputer.isClosed ? sampleCount - 2 : sampleCount - 1;
            
            SplineSample[] samples = _splineComputer.rawSamples;
            var localToWorld = _splineComputer.transform.localToWorldMatrix;

            for (var index = 0; index <= lastIndex; index++)
            {
                var splineSample = samples[index];
                curves.Add(new ZoneShapePoint()
                {
                    Position = math.transform(localToWorld, splineSample.position) + new float3(0f, 0.2f, 0f),
                    Tangent = splineSample.forward,
                    Up = splineSample.up,
                    Right = splineSample.right
                });
            }

            return curves;
        }

        public override List<MinMaxAABB> GetShapesBounds()
        {
            return new List<MinMaxAABB>(){GetShapeBounds()};
        }

        public override IZoneLaneProfile GetZoneLaneProfile()//maybe dream teck component
        {
            return _laneProfileProvider;
        }

        public MinMaxAABB GetShapeBounds()
        {
            var splineMesh = _splineComputer.GetComponent<SplineMesh>();
            if (splineMesh == null)
            {
                return default;
            }

            if (splineMesh.baked == false)
            {
                splineMesh.Bake(true, true);
            }

            var bounds = splineMesh.GetComponent<MeshFilter>().sharedMesh.bounds;
            
            return new MinMaxAABB()
            {
                Max = bounds.center + bounds.extents,
                Min = bounds.center - bounds.extents
            };
        }
    }
}