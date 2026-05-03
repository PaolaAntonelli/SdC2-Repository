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
    public float deflectionScale = 0.001f;
    public int resolutionPoints = 100;
    
    [Header("Tipo Arco")]
    public ArchType archType = ArchType.ThreeHinged;
    
    [Header("Visualizzazione")]
    public bool showDeformation = false;
    public bool showThrustLine = true;
    public bool useVerticalNormals = true;
    
    [Header("Linea di trascinamento carico")]
    public Transform dragLineStart;  // Punto sinistro per la linea di trascinamento
    public Transform dragLineEnd;    // Punto destro per la linea di trascinamento
    
    private Vector3[] originalCurvePoints;
    private float[] arcLengthParams;
    private float totalArcLength;
    private float span;
    private float rise;
    private GameObject currentLoad;
    private LineRenderer dragLineGuide;
    
    void Start()
    {
        if (arcObject != null)
        {
            InitializeArcGeometry();
            CreateDragLineGuide();
            SetupInitialScenario();
        }
    }
    
    void CreateDragLineGuide()
    {
        // Crea una linea guida per il trascinamento del carico
        GameObject guideObject = new GameObject("DragLineGuide");
        guideObject.transform.SetParent(transform);
        dragLineGuide = guideObject.AddComponent<LineRenderer>();
        dragLineGuide.startWidth = 0.05f;
        dragLineGuide.endWidth = 0.05f;
        dragLineGuide.startColor = new Color(1, 1, 0, 0.3f);
        dragLineGuide.endColor = new Color(1, 1, 0, 0.3f);
        dragLineGuide.material = new Material(Shader.Find("Sprites/Default"));
        
        // Calcola i punti estremi per la linea di trascinamento
        Vector3 leftPoint = GetWorldCurvePoints()[0];
        Vector3 rightPoint = GetWorldCurvePoints()[GetWorldCurvePoints().Length - 1];
        
        // Alza leggermente la linea sopra l'arco
        float maxY = rise;
        leftPoint.y = maxY + 1f;
        rightPoint.y = maxY + 1f;
        
        dragLineGuide.positionCount = 2;
        dragLineGuide.SetPosition(0, leftPoint);
        dragLineGuide.SetPosition(1, rightPoint);
        
        // Assegna ai riferimenti public se non sono stati settati
        if (dragLineStart == null)
        {
            dragLineStart = guideObject.transform;
            // Crea child per il punto start
            GameObject startPoint = new GameObject("StartPoint");
            startPoint.transform.SetParent(guideObject.transform);
            startPoint.transform.position = leftPoint;
            dragLineStart = startPoint.transform;
        }
        
        if (dragLineEnd == null)
        {
            GameObject endPoint = new GameObject("EndPoint");
            endPoint.transform.SetParent(guideObject.transform);
            endPoint.transform.position = rightPoint;
            dragLineEnd = endPoint.transform;
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
            float bestZ = Mathf.Abs(bestVertex.z);
            
            foreach (Vector3 v in group.Value)
            {
                float absZ = Mathf.Abs(v.z);
                if (absZ < bestZ)
                {
                    bestZ = absZ;
                    bestVertex = v;
                }
            }
            
            bestVertex.z = 0;
            curvePoints.Add(bestVertex);
        }
        
        curvePoints.Sort((a, b) => a.x.CompareTo(b.x));
        originalCurvePoints = curvePoints.ToArray();
        
        span = originalCurvePoints[originalCurvePoints.Length - 1].x - originalCurvePoints[0].x;
        float minY = float.MaxValue;
        float maxY = float.MinValue;
        foreach (Vector3 p in originalCurvePoints)
        {
            minY = Mathf.Min(minY, p.y);
            maxY = Mathf.Max(maxY, p.y);
        }
        rise = maxY - minY;
        
        arcLengthParams = new float[originalCurvePoints.Length];
        arcLengthParams[0] = 0;
        totalArcLength = 0;
        
        for (int i = 1; i < originalCurvePoints.Length; i++)
        {
            totalArcLength += Vector3.Distance(originalCurvePoints[i], originalCurvePoints[i-1]);
            arcLengthParams[i] = totalArcLength;
        }
        
        Debug.Log($"Arco inizializzato: span={span:F2}, rise={rise:F2}");
    }
    
    Vector3 GetPointOnCurve(float t)
    {
        if (originalCurvePoints == null || originalCurvePoints.Length == 0)
            return Vector3.zero;
        
        t = Mathf.Clamp01(t);
        float targetLength = t * totalArcLength;
        
        for (int i = 0; i < arcLengthParams.Length - 1; i++)
        {
            if (targetLength <= arcLengthParams[i + 1])
            {
                float segLength = arcLengthParams[i + 1] - arcLengthParams[i];
                float segT = segLength > 0 ? (targetLength - arcLengthParams[i]) / segLength : 0;
                Vector3 localPoint = Vector3.Lerp(originalCurvePoints[i], originalCurvePoints[i + 1], segT);
                return arcObject.transform.TransformPoint(localPoint);
            }
        }
        
        return arcObject.transform.TransformPoint(originalCurvePoints[originalCurvePoints.Length - 1]);
    }
    
    // Proietta una posizione sulla linea retta sopra l'arco (per il trascinamento)
    public Vector3 ProjectOnDragLine(Vector3 worldPoint)
    {
        if (dragLineStart == null || dragLineEnd == null)
            return worldPoint;
        
        Vector3 start = dragLineStart.position;
        Vector3 end = dragLineEnd.position;
        
        // Proietta il punto sulla linea
        Vector3 line = end - start;
        float lineLength = line.magnitude;
        
        if (lineLength < 0.001f)
            return start;
        
        Vector3 lineDir = line / lineLength;
        float t = Vector3.Dot(worldPoint - start, lineDir);
        t = Mathf.Clamp01(t / lineLength);
        
        // Altezza costante (la Y della linea)
        Vector3 projectedPoint = start + t * line;
        
        return projectedPoint;
    }
    
    // Converte una posizione sulla linea di trascinamento nel parametro t dell'arco
    public float GetArcTFromDragPosition(Vector3 dragPosition)
    {
        if (dragLineStart == null || dragLineEnd == null)
            return 0.5f;
        
        Vector3 start = dragLineStart.position;
        Vector3 end = dragLineEnd.position;
        
        float totalWidth = end.x - start.x;
        float t = (dragPosition.x - start.x) / totalWidth;
        
        return Mathf.Clamp01(t);
    }
    
    // Ottiene il punto sull'arco corrispondente a una posizione sulla linea di trascinamento
    public Vector3 GetArcPointFromDragPosition(Vector3 dragPosition)
    {
        float t = GetArcTFromDragPosition(dragPosition);
        return GetPointOnCurve(t);
    }
    
    public Vector3 GetCurveNormal(float t)
    {
        if (useVerticalNormals)
        {
            return Vector3.up;
        }
        
        float delta = 0.01f;
        Vector3 p1 = GetPointOnCurve(Mathf.Max(0, t - delta));
        Vector3 p2 = GetPointOnCurve(Mathf.Min(1, t + delta));
        Vector3 tangent = (p2 - p1);
        
        if (tangent.magnitude < 0.0001f)
            return Vector3.up;
        
        tangent.Normalize();
        Vector3 normal = new Vector3(-tangent.y, tangent.x, 0);
        normal.Normalize();
        
        return normal;
    }
    
    void Update()
    {
        if (arcObject == null || originalCurvePoints == null) return;
        
        List<Vector2> loads = GetLoadsOnArc();
        
        GameObject[] supports = GameObject.FindGameObjectsWithTag("Support");
        if (supports.Length < 2) return;
        
        ArchSolution solution = ArcMath.CalculateArchForces(
            GetWorldCurvePoints(),
            arcLengthParams,
            loads,
            archType,
            resolutionPoints
        );
        
        // Calcola valori massimi per scaling automatico
        float maxNormal = 0, maxShear = 0, maxMoment = 0;
        foreach (float v in solution.normalForces) maxNormal = Mathf.Max(maxNormal, Mathf.Abs(v));
        foreach (float v in solution.shearForces) maxShear = Mathf.Max(maxShear, Mathf.Abs(v));
        foreach (float v in solution.moments) maxMoment = Mathf.Max(maxMoment, Mathf.Abs(v));
        
        maxNormal = Mathf.Max(maxNormal, 1f);
        maxShear = Mathf.Max(maxShear, 1f);
        maxMoment = Mathf.Max(maxMoment, 1f);
        
        float normalScaleActual = normalForceScale * (span / maxNormal);
        float shearScaleActual = shearScale * (span / maxShear);
        float momentScaleActual = momentScale * (span / maxMoment);
        
        normalScaleActual = Mathf.Min(normalScaleActual, span * 0.5f);
        shearScaleActual = Mathf.Min(shearScaleActual, span * 0.5f);
        momentScaleActual = Mathf.Min(momentScaleActual, span * 0.5f);
        
        RenderArcDiagram(normalForceLine, solution.normalForces, normalScaleActual, Color.green);
        RenderArcDiagram(shearLine, solution.shearForces, shearScaleActual, Color.cyan);
        RenderArcDiagram(momentLine, solution.moments, momentScaleActual, Color.magenta);
        
        if (showThrustLine && thrustLine != null && solution.thrustLine != null)
            RenderThrustLine(solution.thrustLine);
        else if (thrustLine != null)
            thrustLine.positionCount = 0;
        
        if (showDeformation && deformedShapeLine != null)
            RenderDeformedShape(solution.deformations);
        else if (deformedShapeLine != null)
            deformedShapeLine.positionCount = 0;
    }
    
    void RenderArcDiagram(LineRenderer line, float[] values, float scale, Color color)
    {
        if (line == null || values == null || values.Length == 0) return;
        
        int numPoints = Mathf.Min(values.Length, resolutionPoints);
        line.positionCount = numPoints;
        
        for (int i = 0; i < numPoints; i++)
        {
            float t = (float)i / (numPoints - 1);
            Vector3 basePoint = GetPointOnCurve(t);
            Vector3 normal = GetCurveNormal(t);
            float offset = values[i] * scale;
            offset = Mathf.Clamp(offset, -span, span);
            
            line.SetPosition(i, basePoint + normal * offset);
        }
        
        line.startColor = color;
        line.endColor = color;
        line.startWidth = 0.05f;
        line.endWidth = 0.05f;
    }
    
    void RenderThrustLine(Vector3[] thrustPoints)
    {
        if (thrustLine == null || thrustPoints == null) return;
        
        List<Vector3> validPoints = new List<Vector3>();
        foreach (Vector3 p in thrustPoints)
        {
            if (!float.IsNaN(p.x) && !float.IsInfinity(p.x) &&
                !float.IsNaN(p.y) && !float.IsInfinity(p.y))
            {
                validPoints.Add(p);
            }
        }
        
        thrustLine.positionCount = validPoints.Count;
        for (int i = 0; i < validPoints.Count; i++)
            thrustLine.SetPosition(i, validPoints[i]);
        
        thrustLine.startColor = Color.yellow;
        thrustLine.endColor = Color.yellow;
        thrustLine.startWidth = 0.03f;
        thrustLine.endWidth = 0.03f;
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
            float deformation = deformations[i] * deflectionScale;
            deformation = Mathf.Clamp(deformation, -rise * 0.5f, rise * 0.5f);
            
            deformedShapeLine.SetPosition(i, basePoint + normal * deformation);
        }
        
        deformedShapeLine.startColor = Color.red;
        deformedShapeLine.endColor = Color.red;
        deformedShapeLine.startWidth = 0.05f;
        deformedShapeLine.endWidth = 0.05f;
    }
    
    List<Vector2> GetLoadsOnArc()
    {
        List<Vector2> loads = new List<Vector2>();
        
        if (currentLoad != null)
        {
            // Usa la posizione del carico sulla linea per calcolare il parametro t
            float t = GetArcTFromDragPosition(currentLoad.transform.position);
            loads.Add(new Vector2(t, forceMagnitude));
        }
        
        return loads;
    }
    
    public Vector3[] GetWorldCurvePoints()
    {
        if (originalCurvePoints == null) return new Vector3[0];
        
        Vector3[] worldPoints = new Vector3[originalCurvePoints.Length];
        for (int i = 0; i < originalCurvePoints.Length; i++)
            worldPoints[i] = arcObject.transform.TransformPoint(originalCurvePoints[i]);
        return worldPoints;
    }
    
    void SetupInitialScenario()
    {
        if (originalCurvePoints == null || originalCurvePoints.Length < 2) return;
        
        // Rimuovi oggetti esistenti
        foreach (GameObject obj in GameObject.FindGameObjectsWithTag("Support"))
            Destroy(obj);
        if (currentLoad != null)
            Destroy(currentLoad);
        
        // Crea supporti ai lati (più in basso)
        Vector3 leftSupportPos = GetPointOnCurve(0);
        Vector3 rightSupportPos = GetPointOnCurve(1);
        leftSupportPos.y -= 0.3f;
        rightSupportPos.y -= 0.3f;
        
        GameObject leftSupport = Instantiate(supportPrefab, leftSupportPos, Quaternion.identity);
        GameObject rightSupport = Instantiate(supportPrefab, rightSupportPos, Quaternion.identity);
        leftSupport.tag = "Support";
        rightSupport.tag = "Support";
        
        // Crea carico in mezzeria (sulla linea di trascinamento)
        float midT = 0.5f;
        Vector3 arcMidPoint = GetPointOnCurve(midT);
        
        // Posiziona il carico sulla linea sopra l'arco
        Vector3 loadPos = arcMidPoint;
        loadPos.y = rise + 1f;  // Altezza costante sopra l'arco
        
        currentLoad = Instantiate(loadPrefab, loadPos, Quaternion.identity);
        currentLoad.tag = "Load";
        
        // Configura il carico
        Rigidbody rb = currentLoad.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }
        
        ArcDraggableLoad dragLoad = currentLoad.GetComponent<ArcDraggableLoad>();
        if (dragLoad != null)
        {
            dragLoad.arcController = this;
        }
        
        Debug.Log($"Setup completato: carico in mezzeria alla posizione {loadPos}");
    }
    
    public void ResetStructure()
    {
        SetupInitialScenario();
    }
    
    public void ToggleDeformation() { showDeformation = !showDeformation; }
    public void ToggleThrustLine() { showThrustLine = !showThrustLine; }
    public void ToggleNormals() { useVerticalNormals = !useVerticalNormals; }
}