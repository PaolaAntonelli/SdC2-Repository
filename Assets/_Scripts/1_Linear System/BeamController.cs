using UnityEngine;
using System.Collections.Generic;
using UnityEngine.UI;
using System.Linq;

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
    
    [Header("Diagram Scales")]
    public float shearDiagramScale = 0.2f;      // AUMENTATO da 0.05f
    public float momentDiagramScale = 0.05f;    // AUMENTATO da 0.01f
    public bool autoScaleDiagrams = true;        // ATTIVATO di default
    public float maxDiagramHeight = 3f;          // AUMENTATO da 2f
    
    public float deflectionVisualScale = 100f;
    public Vector3 diagramOffset = new Vector3(0, -2f, 0);
    public float minDistanceBetweenObjects = 0.5f;

    // Riferimenti UI opzionali per mostrare le scale
    public Text shearScaleText;
    public Text momentScaleText;

    private bool showDeflection = false;
    private Vector3[] originalVertices;
    private Mesh deformingMesh;
    private MeshFilter meshFilter;

    private int lengthAxis = 0;
    private float meshMinL, meshSizeL;

    public float BeamStartX { get; private set; }
    public float BeamLength { get; private set; }

    private float currentStructureLength;
    private float currentStructureStartX;

    public BeamData Results { get; private set; }
    public bool HasResults { get; private set; }

    private ArchController archController;
    private StressVisualizer stressVisualizer;

    public void SetSolverType(bool isAnalytic)
    {
        currentSolver = isAnalytic ? SolverType.Analytic : SolverType.FEM;
    }

    void Start()
    {
        archController = GetComponent<ArchController>();
        stressVisualizer = GetComponent<StressVisualizer>();

        // Inizializza scale aumentate
        shearDiagramScale = 0.2f;
        momentDiagramScale = 0.05f;
        maxDiagramHeight = 3f;
        autoScaleDiagrams = true;

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

        // Determina le dimensioni attuali della struttura (trave o arco)
        if (currentStructure == BeamStructureType.Arch && archController != null && archController.IsActive)
        {
            currentStructureLength = archController.ArchLength;
            currentStructureStartX = archController.ArchStartX;
        }
        else
        {
            currentStructureLength = BeamLength;
            currentStructureStartX = BeamStartX;
        }

        List<float> sPos = GetRelativePositions("Support");
        List<float> lPos = GetRelativePositions("Load");
        List<float> lMag = new List<float>();
        foreach (var l in lPos) lMag.Add(forceMagnitude);

        if (sPos.Count >= 1)
        {
            BeamData results;

            // NUOVA LOGICA: Usa il solutore appropriato per archi
            if (currentStructure == BeamStructureType.Arch && archController != null && archController.IsActive)
            {
                results = BeamMath.CalculateArchAnalytic(
                    currentStructureLength,
                    archController.archHeight,
                    lPos, lMag, sPos, 100);
            }
            else if (currentSolver == SolverType.Analytic)
            {
                results = BeamMath.CalculateAnalytic(currentStructureLength, lPos, lMag, sPos, 100);
            }
            else
            {
                results = BeamMath.CalculateFEM(currentStructureLength, lPos, lMag, sPos, 100);
            }

            Results = results;
            HasResults = true;

            // Auto-scaling migliorato
            if (autoScaleDiagrams)
            {
                float maxShear = results.shearPoints.Max(Mathf.Abs);
                float maxMoment = results.momentPoints.Max(Mathf.Abs);
                
                // Usa scale più aggressive per strutture ad arco
                float multiplier = (currentStructure == BeamStructureType.Arch) ? 2.5f : 1.5f;
                
                shearDiagramScale = maxShear > 0.001f ? 
                    (maxDiagramHeight / maxShear) * multiplier : 0.2f;
                momentDiagramScale = maxMoment > 0.001f ? 
                    (maxDiagramHeight / maxMoment) * multiplier : 0.05f;
                    
                // Limiti di sicurezza
                shearDiagramScale = Mathf.Clamp(shearDiagramScale, 0.01f, 1.0f);
                momentDiagramScale = Mathf.Clamp(momentDiagramScale, 0.001f, 0.2f);
            }

            // Aggiorna UI scale se presente
            UpdateScaleUI();

            // Render diagrammi con scale appropriate
            if (currentStructure == BeamStructureType.Arch)
            {
                RenderDiagramForArch(shearLine, results.shearPoints, shearDiagramScale, Color.cyan);
                RenderDiagramForArch(momentLine, results.momentPoints, momentDiagramScale, Color.magenta);
            }
            else
            {
                RenderDiagram(shearLine, results.shearPoints, shearDiagramScale, Color.cyan);
                RenderDiagram(momentLine, results.momentPoints, momentDiagramScale, Color.magenta);
            }

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

    void UpdateScaleUI()
    {
        if (shearScaleText != null)
            shearScaleText.text = $"Scala Taglio: 1 unità = {1f/shearDiagramScale:F2} kN";
        if (momentScaleText != null)
            momentScaleText.text = $"Scala Momento: 1 unità = {1f/momentDiagramScale:F2} kNm";
    }

    public void UpdateArchVisualization()
    {
        if (currentStructure != BeamStructureType.Arch) return;
        if (archController == null) return;
        if (!archController.IsActive) return;
        if (!HasResults) return;

        if (stressVisualizer != null)
        {
            stressVisualizer.enabled = true;
        }
    }

    void RenderDiagramForArch(LineRenderer line, float[] values, float scale, Color color)
    {
        if (line == null) return;
        line.positionCount = values.Length;
        line.startColor = color;
        line.endColor = color;

        for (int i = 0; i < values.Length; i++)
        {
            float t = (float)i / (values.Length - 1);
            float x = Mathf.Lerp(currentStructureStartX, currentStructureStartX + currentStructureLength, t);
            float y = GetArchYAtX(x);
            float yOffset = (line == momentLine) ? diagramOffset.y : 0f;
            line.SetPosition(i, new Vector3(x, y + values[i] * scale + yOffset, beamObject.transform.position.z));
        }
    }

    float GetArchYAtX(float worldX)
    {
        if (archController != null && archController.IsActive)
        {
            return archController.GetArchHeightAtX(worldX);
        }
        else
        {
            float t = (worldX - BeamStartX) / BeamLength;
            return beamObject.transform.position.y + archHeight * 4 * t * (1 - t);
        }
    }

    float GetArchY(float x) // mantenuto per retrocompatibilità
    {
        return GetArchYAtX(x);
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

        if (!showDeflection && currentStructure == BeamStructureType.Arch && archController != null)
        {
            archController.ResetMesh();
        }
    }

    public void SetDiagramScale(float s)
    {
        shearDiagramScale = s;
        momentDiagramScale = s * 0.1f;
        
        if (stressVisualizer != null)
        {
            stressVisualizer.SetDiagramScale(s);
        }
    }

    public void SetShearScale(float s) => shearDiagramScale = s;
    public void SetMomentScale(float s) => momentDiagramScale = s;
    public void SetAutoScale(bool auto) => autoScaleDiagrams = auto;

    public void ResetStructure()
    {
        foreach (GameObject obj in GetAllElements()) Destroy(obj);

        if (currentStructure == BeamStructureType.Arch && archController != null)
        {
            archController.ResetMesh();
        }

        Invoke("SetupInitialScenario", 0.05f);
    }

    void SetupInitialScenario()
    {
        UpdateBeamDimensions();
        SpawnAtPosition(supportPrefab, BeamStartX, -0.6f);
        SpawnAtPosition(supportPrefab, BeamStartX + BeamLength, -0.6f);
        SpawnAtPosition(loadPrefab, BeamStartX + (BeamLength / 2f), 0.6f);
    }

    public void AddSupport() => SpawnAtPosition(supportPrefab, GetValidSpawnX(), -0.6f);
    public void AddLoad() => SpawnAtPosition(loadPrefab, GetValidSpawnX(), 0.6f);

    float GetValidSpawnX()
    {
        float center = currentStructureStartX + (currentStructureLength / 2f);
        for (int i = 0; i < 20; i++)
        {
            float offset = (i / 2 + 1) * minDistanceBetweenObjects * (i % 2 == 0 ? 1 : -1);
            float testX = (i == 0) ? center : center + offset;
            testX = Mathf.Clamp(testX, currentStructureStartX, currentStructureStartX + currentStructureLength);
            bool occupied = false;
            foreach (var obj in GetAllElements())
                if (obj != null && Mathf.Abs(obj.transform.position.x - testX) < minDistanceBetweenObjects * 0.8f) occupied = true;
            if (!occupied) return testX;
        }
        return center;
    }

    private GameObject SpawnAtPosition(GameObject prefab, float worldX, float yOff)
    {
        float yPosition = beamObject.transform.position.y + yOff;
        
        // MODIFICA: Posiziona il carico SOPRA l'arco
        if (currentStructure == BeamStructureType.Arch)
        {
            float archY = GetArchYAtX(worldX);
            yPosition = archY + Mathf.Abs(yOff); // Forza yOff positivo per carichi sopra l'arco
            
            // Per i supporti, mantieni sotto l'arco
            if (prefab.CompareTag("Support"))
            {
                yPosition = archY + yOff; // yOff negativo per supporti
            }
        }
        
        Vector3 pos = new Vector3(worldX, yPosition, beamObject.transform.position.z);
        GameObject inst = Instantiate(prefab, pos, Quaternion.identity);
        if (inst.TryGetComponent(out DraggableLoad drag)) drag.beamController = this;
        return inst;
    }

    void UpdateBeamDimensions()
    {
        Renderer r = beamObject.GetComponent<Renderer>();
        if (r == null) return;
        BeamLength = r.bounds.size.x;
        BeamStartX = r.bounds.min.x;
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
        float startX = currentStructureStartX;
        float length = currentStructureLength;
        foreach (var o in GameObject.FindGameObjectsWithTag(tag))
            p.Add(Mathf.Clamp(o.transform.position.x - startX, 0, length));
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
            float x = currentStructureStartX + (i * (currentStructureLength / (values.Length - 1)));
            Vector3 pos = new Vector3(x, beamObject.transform.position.y, beamObject.transform.position.z) 
                         + diagramOffset + new Vector3(0, values[i] * scale, 0);
            line.SetPosition(i, pos);
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