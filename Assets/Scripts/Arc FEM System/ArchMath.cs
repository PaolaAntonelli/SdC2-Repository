// ============================================================
// File: Arch System/ArchMath.cs
// Descrizione: Solutore FEM dedicato per archi
// ============================================================

using UnityEngine;
using System.Collections.Generic;
using ArchSystem;

namespace ArchSystem
{
    public static class ArchMath
    {
        // Classe interna privata per non interferire con BeamMath
        private static class MatrixSolver
        {
            public static double[] Solve(double[,] A, double[] b)
            {
                int n = b.Length;
                
                // Forward elimination con pivoting
                for (int i = 0; i < n; i++)
                {
                    int pivot = i;
                    for (int j = i + 1; j < n; j++)
                        if (System.Math.Abs(A[j, i]) > System.Math.Abs(A[pivot, i]))
                            pivot = j;
                    
                    for (int k = i; k < n; k++)
                    {
                        double temp = A[i, k];
                        A[i, k] = A[pivot, k];
                        A[pivot, k] = temp;
                    }
                    double tempB = b[i];
                    b[i] = b[pivot];
                    b[pivot] = tempB;
                    
                    if (System.Math.Abs(A[i, i]) < 1e-15) continue;
                    
                    for (int j = i + 1; j < n; j++)
                    {
                        double factor = A[j, i] / A[i, i];
                        b[j] -= factor * b[i];
                        for (int k = i; k < n; k++)
                            A[j, k] -= factor * A[i, k];
                    }
                }
                
                // Back substitution
                double[] x = new double[n];
                for (int i = n - 1; i >= 0; i--)
                {
                    double sum = 0;
                    for (int j = i + 1; j < n; j++)
                        sum += A[i, j] * x[j];
                    if (System.Math.Abs(A[i, i]) > 1e-15)
                        x[i] = (b[i] - sum) / A[i, i];
                }
                return x;
            }
        }

        // ============================================================
        // METODO PRINCIPALE
        // ============================================================
        
        public static ArchResults SolveArch(
            List<Vector2> nodePositions,
            List<float> loadMagnitudes,
            List<int> loadNodeIndices,
            List<int> supportNodeIndices,
            ArchConfig config,
            ArchSolverType solverType = ArchSolverType.FEM_6DOF,
            int resolutionPerElement = 10)
        {
            if (solverType == ArchSolverType.FEM_6DOF)
                return SolveArch6DOF(nodePositions, loadMagnitudes, loadNodeIndices, 
                                    supportNodeIndices, config, resolutionPerElement);
            else
                return SolveArch3DOF(nodePositions, loadMagnitudes, loadNodeIndices, 
                                    supportNodeIndices, config, resolutionPerElement);
        }

        // ============================================================
        // FEM 6 DOF (Spostamento assiale + flessione)
        // ============================================================
        
        private static ArchResults SolveArch6DOF(
            List<Vector2> nodes, List<float> loads, List<int> loadIdx,
            List<int> supports, ArchConfig config, int resPerElem)
        {
            int nNodes = nodes.Count;
            int nElements = nNodes - 1;
            int ndof = nNodes * 3;
            
            Debug.Log($"[Arch FEM-6DOF] Nodi: {nNodes}, DOF: {ndof}");
            
            double[,] K = new double[ndof, ndof];
            double[] F = new double[ndof];
            
            // Assemblaggio elementi
            for (int e = 0; e < nElements; e++)
            {
                Vector2 dir = nodes[e + 1] - nodes[e];
                float Le = dir.magnitude;
                float angle = Mathf.Atan2(dir.y, dir.x);
                
                if (Le < 1e-6f) continue;
                
                double[,] ke = GetElementStiffness6DOF(Le, config.EA, config.EI);
                double[,] R = GetRotationMatrix(angle);
                double[,] ke_global = MultiplyRTKMultR(ke, R);
                
                AssemblaElemento(K, ke_global, e, nNodes);
            }
            
            // Carichi
            for (int i = 0; i < loadIdx.Count; i++)
            {
                if (loadIdx[i] < nNodes)
                    F[loadIdx[i] * 3 + 1] -= loads[i];
            }
            
            // Vincoli
            foreach (int s in supports)
            {
                if (s < nNodes)
                {
                    double penalty = 1e15;
                    K[s * 3 + 0, s * 3 + 0] += penalty;
                    K[s * 3 + 1, s * 3 + 1] += penalty;
                    K[s * 3 + 2, s * 3 + 2] += penalty;
                }
            }
            
            double[] u = MatrixSolver.Solve(K, F);
            return PostProcess(nodes, u, config, resPerElem);
        }

