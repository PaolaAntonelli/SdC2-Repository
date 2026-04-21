using UnityEngine;
using System.Collections.Generic;
using UnityEngine.UI;

public class BeamController : MonoBehaviour
{
    [Header("Impostazioni Solutore")]
    public SolverType currentSolver = SolverType.Analytic;

    [Header("Structure Type")]
    public BeamStructureType currentStructure = BeamStructureType.Beam;
    public float archHeight = 2f;

    public GameObject beamObject;
    public GameObject supportPrefab;
    public GameObject loadPrefab;
    public LineRenderer shearLine;
    public LineRenderer momentLine;

    [Header("Parametri Grafici")]
    public float forceMagnitude = 15f;
    public float currentDiagramScale = 0.05f;
    public float deflectionVisualScale = 100f;
    public Vector3 diagramOffset = new Vector3(0, -2f, 0);
    public float minDistanceBetweenObjects = 0.5f;

    private bool showDeflection = false;
    private Vector3[] originalVertices;
    private Mesh deformingMesh;
    private MeshFilter meshFilter;

    private int lengthAxis = 0;
    private float meshMinL, meshSizeL;

    public float BeamStartX { get; private set; }
    public float BeamLength { get; private set; }

    // Store results for stress visualization
    public BeamData Results { get; private set; }
    public bool HasResults { get; private set; }

    // Riferimenti ai componenti aggiuntivi
    private ArchController archController;
    private StressVisualizer stressVisualizer;

    public void SetSolverType(bool isAnalytic)
    {
        currentSolver = isAnalytic ? SolverType.Analytic : SolverType.FEM;
    }

    void Start()
    {
        // Ottieni riferimenti ai componenti aggiuntivi
        archController = GetComponent<ArchController>();
        stressVisualizer = GetComponent<StressVisualizer>();

        if (beamObject != null)
        {
            meshFilter = beamObject.GetComponent<MeshFilter>();
            Mesh sourceMesh = meshFilter.sharedMesh;

            deformingMesh = new Mesh();
            deformingMesh.name = "DynamicBeamMesh";
            deformingMesh.vertices = sourceMesh.vertices;
            deformingMesh.triangles = sourceMesh.triangles;
            deformingMesh.uv = sourceMesh.uv;
            deformingMesh.normals = sourceMesh.normals;
            deformingMesh.MarkDynamic();

            originalVertices = sourceMesh.vertices;
            meshFilter.mesh = deformingMesh;

            DetectMeshAxes(sourceMesh);
            UpdateBeamDimensions();
            SetupInitialScenario();
        }

        HasResults = false;
    }

    void DetectMeshAxes(Mesh m)
    {
        Bounds b = m.bounds;
        Vector3 sizes = b.size;

        if (sizes.x >= sizes.y && sizes.x >= sizes.z) lengthAxis = 0;
        else if (sizes.y >= sizes.x && sizes.y >= sizes.z) lengthAxis = 1;
        else lengthAxis = 2;

        meshMinL = b.min[lengthAxis];
        meshSizeL = b.size[lengthAxis];
    }

    void Update()
    {
        if (beamObject == null) return;
        UpdateBeamDimensions();

        List<float> sPos = GetRelativePositions("Support");
        List<float> lPos = GetRelativePositions("Load");
        List<float> lMag = new List<float>();
        foreach (var l in lPos) lMag.Add(forceMagnitude);

        if (sPos.Count >= 1)
        {
            BeamData results;

            if (currentSolver == SolverType.Analytic)
            {
                results = BeamMath.CalculateAnalytic(BeamLength, lPos, lMag, sPos, 100);
            }
            else
            {
                results = BeamMath.CalculateFEM(BeamLength, lPos, lMag, sPos, 100);
            }

            // Store results for stress visualization
            Results = results;
            HasResults = true;

            // Render diagrams according to structure type
            if (currentStructure == BeamStructureType.Arch)
            {
                RenderDiagramForArch(shearLine, results.shearPoints, currentDiagramScale, Color.cyan);
                RenderDiagramForArch(momentLine, results.momentPoints, currentDiagramScale, Color.magenta);
            }
            else
            {
                RenderDiagram(shearLine, results.shearPoints, currentDiagramScale, Color.cyan);
                RenderDiagram(momentLine, results.momentPoints, currentDiagramScale, Color.magenta);
            }

            // Aggiorna la visualizzazione per l'arco se attivo
            UpdateArchVisualization();

            if (showDeflection)
            {
                if (currentStructure == BeamStructureType.Arch && archController != null && archController.IsActive)
                {
                    archController.ApplyDeflection(results.deflectionPoints, deflectionVisualScale);
                }
                else
                {
                    ApplyDeflectionToMesh(results.deflectionPoints);
                }
            }
            else
            {
                if (currentStructure == BeamStructureType.Arch && archController != null && archController.IsActive)
                {
                    archController.ResetMesh();
                }
                else
                {
                    ResetMesh();
                }
            }
        }
        else
        {
            HasResults = false;
        }
    }

