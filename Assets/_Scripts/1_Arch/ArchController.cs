using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Gestisce la geometria e i calcoli specifici per strutture ad arco.
/// Coesiste con BeamController senza modificarlo.
/// </summary>
public class ArchController : MonoBehaviour
{
    [Header("Arch Geometry")]
    public GameObject archPrefab;           // Il tuo prefab "Arco geometry"
    public float archHeight = 2f;
    public int archSegments = 50;
    
    [Header("Stress Visualization")]
    public Material stressMaterial;          // Materiale per la heatmap
    public Gradient stressGradient;          // Blu (compressione) -> Rosso (trazione)
    public float stressScaleMultiplier = 1f;
    
    private GameObject archInstance;
    private MeshRenderer archRenderer;
    private MeshFilter archFilter;
    private Mesh originalArchMesh;
    private Mesh workingMesh;
    private Vector3[] originalVertices;
    private Vector3[] originalNormals;
    
    // Riferimenti esterni
    private BeamController beamController;
    
    // Proprietà pubbliche
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
    
    /// <summary>
    /// Attiva la modalità arco sostituendo la geometria
    /// </summary>
    public void ActivateArchMode()
    {
        if (archPrefab == null)
        {
            Debug.LogError("ArchController: archPrefab non assegnato!");
            return;
        }
        
        // Disattiva la geometria della trave
        if (beamController != null && beamController.beamObject != null)
            beamController.beamObject.SetActive(false);
        
        // Istanzia l'arco
        archInstance = Instantiate(archPrefab, transform);
        archInstance.name = "Arch_Instance";
        
        // Posiziona l'arco nella stessa posizione della trave
        if (beamController != null && beamController.beamObject != null)
        {
            archInstance.transform.position = beamController.beamObject.transform.position;
            archInstance.transform.rotation = beamController.beamObject.transform.rotation;
        }
        
        // Ottieni riferimenti alla mesh
        archFilter = archInstance.GetComponent<MeshFilter>();
        archRenderer = archInstance.GetComponent<MeshRenderer>();
        
        if (archFilter != null)
        {
            originalArchMesh = archFilter.sharedMesh;
            workingMesh = Instantiate(originalArchMesh);
            workingMesh.name = "Arch_WorkingMesh";
            archFilter.mesh = workingMesh;
            
            originalVertices = originalArchMesh.vertices;
            originalNormals = originalArchMesh.normals;
        }
        
        // Calcola le dimensioni dell'arco
        CalculateArchDimensions();
        
        IsActive = true;
        Debug.Log("ArchController: Modalità arco attivata");
    }
    
    /// <summary>
    /// Torna alla modalità trave
    /// </summary>
    public void DeactivateArchMode()
    {
        if (archInstance != null)
            Destroy(archInstance);
        
        if (beamController != null && beamController.beamObject != null)
            beamController.beamObject.SetActive(true);
        
        IsActive = false;
        Debug.Log("ArchController: Modalità trave attivata");
    }
    
    /// <summary>
    /// Calcola le dimensioni dell'arco dalla mesh
    /// </summary>
    private void CalculateArchDimensions()
    {
        if (archFilter == null) return;
        
        Bounds bounds = workingMesh.bounds;
        ArchLength = bounds.size.x;
        ArchStartX = bounds.min.x;
        ArchEndX = bounds.max.x;
    }
    
    /// <summary>
    /// Ottiene l'altezza Y dell'arco a una data coordinata X
    /// </summary>
    public float GetArchHeightAtX(float worldX)
    {
        if (!IsActive) return 0;
        
        float t = Mathf.InverseLerp(ArchStartX, ArchEndX, worldX);
        // Equazione parabolica standard per archi
        return archInstance.transform.position.y + archHeight * 4 * t * (1 - t);
    }
    
    /// <summary>
    /// Ottiene la tangente (direzione) dell'arco a una data X
    /// </summary>
    public Vector3 GetArchTangentAtX(float worldX)
    {
        if (!IsActive) return Vector3.right;
        
        float t = Mathf.InverseLerp(ArchStartX, ArchEndX, worldX);
        // Derivata della parabola: dy/dx = archHeight * 4 * (1 - 2t) / ArchLength
        float dydx = archHeight * 4 * (1 - 2 * t) / ArchLength;
        return new Vector3(1, dydx, 0).normalized;
    }
    
    /// <summary>
    /// Ottiene la normale dell'arco a una data X
    /// </summary>
    public Vector3 GetArchNormalAtX(float worldX)
    {
        Vector3 tangent = GetArchTangentAtX(worldX);
        return new Vector3(-tangent.y, tangent.x, 0).normalized;
    }
    
    /// <summary>
    /// Applica la deformazione all'arco (per visualizzare la deflessione)
    /// </summary>
    public void ApplyDeflection(float[] deflections, float visualScale)
    {
        if (!IsActive || workingMesh == null) return;
        
        Vector3[] displaced = new Vector3[originalVertices.Length];
        Vector3[] colors = new Vector3[originalVertices.Length];
        
        for (int i = 0; i < originalVertices.Length; i++)
        {
            Vector3 v = originalVertices[i];
            float worldX = archInstance.transform.TransformPoint(v).x;
            float t = Mathf.InverseLerp(ArchStartX, ArchEndX, worldX);
            
            int idx = Mathf.Clamp(Mathf.RoundToInt(t * (deflections.Length - 1)), 0, deflections.Length - 1);
            float deflection = deflections[idx] * visualScale;
            
            // La deflessione è applicata lungo la normale locale (verticale per deflessione)
            Vector3 normal = GetArchNormalAtX(worldX);
            displaced[i] = v + archInstance.transform.InverseTransformDirection(normal) * deflection;
        }
        
        workingMesh.vertices = displaced;
        workingMesh.RecalculateNormals();
        workingMesh.RecalculateBounds();
    }
    
    /// <summary>
    /// Resetta la mesh alla forma originale
    /// </summary>
    public void ResetMesh()
    {
        if (workingMesh != null && originalVertices != null)
        {
            workingMesh.vertices = originalVertices;
            workingMesh.RecalculateNormals();
            workingMesh.RecalculateBounds();
        }
    }
    
    /// <summary>
    /// Visualizza le sollecitazioni come heatmap sulla mesh
    /// </summary>
    public void VisualizeStress(float[] stressValues, float maxStress)
    {
        if (!IsActive || workingMesh == null || stressMaterial == null) return;
        
        // Crea array di colori per i vertici
        Color[] vertexColors = new Color[workingMesh.vertexCount];
        
        for (int i = 0; i < vertexColors.Length; i++)
        {
            Vector3 worldPos = archInstance.transform.TransformPoint(workingMesh.vertices[i]);
            float t = Mathf.InverseLerp(ArchStartX, ArchEndX, worldPos.x);
            
            int idx = Mathf.Clamp(Mathf.RoundToInt(t * (stressValues.Length - 1)), 0, stressValues.Length - 1);
            float normalizedStress = Mathf.Clamp01(Mathf.Abs(stressValues[idx]) / maxStress);
            
            // Usa il gradiente per il colore
            vertexColors[i] = stressGradient.Evaluate(normalizedStress);
        }
        
        workingMesh.colors = vertexColors;
        
        // Applica il materiale se non già presente
        if (archRenderer != null && archRenderer.sharedMaterial != stressMaterial)
            archRenderer.material = stressMaterial;
    }
}