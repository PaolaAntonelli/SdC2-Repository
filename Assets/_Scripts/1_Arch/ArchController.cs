using UnityEngine;
using System.Collections.Generic;

public class ArchController : MonoBehaviour
{
    [Header("Arch Geometry")]
    public GameObject archPrefab;
    public float archHeight = 2f;
    public int archSegments = 50;
    
    [Header("Stress Visualization")]
    public Material stressMaterial;
    public Gradient stressGradient;
    public float stressScaleMultiplier = 1f;
    
    private GameObject archInstance;
    private MeshRenderer archRenderer;
    private MeshFilter archFilter;
    private Mesh originalArchMesh;
    private Mesh workingMesh;
    private Vector3[] originalVertices;
    
    private BeamController beamController;
    
    public float ArchStartX { get; private set; }
    public float ArchEndX { get; private set; }
    public float ArchLength { get; private set; }
    public float ArchActualHeight { get; private set; }  // Altezza reale dalla mesh
    public bool IsActive { get; private set; }
    
    void Start()
    {
        beamController = GetComponent<BeamController>();
        if (beamController == null)
            beamController = FindFirstObjectByType<BeamController>();
    }
    
    public float GetArchBaseY()
    {
        if (archInstance == null) return 0;
        return archInstance.transform.position.y;
    }
    
    public void UpdateArchDimensions()
    {
        if (archInstance == null) return;
        
        Renderer renderer = archInstance.GetComponent<Renderer>();
        if (renderer != null)
        {
            Bounds bounds = renderer.bounds;
            ArchStartX = bounds.min.x;
            ArchEndX = bounds.max.x;
            ArchLength = bounds.size.x;
            ArchActualHeight = bounds.size.y;  // Altezza reale della mesh
        }
        else if (archFilter != null && workingMesh != null)
        {
            Bounds bounds = workingMesh.bounds;
            Vector3 worldPos = archInstance.transform.TransformPoint(bounds.min);
            ArchStartX = worldPos.x;
            ArchLength = bounds.size.x * archInstance.transform.localScale.x;
            ArchEndX = ArchStartX + ArchLength;
            ArchActualHeight = bounds.size.y * archInstance.transform.localScale.y;
        }
        
        // Se abbiamo un'altezza reale diversa da quella impostata, aggiorniamo archHeight
        if (ArchActualHeight > 0 && Mathf.Abs(archHeight - ArchActualHeight) > 0.01f)
        {
            Debug.Log($"Updating archHeight from {archHeight} to actual {ArchActualHeight}");
            archHeight = ArchActualHeight;
            if (beamController != null)
                beamController.archHeight = ArchActualHeight;
        }
        
        Debug.Log($"Arch: StartX={ArchStartX}, EndX={ArchEndX}, Length={ArchLength}, Height={ArchActualHeight}");
    }
    
    /// <summary>
    /// Calcola l'altezza usando la formula matematica (curva liscia)
    /// </summary>
    public float GetArchHeightAtX(float worldX)
    {
        if (!IsActive || archInstance == null) return 0;
        
        float t = Mathf.InverseLerp(ArchStartX, ArchEndX, worldX);
        t = Mathf.Clamp01(t);
        
        // Parabola: y = 4 * h * t * (1 - t)
        float baseY = archInstance.transform.position.y;
        return baseY + archHeight * 4 * t * (1 - t);
    }
    
    /// <summary>
    /// Ottiene la tangente dell'arco a una data X (per disegnare diagrammi)
    /// </summary>
    public Vector3 GetArchTangentAtX(float worldX)
    {
        if (!IsActive) return Vector3.right;
        
        float t = Mathf.InverseLerp(ArchStartX, ArchEndX, worldX);
        t = Mathf.Clamp01(t);
        
        // Derivata: dy/dx = (4 * h * (1 - 2*t)) / Length
        float dydx = (archHeight * 4 * (1 - 2 * t)) / ArchLength;
        return new Vector3(1, dydx, 0).normalized;
    }
    
    /// <summary>
    /// Ottiene la normale all'arco (perpendicolare alla tangente)
    /// </summary>
    public Vector3 GetArchNormalAtX(float worldX)
    {
        Vector3 tangent = GetArchTangentAtX(worldX);
        return new Vector3(-tangent.y, tangent.x, 0).normalized;
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
        
        if (archFilter != null && archFilter.sharedMesh != null)
        {
            originalArchMesh = archFilter.sharedMesh;
            workingMesh = Instantiate(originalArchMesh);
            workingMesh.name = "Arch_WorkingMesh";
            archFilter.mesh = workingMesh;
            originalVertices = originalArchMesh.vertices;
        }
        
        // Aspetta un frame per avere i bounds corretti
        Invoke(nameof(UpdateArchDimensions), 0.02f);
        
        IsActive = true;
        Debug.Log("ArchController: Modalità arco attivata");
    }
    
    public void DeactivateArchMode()
    {
        if (archInstance != null)
            Destroy(archInstance);
        
        if (beamController != null && beamController.beamObject != null)
            beamController.beamObject.SetActive(true);
        
        IsActive = false;
        Debug.Log("ArchController: Modalità trave attivata");
    }
    
    public void ApplyDeflection(float[] deflections, float visualScale)
    {
        if (!IsActive || workingMesh == null || originalVertices == null) return;
        if (deflections == null || deflections.Length == 0) return;
        
        UpdateArchDimensions();
        
        Vector3[] displaced = new Vector3[originalVertices.Length];
        Vector3 localUp = archInstance.transform.InverseTransformDirection(Vector3.up);
        
        for (int i = 0; i < originalVertices.Length; i++)
        {
            Vector3 v = originalVertices[i];
            Vector3 worldPos = archInstance.transform.TransformPoint(v);
            
            // Trova la posizione relativa lungo l'arco
            float t = Mathf.InverseLerp(ArchStartX, ArchEndX, worldPos.x);
            int idx = Mathf.Clamp(Mathf.RoundToInt(t * (deflections.Length - 1)), 0, deflections.Length - 1);
            
            float deflection = deflections[idx] * visualScale;
            
            // Applica deflessione lungo la normale all'arco
            Vector3 normal = GetArchNormalAtX(worldPos.x);
            displaced[i] = v + archInstance.transform.InverseTransformDirection(normal) * deflection;
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
        
        UpdateArchDimensions();
        
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