    /// <summary>
    /// Aggiorna la visualizzazione specifica per l'arco
    /// </summary>
    public void UpdateArchVisualization()
    {
        if (currentStructure != BeamStructureType.Arch) return;
        if (archController == null) return;
        if (!archController.IsActive) return;
        if (!HasResults) return;

        // Se lo stress visualizer è presente, forzalo ad aggiornarsi
        if (stressVisualizer != null)
        {
            stressVisualizer.enabled = true;
        }
    }

    // New method for arch diagram rendering
    void RenderDiagramForArch(LineRenderer line, float[] values, float scale, Color color)
    {
        if (line == null) return;
        line.positionCount = values.Length;
        line.startColor = color;
        line.endColor = color;
        
        for (int i = 0; i < values.Length; i++)
        {
            float x = BeamStartX + (i * (BeamLength / (values.Length - 1)));
            float y = GetArchY(x);
            line.SetPosition(i, new Vector3(x, y + values[i] * scale, beamObject.transform.position.z));
        }
    }

    float GetArchY(float x)
    {
        float t = (x - BeamStartX) / BeamLength;
        return beamObject.transform.position.y + archHeight * 4 * t * (1 - t);
    }

    void ApplyDeflectionToMesh(float[] deflections)
    {
        Vector3[] displacedVertices = new Vector3[originalVertices.Length];
        Vector3 localDown = beamObject.transform.InverseTransformDirection(Vector3.down);

        for (int i = 0; i < originalVertices.Length; i++)
        {
            Vector3 v = originalVertices[i];
            float relL = (v[lengthAxis] - meshMinL) / meshSizeL;
            int idx = Mathf.Clamp(Mathf.RoundToInt(relL * (deflections.Length - 1)), 0, deflections.Length - 1);
            float dAmount = deflections[idx] * deflectionVisualScale;
            displacedVertices[i] = v + (localDown * dAmount);
        }

        deformingMesh.vertices = displacedVertices;
        deformingMesh.RecalculateNormals();
        deformingMesh.RecalculateBounds();
    }

    void ResetMesh()
    {
        if (deformingMesh != null && deformingMesh.vertices.Length > 0 && deformingMesh.vertices != originalVertices)
        {
            deformingMesh.vertices = originalVertices;
            deformingMesh.RecalculateNormals();
            deformingMesh.RecalculateBounds();
        }
    }

    public void ToggleDeflection() 
    { 
        showDeflection = !showDeflection;
        
        // Se disattiviamo la deflessione, resettiamo anche la mesh dell'arco
        if (!showDeflection && currentStructure == BeamStructureType.Arch && archController != null)
        {
            archController.ResetMesh();
        }
    }
    
    public void SetDiagramScale(float s) 
    { 
        currentDiagramScale = s;
        
        // Propaga la scala anche allo stress visualizer
        if (stressVisualizer != null)
        {
            stressVisualizer.SetDiagramScale(s);
        }
    }

    public void ResetStructure()
    {
        foreach (GameObject obj in GetAllElements()) Destroy(obj);
        
        // Reset della mesh dell'arco se attivo
        if (currentStructure == BeamStructureType.Arch && archController != null)
        {
            archController.ResetMesh();
        }
        
        // Resetta lo stato dei risultati
        ResetResults();
        
        Invoke("SetupInitialScenario", 0.05f);
    }

