using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public enum SolverType { Analytic, FEM }

public struct BeamData
{
    public float[] momentPoints;
    public float[] shearPoints;
    public float[] deflectionPoints;
}

public static class BeamMath
{
    private const float EI = 1000f;

    // --- METODO 1: ANALITICO (LINEE DI INFLUENZA / INTEGRAZIONE IV GRADO) ---
    public static BeamData CalculateAnalytic(float L, List<float> loadX, List<float> loadP, List<float> suppX, int resolution)
    {
        List<float> nodes = new List<float> { 0, L };
        nodes.AddRange(loadX);
        nodes.AddRange(suppX);
        nodes = nodes.Where(n => n >= 0 && n <= L).OrderBy(n => n).ToList();

        List<float> cleanNodes = new List<float>();
        if (nodes.Count > 0)
        {
            cleanNodes.Add(nodes[0]);
            foreach (var n in nodes)
            {
                if (n - cleanNodes.Last() > 0.005f) cleanNodes.Add(n);
            }
        }
        if (Mathf.Abs(L - cleanNodes.Last()) > 0.005f && L > cleanNodes.Last()) cleanNodes.Add(L);

        int nSeg = cleanNodes.Count - 1;
        int dim = nSeg * 4;

        Debug.Log($"<color=cyan>[Analytic Solver]</color> Matrice: <b>{dim}x{dim}</b> ({nSeg} segmenti)");

        double[,] A = new double[dim, dim];
        double[] B = new double[dim];
        int row = 0;

        for (int i = 0; i < cleanNodes.Count; i++)
        {
            float x = cleanNodes[i];
            bool isSupport = suppX.Any(sx => Mathf.Abs(sx - x) < 0.02f);
            bool isLoad = loadX.Any(lx => Mathf.Abs(lx - x) < 0.02f);
            float pValue = isLoad ? loadP[loadX.FindIndex(lx => Mathf.Abs(lx - x) < 0.02f)] : 0;

            if (i == 0)
            {
                if (isSupport) { Add_V(A, B, 0, 0, 0, ref row); Add_M(A, B, 0, 0, 0, ref row); }
                else { Add_M(A, B, 0, 0, 0, ref row); Add_T(A, B, 0, 0, -pValue, ref row); }
            }
            else if (i == cleanNodes.Count - 1)
            {
                float L_last = cleanNodes[i] - cleanNodes[i - 1];
                if (isSupport) { Add_V(A, B, nSeg - 1, L_last, 0, ref row); Add_M(A, B, nSeg - 1, L_last, 0, ref row); }
                else { Add_M(A, B, nSeg - 1, L_last, 0, ref row); Add_T(A, B, nSeg - 1, L_last, -pValue, ref row); }
            }
            else
            {
                float L_left = cleanNodes[i] - cleanNodes[i - 1];
                Add_Cont_V(A, B, i - 1, i, L_left, ref row);
                Add_Cont_Phi(A, B, i - 1, i, L_left, ref row);
                Add_Cont_M(A, B, i - 1, i, L_left, ref row);
                if (isSupport) Add_V(A, B, i, 0, 0, ref row);
                else Add_Cont_T(A, B, i - 1, i, L_left, pValue, ref row);
            }
        }

        double[] coeffs = Solve(A, B);
        BeamData data = new BeamData { momentPoints = new float[resolution], shearPoints = new float[resolution], deflectionPoints = new float[resolution] };

        for (int j = 0; j < resolution; j++)
        {
            float x = (L / (resolution - 1)) * j;
            int sIdx = 0;
            for (int i = 0; i < cleanNodes.Count - 1; i++) if (x >= cleanNodes[i] - 0.001f) sIdx = i;
            float z = x - cleanNodes[sIdx];
            int c = sIdx * 4;
            data.deflectionPoints[j] = (float)((1.0 / EI) * (coeffs[c + 0] * z * z * z / 6.0 + coeffs[c + 1] * z * z / 2.0 + coeffs[c + 2] * z + coeffs[c + 3]));
            data.momentPoints[j] = -(float)(coeffs[c + 0] * z + coeffs[c + 1]);
            data.shearPoints[j] = -(float)(coeffs[c + 0]);
        }
        return data;
    }

