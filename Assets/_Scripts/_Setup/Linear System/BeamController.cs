using UnityEngine;
using System.Collections.Generic;
using UnityEngine.UI;
using System.Collections;
using System;

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

    [Header("Height Offsets")]
    [Tooltip("Offset di altezza per carichi e supporti per ogni tipo di struttura")]
    public List<StructureHeightOffset> structureHeightOffsets = new List<StructureHeightOffset>
    {
        new StructureHeightOffset 
        { 
            structureType = BeamStructureType.Beam, 
            loadHeightOffset = 0.6f,
            supportHeightOffset = -0.6f
        },
        new StructureHeightOffset 
        { 
            structureType = BeamStructureType.Arch, 
            loadHeightOffset = 2.6f,
            supportHeightOffset = -0.6f
        }
    };

    private bool showDeflection = false;
    private Vector3[] originalVertices;
    private Mesh deformingMesh;
    private MeshFilter meshFilter;

    private int lengthAxis = 0;
    private float meshMinL, meshSizeL;

    public float BeamStartX { get; private set; }
    public float BeamLength { get; private set; }

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
            StartCoroutine(DelayedConfiguration());
        }

        HasResults = false;
    }

    private void CleanupAllElements()
    {
        var supports = GameObject.FindGameObjectsWithTag("Support");
        var loads = GameObject.FindGameObjectsWithTag("Load");
        
        foreach (var obj in supports) 
        {
            if (obj != null) DestroyImmediate(obj);
        }
        foreach (var obj in loads) 
        {
            if (obj != null) DestroyImmediate(obj);
        }
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
        
        if (currentStructure == BeamStructureType.Arch && archController != null && archController.IsActive)
        {
            archController.UpdateArchDimensions();
            BeamStartX = archController.ArchStartX;
            BeamLength = archController.ArchLength;
        }
        else
        {
            UpdateBeamDimensions();
        }

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

            Results = results;
            HasResults = true;

            if (currentStructure == BeamStructureType.Arch && archController != null && archController.IsActive)
            {
                RenderDiagramForArch(shearLine, results.shearPoints, currentDiagramScale, Color.cyan);
                RenderDiagramForArch(momentLine, results.momentPoints, currentDiagramScale, Color.magenta);
            }
            else
            {
                RenderDiagram(shearLine, results.shearPoints, currentDiagramScale, Color.cyan);
                RenderDiagram(momentLine, results.momentPoints, currentDiagramScale, Color.magenta);
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
        if (archController == null || !archController.IsActive) return;
        
        line.positionCount = values.Length;
        line.startColor = color;
        line.endColor = color;
        
        float startX = archController.ArchStartX;
        float endX = archController.ArchEndX;
        
        for (int i = 0; i < values.Length; i++)
        {
            float t = (float)i / (values.Length - 1);
            float x = Mathf.Lerp(startX, endX, t);
            float yOnArch = archController.GetArchHeightAtX(x);
            Vector3 normal = archController.GetArchNormalAtX(x);
            Vector3 basePos = new Vector3(x, yOnArch, beamObject.transform.position.z);
            Vector3 diagramPos = basePos + normal * values[i] * scale;
            line.SetPosition(i, diagramPos);
        }
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
        currentDiagramScale = s;
        
        if (stressVisualizer != null)
        {
            stressVisualizer.SetDiagramScale(s);
        }
    }

    public void ResetStructure()
    {
        ResetToDefaultConfiguration();
    
        if (currentStructure == BeamStructureType.Arch && archController != null)
        {
            archController.ResetMesh();
        }
        
        ResetResults();
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

    public void SpawnSupport(float worldX)
    {
        if (supportPrefab == null) return;
        
        float yPos;
        
        if (currentStructure == BeamStructureType.Arch && archController != null && archController.IsActive)
        {
            yPos = archController.GetArchHeightAtX(worldX) + GetCurrentSupportHeightOffset();
        }
        else
        {
            yPos = beamObject.transform.position.y + GetCurrentSupportHeightOffset();
        }
        
        Vector3 pos = new Vector3(worldX, yPos, beamObject.transform.position.z);
        GameObject inst = Instantiate(supportPrefab, pos, Quaternion.identity);
        if (inst.TryGetComponent(out DraggableLoad drag)) 
        {
            drag.beamController = this;
            drag.UpdateYOffset();
        }
    }

    public void SpawnLoad(float worldX)
    {
        if (loadPrefab == null) return;
        
        float loadOffset = GetCurrentLoadHeightOffset();
        float yPos = beamObject.transform.position.y + loadOffset;
        
        Vector3 pos = new Vector3(worldX, yPos, beamObject.transform.position.z);
        GameObject inst = Instantiate(loadPrefab, pos, Quaternion.identity);
        
        if (inst.TryGetComponent(out DraggableLoad drag))
        {
            drag.beamController = this;
            drag.UpdateYOffset();
        }
    }

    public void UpdateBeamDimensions()
    {
        Renderer r = beamObject.GetComponent<Renderer>();
        if (r == null) return;
        BeamLength = r.bounds.size.x;
        BeamStartX = r.bounds.min.x;
    }

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
        
        // Aspetta che la struttura sia inizializzata poi aggiorna gli elementi
        StartCoroutine(UpdateElementsDelayed());
    }

    private IEnumerator UpdateElementsDelayed()
    {
        yield return new WaitForEndOfFrame();
        yield return new WaitForEndOfFrame();
        UpdateAllElementsYPosition();
    }

    public void UpdateAllElementsYPosition()
    {
        var allLoads = GameObject.FindGameObjectsWithTag("Load");
        var allSupports = GameObject.FindGameObjectsWithTag("Support");
        
        foreach (var obj in allLoads)
        {
            if (obj != null && obj.TryGetComponent(out DraggableLoad drag))
            {
                drag.beamController = this;
                drag.UpdateYOffset();
            }
        }
        
        foreach (var obj in allSupports)
        {
            if (obj != null && obj.TryGetComponent(out DraggableLoad drag))
            {
                drag.beamController = this;
                drag.ForceArchSupportUpdate();
                drag.UpdateYOffset();
            }
        }
    }

    public void ResetToDefaultConfiguration()
    {
        CleanupAllElements();
        
        if (currentStructure == BeamStructureType.Arch && archController != null && archController.IsActive)
        {
            archController.UpdateArchDimensions();
            BeamStartX = archController.ArchStartX;
            BeamLength = archController.ArchLength;
        }
        else
        {
            UpdateBeamDimensions();
        }
        
        if (BeamLength <= 0.01f)
        {
            Debug.LogWarning($"BeamLength non valida: {BeamLength}, riprovo tra poco...");
            Invoke(nameof(ResetToDefaultConfiguration), 0.1f);
            return;
        }
        
        SpawnSupport(BeamStartX);
        SpawnSupport(BeamStartX + BeamLength);
        SpawnLoad(BeamStartX + (BeamLength / 2f));
    }

    private IEnumerator DelayedConfiguration()
    {
        yield return null;
        UpdateBeamDimensions();
        ResetToDefaultConfiguration();
    }

    // METODI PER GLI OFFSET

    private StructureHeightOffset GetCurrentStructureOffset()
    {
        foreach (var offset in structureHeightOffsets)
        {
            if (offset.structureType == currentStructure)
                return offset;
        }
        
        Debug.LogWarning($"Nessun offset definito per {currentStructure}, uso default");
        return new StructureHeightOffset 
        { 
            structureType = currentStructure, 
            loadHeightOffset = 0.6f,
            supportHeightOffset = -0.6f
        };
    }

    public float GetCurrentLoadHeightOffset()
    {
        return GetCurrentStructureOffset().loadHeightOffset;
    }

    public float GetCurrentSupportHeightOffset()
    {
        return GetCurrentStructureOffset().supportHeightOffset;
    }

    public void SetLoadHeightOffset(BeamStructureType structureType, float offset)
    {
        var existing = structureHeightOffsets.Find(x => x.structureType == structureType);
        if (existing != null)
        {
            existing.loadHeightOffset = offset;
        }
        else
        {
            structureHeightOffsets.Add(new StructureHeightOffset 
            { 
                structureType = structureType, 
                loadHeightOffset = offset,
                supportHeightOffset = -0.6f
            });
        }
    }

    public void SetSupportHeightOffset(BeamStructureType structureType, float offset)
    {
        var existing = structureHeightOffsets.Find(x => x.structureType == structureType);
        if (existing != null)
        {
            existing.supportHeightOffset = offset;
        }
        else
        {
            structureHeightOffsets.Add(new StructureHeightOffset 
            { 
                structureType = structureType, 
                loadHeightOffset = 0.6f,
                supportHeightOffset = offset
            });
        }
    }
}

[System.Serializable]
public class StructureHeightOffset
{
    [Tooltip("Tipo di struttura a cui applicare questi offset")]
    public BeamStructureType structureType;
    
    [Tooltip("Altezza dei CARICHI sopra beamObject.transform.position.y")]
    public float loadHeightOffset = 0.6f;
    
    [Tooltip("Altezza dei SUPPORTI sopra beamObject.transform.position.y (negativo = sotto)")]
    public float supportHeightOffset = -0.6f;
}