// ============================================================
// File: Arch System/ArchController.cs
// Descrizione: Controller principale per l'arco
// ============================================================

using UnityEngine;
using System.Collections.Generic;
using ArchSystem;

namespace ArchSystem
{
    public class ArchController : MonoBehaviour
    {
        [Header("Geometria Arco")]
        public float radius = 5f;
        [Range(30f, 360f)]
        public float angleDegrees = 180f;
        [Range(4, 50)]
        public int segments = 20;
        public float height = 2f;
        
        [Header("Proprietà Strutturali")]
        public float EI = 1000f;
        public float EA = 50000f;
        
        [Header("Carichi e Vincoli")]
        public float loadMagnitude = 1000f;
        public List<int> loadNodeIndices = new List<int>();
        public List<int> supportNodeIndices = new List<int>();
        
        [Header("Solver")]
        public ArchSolverType solverType = ArchSolverType.FEM_6DOF;
        [Range(2, 50)]
        public int resolutionPerElement = 10;
        
        [Header("Visualizzazione")]
        public bool showDeformed = true;
        public float deformationScale = 1f;
        public Color archColor = Color.white;
        public Color deformedColor = Color.red;
        
        // Riferimenti interni
        private List<Vector2> nodePositions;
        private ArchResults currentResults;
        private ArchConfig currentConfig;
        
        void Start()
        {
            GenerateArchGeometry();
            SetupDefaultBoundaries();
            Solve();
        }
        
        void Update()
        {
            if (Input.GetKeyDown(KeyCode.Space))
                Solve();
        }
        
        [ContextMenu("Risolvi Arco")]
        public void Solve()
        {
            if (nodePositions == null || nodePositions.Count < 2)
            {
                GenerateArchGeometry();
                SetupDefaultBoundaries();
            }
            
            currentConfig = new ArchConfig
            {
                radius = radius,
                angleDegrees = angleDegrees,
                segments = segments,
                height = height,
                EI = EI,
                EA = EA
            };
            
            List<float> loads = new List<float>();
            for (int i = 0; i < loadNodeIndices.Count; i++)
                loads.Add(loadMagnitude);
            
            currentResults = ArchMath.SolveArch(
                nodePositions,
                loads,
                loadNodeIndices,
                supportNodeIndices,
                currentConfig,
                solverType,
                resolutionPerElement
            );
            
            Debug.Log($"[ArchController] Arco risolto! " +
                     $"Max deflessione: {MaxAbs(currentResults.deflectionPoints):F3}");
        }
        
        void OnDrawGizmos()
        {
            if (nodePositions == null) return;
            
            // Disegna arco originale
            Gizmos.color = archColor;
            for (int i = 0; i < nodePositions.Count - 1; i++)
            {
                Vector3 a = new Vector3(nodePositions[i].x, nodePositions[i].y, 0);
                Vector3 b = new Vector3(nodePositions[i + 1].x, nodePositions[i + 1].y, 0);
                Gizmos.DrawLine(a, b);
            }
            
            // Disegna vincoli
            Gizmos.color = Color.blue;
            foreach (int idx in supportNodeIndices)
            {
                if (idx < nodePositions.Count)
                {
                    Vector3 pos = new Vector3(nodePositions[idx].x, nodePositions[idx].y, 0);
                    Gizmos.DrawCube(pos, Vector3.one * 0.3f);
                }
            }
            
            // Disegna carichi
            Gizmos.color = Color.red;
            foreach (int idx in loadNodeIndices)
            {
                if (idx < nodePositions.Count)
                {
                    Vector3 pos = new Vector3(nodePositions[idx].x, nodePositions[idx].y, 0);
                    Gizmos.DrawLine(pos, pos + Vector3.down * 1f);
                    Gizmos.DrawWireSphere(pos + Vector3.down * 1f, 0.1f);
                }
            }
            
            // Disegna deformata
            if (showDeformed && currentResults.deformedPositions != null)
            {
                Gizmos.color = deformedColor;
                for (int i = 0; i < currentResults.deformedPositions.Length - 1; i++)
                {
                    Vector3 a = new Vector3(
                        currentResults.deformedPositions[i].x,
                        currentResults.deformedPositions[i].y, 0);
                    Vector3 b = new Vector3(
                        currentResults.deformedPositions[i + 1].x,
                        currentResults.deformedPositions[i + 1].y, 0);
                    Gizmos.DrawLine(a, b);
                }
            }
        }
        
        void GenerateArchGeometry()
        {
            nodePositions = new List<Vector2>();
            
            float startAngle = -angleDegrees / 2f * Mathf.Deg2Rad;
            float endAngle = angleDegrees / 2f * Mathf.Deg2Rad;
            
            for (int i = 0; i <= segments; i++)
            {
                float t = (float)i / segments;
                float angle = Mathf.Lerp(startAngle, endAngle, t);
                
                float x = radius * Mathf.Sin(angle);
                float y = height + radius * (1f - Mathf.Cos(angle));
                
                nodePositions.Add(new Vector2(x, y));
            }
        }
        
        void SetupDefaultBoundaries()
        {
            supportNodeIndices.Clear();
            supportNodeIndices.Add(0);
            supportNodeIndices.Add(segments);
            
            loadNodeIndices.Clear();
            loadNodeIndices.Add(segments / 2);
        }
        
        float MaxAbs(float[] array)
        {
            float max = 0;
            foreach (float v in array)
                if (Mathf.Abs(v) > max) max = Mathf.Abs(v);
            return max;
        }
    }
}