    // --- METODO 2: FEM (FINITE ELEMENT METHOD) ---
    public static BeamData CalculateFEM(float L, List<float> loadX, List<float> loadP, List<float> suppX, int resolution)
    {
        int nNodes = resolution;
        int nElems = nNodes - 1;
        float Le = L / nElems;
        int ndof = nNodes * 2;

        Debug.Log($"<color=yellow>[FEM Solver]</color> Matrice: <b>{ndof}x{ndof}</b> ({nNodes} nodi)");

        double[,] K = new double[ndof, ndof];
        double[] F = new double[ndof];

        // Assemblaggio matrice globale
        for (int e = 0; e < nElems; e++)
        {
            double[,] ke = GetLocalFEMStiffness(Le, EI);
            int[] idx = { e * 2, e * 2 + 1, (e + 1) * 2, (e + 1) * 2 + 1 };
            for (int i = 0; i < 4; i++)
                for (int j = 0; j < 4; j++) K[idx[i], idx[j]] += ke[i, j];
        }

        // Carichi
        for (int i = 0; i < loadX.Count; i++)
        {
            int nodeIdx = Mathf.Clamp(Mathf.RoundToInt((loadX[i] / L) * nElems), 0, nNodes - 1);
            F[nodeIdx * 2] -= loadP[i];
        }

        // Vincoli (Penalizzazione)
        foreach (float sx in suppX)
        {
            int nodeIdx = Mathf.Clamp(Mathf.RoundToInt((sx / L) * nElems), 0, nNodes - 1);
            K[nodeIdx * 2, nodeIdx * 2] += 1e12;
        }

        double[] u = Solve(K, F);

        BeamData data = new BeamData { momentPoints = new float[resolution], shearPoints = new float[resolution], deflectionPoints = new float[resolution] };
        for (int i = 0; i < nNodes; i++)
        {
            data.deflectionPoints[i] = -(float)u[i * 2];
            if (i < nNodes - 1)
            {
                // Calcolo Taglio e Momento dagli spostamenti dell'elemento
                float z = 0; // Inizio elemento
                double[] ue = { u[i * 2], u[i * 2 + 1], u[(i + 1) * 2], u[(i + 1) * 2 + 1] };
                data.momentPoints[i] = (float)(EI * (ue[0] * (-6 / Le / Le + 12 * z / Le / Le / Le) + ue[1] * (-4 / Le + 6 * z / Le / Le) + ue[2] * (6 / Le / Le - 12 * z / Le / Le / Le) + ue[3] * (-2 / Le + 6 * z / Le / Le)));
                data.shearPoints[i] = (float)(EI * (ue[0] * (12 / Le / Le / Le) + ue[1] * (6 / Le / Le) + ue[2] * (-12 / Le / Le / Le) + ue[3] * (6 / Le / Le)));
            }
        }
        // Chiudiamo l'ultimo punto del diagramma
        data.momentPoints[nNodes - 1] = data.momentPoints[nNodes - 2];
        data.shearPoints[nNodes - 1] = data.shearPoints[nNodes - 2];

        return data;
    }

    // --- HELPERS COMUNI ---
    static void Add_V(double[,] A, double[] B, int s, float z, double val, ref int r) { A[r, s * 4 + 0] = z * z * z / 6.0; A[r, s * 4 + 1] = z * z / 2.0; A[r, s * 4 + 2] = z; A[r, s * 4 + 3] = 1; B[r] = val; r++; }
    static void Add_M(double[,] A, double[] B, int s, float z, double val, ref int r) { A[r, s * 4 + 0] = z; A[r, s * 4 + 1] = 1; B[r] = val; r++; }
    static void Add_T(double[,] A, double[] B, int s, float z, double val, ref int r) { A[r, s * 4 + 0] = 1; B[r] = val; r++; }
    static void Add_Cont_V(double[,] A, double[] B, int s1, int s2, float L1, ref int r) { A[r, s1 * 4 + 0] = L1 * L1 * L1 / 6.0; A[r, s1 * 4 + 1] = L1 * L1 / 2.0; A[r, s1 * 4 + 2] = L1; A[r, s1 * 4 + 3] = 1; A[r, s2 * 4 + 3] = -1; r++; }
    static void Add_Cont_Phi(double[,] A, double[] B, int s1, int s2, float L1, ref int r) { A[r, s1 * 4 + 0] = L1 * L1 / 2.0; A[r, s1 * 4 + 1] = L1; A[r, s1 * 4 + 2] = 1; A[r, s2 * 4 + 2] = -1; r++; }
    static void Add_Cont_M(double[,] A, double[] B, int s1, int s2, float L1, ref int r) { A[r, s1 * 4 + 0] = L1; A[r, s1 * 4 + 1] = 1; A[r, s2 * 4 + 1] = -1; r++; }
    static void Add_Cont_T(double[,] A, double[] B, int s1, int s2, float L1, float P, ref int r) { A[r, s1 * 4 + 0] = 1; A[r, s2 * 4 + 0] = -1; B[r] = -P; r++; }

    private static double[,] GetLocalFEMStiffness(float l, float EI)
    {
        double l2 = l * l; double l3 = l2 * l;
        return new double[4, 4] {
            { 12*EI/l3, 6*EI/l2, -12*EI/l3, 6*EI/l2 },
            { 6*EI/l2,  4*EI/l,  -6*EI/l2,  2*EI/l  },
            { -12*EI/l3,-6*EI/l2, 12*EI/l3, -6*EI/l2 },
            { 6*EI/l2,  2*EI/l,  -6*EI/l2,  4*EI/l  }
        };
    }

    private static double[] Solve(double[,] A, double[] b)
    {
        int n = b.Length;
        for (int i = 0; i < n; i++)
        {
            int pivot = i;
            for (int j = i + 1; j < n; j++) if (System.Math.Abs(A[j, i]) > System.Math.Abs(A[pivot, i])) pivot = j;
            for (int k = i; k < n; k++) { double t = A[i, k]; A[i, k] = A[pivot, k]; A[pivot, k] = t; }
            double tempB = b[i]; b[i] = b[pivot]; b[pivot] = tempB;
            if (System.Math.Abs(A[i, i]) < 1e-15) continue;
            for (int j = i + 1; j < n; j++)
            {
                double f = A[j, i] / A[i, i];
                b[j] -= f * b[i];
                for (int k = i; k < n; k++) A[j, k] -= f * A[i, k];
            }
        }
        double[] x = new double[n];
        for (int i = n - 1; i >= 0; i--)
        {
            double s = 0;
            for (int j = i + 1; j < n; j++) s += A[i, j] * x[j];
            if (System.Math.Abs(A[i, i]) > 1e-15) x[i] = (b[i] - s) / A[i, i];
        }
        return x;
    }
}