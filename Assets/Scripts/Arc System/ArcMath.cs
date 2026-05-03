using UnityEngine;
using System.Collections.Generic;

// Struct e Enum pubblici (accessibili da altri script)
public enum ArchType 
{ 
    ThreeHinged,
    TwoHinged,
    Fixed
}

public struct ArchSolution
{
    public float[] normalForces;
    public float[] shearForces;
    public float[] moments;
    public float[] deformations;
    public Vector3[] thrustLine;
}

public static class ArcMath
{
    private const float EA = 50000f;
    private const float EI = 10000f;
    
    public static ArchSolution CalculateArchForces(
        Vector3[] archPoints,
        float[] arcLengthParams,
        List<Vector2> loads,
        ArchType type,
        int resolution)
    {
        if (archPoints == null || archPoints.Length < 3 || loads == null || loads.Count == 0)
            return new ArchSolution();
        
        switch(type)
        {
            case ArchType.ThreeHinged:
                return SolveThreeHingedArch(archPoints, arcLengthParams, loads, resolution);
            case ArchType.TwoHinged:
                return SolveTwoHingedArch(archPoints, arcLengthParams, loads, resolution);
            default:
                return SolveThreeHingedArch(archPoints, arcLengthParams, loads, resolution);
        }
    }
    
    private static ArchSolution SolveThreeHingedArch(
        Vector3[] points, float[] arcLengths, 
        List<Vector2> loads, int resolution)
    {
        int n = resolution;
        ArchSolution result = new ArchSolution
        {
            normalForces = new float[n],
            shearForces = new float[n],
            moments = new float[n],
            deformations = new float[n],
            thrustLine = new Vector3[n]
        };
        
        Vector3 hingeLeft = points[0];
        Vector3 hingeRight = points[points.Length - 1];
        
        int crownIndex = 0;
        float maxY = float.MinValue;
        for (int i = 0; i < points.Length; i++)
        {
            if (points[i].y > maxY)
            {
                maxY = points[i].y;
                crownIndex = i;
            }
        }
        Vector3 hingeCrown = points[crownIndex];
        
        float span = hingeRight.x - hingeLeft.x;
        float rise = hingeCrown.y - hingeLeft.y;
        
        float totalVerticalLoad = 0;
        float momentAboutLeft = 0;
        
        foreach (Vector2 load in loads)
        {
            Vector3 loadPoint = GetPointAtT(points, arcLengths, load.x);
            totalVerticalLoad += load.y;
            momentAboutLeft += load.y * (loadPoint.x - hingeLeft.x);
        }
        
        float RV_right = momentAboutLeft / span;
        float RV_left = totalVerticalLoad - RV_right;
        
        float momentAtCrownFromLeft = RV_left * (hingeCrown.x - hingeLeft.x);
        float momentLoadsLeft = 0;
        
        foreach (Vector2 load in loads)
        {
            Vector3 loadPoint = GetPointAtT(points, arcLengths, load.x);
            if (loadPoint.x < hingeCrown.x)
                momentLoadsLeft += load.y * (hingeCrown.x - loadPoint.x);
        }
        
        float H = (momentAtCrownFromLeft - momentLoadsLeft) / (hingeCrown.y - hingeLeft.y);
        
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / (n - 1);
            Vector3 sectionPoint = GetPointAtT(points, arcLengths, t);
            float sectionAngle = GetTangentAngleAtT(points, arcLengths, t);
            
            float V_section = RV_left;
            float M_section = RV_left * (sectionPoint.x - hingeLeft.x) - H * (sectionPoint.y - hingeLeft.y);
            
            foreach (Vector2 load in loads)
            {
                Vector3 loadPoint = GetPointAtT(points, arcLengths, load.x);
                if (load.x <= t + 0.001f)
                {
                    V_section -= load.y;
                    M_section -= load.y * (sectionPoint.x - loadPoint.x);
                }
            }
            
            float cosA = Mathf.Cos(sectionAngle);
            float sinA = Mathf.Sin(sectionAngle);
            
            result.normalForces[i] = -(H * cosA + V_section * sinA);
            result.shearForces[i] = -H * sinA + V_section * cosA;
            result.moments[i] = M_section;
            
            float eccentricity = 0;
            if (Mathf.Abs(result.normalForces[i]) > 0.001f)
                eccentricity = result.moments[i] / result.normalForces[i];
            
            Vector3 normalDir = new Vector3(-sinA, cosA, 0);
            result.thrustLine[i] = sectionPoint + normalDir * eccentricity;
            
            result.deformations[i] = -(result.normalForces[i] / EA * span + result.moments[i] / EI * span * span / 10);
        }
        
        return result;
    }
    
    private static ArchSolution SolveTwoHingedArch(
        Vector3[] points, float[] arcLengths, 
        List<Vector2> loads, int resolution)
    {
        // Approssimazione: usa soluzione a 3 cerniere
        return SolveThreeHingedArch(points, arcLengths, loads, resolution);
    }
    
    private static Vector3 GetPointAtT(Vector3[] points, float[] arcLengths, float t)
    {
        t = Mathf.Clamp01(t);
        
        if (arcLengths == null || arcLengths.Length == 0)
        {
            float indexFloat = t * (points.Length - 1);
            int index = Mathf.FloorToInt(indexFloat);
            float frac = indexFloat - index;
            if (index >= points.Length - 1) return points[points.Length - 1];
            return Vector3.Lerp(points[index], points[index + 1], frac);
        }
        
        float totalLength = arcLengths[arcLengths.Length - 1];
        float targetLength = t * totalLength;
        
        for (int i = 0; i < arcLengths.Length - 1; i++)
        {
            if (targetLength <= arcLengths[i + 1])
            {
                float segLength = arcLengths[i + 1] - arcLengths[i];
                float segT = segLength > 0 ? (targetLength - arcLengths[i]) / segLength : 0;
                return Vector3.Lerp(points[i], points[i + 1], segT);
            }
        }
        
        return points[points.Length - 1];
    }
    
    private static float GetTangentAngleAtT(Vector3[] points, float[] arcLengths, float t)
    {
        float delta = 0.001f;
        float t1 = Mathf.Max(0, t - delta);
        float t2 = Mathf.Min(1, t + delta);
        
        Vector3 p1 = GetPointAtT(points, arcLengths, t1);
        Vector3 p2 = GetPointAtT(points, arcLengths, t2);
        
        Vector3 tangent = (p2 - p1).normalized;
        return Mathf.Atan2(tangent.y, tangent.x);
    }
}