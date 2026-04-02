using UnityEngine;
using System.Collections.Generic;
using UnityEngine.UI;

public class BeamController : MonoBehaviour
{
    [Header("Impostazioni Solutore")]
    public SolverType currentSolver = SolverType.Analytic;

    public GameObject beamObject;
    public GameObject supportPrefab;
    public GameObject loadPrefab;
    public LineRenderer shearLine;
    public LineRenderer momentLine;

    [Header("Parametri Grafici")]
    public float forceMagnitude = 15f;
    public float currentDiagramScale = 0.05f;
    // Se la deformazione va verso l'alto, cambia questo valore in negativo (es. -100)
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

    // Questa funzione può essere collegata a un UI Toggle o Button
    public void SetSolverType(bool isAnalytic)
    {
        currentSolver = isAnalytic ? SolverType.Analytic : SolverType.FEM;
    }
    void Start()
    {
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

            // SWITCH TRA I DUE METODI
            if (currentSolver == SolverType.Analytic)
            {
                results = BeamMath.CalculateAnalytic(BeamLength, lPos, lMag, sPos, 100);
            }
            else
            {
                results = BeamMath.CalculateFEM(BeamLength, lPos, lMag, sPos, 100);
            }

            RenderDiagram(shearLine, results.shearPoints, currentDiagramScale, Color.cyan);
            RenderDiagram(momentLine, results.momentPoints, currentDiagramScale, Color.magenta);

            if (showDeflection) ApplyDeflectionToMesh(results.deflectionPoints);
            else ResetMesh();
        }
    }

    void ApplyDeflectionToMesh(float[] deflections)
    {
        Vector3[] displacedVertices = new Vector3[originalVertices.Length];

        // localDown identifica la direzione "gių" nel sistema di coordinate della mesh
        Vector3 localDown = beamObject.transform.InverseTransformDirection(Vector3.down);

        for (int i = 0; i < originalVertices.Length; i++)
        {
            Vector3 v = originalVertices[i];
            float relL = (v[lengthAxis] - meshMinL) / meshSizeL;
            int idx = Mathf.Clamp(Mathf.RoundToInt(relL * (deflections.Length - 1)), 0, deflections.Length - 1);

            // CORREZIONE VERSO: Moltiplichiamo per deflectionVisualScale. 
            // Se deflections[idx] č negativo (abbassamento), dAmount deve spingere verso localDown.
            float dAmount = deflections[idx] * deflectionVisualScale;

            // Sommiamo lo spostamento alla posizione originale del vertice
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

    // --- LOGICA SPAWN E UI ---

    public void ToggleDeflection() => showDeflection = !showDeflection;
    public void SetDiagramScale(float s) => currentDiagramScale = s;

    public void ResetStructure()
    {
        foreach (GameObject obj in GetAllElements()) Destroy(obj);
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

    private GameObject SpawnAtPosition(GameObject prefab, float worldX, float yOff)
    {
        Vector3 pos = new Vector3(worldX, beamObject.transform.position.y + yOff, beamObject.transform.position.z);
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
        foreach (var o in GameObject.FindGameObjectsWithTag(tag))
            p.Add(Mathf.Clamp(o.transform.position.x - BeamStartX, 0, BeamLength));
        return p;
    }

    void RenderDiagram(LineRenderer line, float[] values, float scale, Color color)
    {
        if (line == null) return;
        line.positionCount = values.Length;
        for (int i = 0; i < values.Length; i++)
        {
            float x = BeamStartX + (i * (BeamLength / (values.Length - 1)));
            line.SetPosition(i, new Vector3(x, beamObject.transform.position.y, beamObject.transform.position.z) + diagramOffset + new Vector3(0, values[i] * scale, 0));
        }
    }
}