using UnityEngine;
using System.Collections.Generic;

public class ArcBeamController : MonoBehaviour
{
    [Header("Impostazioni Arco")]
    public GameObject arcObject;
    public GameObject supportPrefab;
    public GameObject loadPrefab;
    public LineRenderer normalForceLine;
    public LineRenderer shearLine;
    public LineRenderer momentLine;
    public LineRenderer deformedShapeLine;
    public LineRenderer thrustLine;
    
    [Header("Parametri Grafici")]
    public float forceMagnitude = 15f;
    public float normalForceScale = 0.02f;
    public float shearScale = 0.05f;
    public float momentScale = 0.1f;
    public float deflectionScale = 100f;
    public int resolutionPoints = 100;
    
    [Header("Tipo Arco")]
    public ArchType archType = ArchType.ThreeHinged;
    
    [Header("Visualizzazione")]
    public bool showDeformation = false;
    public bool showThrustLine = true;
    
    private Vector3[] originalCurvePoints;
    private float[] arcLengthParams;
    private float totalArcLength;
    
    void Start()
    {
        if (arcObject != null)
        {
            InitializeArcGeometry();
            SetupInitialScenario();
        }
    }
    
    void InitializeArcGeometry()
    {
        MeshFilter meshFilter = arcObject.GetComponent<MeshFilter>();
        if (meshFilter == null)
        {
            Debug.LogError("ArcObject necessita di un MeshFilter!");
            return;
        }
        
        Mesh mesh = meshFilter.sharedMesh;
        Vector3[] vertices = mesh.vertices;
        
        Dictionary<float, List<Vector3>> xGroups = new Dictionary<float, List<Vector3>>();
        foreach (Vector3 v in vertices)
        {
            float xRounded = Mathf.Round(v.x * 100f) / 100f;
            if (!xGroups.ContainsKey(xRounded))
                xGroups[xRounded] = new List<Vector3>();
            xGroups[xRounded].Add(v);
        }
        
        List<Vector3> curvePoints = new List<Vector3>();
        foreach (var group in xGroups)
        {
            Vector3 bestVertex = group.Value[0];
            float bestZ = Mathf.Abs(group.Value[0].z);
            float bestY = group.Value[0].y;
            
            foreach (Vector3 v in group.Value)
            {
                if (Mathf.Abs(v.z) < bestZ || (Mathf.Abs(v.z) == bestZ && v.y > bestY))
                {
                    bestZ = Mathf.Abs(v.z);
                    bestY = v.y;
                    bestVertex = v;
                }
            }
            curvePoints.Add(bestVertex);
        }
        
        curvePoints.Sort((a, b) => a.x.CompareTo(b.x));
        originalCurvePoints = curvePoints.ToArray();
        
        arcLengthParams = new float[originalCurvePoints.Length];
        arcLengthParams[0] = 0;
        totalArcLength = 0;
        
        for (int i = 1; i < originalCurvePoints.Length; i++)
        {
            totalArcLength += Vector3.Distance(originalCurvePoints[i], originalCurvePoints[i-1]);
            arcLengthParams[i] = totalArcLength;
        }
        
        Debug.Log($"Arco inizializzato: {originalCurvePoints.Length} punti, lunghezza: {totalArcLength:F2}");
    }
    
    void Update()
    {
        if (arcObject == null || originalCurvePoints == null) return;
        
        List<Vector2> loads = GetLoadsOnArc();
        if (loads.Count == 0) return;
        
        GameObject[] supports = GameObject.FindGameObjectsWithTag("Support");
        if (supports.Length < 2)
        {
            Debug.LogWarning("Servono almeno 2 supporti!");
            return;
        }
        
        ArchSolution solution = ArcMath.CalculateArchForces(
            GetWorldCurvePoints(),
            arcLengthParams,
            loads,
            archType,
            resolutionPoints
        );
        
        RenderArcDiagram(normalForceLine, solution.normalForces, normalForceScale, Color.green, true);
        RenderArcDiagram(shearLine, solution.shearForces, shearScale, Color.cyan, true);
        RenderArcDiagram(momentLine, solution.moments, momentScale, Color.magenta, true);
        
        if (showThrustLine && thrustLine != null)
            RenderThrustLine(solution.thrustLine);
        else if (thrustLine != null)
            thrustLine.positionCount = 0;
        
        if (showDeformation && deformedShapeLine != null)
            RenderDeformedShape(solution.deformations);
        else if (deformedShapeLine != null)
            deformedShapeLine.positionCount = 0;
    }
    
    List<Vector2> GetLoadsOnArc()
    {
        List<Vector2> loads = new List<Vector2>();
        GameObject[] loadObjects = GameObject.FindGameObjectsWithTag("Load");
        
        foreach (GameObject loadObj in loadObjects)
        {
            float normalizedPos = ProjectPointOntoArc(loadObj.transform.position);
            if (normalizedPos >= 0)
                loads.Add(new Vector2(normalizedPos, forceMagnitude));
        }
        return loads;
    }
    
    float ProjectPointOntoArc(Vector3 worldPoint)
    {
        Vector3[] worldCurve = GetWorldCurvePoints();
        float minDist = float.MaxValue;
        int closestIndex = 0;
        
        for (int i = 0; i < worldCurve.Length; i++)
        {
            float dist = Vector2.Distance(
                new Vector2(worldPoint.x, worldPoint.y),
                new Vector2(worldCurve[i].x, worldCurve[i].y)
            );
            if (dist < minDist)
            {
                minDist = dist;
                closestIndex = i;
            }
        }
        
        return arcLengthParams[closestIndex] / totalArcLength;
    }
    
