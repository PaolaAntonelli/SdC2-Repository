// ============================================================
// File: Arch System/ArchVisualizer.cs
// Descrizione: Gestisce la visualizzazione 3D dell'arco,
//              deformata e diagrammi delle sollecitazioni
// ============================================================

using UnityEngine;
using System.Collections.Generic;
using ArchSystem;

namespace ArchSystem
{
    [RequireComponent(typeof(ArchController))]
    public class ArchVisualizer : MonoBehaviour
    {
        [Header("Riferimenti Mesh")]
        public GameObject archMeshObject;           // GameObject con MeshFilter dell'arco
        public Material archMaterial;               // Materiale per l'arco originale
        public Material deformedMaterial;           // Materiale per la deformata (opzionale)
        
        [Header("Line Renderer")]
        public LineRenderer archLineRenderer;       // Linea dell'arco originale
        public LineRenderer deformedLineRenderer;   // Linea della deformata
        public LineRenderer momentLineRenderer;     // Diagramma momento
        public LineRenderer shearLineRenderer;      // Diagramma taglio
        public LineRenderer axialLineRenderer;      // Diagramma sforzo assiale
        
        [Header("Opzioni Visualizzazione")]
        public bool showOriginalArch = true;
        public bool showDeformedArch = true;
        public bool showMomentDiagram = false;
        public bool showShearDiagram = false;
        public bool showAxialDiagram = false;
        
        [Header("Stili Diagrammi")]
        public Color momentColor = Color.magenta;
        public Color shearColor = Color.cyan;
        public Color axialColor = Color.yellow;
        public Color originalColor = Color.white;
        public Color deformedColor = Color.red;
        public float diagramOffsetY = -2f;
        public float diagramScaleMoment = 0.01f;
        public float diagramScaleShear = 0.05f;
        public float diagramScaleAxial = 0.001f;
        
        [Header("Deformazione Mesh")]
        public bool deformMesh = false;
        [Range(0.1f, 1000f)]
        public float meshDeformationScale = 100f;
        
        // Riferimenti interni
        private ArchController archController;
        private MeshFilter meshFilter;
        private Mesh originalMesh;
        private Mesh deformedMesh;
        private Vector3[] originalVertices;
        
        void Start()
        {
            archController = GetComponent<ArchController>();
            
            // Setup iniziale
            SetupLineRenderers();
            SetupMeshDeformation();
        }
        
        void Update()
        {
            // Aggiorna visualizzazione in base ai dati dell'ArchController
            UpdateArchVisualization();
        }
        
        void SetupLineRenderers()
        {
            // Configura i LineRenderer se non sono stati assegnati
            if (archLineRenderer != null)
            {
                archLineRenderer.startWidth = 0.05f;
                archLineRenderer.endWidth = 0.05f;
                archLineRenderer.material = new Material(Shader.Find("Sprites/Default"));
                archLineRenderer.startColor = originalColor;
                archLineRenderer.endColor = originalColor;
            }
            
            if (deformedLineRenderer != null)
            {
                deformedLineRenderer.startWidth = 0.08f;
                deformedLineRenderer.endWidth = 0.08f;
                deformedLineRenderer.material = new Material(Shader.Find("Sprites/Default"));
                deformedLineRenderer.startColor = deformedColor;
                deformedLineRenderer.endColor = deformedColor;
            }
            
            // Setup diagrammi
            SetupDiagramRenderer(momentLineRenderer, momentColor, 0.03f);
            SetupDiagramRenderer(shearLineRenderer, shearColor, 0.03f);
            SetupDiagramRenderer(axialLineRenderer, axialColor, 0.03f);
        }
        
        void SetupDiagramRenderer(LineRenderer lr, Color color, float width)
        {
            if (lr == null) return;
            lr.startWidth = width;
            lr.endWidth = width;
            lr.material = new Material(Shader.Find("Sprites/Default"));
            lr.startColor = color;
            lr.endColor = color;
        }
        
