using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Runtime;
using Unity.Mathematics;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Splines;
using UnityEngine.UIElements;
using Runtime.ZoneGraphData;

public class SplineTessellate : MonoBehaviour
{
    [SerializeField] private SplineContainer _splineContainer;
    [SerializeField] private GameObject _debug;
    void OnValidate()
    {
        //var tan = _splineContainer.Splines[0].Knots.ToArray()[0].TangentOut;
        //Debug.Log($"First tangent length: {math.length(tan)}");
    }

    private void Start()
    {
        Tessellate();
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    void Tessellate()
    {
        foreach (var spline in _splineContainer.Splines)
        {
            //spline.CalculateUpVector()
            //spline.TryGetFloat4Data()
            var knots = spline.Knots.ToArray();
            int startIndex = spline.Closed ? knots.Length - 1 : 0;
            int endIndex = spline.Closed ? 0 : 1;
            int curveIndex = 0;
            while (endIndex < knots.Length && curveIndex < spline.GetCurveCount())
            {
                var startKnot = knots[startIndex % knots.Length];
                var endKnot = knots[endIndex];

                var startPosition = _splineContainer.transform.TransformPoint(startKnot.Position);
                var p1 = _splineContainer.transform.TransformPoint(
                    startKnot.Position +
                    math.mul(startKnot.Rotation, math.forward()) * math.length(startKnot.TangentOut));
                var endPosition = _splineContainer.transform.TransformPoint(endKnot.Position);
                var p2 = _splineContainer.transform.TransformPoint(
                    endKnot.Position + math.mul(endKnot.Rotation, math.back()) * math.length(endKnot.TangentIn));

                p1 = _splineContainer.transform.TransformPoint(spline.GetCurve(curveIndex).P1);//same result
                
                GameObject.Instantiate(_debug, p1, Quaternion.identity);
                GameObject.Instantiate(_debug, p2, Quaternion.identity);
                GameObject.Instantiate(_debug, startPosition, Quaternion.identity);
                GameObject.Instantiate(_debug, endPosition, Quaternion.identity);

                startIndex++;
                endIndex++;
                curveIndex++;
            }

            //get spline length total, set minimum distance, make t's for these distances increased, calculate for these values
            spline.GetCurveInterpolation(0, .1f);
        }
    }

    void TessellateCurves()//call recursively
    {
        // float tolleranceSq = 0.1f * 0.1f;

        List<ZoneShapePoint> splinePoints = new List<ZoneShapePoint>();
        
        foreach (var spline in _splineContainer.Splines)
        {
            // float minDistanceSqP1;//distances for p1 and p2
            // float minDistanceSqP2;//distances for p1 and p2
            float t = 0;
            float increment = 0.01f;
            /*.5 /2 */
            while (t < 1.0f)
            {
                if (spline.Evaluate(t, out var position, out var tangent, out var upVector))
                {
                    splinePoints.Add(new ZoneShapePoint()
                    {
                        Position = position,
                        Tangent = tangent,
                        Up = upVector,
                        Right = math.normalize(math.cross(upVector, tangent))
                    });
                }

                t += increment;
            }
            
        }
    }
    
    
    void TessellateRecursive(float3 p0, float3 p1, float3 p2, float3 p3, float toleranceSqr, int level, int maxLevel, ref List<float3> outPoints)
    {
        // Handle degenerate segment.
        float3 dir = p3 - p0;
        if (math.length(math.float3(0.0001f, 0.0001f, 0.0001f)) > math.length(dir))
        {
            outPoints.Add(p3);
            return;
        }

        // If the control points are close enough to approximate a line within tolerance, stop recursing.
        dir = math.normalize(dir);
        float3 relP1 = p1 - p0;
        float3 relP2 = p2 - p0;
        float3 projP1 = dir * math.dot(dir, relP1);//todo use this method to stop tessellating, if this distance is very small it means we have almost flat curve
        float3 projP2 = dir * math.dot(dir, relP2);
        float distP1Sqr = math.distancesq(relP1, projP1);
        float distP2Sqr = math.distancesq(relP2, projP2);
        
        if (distP1Sqr < toleranceSqr && distP2Sqr < toleranceSqr)
        {
            outPoints.Add(p3);
            return;
        }

        if (level < maxLevel)
        {
            // Split the curve in half and recurse, calculating positions for ever smaller t
            float3 p01 = math.lerp(p0, p1, 0.5f);
            float3 p12 = math.lerp(p1, p2, 0.5f);
            float3 p23 = math.lerp(p2, p3, 0.5f);
            float3 p012 = math.lerp(p01, p12, 0.5f);
            float3 p123 = math.lerp(p12, p23, 0.5f);
            float3 p0123 = math.lerp(p012, p123, 0.5f);

            TessellateRecursive(p0, p01, p012, p0123, toleranceSqr, level + 1, maxLevel, ref outPoints);
            TessellateRecursive(p0123, p123, p23, p3, toleranceSqr, level + 1, maxLevel, ref outPoints);
        }
    }

    void Tessellate(float3 p0, float3 p1, float3 p2, float3 p3, float toleranceSqr, int maxLevel,
        ref List<float3> outPoints)
    {
        TessellateRecursive(p0, p1, p2, p3, toleranceSqr, 0, maxLevel, ref outPoints);
    }
    
    //convert it to dots job matrix multiplication
    /*
    //UnityEngine.Transform InverseTransformPoint
    localPosition = math.transform(math.inverse(localToWorld.Value), worldPostion)

    // UnityEngine.Transform TransformPoint
    worldPosition = math.transform(localToWorld.Value, localPosition)

    // UnityEngine.Transform InverseTransformDirection
    localVector = math.rotate(math.inverse(localToWorld.Value), worldDirection)

    // UnityEngine.Transform TransformDirection
    worldVector = math.rotate(localToWorld.Value, localDirection)
     */
}
