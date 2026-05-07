using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class ArcController : MonoBehaviour
{
    [Header("Impostazioni Solutore")]
    public SolverType currentSolver = SolverType.FEM;

    public GameObject arcObject;
    public GameObject supportPrefab;
    public GameObject loadPrefab;
    public LineRenderer shearLine;
    public LineRenderer momentLine;
    public LineRenderer normalLine;

    [Header("Parametri Grafici")]
    public float forceMagnitude = 15f;
    public float currentDiagramScale = 0.05f;
    public float deflectionVisualScale = 100f;
    public Vector3 diagramOffset = new Vector3(0, -2f, 0);
    public float minDistanceBetweenObjects = 0.5f;
    
    [Header("Colori Diagrammi")]
    public Color shearDiagramColor = new Color(0f, 1f, 1f, 1f);
    public Color momentDiagramColor = new Color(1f, 0f, 1f, 1f);
    public Color normalDiagramColor = new Color(0f, 1f, 0f, 1f);

    [Header("Parametri Arco (posizioni esplicite)")]
    public Vector3 leftSupportPoint = new Vector3(-5f, 0f, 0f);    // Punto sinistro dell'arco
    public Vector3 rightSupportPoint = new Vector3(5f, 0f, 0f);     // Punto destro dell'arco
    public Vector3 crownPoint = new Vector3(0f, 3f, 0f);            // Punto più alto (mezzeria)
    
    [Header("Parametri Calcolo")]
    public int arcResolution = 50;
    public float EI = 10000f;
    public float EA = 500000f;

    [Header("Offset Posizionamento")]
    public float supportVerticalOffset = -0.5f;  // Quanto sotto l'arco va l'appoggio
    public float loadVerticalOffset = 0.3f;      // Quanto sopra l'arco va il carico

    // Proprietà calcolate automaticamente
    public float ArcStartX { get; private set; }
    public float ArcEndX { get; private set; }
    public float ArcSpan { get; private set; }
    public float ArcRise { get; private set; }
    public Vector3[] ArcPathPoints { get; private set; }

    private bool showDeflection = false;
    private Mesh deformingMesh;
    private MeshFilter meshFilter;
    private Vector3[] originalVertices;

    void Start()
    {
        // Calcola le dimensioni dell'arco dalle posizioni esplicite
        CalculateArcDimensionsFromPoints();
        
        if (arcObject != null)
        {
            meshFilter = arcObject.GetComponent<MeshFilter>();
            if (meshFilter != null && meshFilter.sharedMesh != null)
            {
                Mesh sourceMesh = meshFilter.sharedMesh;
                
                deformingMesh = new Mesh();
                deformingMesh.name = "DynamicArcMesh";
                deformingMesh.vertices = sourceMesh.vertices;
                deformingMesh.triangles = sourceMesh.triangles;
                deformingMesh.uv = sourceMesh.uv;
                deformingMesh.normals = sourceMesh.normals;
                
                originalVertices = sourceMesh.vertices;
                meshFilter.mesh = deformingMesh;
            }
        }
        
        UpdateArcPathPoints();
        SetupInitialScenario();
    }

    void CalculateArcDimensionsFromPoints()
    {
        // Calcola luci e freccia dai punti definiti
        ArcStartX = leftSupportPoint.x;
        ArcEndX = rightSupportPoint.x;
        ArcSpan = Mathf.Abs(ArcEndX - ArcStartX);
        ArcRise = crownPoint.y - Mathf.Min(leftSupportPoint.y, rightSupportPoint.y);
        
        // Allinea i punti base (gli estremi devono essere alla stessa altezza)
        float baseY = leftSupportPoint.y;
        leftSupportPoint.y = baseY;
        rightSupportPoint.y = baseY;
        
        Debug.Log($"Dimensioni arco calcolate: StartX={ArcStartX}, EndX={ArcEndX}, Span={ArcSpan}, Rise={ArcRise}");
    }

    void UpdateArcPathPoints()
    {
        // Genera punti lungo l'arco parabolico usando i tre punti di controllo
        ArcPathPoints = new Vector3[arcResolution];
        
        for (int i = 0; i < arcResolution; i++)
        {
            float t = i / (float)(arcResolution - 1);
            Vector3 point = GetParabolicArcPoint(t);
            ArcPathPoints[i] = point;
        }
    }

    Vector3 GetParabolicArcPoint(float t)
    {
        // Interpolazione parabolica usando i tre punti: sinistro, coronamento, destro
        // t=0 -> left, t=0.5 -> crown, t=1 -> right
        
        if (t <= 0.5f)
        {
            // Da sinistro a coronamento (t 0->0.5 mappa a u 0->1)
            float u = t / 0.5f; // 0 to 1
            return QuadraticInterpolate(leftSupportPoint, crownPoint, u);
        }
        else
        {
            // Da coronamento a destro (t 0.5->1 mappa a u 0->1)
            float u = (t - 0.5f) / 0.5f; // 0 to 1
            return QuadraticInterpolate(crownPoint, rightSupportPoint, u);
        }
    }

    Vector3 QuadraticInterpolate(Vector3 p0, Vector3 p1, float t)
    {
        // Interpolazione quadratica: (1-t)²P0 + 2(1-t)tP1 + t²P2
        // Qui usiamo una parabola semplice: lerp con curvatura
        Vector3 result = Vector3.Lerp(p0, p1, t);
        
        // Aggiunge curvatura: massima al centro
        float curvature = 4f * t * (1f - t);
        float heightOffset = ArcRise * curvature;
        
        // Se stiamo interpolando tra sinistro e coronamento
        if (p0 == leftSupportPoint && p1 == crownPoint)
        {
            result.y = p0.y + (p1.y - p0.y) * t + heightOffset * 0.5f;
        }
        // Se tra coronamento e destro
        else if (p0 == crownPoint && p1 == rightSupportPoint)
        {
            result.y = p0.y + (p1.y - p0.y) * t + heightOffset * 0.5f;
        }
        
        return result;
    }

    void SetupInitialScenario()
    {
        Debug.Log($"Setup iniziale: StartX={ArcStartX}, EndX={ArcEndX}, Span={ArcSpan}");
        
        // Posiziona appoggio sinistro
        SpawnSupportAtArcPosition(0f, "LeftSupport");
        
        // Posiziona appoggio destro
        SpawnSupportAtArcPosition(ArcSpan, "RightSupport");
        
        // Posiziona carico in mezzeria
        SpawnLoadAtArcPosition(ArcSpan / 2f, "CenterLoad");
    }

    void SpawnSupportAtArcPosition(float distanceFromLeft, string customName = null)
    {
        // distanceFromLeft è la distanza lungo X dal punto sinistro
        float t = distanceFromLeft / ArcSpan;
        Vector3 arcPoint = GetParabolicArcPoint(t);
        
        float worldX = arcPoint.x;
        float supportY = arcPoint.y + supportVerticalOffset;
        
        Vector3 pos = new Vector3(worldX, supportY, arcObject.transform.position.z);
        GameObject inst = Instantiate(supportPrefab, pos, Quaternion.identity);
        inst.tag = "Support";
        
        if (customName != null)
            inst.name = customName;
            
        if (inst.TryGetComponent(out DraggableLoadArc drag)) 
            drag.arcController = this;
            
        Debug.Log($"Creato {customName} a x={worldX}, y={supportY} (arcY={arcPoint.y})");
    }

    void SpawnLoadAtArcPosition(float distanceFromLeft, string customName = null)
    {
        float t = distanceFromLeft / ArcSpan;
        Vector3 arcPoint = GetParabolicArcPoint(t);
        
        float worldX = arcPoint.x;
        float loadY = arcPoint.y + loadVerticalOffset;
        
        Vector3 pos = new Vector3(worldX, loadY, arcObject.transform.position.z);
        GameObject inst = Instantiate(loadPrefab, pos, Quaternion.identity);
        inst.tag = "Load";
        
        if (customName != null)
            inst.name = customName;
            
        if (inst.TryGetComponent(out DraggableLoadArc drag)) 
            drag.arcController = this;
            
        Debug.Log($"Creato {customName} a x={worldX}, y={loadY} (arcY={arcPoint.y})");
    }

    public void AddSupport()
    {
        float spawnX = GetValidSpawnX();
        SpawnSupportAtArcPosition(spawnX, $"Support_{spawnX:F2}");
    }

    public void AddLoad()
    {
        float spawnX = GetValidSpawnX();
        SpawnLoadAtArcPosition(spawnX, $"Load_{spawnX:F2}");
    }

    float GetValidSpawnX()
    {
        float center = ArcSpan / 2f;
        
        for (int i = 0; i < 20; i++)
        {
            float offset = (i / 2 + 1) * minDistanceBetweenObjects * (i % 2 == 0 ? 1 : -1);
            float testX = (i == 0) ? center : center + offset;
            testX = Mathf.Clamp(testX, 0.1f, ArcSpan - 0.1f);
            
            bool occupied = false;
            foreach (var obj in GetAllElements())
            {
                if (obj != null)
                {
                    float objRelativeX = obj.transform.position.x - ArcStartX;
                    if (Mathf.Abs(objRelativeX - testX) < minDistanceBetweenObjects * 0.8f)
                    {
                        occupied = true;
                        break;
                    }
                }
            }
                    
            if (!occupied) return testX;
        }
        return center;
    }

    void Update()
    {
        if (arcObject == null) return;

        List<float> sPos = GetRelativePositions("Support");
        List<float> lPos = GetRelativePositions("Load");
        List<float> lMag = new List<float>();
        foreach (var l in lPos) lMag.Add(forceMagnitude);

        if (sPos.Count >= 2)
        {
            ArcData results;
            
            if (currentSolver == SolverType.FEM)
            {
                results = ArcMath.CalculateFEM(
                    ArcSpan, ArcRise, ArcPathPoints, 
                    lPos, lMag, sPos, arcResolution, EI, EA
                );
            }
            else
            {
                results = ArcMath.CalculateAnalyticParabolic(
                    ArcSpan, ArcRise, lPos, lMag, sPos, arcResolution, EI
                );
            }

            RenderDiagram(shearLine, results.shearPoints, currentDiagramScale, shearDiagramColor);
            RenderDiagram(momentLine, results.momentPoints, currentDiagramScale, momentDiagramColor);
            RenderDiagram(normalLine, results.normalPoints, currentDiagramScale, normalDiagramColor);

            if (showDeflection) ApplyDeflectionToMesh(results.deflectionPoints);
            else ResetMesh();
        }
    }

    void ApplyDeflectionToMesh(float[] deflections)
    {
        if (deformingMesh == null || originalVertices == null) return;
        
        Vector3[] displacedVertices = new Vector3[originalVertices.Length];
        Vector3 localDown = arcObject.transform.InverseTransformDirection(Vector3.down);

        for (int i = 0; i < originalVertices.Length; i++)
        {
            Vector3 v = originalVertices[i];
            float t = Mathf.InverseLerp(ArcStartX, ArcEndX, v.x);
            int idx = Mathf.Clamp(Mathf.RoundToInt(t * (deflections.Length - 1)), 0, deflections.Length - 1);
            
            float dAmount = deflections[idx] * deflectionVisualScale;
            displacedVertices[i] = v + (localDown * dAmount);
        }

        deformingMesh.vertices = displacedVertices;
        deformingMesh.RecalculateNormals();
        deformingMesh.RecalculateBounds();
    }

    void ResetMesh()
    {
        if (deformingMesh != null && originalVertices != null && deformingMesh.vertices.Length > 0)
        {
            deformingMesh.vertices = originalVertices;
            deformingMesh.RecalculateNormals();
            deformingMesh.RecalculateBounds();
        }
    }

    public void ToggleDeflection() => showDeflection = !showDeflection;
    public void SetDiagramScale(float s) => currentDiagramScale = s;

    public void ResetStructure()
    {
        foreach (GameObject obj in GetAllElements()) 
            if (obj != null) Destroy(obj);
        
        SetupInitialScenario();
    }

    GameObject[] GetAllElements()
    {
        var l = new List<GameObject>();
        l.AddRange(GameObject.FindGameObjectsWithTag("Support"));
        l.AddRange(GameObject.FindGameObjectsWithTag("Load"));
        return l.ToArray();
    }

    List<float> GetRelativePositions(string tag)
    {
        var p = new List<float>();
        foreach (var o in GameObject.FindGameObjectsWithTag(tag))
        {
            if (o != null)
            {
                float relativeX = Mathf.Clamp(o.transform.position.x - ArcStartX, 0, ArcSpan);
                p.Add(relativeX);
            }
        }
        return p;
    }

    void RenderDiagram(LineRenderer line, float[] values, float scale, Color color)
    {
        if (line == null || ArcPathPoints == null) return;
        
        line.positionCount = values.Length;
        line.startColor = color;
        line.endColor = color;
        
        for (int i = 0; i < values.Length && i < ArcPathPoints.Length; i++)
        {
            Vector3 normalDir = GetNormalAtPoint(ArcPathPoints[i]);
            Vector3 pos = ArcPathPoints[i] + diagramOffset + normalDir * values[i] * scale;
            line.SetPosition(i, pos);
        }
    }

    Vector3 GetNormalAtPoint(Vector3 point)
    {
        Vector3 tangent = GetTangentAtPoint(point);
        Vector3 normal = new Vector3(-tangent.y, tangent.x, 0);
        if (normal.magnitude > 0.001f)
            normal.Normalize();
        else
            normal = Vector3.up;
        return normal;
    }

    Vector3 GetTangentAtPoint(Vector3 point)
    {
        // Calcola tangente usando i punti adiacenti sulla curva
        float tolerance = 0.05f;
        Vector3 pointAhead = point;
        Vector3 pointBehind = point;
        
        // Trova punti vicini sulla curva
        for (int i = 0; i < ArcPathPoints.Length; i++)
        {
            if (Vector3.Distance(ArcPathPoints[i], point) < tolerance)
            {
                if (i > 0) pointBehind = ArcPathPoints[i - 1];
                if (i < ArcPathPoints.Length - 1) pointAhead = ArcPathPoints[i + 1];
                break;
            }
        }
        
        Vector3 tangent = pointAhead - pointBehind;
        if (tangent.magnitude > 0.001f)
            tangent.Normalize();
        else
            tangent = Vector3.right;
            
        return tangent;
    }
}