    void SetupInitialScenario()
    {
        UpdateBeamDimensions();
        SpawnSupport(BeamStartX);
        SpawnSupport(BeamStartX + BeamLength);
        SpawnLoad(BeamStartX + (BeamLength / 2f));
    }

    public void AddSupport() => SpawnSupport(GetValidSpawnX());
    public void AddLoad() => SpawnLoad(GetValidSpawnX());

    float GetValidSpawnX()
    {
        float center = BeamStartX + (BeamLength / 2f);
        for (int i = 0; i < 20; i++)
        {
            float offset = (i / 2 + 1) * minDistanceBetweenObjects * (i % 2 == 0 ? 1 : -1);
            float testX = (i == 0) ? center : center + offset;
            testX = Mathf.Clamp(testX, BeamStartX, BeamStartX + BeamLength);
            bool occupied = false;
            foreach (var obj in GetAllElements())
                if (obj != null && Mathf.Abs(obj.transform.position.x - testX) < minDistanceBetweenObjects * 0.8f) occupied = true;
            if (!occupied) return testX;
        }
        return center;
    }

    /// <summary>
    /// Spawna un supporto in una posizione specifica
    /// </summary>
    public void SpawnSupport(float worldX)
    {
        if (supportPrefab == null) return;
        Vector3 pos = new Vector3(worldX, beamObject.transform.position.y - 0.6f, beamObject.transform.position.z);
        GameObject inst = Instantiate(supportPrefab, pos, Quaternion.identity);
        if (inst.TryGetComponent(out DraggableLoad drag)) drag.beamController = this;
    }

    /// <summary>
    /// Spawna un carico in una posizione specifica
    /// </summary>
    public void SpawnLoad(float worldX)
    {
        if (loadPrefab == null) return;
        Vector3 pos = new Vector3(worldX, beamObject.transform.position.y + 0.6f, beamObject.transform.position.z);
        GameObject inst = Instantiate(loadPrefab, pos, Quaternion.identity);
        if (inst.TryGetComponent(out DraggableLoad drag)) drag.beamController = this;
    }

    /// <summary>
    /// Aggiorna le dimensioni della trave
    /// </summary>
    public void UpdateBeamDimensions()
    {
        Renderer r = beamObject.GetComponent<Renderer>();
        if (r == null) return;
        BeamLength = r.bounds.size.x;
        BeamStartX = r.bounds.min.x;
    }

    /// <summary>
    /// Resetta lo stato dei risultati
    /// </summary>
    public void ResetResults()
    {
        HasResults = false;
        Results = default(BeamData);
    }

    GameObject[] GetAllElements()
    {
        var l = new List<GameObject>(GameObject.FindGameObjectsWithTag("Support"));
        l.AddRange(GameObject.FindGameObjectsWithTag("Load"));
        return l.ToArray();
    }

    List<float> GetRelativePositions(string tag)
    {
        var p = new List<float>();
        foreach (var o in GameObject.FindGameObjectsWithTag(tag))
            p.Add(Mathf.Clamp(o.transform.position.x - BeamStartX, 0, BeamLength));
        return p;
    }

    void RenderDiagram(LineRenderer line, float[] values, float scale, Color color)
    {
        if (line == null) return;
        line.positionCount = values.Length;
        line.startColor = color;
        line.endColor = color;
        
        for (int i = 0; i < values.Length; i++)
        {
            float x = BeamStartX + (i * (BeamLength / (values.Length - 1)));
            line.SetPosition(i, new Vector3(x, beamObject.transform.position.y, beamObject.transform.position.z) + diagramOffset + new Vector3(0, values[i] * scale, 0));
        }
    }

    public bool IsShowingDeflection() => showDeflection;
    
    public void SetStructureType(int type)
    {
        BeamStructureType newType = (BeamStructureType)type;
        
        if (newType == currentStructure) return;
        
        currentStructure = newType;
        
        if (currentStructure == BeamStructureType.Arch && archController != null)
        {
            archController.ActivateArchMode();
        }
        else if (archController != null)
        {
            archController.DeactivateArchMode();
        }
        
        HasResults = false;
    }
}