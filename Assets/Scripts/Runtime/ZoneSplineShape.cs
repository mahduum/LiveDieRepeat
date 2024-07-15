using System;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEditor.Splines;
using UnityEngine;
using UnityEngine.Splines;
using Object = UnityEngine.Object;

namespace Runtime
{
    
    [ExecuteAlways]
    public class ZoneSplineShape : ZoneShape
    {
        [SerializeField] private SplineContainer _splineContainer;

        public override Component GetDependency()
        {
            return _splineContainer;
        }

        void OnEnable()
        {
#if UNITY_EDITOR
            EditorSplineUtility.AfterSplineWasModified += OnShapeChanged;
            Debug.Log("Subscribed to on entity changed editor.");
#else
            Debug.Log("Subscribed to on entity changed PLAY.");
            Spline.Changed += OnSplineChanged;
#endif
        }
        
        void OnDisable()
        {
#if UNITY_EDITOR
            EditorSplineUtility.AfterSplineWasModified -= OnShapeChanged;
            Debug.Log("Unsubscribed to on entity changed editor.");

#else
            Debug.Log("Unsubscribed to on entity changed PLAY.");
            Spline.Changed -= OnSplineChanged;
#endif
        }

        private void OnShapeChanged(Spline spline, int knot, SplineModification modification)
        {
            //will have entity it belongs to 
            //spline.data
        }
        
        private void OnShapeChanged(Spline spline)
        {
            Debug.Log($"Spline changed! Id: {spline.GetHashCode()}");//use int data to find entity
        }

        //what I need from spline: to be able to set data
        public override List<ZoneShapePoint>[] GetShapesAsPoints()//return Ireadonly
        {
            //get spline lenght, divide by spacing, the number is how many approx divisions we need to have, divide 1 by it
            //for each spline
            List<ZoneShapePoint>[] curves = new List<ZoneShapePoint>[_splineContainer.Splines.Count];
            var localToWorld = _splineContainer.transform.localToWorldMatrix;

            for (var index = 0; index < _splineContainer.Splines.Count; index++)
            {
                var spline = _splineContainer.Splines[index];
                var splinePoints = curves[index] = new List<ZoneShapePoint>();
                var splineLength = spline.GetLength();
                var distributionFrequency = splineLength / _pointsSpacing;
                var tIncrement = 1f / distributionFrequency;
                
                float t = 0;
                while (spline.Closed ? t < 1.0f : t <= 1.0f)
                {
                    if (spline.Evaluate(t, out var position, out var tangent, out var upVector))
                    {
                        splinePoints.Add(new ZoneShapePoint()
                        {
                            Position = math.transform(localToWorld, position),
                            Tangent = math.rotate(localToWorld, tangent),
                            Up = math.normalize(math.rotate(localToWorld, upVector)),
                            Right = math.normalize( math.rotate(localToWorld, math.cross(upVector, tangent)))
                        });
                    }

                    t += tIncrement;
                }
            }

            return curves;//cache the containers
        }
    }
}