    public Vector3[] GetWorldCurvePoints()
    {
        if (originalCurvePoints == null) return new Vector3[0];
        
        Vector3[] worldPoints = new Vector3[originalCurvePoints.Length];
        for (int i = 0; i < originalCurvePoints.Length; i++)
            worldPoints[i] = arcObject.transform.TransformPoint(originalCurvePoints[i]);
        return worldPoints;
    }
    
    Vector3 GetPointOnCurve(float t)
    {
        float targetLength = t * totalArcLength;
        for (int i = 0; i < arcLengthParams.Length - 1; i++)
        {
            if (targetLength <= arcLengthParams[i + 1])
            {
                float segT = (targetLength - arcLengthParams[i]) / (arcLengthParams[i + 1] - arcLengthParams[i]);
                Vector3 localPoint = Vector3.Lerp(originalCurvePoints[i], originalCurvePoints[i + 1], segT);
                return arcObject.transform.TransformPoint(localPoint);
            }
        }
        return arcObject.transform.TransformPoint(originalCurvePoints[originalCurvePoints.Length - 1]);
    }
    
    Vector3 GetCurveNormal(float t)
    {
        float delta = 0.001f;
        Vector3 p1 = GetPointOnCurve(Mathf.Max(0, t - delta));
        Vector3 p2 = GetPointOnCurve(Mathf.Min(1, t + delta));
        Vector3 tangent = (p2 - p1).normalized;
        return Vector3.Cross(tangent, Vector3.forward).normalized;
    }
    
    void RenderArcDiagram(LineRenderer line, float[] values, float scale, Color color, bool perpendicular)
    {
        if (line == null || values == null) return;
        
        int numPoints = Mathf.Min(values.Length, resolutionPoints);
        line.positionCount = numPoints;
        
        for (int i = 0; i < numPoints; i++)
        {
            float t = (float)i / (numPoints - 1);
            Vector3 basePoint = GetPointOnCurve(t);
            Vector3 normal = GetCurveNormal(t);
            line.SetPosition(i, basePoint + normal * (values[i] * scale));
        }
        
        line.startColor = color;
        line.endColor = color;
    }
    
    void RenderThrustLine(Vector3[] thrustPoints)
    {
        if (thrustLine == null || thrustPoints == null) return;
        thrustLine.positionCount = thrustPoints.Length;
        for (int i = 0; i < thrustPoints.Length; i++)
            thrustLine.SetPosition(i, thrustPoints[i]);
        thrustLine.startColor = Color.yellow;
        thrustLine.endColor = Color.yellow;
    }
    
    void RenderDeformedShape(float[] deformations)
    {
        if (deformedShapeLine == null || deformations == null) return;
        
        int numPoints = Mathf.Min(deformations.Length, resolutionPoints);
        deformedShapeLine.positionCount = numPoints;
        
        for (int i = 0; i < numPoints; i++)
        {
            float t = (float)i / (numPoints - 1);
            Vector3 basePoint = GetPointOnCurve(t);
            Vector3 normal = GetCurveNormal(t);
            deformedShapeLine.SetPosition(i, basePoint + normal * (deformations[i] * deflectionScale));
        }
        
        deformedShapeLine.startColor = Color.red;
        deformedShapeLine.endColor = Color.red;
    }
    
    void SetupInitialScenario()
    {
        if (originalCurvePoints == null || originalCurvePoints.Length < 2) return;
        
        SpawnAtCurvePoint(supportPrefab, 0, -0.5f);
        SpawnAtCurvePoint(supportPrefab, 1, -0.5f);
        
        int crownIndex = 0;
        float maxY = float.MinValue;
        for (int i = 0; i < originalCurvePoints.Length; i++)
        {
            if (originalCurvePoints[i].y > maxY)
            {
                maxY = originalCurvePoints[i].y;
                crownIndex = i;
            }
        }
        
        float crownT = arcLengthParams[crownIndex] / totalArcLength;
        SpawnAtCurvePoint(loadPrefab, crownT, 0.5f);
    }
    
    void SpawnAtCurvePoint(GameObject prefab, float t, float verticalOffset)
    {
        Vector3 basePoint = GetPointOnCurve(t);
        Vector3 normal = GetCurveNormal(t);
        Vector3 spawnPos = basePoint + normal * verticalOffset;
        
        GameObject instance = Instantiate(prefab, spawnPos, Quaternion.identity);
        
        ArcDraggableLoad dragLoad = instance.GetComponent<ArcDraggableLoad>();
        if (dragLoad != null)
            dragLoad.arcController = this;
    }
    
    // Public methods for UI
    public void ToggleDeformation() { showDeformation = !showDeformation; }
    public void ToggleThrustLine() { showThrustLine = !showThrustLine; }
    public void ResetStructure()
    {
        foreach (GameObject obj in GameObject.FindGameObjectsWithTag("Support"))
            Destroy(obj);
        foreach (GameObject obj in GameObject.FindGameObjectsWithTag("Load"))
            Destroy(obj);
        Invoke("SetupInitialScenario", 0.05f);
    }
}