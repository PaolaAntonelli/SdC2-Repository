using UnityEngine;
using System.Collections.Generic;

public class ArcBeamController : MonoBehaviour
{
    [Header("Riferimenti")]
    public GameObject arcObject;
    public GameObject supportPrefab;
    public GameObject loadPrefab;
    public LineRenderer axialForceLine;
    public LineRenderer shearLine;
    public LineRenderer momentLine;
    public LineRenderer deformedShapeLine;
    
    [Header("Parametri Visualizzazione")]
    public float axialScale = 0.0001f;
    public float shearScale = 0.0001f;
    public float momentScale = 0.01f;
    public float deflectionScale = 50f;
    public Vector3 diagramOffset = new Vector3(0, -3f, 0);
    
    [Header("Carichi")]
    public float loadMagnitude = 10000f;
    
    [Header("Risoluzione FEM")]
    [Range(10, 100)]
    public int femResolution = 30;  // Numero di elementi FEM lungo l'arco
    
    private MeshFilter meshFilter;
    private Mesh originalMesh;
    private Vector3[] arcPoints;  // Punti campionati dalla mesh originale
    private List<GameObject> loads = new List<GameObject>();
    private List<GameObject> supports = new List<GameObject>();
    
    // Limiti per il movimento dei carichi
    public float ArcStartX { get; private set; }
    public float ArcLength { get; private set; }
    private float horizontalLineY;  // Y della linea orizzontale superiore per i carichi
    
    void Start()
    {
        if (arcObject == null)
        {
            Debug.LogError("ArcBeamController: arcObject non assegnato!");
            return;
        }
        
        // Ottieni la mesh originale
        meshFilter = arcObject.GetComponent<MeshFilter>();
        if (meshFilter == null)
        {
            Debug.LogError("ArcBeamController: MeshFilter non trovato su arcObject!");
            return;
        }
        
        originalMesh = meshFilter.sharedMesh;
        
        // Campiona punti dalla geometria originale
        SampleArcPointsFromMesh();
        
        // Calcola i limiti
        UpdateArcDimensions();
        
        // Determina l'altezza della linea orizzontale per i carichi
        horizontalLineY = GetMaxArcY() + 0.5f;
        
        // Setup iniziale
        SetupInitialScenario();
    }
    
    void Update()
    {
        if (arcObject == null || arcPoints == null || arcPoints.Length < 2) return;
        
        // Raccogli posizioni dei carichi e appoggi
        List<float> loadPositions = GetRelativePositions("Load");
        List<float> loadMags = new List<float>();
        foreach (var pos in loadPositions) loadMags.Add(loadMagnitude);
        
        List<float> supportPositions = GetRelativePositions("Support");
        
        if (supportPositions.Count >= 2)
        {
            // Calcola FEM per l'arco
            ArcBeamData results = ArcFEMCalculator.CalculateArcFEM(
                arcPoints,
                loadPositions,
                loadMags,
                supportPositions,
                100
            );
            
            // Renderizza i diagrammi
            RenderArcDiagram(axialForceLine, results.axialForcePoints, axialScale, Color.red);
            RenderArcDiagram(shearLine, results.shearPoints, shearScale, Color.cyan);
            RenderArcDiagram(momentLine, results.momentPoints, momentScale, Color.magenta);
            RenderDeformedShape(deformedShapeLine, results, deflectionScale);
        }
    }
    
