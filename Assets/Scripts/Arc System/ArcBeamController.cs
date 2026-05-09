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
    
    [Header("Parametri Visualizzazione Diagrammi")]
    public float axialScale = 0.00001f;
    public float shearScale = 0.0001f;
    public float momentScale = 0.0001f;
    public float deflectionScale = 0.05f;      // Il tuo valore ottimale
    
    [Header("Offset Verticali Diagrammi")]
    public float axialForceYOffset = -2f;
    public float shearYOffset = -3f;
    public float momentYOffset = -4f;
    
    [Header("Carichi")]
    public float loadMagnitude = 10000f;
    
    [Header("Risoluzione")]
    [Range(50, 200)]
    public int femResolution = 100;            // AUMENTATO per calcoli più precisi
    
    [Range(100, 500)]
    public int visualizationPoints = 300;      // NUOVO: punti per la visualizzazione
    
    [Header("Opzioni Visualizzazione")]
    public bool showAxialForce = true;
    public bool showShear = true;
    public bool showMoment = true;
    public bool showDeformedShape = true;
    
    private MeshFilter meshFilter;
    private Vector3[] arcPoints;
    
    public float ArcStartX { get; private set; }
    public float ArcLength { get; private set; }
    private float horizontalLineY;
    
    void Start()
    {
        if (arcObject == null)
        {
            Debug.LogError("ArcBeamController: arcObject non assegnato!");
            return;
        }
        
        meshFilter = arcObject.GetComponent<MeshFilter>();
        if (meshFilter == null)
        {
            Debug.LogError("ArcBeamController: MeshFilter non trovato su arcObject!");
            return;
        }
        
        SampleArcPointsFromMesh();
        UpdateArcDimensions();
        horizontalLineY = GetMaxArcY() + 0.5f;
        
        InitializeLineRenderers();
        SetupInitialScenario();
    }
    
    void InitializeLineRenderers()
    {
        if (deformedShapeLine != null)
        {
            deformedShapeLine.startWidth = 0.05f;
            deformedShapeLine.endWidth = 0.05f;
            deformedShapeLine.startColor = Color.yellow;
            deformedShapeLine.endColor = Color.yellow;
            deformedShapeLine.material = new Material(Shader.Find("Sprites/Default"));
            deformedShapeLine.numCornerVertices = 10;     // Angoli più smooth
            deformedShapeLine.numCapVertices = 10;        // Estremità più smooth
            deformedShapeLine.gameObject.SetActive(showDeformedShape);
        }
        
        if (axialForceLine != null)
        {
            axialForceLine.startWidth = 0.1f;
            axialForceLine.endWidth = 0.1f;
            axialForceLine.startColor = Color.red;
            axialForceLine.endColor = Color.red;
            axialForceLine.material = new Material(Shader.Find("Sprites/Default"));
            axialForceLine.gameObject.SetActive(showAxialForce);
        }
        
        if (shearLine != null)
        {
            shearLine.startWidth = 0.1f;
            shearLine.endWidth = 0.1f;
            shearLine.startColor = Color.cyan;
            shearLine.endColor = Color.cyan;
            shearLine.material = new Material(Shader.Find("Sprites/Default"));
            shearLine.gameObject.SetActive(showShear);
        }
        
        if (momentLine != null)
        {
            momentLine.startWidth = 0.1f;
            momentLine.endWidth = 0.1f;
            momentLine.startColor = Color.magenta;
            momentLine.endColor = Color.magenta;
            momentLine.material = new Material(Shader.Find("Sprites/Default"));
            momentLine.gameObject.SetActive(showMoment);
        }
    }
    
    void Update()
    {
        if (arcObject == null || arcPoints == null || arcPoints.Length < 2) return;
        
        List<float> loadPositions = GetRelativePositions("Load");
        List<float> loadMags = new List<float>();
        foreach (var pos in loadPositions) loadMags.Add(loadMagnitude);
        
        List<float> supportPositions = GetRelativePositions("Support");
        
        if (supportPositions.Count >= 2)
        {
            // Usa la risoluzione FEM per il calcolo
            ArcBeamData results = ArcFEMCalculator.CalculateArcFEM(
                arcPoints,
                loadPositions,
                loadMags,
                supportPositions,
                visualizationPoints   // Alta risoluzione per la visualizzazione
            );
            
            if (showAxialForce && axialForceLine != null)
                RenderArcDiagram(axialForceLine, results.axialForcePoints, axialScale, 
                               Color.red, axialForceYOffset);
            
            if (showShear && shearLine != null)
                RenderArcDiagram(shearLine, results.shearPoints, shearScale, 
                               Color.cyan, shearYOffset);
            
            if (showMoment && momentLine != null)
                RenderArcDiagram(momentLine, results.momentPoints, momentScale, 
                               Color.magenta, momentYOffset);
            
            if (showDeformedShape && deformedShapeLine != null)
                RenderDeformedShape(deformedShapeLine, results, deflectionScale);
        }
    }
    
    void RenderDeformedShape(LineRenderer line, ArcBeamData results, float scale)
    {
        if (line == null || arcPoints == null || results.verticalDeflection == null) return;
        
        // Non serve ri-campionare a 500 punti, usiamo l'alta risoluzione già passata al FEM (es. 300)
        int pointsCount = results.verticalDeflection.Length;
        line.positionCount = pointsCount;
        
        float zPos = arcObject.transform.position.z - 0.1f; // Offset Z per AR
        
        for (int i = 0; i < pointsCount; i++)
        {
            float t = (float)i / (pointsCount - 1);
            
            // Interpola la posizione originale ESATTA sull'arco del prefab
            Vector3 originalPoint = InterpolateArcPoint(t);
            
            // Applica la deformazione globale calcolata dal FEM
            Vector3 deformed = originalPoint;
            deformed.x += results.horizontalDeflection[i] * scale;
            deformed.y += results.verticalDeflection[i] * scale;
            deformed.z = zPos; 
            
            line.SetPosition(i, deformed);
        }
    }
    
    // Interpola un punto sull'arco originale
    Vector3 InterpolateArcPoint(float t)
    {
        if (arcPoints == null || arcPoints.Length < 2)
            return Vector3.zero;
        
        // Calcola la lunghezza totale
        float totalLength = 0;
        float[] segmentLengths = new float[arcPoints.Length - 1];
        for (int i = 0; i < arcPoints.Length - 1; i++)
        {
            segmentLengths[i] = Vector3.Distance(arcPoints[i], arcPoints[i + 1]);
            totalLength += segmentLengths[i];
        }
        
        // Trova la posizione target lungo l'arco
        float targetDist = t * totalLength;
        float currentDist = 0;
        
        for (int i = 0; i < segmentLengths.Length; i++)
        {
            if (currentDist + segmentLengths[i] >= targetDist)
            {
                float localT = (targetDist - currentDist) / segmentLengths[i];
                return Vector3.Lerp(arcPoints[i], arcPoints[i + 1], localT);
            }
            currentDist += segmentLengths[i];
        }
        
        // Fallback: ultimo punto
        return arcPoints[arcPoints.Length - 1];
    }
    
    void SampleArcPointsFromMesh()
    {
        if (meshFilter == null || meshFilter.sharedMesh == null) return;
        
        Vector3[] vertices = meshFilter.sharedMesh.vertices;
        if (vertices.Length == 0) return;
        
        // Trova i limiti della mesh in coordinate mondo
        float minX = float.MaxValue;
        float maxX = float.MinValue;
        float minY = float.MaxValue;
        float maxY = float.MinValue;
        
        foreach (Vector3 v in vertices)
        {
            Vector3 worldV = arcObject.transform.TransformPoint(v);
            if (worldV.x < minX) minX = worldV.x;
            if (worldV.x > maxX) maxX = worldV.x;
            if (worldV.y < minY) minY = worldV.y;
            if (worldV.y > maxY) maxY = worldV.y;
        }
        
        // Campiona punti lungo l'asse X per creare la linea media dell'arco
        arcPoints = new Vector3[femResolution];
        
        for (int i = 0; i < femResolution; i++)
        {
            float t = (float)i / (femResolution - 1);
            float x = Mathf.Lerp(minX, maxX, t);
            
            // Trova tutti i vertici nella fascia corrente
            float bandWidth = (maxX - minX) / femResolution * 1.5f;
            float sumY_top = 0;
            float sumY_bottom = 0;
            int count_top = 0;
            int count_bottom = 0;
            float midY = (minY + maxY) / 2f;
            
            foreach (Vector3 v in vertices)
            {
                Vector3 worldV = arcObject.transform.TransformPoint(v);
                if (Mathf.Abs(worldV.x - x) < bandWidth)
                {
                    if (worldV.y > midY)
                    {
                        sumY_top += worldV.y;
                        count_top++;
                    }
                    else
                    {
                        sumY_bottom += worldV.y;
                        count_bottom++;
                    }
                }
            }
            
            // Usa il punto medio tra superiore e inferiore
            float y_top = count_top > 0 ? sumY_top / count_top : maxY;
            float y_bottom = count_bottom > 0 ? sumY_bottom / count_bottom : minY;
            float y = (y_top + y_bottom) / 2f;
            
            // Per il primo e ultimo punto, prendi il valore più basso (base dell'arco)
            if (i == 0 || i == femResolution - 1)
                y = Mathf.Min(y_top, y_bottom);
            
            arcPoints[i] = new Vector3(x, y, arcObject.transform.position.z);
        }
        
        Debug.Log($"<color=green>Arco campionato: {femResolution} punti, " +
                  $"X:[{minX:F2} - {maxX:F2}], Y:[{minY:F2} - {maxY:F2}]</color>");
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
        
        // Appoggi agli estremi
        Vector3 leftPoint = arcPoints[0];
        Vector3 rightPoint = arcPoints[arcPoints.Length - 1];
        
        SpawnAtPosition(supportPrefab, leftPoint.x, leftPoint.y - 0.3f);
        SpawnAtPosition(supportPrefab, rightPoint.x, rightPoint.y - 0.3f);
        
        // Carico in mezzeria
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
        foreach (GameObject obj in GetAllElements())
            Destroy(obj);
        
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
        
        // Fuori range: estrapola
        if (x < arcPoints[0].x) return arcPoints[0].y;
        return arcPoints[arcPoints.Length - 1].y;
    }
    
    GameObject SpawnAtPosition(GameObject prefab, float worldX, float worldY)
    {
        Vector3 pos = new Vector3(worldX, worldY, arcObject.transform.position.z);
        GameObject inst = Instantiate(prefab, pos, Quaternion.identity);
        
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
    
    void RenderArcDiagram(LineRenderer line, float[] values, float scale, Color color, float yOffset)
    {
        if (line == null || values == null || values.Length == 0) return;
        
        line.startColor = color;
        line.endColor = color;
        line.positionCount = values.Length;
        
        float zPos = arcObject.transform.position.z;
        
        for (int i = 0; i < values.Length; i++)
        {
            float t = (float)i / (values.Length - 1);
            
            // 1. Trova il punto base sull'arco
            Vector3 basePoint = InterpolateArcPoint(t);
            
            // 2. Trova la direzione normale (perpendicolare) in quel punto
            Vector3 normal = GetArcNormal(t);
            
            // 3. Proietta il valore del diagramma lungo la normale, includendo l'offset
            // L'offset allontanerà il diagramma dall'arco, seguendone la curvatura
            Vector3 diagramPoint = basePoint + normal * (yOffset + (values[i] * scale));
            diagramPoint.z = zPos;
            
            line.SetPosition(i, diagramPoint);
        }
    }
    // NUOVO METODO: Calcola la normale (perpendicolare) in un punto t (da 0 a 1) dell'arco
    Vector3 GetArcNormal(float t)
    {
        // Usiamo il metodo delle differenze finite per trovare la tangente
        float delta = 0.01f;
        float t1 = Mathf.Max(0, t - delta);
        float t2 = Mathf.Min(1, t + delta);
        
        Vector3 p1 = InterpolateArcPoint(t1);
        Vector3 p2 = InterpolateArcPoint(t2);
        
        Vector3 tangent = (p2 - p1).normalized;
        if (tangent == Vector3.zero) tangent = Vector3.right;
        
        // La normale è perpendicolare alla tangente (-Y, X)
        Vector3 normal = new Vector3(-tangent.y, tangent.x, 0).normalized;
        
        // Assicuriamoci che la normale punti verso l'esterno/alto dell'arco 
        // (utile per non far sovrapporre i diagrammi se l'arco è convesso)
        if (normal.y < 0) normal = -normal;
        
        return normal;
    }
    
    // Metodi pubblici per UI
    public void SetAxialScale(float scale) => axialScale = scale;
    public void SetShearScale(float scale) => shearScale = scale;
    public void SetMomentScale(float scale) => momentScale = scale;
    public void SetDeflectionScale(float scale) => deflectionScale = scale;
    
    public void ToggleAxialForce(bool show) 
    { 
        showAxialForce = show; 
        if (axialForceLine != null) axialForceLine.gameObject.SetActive(show);
    }
    
    public void ToggleShear(bool show) 
    { 
        showShear = show; 
        if (shearLine != null) shearLine.gameObject.SetActive(show);
    }
    
    public void ToggleMoment(bool show) 
    { 
        showMoment = show; 
        if (momentLine != null) momentLine.gameObject.SetActive(show);
    }
    
    public void ToggleDeformedShape(bool show) 
    { 
        showDeformedShape = show; 
        if (deformedShapeLine != null) deformedShapeLine.gameObject.SetActive(show);
    }
    
    void OnDrawGizmosSelected()
    {
        if (arcPoints == null) return;
        
        Gizmos.color = Color.green;
        for (int i = 0; i < arcPoints.Length - 1; i++)
        {
            Gizmos.DrawLine(arcPoints[i], arcPoints[i + 1]);
        }
    }
}