        void SetupMeshDeformation()
        {
            if (archMeshObject != null)
            {
                meshFilter = archMeshObject.GetComponent<MeshFilter>();
                if (meshFilter != null && meshFilter.sharedMesh != null)
                {
                    originalMesh = meshFilter.sharedMesh;
                    deformedMesh = new Mesh();
                    deformedMesh.name = "DeformedArchMesh";
                    
                    // Copia la mesh originale
                    deformedMesh.vertices = originalMesh.vertices;
                    deformedMesh.triangles = originalMesh.triangles;
                    deformedMesh.uv = originalMesh.uv;
                    deformedMesh.normals = originalMesh.normals;
                    deformedMesh.MarkDynamic();
                    
                    originalVertices = originalMesh.vertices;
                    
                    // Assegna la mesh deformabile
                    meshFilter.mesh = deformedMesh;
                }
            }
        }
        
        void UpdateArchVisualization()
        {
            if (archController == null) return;
            
            // Ottieni i dati tramite reflection o rendili pubblici in ArchController
            var nodePositions = GetNodePositions();
            var results = GetResults();
            
            if (nodePositions == null || nodePositions.Count < 2) return;
            
            // Aggiorna linea arco originale
            UpdateOriginalArchLine(nodePositions);
            
            // Aggiorna deformata
            if (results.deformedPositions != null && results.deformedPositions.Length > 0)
            {
                UpdateDeformedLine(results.deformedPositions);
                UpdateDiagrams(nodePositions, results);
                
                if (deformMesh)
                    UpdateMeshDeformation(results);
            }
        }
        
        void UpdateOriginalArchLine(List<Vector2> nodePositions)
        {
            if (archLineRenderer == null || !showOriginalArch) return;
            
            archLineRenderer.positionCount = nodePositions.Count;
            archLineRenderer.enabled = true;
            
            Vector3 archPosition = transform.position;
            for (int i = 0; i < nodePositions.Count; i++)
            {
                archLineRenderer.SetPosition(i, new Vector3(
                    nodePositions[i].x + archPosition.x,
                    nodePositions[i].y + archPosition.y,
                    archPosition.z
                ));
            }
        }
        
        void UpdateDeformedLine(Vector2[] deformedPositions)
        {
            if (deformedLineRenderer == null || !showDeformedArch) return;
            
            deformedLineRenderer.positionCount = deformedPositions.Length;
            deformedLineRenderer.enabled = true;
            
            Vector3 archPosition = transform.position;
            for (int i = 0; i < deformedPositions.Length; i++)
            {
                deformedLineRenderer.SetPosition(i, new Vector3(
                    deformedPositions[i].x + archPosition.x,
                    deformedPositions[i].y + archPosition.y,
                    archPosition.z
                ));
            }
        }
        
        void UpdateDiagrams(List<Vector2> nodePositions, ArchResults results)
        {
            Vector3 basePosition = transform.position + Vector3.up * diagramOffsetY;
            
            // Diagramma momento
            if (showMomentDiagram && momentLineRenderer != null && results.momentPoints != null)
            {
                UpdateDiagramLine(momentLineRenderer, nodePositions, results.momentPoints, 
                                 basePosition, diagramScaleMoment);
            }
            else if (momentLineRenderer != null)
            {
                momentLineRenderer.enabled = false;
            }
            
            // Diagramma taglio
            if (showShearDiagram && shearLineRenderer != null && results.shearPoints != null)
            {
                UpdateDiagramLine(shearLineRenderer, nodePositions, results.shearPoints, 
                                 basePosition, diagramScaleShear);
            }
            else if (shearLineRenderer != null)
            {
                shearLineRenderer.enabled = false;
            }
            
            // Diagramma sforzo assiale
            if (showAxialDiagram && axialLineRenderer != null && results.axialPoints != null)
            {
                UpdateDiagramLine(axialLineRenderer, nodePositions, results.axialPoints, 
                                 basePosition, diagramScaleAxial);
            }
            else if (axialLineRenderer != null)
            {
                axialLineRenderer.enabled = false;
            }
        }
        
        void UpdateDiagramLine(LineRenderer lr, List<Vector2> nodePositions, 
                              float[] values, Vector3 basePos, float scale)
        {
            if (values == null || values.Length == 0) return;
            
            lr.positionCount = values.Length;
            lr.enabled = true;
            
            for (int i = 0; i < values.Length; i++)
            {
                float t = (float)i / (values.Length - 1);
                Vector2 archPoint = InterpolateArchPoint(nodePositions, t);
                
                lr.SetPosition(i, new Vector3(
                    archPoint.x + basePos.x,
                    values[i] * scale + basePos.y,
                    basePos.z
                ));
            }
        }
        