    void SampleArcPointsFromMesh()
    {
        if (originalMesh == null) return;
        
        Vector3[] vertices = originalMesh.vertices;
        
        if (vertices.Length == 0) return;
        
        // Trova i limiti della mesh
        float minX = float.MaxValue;
        float maxX = float.MinValue;
        float minY = float.MaxValue;
        float maxY = float.MinValue;
        
        foreach (Vector3 v in vertices)
        {
            // Trasforma in coordinate mondo
            Vector3 worldV = arcObject.transform.TransformPoint(v);
            
            if (worldV.x < minX) minX = worldV.x;
            if (worldV.x > maxX) maxX = worldV.x;
            if (worldV.y < minY) minY = worldV.y;
            if (worldV.y > maxY) maxY = worldV.y;
        }
        
        // Campiona punti lungo l'asse X per ottenere la linea media dell'arco
        arcPoints = new Vector3[femResolution];
        
        for (int i = 0; i < femResolution; i++)
        {
            float t = (float)i / (femResolution - 1);
            float x = Mathf.Lerp(minX, maxX, t);
            
            // Per ogni X, trova la Y media dei vertici vicini
            float sumY = 0;
            int count = 0;
            
            foreach (Vector3 v in vertices)
            {
                Vector3 worldV = arcObject.transform.TransformPoint(v);
                if (Mathf.Abs(worldV.x - x) < (maxX - minX) / femResolution * 1.5f)
                {
                    sumY += worldV.y;
                    count++;
                }
            }
            
            float y = count > 0 ? sumY / count : Mathf.Lerp(minY, maxY, t);
            
            arcPoints[i] = new Vector3(x, y, arcObject.transform.position.z);
        }
        
        // Assicura che il primo e ultimo punto siano corretti
        arcPoints[0] = new Vector3(minX, GetArcYAtExtreme(minX, vertices, true), arcObject.transform.position.z);
        arcPoints[femResolution - 1] = new Vector3(maxX, GetArcYAtExtreme(maxX, vertices, false), arcObject.transform.position.z);
    }
    
    float GetArcYAtExtreme(float x, Vector3[] vertices, bool isMin)
    {
        float closestY = 0;
        float closestDist = float.MaxValue;
        
        foreach (Vector3 v in vertices)
        {
            Vector3 worldV = arcObject.transform.TransformPoint(v);
            float dist = Mathf.Abs(worldV.x - x);
            if (dist < closestDist)
            {
                closestDist = dist;
                closestY = worldV.y;
            }
        }
        
        return closestY;
    }
    
    float GetMaxArcY()
    {
        float maxY = float.MinValue;
        foreach (Vector3 point in arcPoints)
        {
            if (point.y > maxY) maxY = point.y;
        }
        return maxY;
    }
    
    void SetupInitialScenario()
    {
        UpdateArcDimensions();
        
        // Posiziona due appoggi agli estremi dell'arco
        Vector3 leftPoint = arcPoints[0];
        Vector3 rightPoint = arcPoints[arcPoints.Length - 1];
        
        SpawnAtPosition(supportPrefab, leftPoint.x, leftPoint.y - 0.3f);
        SpawnAtPosition(supportPrefab, rightPoint.x, rightPoint.y - 0.3f);
        
        // Posiziona un carico in mezzeria (sopra l'arco)
        int midIndex = arcPoints.Length / 2;
        Vector3 midPoint = arcPoints[midIndex];
        SpawnAtPosition(loadPrefab, midPoint.x, horizontalLineY);
    }
    
    public void AddSupport()
    {
        float x = GetValidSpawnX();
        float y = GetArcYAtX(x) - 0.3f;
        SpawnAtPosition(supportPrefab, x, y);
    }
    
    public void AddLoad()
    {
        float x = GetValidSpawnX();
        SpawnAtPosition(loadPrefab, x, horizontalLineY);
    }
    
    public void ResetStructure()
    {
        // Rimuovi tutti gli elementi
        foreach (GameObject obj in GetAllElements())
            Destroy(obj);
        
        loads.Clear();
        supports.Clear();
        
        SetupInitialScenario();
    }
    
    float GetValidSpawnX()
    {
        float minX = arcPoints[0].x;
        float maxX = arcPoints[arcPoints.Length - 1].x;
        float center = (minX + maxX) / 2f;
        
        for (int i = 0; i < 20; i++)
        {
            float offset = (i / 2 + 1) * 0.5f * (i % 2 == 0 ? 1 : -1);
            float testX = (i == 0) ? center : center + offset;
            testX = Mathf.Clamp(testX, minX, maxX);
            
            bool occupied = false;
            foreach (var obj in GetAllElements())
            {
                if (obj != null && Mathf.Abs(obj.transform.position.x - testX) < 0.4f)
                    occupied = true;
            }
            
            if (!occupied) return testX;
        }
        
        return center;
    }
    