        // ============================================================
        // FEM 3 DOF (Solo flessione - versione semplificata per archi)
        // ============================================================
        
        private static ArchResults SolveArch3DOF(
            List<Vector2> nodes, List<float> loads, List<int> loadIdx,
            List<int> supports, ArchConfig config, int resPerElem)
        {
            int nNodes = nodes.Count;
            int nElements = nNodes - 1;
            int ndof = nNodes * 2;  // Solo v e θ
            
            Debug.Log($"[Arch FEM-3DOF] Nodi: {nNodes}, DOF: {ndof}");
            
            double[,] K = new double[ndof, ndof];
            double[] F = new double[ndof];
            
            for (int e = 0; e < nElements; e++)
            {
                Vector2 dir = nodes[e + 1] - nodes[e];
                float Le = dir.magnitude;
                
                if (Le < 1e-6f) continue;
                
                // Matrice locale 4x4 per trave di Eulero-Bernoulli
                double L2 = Le * Le;
                double L3 = L2 * Le;
                double[,] ke = new double[4, 4] {
                    { 12*config.EI/L3,  6*config.EI/L2, -12*config.EI/L3,  6*config.EI/L2 },
                    {  6*config.EI/L2,  4*config.EI/Le,  -6*config.EI/L2,  2*config.EI/Le },
                    { -12*config.EI/L3, -6*config.EI/L2,  12*config.EI/L3, -6*config.EI/L2 },
                    {  6*config.EI/L2,  2*config.EI/Le,  -6*config.EI/L2,  4*config.EI/Le }
                };
                
                int[] idx = { e * 2, e * 2 + 1, (e + 1) * 2, (e + 1) * 2 + 1 };
                for (int i = 0; i < 4; i++)
                    for (int j = 0; j < 4; j++)
                        K[idx[i], idx[j]] += ke[i, j];
            }
            
            for (int i = 0; i < loadIdx.Count; i++)
                if (loadIdx[i] < nNodes)
                    F[loadIdx[i] * 2] -= loads[i];
            
            foreach (int s in supports)
                if (s < nNodes)
                    K[s * 2, s * 2] += 1e15;
            
            double[] u = MatrixSolver.Solve(K, F);
            
            // Converti in formato ArchResults
            ArchResults results = new ArchResults();
            int totalPoints = nElements * resPerElem + 1;
            
            results.deflectionPoints = new float[totalPoints];
            results.deformedPositions = new Vector2[totalPoints];
            results.axialPoints = new float[totalPoints];
            results.shearPoints = new float[totalPoints];
            results.momentPoints = new float[totalPoints];
            results.axialDisplacement = new float[totalPoints];
            
            for (int i = 0; i < totalPoints; i++)
            {
                float t = (float)i / (totalPoints - 1);
                results.deflectionPoints[i] = (float)Interpola(u, t, nElements);
                results.deformedPositions[i] = InterpolaPosizione(nodes, t) + 
                    Vector2.up * results.deflectionPoints[i] * 0.1f;
            }
            
            return results;
        }

        // ============================================================
        // FUNZIONI DI SUPPORTO
        // ============================================================
        
        private static double[,] GetElementStiffness6DOF(float L, float EA, float EI)
        {
            double L2 = L * L;
            double L3 = L2 * L;
            
            return new double[6, 6] {
                {  EA/L,       0,          0,      -EA/L,       0,          0      },
                {  0,          12*EI/L3,   6*EI/L2,  0,        -12*EI/L3,    6*EI/L2 },
                {  0,          6*EI/L2,    4*EI/L,   0,        -6*EI/L2,     2*EI/L  },
                { -EA/L,       0,          0,        EA/L,       0,          0      },
                {  0,         -12*EI/L3,  -6*EI/L2,  0,         12*EI/L3,   -6*EI/L2 },
                {  0,          6*EI/L2,    2*EI/L,   0,        -6*EI/L2,     4*EI/L  }
            };
        }
        