        void UpdateMeshDeformation(ArchResults results)
        {
            if (meshFilter == null || deformedMesh == null || originalVertices == null)
                return;
            
            if (results.deflectionPoints == null || results.deflectionPoints.Length == 0)
                return;
            
            Vector3[] displacedVertices = new Vector3[originalVertices.Length];
            
            // Ottieni i bounds della mesh per mappare i vertici
            Bounds bounds = originalMesh.bounds;
            float meshMinX = bounds.min.x;
            float meshSizeX = bounds.size.x;
            
            // Ottieni i nodi dell'arco per il mapping
            var nodePositions = GetNodePositions();
            if (nodePositions == null || nodePositions.Count < 2) return;
            
            float archMinX = nodePositions[0].x;
            float archMaxX = nodePositions[nodePositions.Count - 1].x;
            float archSizeX = archMaxX - archMinX;
            
            for (int i = 0; i < originalVertices.Length; i++)
            {
                Vector3 vertex = originalVertices[i];
                
                // Mappa la posizione X del vertice alla posizione sull'arco
                float normalizedX = (vertex.x - archMinX) / archSizeX;
                normalizedX = Mathf.Clamp01(normalizedX);
                
                // Trova l'indice corrispondente nei risultati
                int resultIndex = Mathf.RoundToInt(normalizedX * (results.deflectionPoints.Length - 1));
                resultIndex = Mathf.Clamp(resultIndex, 0, results.deflectionPoints.Length - 1);
                
                // Applica la deformazione (principalmente in Y per archi)
                float deflection = results.deflectionPoints[resultIndex] * meshDeformationScale;
                float axialDisp = results.axialDisplacement[resultIndex] * meshDeformationScale;
                
                displacedVertices[i] = new Vector3(
                    vertex.x + axialDisp,
                    vertex.y + deflection,
                    vertex.z
                );
            }
            
            deformedMesh.vertices = displacedVertices;
            deformedMesh.RecalculateNormals();
            deformedMesh.RecalculateBounds();
        }
        
        Vector2 InterpolateArchPoint(List<Vector2> points, float t)
        {
            if (points.Count < 2) return Vector2.zero;
            
            float pos = t * (points.Count - 1);
            int index = Mathf.FloorToInt(pos);
            float frac = pos - index;
            
            if (index >= points.Count - 1)
                return points[points.Count - 1];
            
            return Vector2.Lerp(points[index], points[index + 1], frac);
        }
        
        // Metodi helper per accedere ai dati dell'ArchController
        List<Vector2> GetNodePositions()
        {
            var field = typeof(ArchController).GetField("nodePositions", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            return field?.GetValue(archController) as List<Vector2>;
        }
        
        ArchResults GetResults()
        {
            var field = typeof(ArchController).GetField("currentResults", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            return (ArchResults)(field?.GetValue(archController) ?? default(ArchResults));
        }
        
        // Metodi pubblici per controllo esterno
        public void ToggleOriginalArch() => showOriginalArch = !showOriginalArch;
        public void ToggleDeformedArch() => showDeformedArch = !showDeformedArch;
        public void ToggleMomentDiagram() => showMomentDiagram = !showMomentDiagram;
        public void ToggleShearDiagram() => showShearDiagram = !showShearDiagram;
        public void ToggleAxialDiagram() => showAxialDiagram = !showAxialDiagram;
        
        public void SetDiagramScale(string type, float scale)
        {
            switch (type.ToLower())
            {
                case "moment": diagramScaleMoment = scale; break;
                case "shear": diagramScaleShear = scale; break;
                case "axial": diagramScaleAxial = scale; break;
            }
        }
        
        void OnDrawGizmos()
        {
            // Visualizza i punti di ancoraggio dei diagrammi
            if (archController == null) return;
            
            Gizmos.color = Color.green;
            Vector3 basePos = transform.position + Vector3.up * diagramOffsetY;
            Gizmos.DrawWireCube(basePos, Vector3.one * 0.2f);
        }
    }
}