    float GetArcYAtX(float x)
    {
        // Interpola linearmente tra i punti campionati
        for (int i = 0; i < arcPoints.Length - 1; i++)
        {
            if (x >= arcPoints[i].x && x <= arcPoints[i + 1].x)
            {
                float t = (x - arcPoints[i].x) / (arcPoints[i + 1].x - arcPoints[i].x);
                return Mathf.Lerp(arcPoints[i].y, arcPoints[i + 1].y, t);
            }
        }
        
        // Fallback
        return arcPoints[arcPoints.Length / 2].y;
    }
    
    private GameObject SpawnAtPosition(GameObject prefab, float worldX, float worldY)
    {
        Vector3 pos = new Vector3(worldX, worldY, arcObject.transform.position.z);
        GameObject inst = Instantiate(prefab, pos, Quaternion.identity);
        
        // Configura il componente di drag se presente
        if (inst.TryGetComponent(out ArcDraggableLoad drag))
            drag.arcController = this;
        
        return inst;
    }
    
    void UpdateArcDimensions()
    {
        if (arcPoints == null || arcPoints.Length < 2) return;
        
        ArcStartX = arcPoints[0].x;
        ArcLength = arcPoints[arcPoints.Length - 1].x - arcPoints[0].x;
    }
    
    GameObject[] GetAllElements()
    {
        var elements = new List<GameObject>(GameObject.FindGameObjectsWithTag("Support"));
        elements.AddRange(GameObject.FindGameObjectsWithTag("Load"));
        return elements.ToArray();
    }
    
    List<float> GetRelativePositions(string tag)
    {
        var positions = new List<float>();
        foreach (var obj in GameObject.FindGameObjectsWithTag(tag))
        {
            float relPos = (obj.transform.position.x - ArcStartX) / ArcLength;
            positions.Add(Mathf.Clamp01(relPos));
        }
        return positions;
    }
    
    void RenderArcDiagram(LineRenderer line, float[] values, float scale, Color color)
    {
        if (line == null || values == null || values.Length == 0) return;
        
        line.startColor = color;
        line.endColor = color;
        line.positionCount = values.Length;
        
        Vector3 basePosition = arcObject.transform.position + diagramOffset;
        
        for (int i = 0; i < values.Length; i++)
        {
            float t = (float)i / (values.Length - 1);
            float x = Mathf.Lerp(ArcStartX, ArcStartX + ArcLength, t);
            float y = basePosition.y + values[i] * scale;
            
            line.SetPosition(i, new Vector3(x, y, basePosition.z));
        }
    }
    
    void RenderDeformedShape(LineRenderer line, ArcBeamData results, float scale)
    {
        if (line == null || arcPoints == null) return;
        
        line.startColor = Color.yellow;
        line.endColor = Color.yellow;
        line.positionCount = arcPoints.Length;
        
        for (int i = 0; i < arcPoints.Length; i++)
        {
            // Mappa l'indice dell'arco ai risultati
            int resultIndex = Mathf.RoundToInt((float)i / (arcPoints.Length - 1) * 
                                               (results.horizontalDeflection.Length - 1));
            
            Vector3 deformed = arcPoints[i];
            deformed.x += results.horizontalDeflection[resultIndex] * scale;
            deformed.y += results.verticalDeflection[resultIndex] * scale;
            deformed.z = arcObject.transform.position.z - 0.1f;
            
            line.SetPosition(i, deformed);
        }
    }
    
    // Metodi pubblici per UI
    public void SetAxialScale(float scale) => axialScale = scale;
    public void SetShearScale(float scale) => shearScale = scale;
    public void SetMomentScale(float scale) => momentScale = scale;
    public void SetDeflectionScale(float scale) => deflectionScale = scale;
    
    // Visualizza i punti campionati nell'editor
    void OnDrawGizmosSelected()
    {
        if (arcPoints == null) return;
        
        Gizmos.color = Color.green;
        for (int i = 0; i < arcPoints.Length - 1; i++)
        {
            Gizmos.DrawLine(arcPoints[i], arcPoints[i + 1]);
        }
        
        Gizmos.color = Color.red;
        foreach (var point in arcPoints)
        {
            Gizmos.DrawSphere(point, 0.1f);
        }
    }
}