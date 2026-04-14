using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class ArchController : MonoBehaviour
{
    [Header("Arch Geometry")]
    public GameObject archPrefab;
    public float archHeight = 2f;           // fallback
    public int archSegments = 50;
    public bool smoothProfile = true;
    public int smoothingWindow = 5;

    [Header("Stress Visualization")]
    public Material stressMaterial;
    public Gradient stressGradient;
    public float stressScaleMultiplier = 1f;

    private GameObject archInstance;
    private MeshRenderer archRenderer;
    private MeshFilter archFilter;
    private Mesh workingMesh;
    private Vector3[] originalVertices;

    private BeamController beamController;

    private List<Vector2> archProfilePoints = new List<Vector2>();

    public float ArchStartX { get; private set; }
    public float ArchEndX { get; private set; }
    public float ArchLength { get; private set; }
    public bool IsActive { get; private set; }

    void Start()
    {
        beamController = GetComponent<BeamController>();
        if (beamController == null)
            beamController = FindFirstObjectByType<BeamController>();
    }

    public void ActivateArchMode()
    {
        if (archPrefab == null)
        {
            Debug.LogError("ArchController: archPrefab non assegnato!");
            return;
        }

        if (beamController != null && beamController.beamObject != null)
            beamController.beamObject.SetActive(false);

        archInstance = Instantiate(archPrefab, transform);
        archInstance.name = "Arch_Instance";

        if (beamController != null && beamController.beamObject != null)
        {
            archInstance.transform.position = beamController.beamObject.transform.position;
            archInstance.transform.rotation = beamController.beamObject.transform.rotation;
        }

        archFilter = archInstance.GetComponent<MeshFilter>();
        archRenderer = archInstance.GetComponent<MeshRenderer>();

        if (archFilter != null)
        {
            Mesh originalMesh = archFilter.sharedMesh;
            workingMesh = Instantiate(originalMesh);
            workingMesh.name = "Arch_WorkingMesh";
            archFilter.mesh = workingMesh;
            originalVertices = originalMesh.vertices;
        }

        ExtractArchProfile();
        CalculateArchDimensions();
        IsActive = true;

        if (beamController != null)
        {
            beamController.Invoke("UpdateBeamDimensions", 0.1f);
        }

        Debug.Log($"ArchController: Profilo estratto con {archProfilePoints.Count} punti.");
    }

    public void DeactivateArchMode()
    {
        if (archInstance != null)
            Destroy(archInstance);

        if (beamController != null && beamController.beamObject != null)
            beamController.beamObject.SetActive(true);

        IsActive = false;
        archProfilePoints.Clear();
    }

    private void ExtractArchProfile()
    {
        archProfilePoints.Clear();
        if (archFilter == null || workingMesh == null) return;

        Vector3[] worldVerts = new Vector3[workingMesh.vertexCount];
        Vector3[] worldNormals = new Vector3[workingMesh.vertexCount];
        for (int i = 0; i < workingMesh.vertexCount; i++)
        {
            worldVerts[i] = archInstance.transform.TransformPoint(workingMesh.vertices[i]);
            worldNormals[i] = archInstance.transform.TransformDirection(workingMesh.normals[i]);
        }

        // Filtraggio: prendiamo solo i vertici con normale puntata verso l'alto (estradosso)
        List<Vector3> topVerts = new List<Vector3>();
        for (int i = 0; i < worldVerts.Length; i++)
        {
            if (worldNormals[i].y > 0.5f)  // soglia per normale verso l'alto
                topVerts.Add(worldVerts[i]);
        }

        // Se non ci sono abbastanza vertici filtrati, usa tutti i vertici
        if (topVerts.Count < 10)
        {
            topVerts = new List<Vector3>(worldVerts);
        }

        // Ordina per X
        topVerts.Sort((a, b) => a.x.CompareTo(b.x));

        float minX = topVerts.Min(v => v.x);
        float maxX = topVerts.Max(v => v.x);
        float span = maxX - minX;
        float halfWidth = span / (archSegments * 2f);

        for (int i = 0; i <= archSegments; i++)
        {
            float t = (float)i / archSegments;
            float sampleX = Mathf.Lerp(minX, maxX, t);

            var inRange = topVerts.Where(v => Mathf.Abs(v.x - sampleX) <= halfWidth);
            float maxY;
            if (inRange.Any())
            {
                maxY = inRange.Max(v => v.y);
            }
            else
            {
                // Interpola tra i due punti più vicini
                var nearest = topVerts.OrderBy(v => Mathf.Abs(v.x - sampleX)).Take(2).ToArray();
                if (nearest.Length == 2)
                {
                    float t2 = (sampleX - nearest[0].x) / (nearest[1].x - nearest[0].x);
                    maxY = Mathf.Lerp(nearest[0].y, nearest[1].y, t2);
                }
                else
                {
                    maxY = nearest.Length > 0 ? nearest[0].y : archInstance.transform.position.y;
                }
            }
            archProfilePoints.Add(new Vector2(sampleX, maxY));
        }

        if (smoothProfile)
        {
            SmoothProfile(smoothingWindow);
        }
    }

    private void SmoothProfile(int windowSize)
    {
        if (archProfilePoints.Count < windowSize) return;
        List<Vector2> smoothed = new List<Vector2>();
        for (int i = 0; i < archProfilePoints.Count; i++)
        {
            float sumY = 0;
            int count = 0;
            int half = windowSize / 2;
            for (int j = Mathf.Max(0, i - half); j <= Mathf.Min(archProfilePoints.Count - 1, i + half); j++)
            {
                sumY += archProfilePoints[j].y;
                count++;
            }
            smoothed.Add(new Vector2(archProfilePoints[i].x, sumY / count));
        }
        archProfilePoints = smoothed;
    }

    private void CalculateArchDimensions()
    {
        if (archProfilePoints.Count > 0)
        {
            ArchStartX = archProfilePoints[0].x;
            ArchEndX = archProfilePoints[archProfilePoints.Count - 1].x;
        }
        else
        {
            Bounds b = workingMesh != null ? workingMesh.bounds : new Bounds();
            Vector3 worldMin = archInstance.transform.TransformPoint(b.min);
            Vector3 worldMax = archInstance.transform.TransformPoint(b.max);
            ArchStartX = worldMin.x;
            ArchEndX = worldMax.x;
        }
        ArchLength = ArchEndX - ArchStartX;
    }

    public float GetArchHeightAtX(float worldX)
    {
        if (!IsActive || archProfilePoints.Count == 0)
        {
            float t = Mathf.InverseLerp(ArchStartX, ArchEndX, worldX);
            return archInstance != null ? archInstance.transform.position.y + archHeight * 4 * t * (1 - t) : 0;
        }

        worldX = Mathf.Clamp(worldX, ArchStartX, ArchEndX);

        // Ricerca binaria
        int index = archProfilePoints.BinarySearch(new Vector2(worldX, 0), Comparer<Vector2>.Create((a, b) => a.x.CompareTo(b.x)));
        if (index < 0)
        {
            index = ~index;
            if (index >= archProfilePoints.Count) return archProfilePoints[archProfilePoints.Count - 1].y;
            if (index <= 0) return archProfilePoints[0].y;

            Vector2 left = archProfilePoints[index - 1];
            Vector2 right = archProfilePoints[index];
            float t = (worldX - left.x) / (right.x - left.x);
            return Mathf.Lerp(left.y, right.y, t);
        }
        else
        {
            return archProfilePoints[index].y;
        }
    }

    public Vector3 GetArchTangentAtX(float worldX)
    {
        if (!IsActive) return Vector3.right;

        float delta = ArchLength / (archSegments * 10f);
        float y1 = GetArchHeightAtX(worldX - delta);
        float y2 = GetArchHeightAtX(worldX + delta);
        float dydx = (y2 - y1) / (2f * delta);
        return new Vector3(1, dydx, 0).normalized;
    }

    public Vector3 GetArchNormalAtX(float worldX)
    {
        Vector3 tangent = GetArchTangentAtX(worldX);
        return new Vector3(-tangent.y, tangent.x, 0).normalized;
    }

    public void ApplyDeflection(float[] deflections, float visualScale)
    {
        if (!IsActive || workingMesh == null) return;

        Vector3[] displaced = new Vector3[originalVertices.Length];

        for (int i = 0; i < originalVertices.Length; i++)
        {
            Vector3 localV = originalVertices[i];
            Vector3 worldV = archInstance.transform.TransformPoint(localV);
            float worldX = worldV.x;

            float t = Mathf.InverseLerp(ArchStartX, ArchEndX, worldX);
            int idx = Mathf.Clamp(Mathf.RoundToInt(t * (deflections.Length - 1)), 0, deflections.Length - 1);
            float deflection = deflections[idx] * visualScale;

            Vector3 normal = GetArchNormalAtX(worldX);
            Vector3 worldDisplaced = worldV + normal * deflection;
            displaced[i] = archInstance.transform.InverseTransformPoint(worldDisplaced);
        }

        workingMesh.vertices = displaced;
        workingMesh.RecalculateNormals();
        workingMesh.RecalculateBounds();
    }

    public void ResetMesh()
    {
        if (workingMesh != null && originalVertices != null)
        {
            workingMesh.vertices = originalVertices;
            workingMesh.RecalculateNormals();
            workingMesh.RecalculateBounds();
        }
    }

    public void VisualizeStress(float[] stressValues, float maxStress)
    {
        if (!IsActive || workingMesh == null || stressMaterial == null) return;

        Color[] vertexColors = new Color[workingMesh.vertexCount];

        for (int i = 0; i < vertexColors.Length; i++)
        {
            Vector3 worldPos = archInstance.transform.TransformPoint(workingMesh.vertices[i]);
            float t = Mathf.InverseLerp(ArchStartX, ArchEndX, worldPos.x);
            int idx = Mathf.Clamp(Mathf.RoundToInt(t * (stressValues.Length - 1)), 0, stressValues.Length - 1);
            float normalizedStress = Mathf.Clamp01(Mathf.Abs(stressValues[idx]) / maxStress);
            vertexColors[i] = stressGradient.Evaluate(normalizedStress);
        }

        workingMesh.colors = vertexColors;

        if (archRenderer != null && archRenderer.sharedMaterial != stressMaterial)
            archRenderer.material = stressMaterial;
    }
}