        private static double[,] GetRotationMatrix(float angle)
        {
            double c = Mathf.Cos(angle);
            double s = Mathf.Sin(angle);
            
            double[,] R = new double[6, 6];
            R[0, 0] = c;  R[0, 1] = s;
            R[1, 0] = -s; R[1, 1] = c;
            R[2, 2] = 1;
            R[3, 3] = c;  R[3, 4] = s;
            R[4, 3] = -s; R[4, 4] = c;
            R[5, 5] = 1;
            
            return R;
        }
        
        private static double[,] MultiplyRTKMultR(double[,] K, double[,] R)
        {
            int n = 6;
            double[,] temp = new double[n, n];
            double[,] result = new double[n, n];
            
            // temp = K * R
            for (int i = 0; i < n; i++)
                for (int j = 0; j < n; j++)
                    for (int k = 0; k < n; k++)
                        temp[i, j] += K[i, k] * R[k, j];
            
            // result = R^T * temp
            for (int i = 0; i < n; i++)
                for (int j = 0; j < n; j++)
                    for (int k = 0; k < n; k++)
                        result[i, j] += R[k, i] * temp[k, j];
            
            return result;
        }
        
        private static void AssemblaElemento(double[,] K, double[,] ke, int e, int nNodes)
        {
            int[] dof = {
                e * 3, e * 3 + 1, e * 3 + 2,
                (e + 1) * 3, (e + 1) * 3 + 1, (e + 1) * 3 + 2
            };
            
            for (int i = 0; i < 6; i++)
                for (int j = 0; j < 6; j++)
                    K[dof[i], dof[j]] += ke[i, j];
        }
        
        private static ArchResults PostProcess(List<Vector2> nodes, double[] u, ArchConfig config, int resPerElem)
        {
            int nElements = nodes.Count - 1;
            int totalPoints = nElements * resPerElem + 1;
            
            ArchResults results = new ArchResults {
                momentPoints = new float[totalPoints],
                shearPoints = new float[totalPoints],
                axialPoints = new float[totalPoints],
                deflectionPoints = new float[totalPoints],
                axialDisplacement = new float[totalPoints],
                deformedPositions = new Vector2[totalPoints]
            };
            
            for (int i = 0; i < totalPoints; i++)
            {
                float t = (float)i / (totalPoints - 1);
                int elem = Mathf.Min((int)(t * nElements), nElements - 1);
                float localT = (t * nElements) - elem;
                
                Vector2 pA = nodes[elem];
                Vector2 pB = nodes[elem + 1];
                float Le = Vector2.Distance(pA, pB);
                float z = localT * Le;
                
                double[] ue = {
                    u[elem * 3], u[elem * 3 + 1], u[elem * 3 + 2],
                    u[(elem + 1) * 3], u[(elem + 1) * 3 + 1], u[(elem + 1) * 3 + 2]
                };
                
                // Spostamenti
                results.axialDisplacement[i] = (float)((1 - localT) * ue[0] + localT * ue[3]);
                results.deflectionPoints[i] = (float)InterpolaCubica(ue, localT, Le);
                
                // Deformata
                Vector2 original = Vector2.Lerp(pA, pB, localT);
                results.deformedPositions[i] = original + 
                    new Vector2(results.axialDisplacement[i], results.deflectionPoints[i]) * 0.1f;
            }
            
            return results;
        }
        
        private static double InterpolaCubica(double[] ue, double xi, float L)
        {
            double N1 = 1 - 3*xi*xi + 2*xi*xi*xi;
            double N2 = L * (xi - 2*xi*xi + xi*xi*xi);
            double N3 = 3*xi*xi - 2*xi*xi*xi;
            double N4 = L * (-xi*xi + xi*xi*xi);
            return N1 * ue[1] + N2 * ue[2] + N3 * ue[4] + N4 * ue[5];
        }
        
        private static double Interpola(double[] u, float t, int nElements)
        {
            float pos = t * nElements;
            int elem = Mathf.Min((int)pos, nElements - 1);
            float frac = pos - elem;
            return (1 - frac) * u[elem * 2] + frac * u[(elem + 1) * 2];
        }
        
        private static Vector2 InterpolaPosizione(List<Vector2> nodes, float t)
        {
            float pos = t * (nodes.Count - 1);
            int idx = Mathf.Min((int)pos, nodes.Count - 2);
            float frac = pos - idx;
            return Vector2.Lerp(nodes[idx], nodes[idx + 1], frac);
        }
    }
}