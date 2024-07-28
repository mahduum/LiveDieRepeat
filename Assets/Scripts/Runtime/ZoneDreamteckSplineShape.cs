using System;
using System.Collections.Generic;
using Dreamteck.Splines;
using Unity.Mathematics;
using UnityEngine;

namespace Runtime
{
    public class ZoneDreamteckSplineShape : ZoneShape
    {
        [SerializeField] private SplineComputer _splineComputer;
        [SerializeField] private GameObject _segmentPrefab;
        //todo make channels configurable, make a middle interface that introduces the lane profile
        //and with that it automatically sets channels, offsets etc.
        //override mesh offset
        //add interface ILaneProfile, the shape will provide the profile, width, spacing etc. the problem is direction
        //todo make a partial class that will provide zone graph setting
        //consider adding additional data from spline mesh, within one channel there can be multiple lanes
        //how many lanes a single channel has etc. 

        public void OnLaneProfile()
        {
            var splineMesh = _splineComputer.GetComponent<SplineMesh>();
            if (splineMesh == null)
            {
                return;
            }
            //todo for a given tag in profile assign a specific mesh and choose from those
            var channelCount = splineMesh.GetChannelCount();
            for (int i = 0; i < channelCount; i++)
            {
                splineMesh.RemoveChannel(i);
            }

            var profile = GetLaneProfile();
            float accWidth = 0;
            for (int i = 0; i < profile._lanes.Length; i++)
            {
                
                SplineMesh.Channel channel = splineMesh.AddChannel(_segmentPrefab.GetComponent<MeshFilter>().mesh,
                    $"lane_mesh_{profile._lanes[i]._direction.ToString()}_{i}");
                channel.minOffset = new Vector2(accWidth, 0);//todo should we add them on both sides?
                accWidth += profile._lanes[i]._width;
            }
        }
        
        public override Component GetBakerDependency()
        {
            return _splineComputer;//todo or lane profile
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
                    Position = math.transform(localToWorld, splineSample.position),
                    Tangent = math.transform(localToWorld, splineSample.forward),
                    Up = math.transform(localToWorld, splineSample.up),
                    Right = math.transform(localToWorld, splineSample.right)
                });
            }

            return curves;
        }

        public override List<MinMaxAABB> GetShapesBounds()
        {
            return new List<MinMaxAABB>(){GetShapeBounds()};
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