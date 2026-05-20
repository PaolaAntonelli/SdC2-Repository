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
    
    // Scala fissa: il riferimento è loadMagnitude (N). Un carico pari a loadMagnitude
    // visualizza un'altezza pari a diagramHeightFraction × freccia dell'arco.
    // axialScale/shearScale sono moltiplicatori supplementari (default 1).
    [Header("Scala Diagrammi (moltiplicatore su riferimento fisso = loadMagnitude)")]
    [Range(0.1f, 5f)] public float axialScale = 1f;
    [Range(0.1f, 5f)] public float shearScale = 1f;
    [Range(0.1f, 5f)] public float momentScale = 1f;
    // Esagerazione deformata: 1 = max spostamento → 15% della freccia (visibile ma non enorme)
    [Range(0.1f, 10f)] public float deflectionScale = 1f;

    [Header("Proporzione max diagramma / freccia arco (riferimento = loadMagnitude)")]
    [Range(0.05f, 1f)] public float diagramHeightFraction = 0.35f;

    [Header("Offset Perpendicolare Diagrammi (lungo normale, Unity units)")]
    public float axialForceYOffset = 0f;
    public float shearYOffset = 0f;
    public float momentYOffset = 0f;
    
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
            ArcBeamData results = ArcFEMCalculator.CalculateArcFEM(
                arcPoints,
                loadPositions,
                loadMags,
                supportPositions,
                visualizationPoints
            );

            // Scala fissa: baseHeight / fRef converte N → Unity units.
            // Non cambia al variare dei valori calcolati → il diagramma si modifica visibilmente
            // quando il carico si sposta (cambiano N, V, M ma la scala resta costante).
            float feetY = Mathf.Min(arcPoints[0].y, arcPoints[arcPoints.Length - 1].y);
            float arcRise = Mathf.Max(GetMaxArcY() - feetY, 0.01f);
            float arcSpan = Mathf.Max(arcPoints[arcPoints.Length - 1].x - arcPoints[0].x, 0.01f);
            float baseHeight = arcRise * diagramHeightFraction;
            float fRef = Mathf.Max(loadMagnitude, 1f);            // riferimento forza [N]
            float mRef = fRef * arcSpan;                           // riferimento momento [N·m]

            if (showAxialForce && axialForceLine != null)
                RenderArcDiagram(axialForceLine, results.axialForcePoints,
                               (baseHeight / fRef) * axialScale, Color.red, axialForceYOffset);

            if (showShear && shearLine != null)
                RenderArcDiagram(shearLine, results.shearPoints,
                               (baseHeight / fRef) * shearScale, Color.cyan, shearYOffset);

            if (showMoment && momentLine != null)
                RenderArcDiagram(momentLine, results.momentPoints,
                               (baseHeight / mRef) * momentScale, Color.magenta, momentYOffset);

            if (showDeformedShape && deformedShapeLine != null)
            {
                float maxDisp = 0f;
                for (int i = 0; i < results.verticalDeflection.Length; i++)
                {
                    maxDisp = Mathf.Max(maxDisp, Mathf.Abs(results.verticalDeflection[i]));
                    maxDisp = Mathf.Max(maxDisp, Mathf.Abs(results.horizontalDeflection[i]));
                }
                // deflectionScale è un moltiplicatore puro (1 = max spost. → 15% freccia)
                float deformScale = maxDisp > 1e-12f
                    ? (arcRise * 0.15f / maxDisp) * deflectionScale
                    : 1f;
                RenderDeformedShape(deformedShapeLine, results, deformScale);
            }
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

        // Limiti AABB in world space
        float minX = float.MaxValue, maxX = float.MinValue;
        float minY = float.MaxValue, maxY = float.MinValue;
        foreach (Vector3 v in vertices)
        {
            Vector3 w = arcObject.transform.TransformPoint(v);
            if (w.x < minX) minX = w.x;
            if (w.x > maxX) maxX = w.x;
            if (w.y < minY) minY = w.y;
            if (w.y > maxY) maxY = w.y;
        }

        // Passo 1: campionamento denso (10× la risoluzione) a X uniforme → linea media grezza
        int denseN = femResolution * 10;
        var densePts = new System.Collections.Generic.List<Vector3>();
        float midY_global = (minY + maxY) / 2f;
        float bandW = (maxX - minX) / denseN * 2f;

        for (int i = 0; i < denseN; i++)
        {
            float x = Mathf.Lerp(minX, maxX, (float)i / (denseN - 1));
            float sumTop = 0, sumBot = 0;
            int cTop = 0, cBot = 0;
            foreach (Vector3 v in vertices)
            {
                Vector3 w = arcObject.transform.TransformPoint(v);
                if (Mathf.Abs(w.x - x) < bandW)
                {
                    if (w.y >= midY_global) { sumTop += w.y; cTop++; }
                    else                    { sumBot += w.y; cBot++; }
                }
            }
            float yTop = cTop > 0 ? sumTop / cTop : maxY;
            float yBot = cBot > 0 ? sumBot / cBot : minY;
            float y = (i == 0 || i == denseN - 1) ? Mathf.Min(yTop, yBot) : (yTop + yBot) / 2f;
            densePts.Add(new Vector3(x, y, arcObject.transform.position.z));
        }

        // Passo 2: calcola la lunghezza d'arco cumulativa della curva densa
        float[] cumLen = new float[densePts.Count];
        cumLen[0] = 0f;
        for (int i = 1; i < densePts.Count; i++)
            cumLen[i] = cumLen[i - 1] + Vector3.Distance(densePts[i - 1], densePts[i]);
        float totalArc = cumLen[densePts.Count - 1];

        // Passo 3: ri-campiona a lunghezza d'arco UNIFORME → elementi FEM quasi uguali
        arcPoints = new Vector3[femResolution];
        arcPoints[0] = densePts[0];
        arcPoints[femResolution - 1] = densePts[densePts.Count - 1];
        int di = 0;
        for (int i = 1; i < femResolution - 1; i++)
        {
            float target = totalArc * i / (femResolution - 1);
            while (di < densePts.Count - 2 && cumLen[di + 1] < target) di++;
            float segLen = cumLen[di + 1] - cumLen[di];
            float lt = segLen > 1e-8f ? (target - cumLen[di]) / segLen : 0f;
            arcPoints[i] = Vector3.Lerp(densePts[di], densePts[di + 1], lt);
        }

        Debug.Log($"<color=green>Arco campionato (arc-length uniforme): {femResolution} nodi, " +
                  $"lunghezza arco={totalArc:F3} m, L_elem≈{totalArc/(femResolution-1)*1000:F1} mm</color>");
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