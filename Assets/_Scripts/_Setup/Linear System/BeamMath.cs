using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public enum SolverType { Analytic, FEM }
public enum BeamStructureType { Beam, Arch }

public struct BeamData
{
    public float[] momentPoints;
    public float[] shearPoints;
    public float[] deflectionPoints;
    public float[] stressPoints;
}

public static class BeamMath
{
    private const float EI = 1000f;

    public static BeamData CalculateAnalytic(float L, List<float> loadX, List<float> loadP, List<float> suppX, int resolution)
    {
        // Validazione input
        if (L <= 0 || resolution <= 1)
        {
            Debug.LogError($"Invalid input: L={L}, resolution={resolution}");
            return CreateEmptyData(resolution);
        }

        // Prepara nodi inclusi supporti e carichi
        List<float> nodes = new List<float> { 0f, L };
        nodes.AddRange(loadX.Where(x => x > 0 && x < L));
        nodes.AddRange(suppX.Where(x => x > 0 && x < L));
        
        nodes = nodes.Distinct().OrderBy(x => x).ToList();
        
        // Pulisci nodi troppo vicini
        List<float> cleanNodes = new List<float>();
        float tolerance = 0.001f * L;
        foreach (float node in nodes)
        {
            if (cleanNodes.Count == 0 || Mathf.Abs(node - cleanNodes.Last()) > tolerance)
                cleanNodes.Add(node);
        }

        int nSeg = cleanNodes.Count - 1;
        if (nSeg < 1)
        {
            Debug.LogWarning("No valid segments, using FEM");
            return CalculateFEM(L, loadX, loadP, suppX, resolution);
        }

        int dim = nSeg * 4;
        double[,] A = new double[dim, dim];
        double[] B = new double[dim];
        int row = 0;

        try
        {
            for (int i = 0; i < cleanNodes.Count; i++)
            {
                float x = cleanNodes[i];
                bool isSupport = suppX.Any(sx => Mathf.Abs(sx - x) < 0.01f);
                bool isLoad = loadX.Any(lx => Mathf.Abs(lx - x) < 0.01f);
                float pValue = 0f;
                
                if (isLoad)
                {
                    int loadIdx = loadX.FindIndex(lx => Mathf.Abs(lx - x) < 0.01f);
                    if (loadIdx >= 0 && loadIdx < loadP.Count)
                        pValue = loadP[loadIdx];
                }

                if (i == 0) // Nodo sinistro
                {
                    if (isSupport)
                    {
                        AddEquation_V(A, B, 0, 0f, 0.0, ref row);
                        AddEquation_M(A, B, 0, 0f, 0.0, ref row);
                    }
                    else
                    {
                        AddEquation_M(A, B, 0, 0f, 0.0, ref row);
                        AddEquation_T(A, B, 0, 0f, (double)-pValue, ref row);
                    }
                }
                else if (i == cleanNodes.Count - 1) // Nodo destro
                {
                    float L_last = cleanNodes[i] - cleanNodes[i - 1];
                    if (isSupport)
                    {
                        AddEquation_V(A, B, nSeg - 1, L_last, 0.0, ref row);
                        AddEquation_M(A, B, nSeg - 1, L_last, 0.0, ref row);
                    }
                    else
                    {
                        AddEquation_M(A, B, nSeg - 1, L_last, 0.0, ref row);
                        AddEquation_T(A, B, nSeg - 1, L_last, (double)-pValue, ref row);
                    }
                }
                else // Nodo interno
                {
                    float L_left = cleanNodes[i] - cleanNodes[i - 1];
                    
                    AddContinuity_V(A, B, i - 1, i, L_left, ref row);
                    AddContinuity_Phi(A, B, i - 1, i, L_left, ref row);
                    AddContinuity_M(A, B, i - 1, i, L_left, ref row);
                    
                    if (isSupport)
                    {
                        AddEquation_V(A, B, i, 0f, 0.0, ref row);
                    }
                    else
                    {
                        AddEquilibrium_T(A, B, i - 1, i, L_left, (double)pValue, ref row);
                    }
                }
            }

            double[] coeffs = SolveLinearSystem(A, B, dim);
            
            if (coeffs == null || coeffs.All(c => System.Math.Abs(c) < 1e-8))
            {
                Debug.LogWarning("Analytic solver failed, falling back to FEM");
                return CalculateFEM(L, loadX, loadP, suppX, resolution);
            }

            return InterpolateResults(cleanNodes, coeffs, L, resolution);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Analytic solver exception: {e.Message}\n{e.StackTrace}");
            return CalculateFEM(L, loadX, loadP, suppX, resolution);
        }
    }

    private static BeamData InterpolateResults(List<float> nodes, double[] coeffs, float L, int resolution)
    {
        BeamData data = new BeamData
        {
            momentPoints = new float[resolution],
            shearPoints = new float[resolution],
            deflectionPoints = new float[resolution],
            stressPoints = new float[resolution]
        };

        for (int j = 0; j < resolution; j++)
        {
            float x = (L / (resolution - 1)) * j;
            
            int segIdx = 0;
            for (int i = 0; i < nodes.Count - 1; i++)
            {
                if (x >= nodes[i] - 0.001f && x <= nodes[i + 1] + 0.001f)
                {
                    segIdx = i;
                    break;
                }
            }
            
            float z = x - nodes[segIdx];
            int c = segIdx * 4;
            
            if (c + 3 < coeffs.Length)
            {
                data.deflectionPoints[j] = (float)((1.0 / EI) * (
                    coeffs[c + 0] * z * z * z / 6.0 + 
                    coeffs[c + 1] * z * z / 2.0 + 
                    coeffs[c + 2] * z + 
                    coeffs[c + 3]));
                
                data.momentPoints[j] = -(float)(coeffs[c + 0] * z + coeffs[c + 1]);
                data.shearPoints[j] = -(float)(coeffs[c + 0]);
                data.stressPoints[j] = Mathf.Abs(data.momentPoints[j]) * 0.05f;
            }
        }
        
        return data;
    }

    public static BeamData CalculateFEM(float L, List<float> loadX, List<float> loadP, List<float> suppX, int resolution)
    {
        int nNodes = Mathf.Max(resolution, 10);
        int nElems = nNodes - 1;
        float Le = L / nElems;
        int ndof = nNodes * 2;

        double[,] K = new double[ndof, ndof];
        double[] F = new double[ndof];

        for (int e = 0; e < nElems; e++)
        {
            double[,] ke = GetLocalFEMStiffness(Le, EI);
            int[] idx = { e * 2, e * 2 + 1, (e + 1) * 2, (e + 1) * 2 + 1 };
            for (int i = 0; i < 4; i++)
                for (int j = 0; j < 4; j++) 
                    K[idx[i], idx[j]] += ke[i, j];
        }

        for (int i = 0; i < loadX.Count; i++)
        {
            float x = Mathf.Clamp(loadX[i], 0, L);
            int nodeIdx = Mathf.Clamp(Mathf.RoundToInt((x / L) * nElems), 0, nNodes - 1);
            F[nodeIdx * 2] += loadP[i];
        }

        foreach (float sx in suppX)
        {
            float x = Mathf.Clamp(sx, 0, L);
            int nodeIdx = Mathf.Clamp(Mathf.RoundToInt((x / L) * nElems), 0, nNodes - 1);
            K[nodeIdx * 2, nodeIdx * 2] += 1e12;
            F[nodeIdx * 2] = 0;
        }

        double[] u = SolveLinearSystem(K, F, ndof);
        
        if (u == null)
        {
            Debug.LogError("FEM solver failed");
            return CreateEmptyData(resolution);
        }

        BeamData data = new BeamData
        {
            momentPoints = new float[resolution],
            shearPoints = new float[resolution],
            deflectionPoints = new float[resolution],
            stressPoints = new float[resolution]
        };
        
        for (int i = 0; i < nNodes && i < resolution; i++)
        {
            data.deflectionPoints[i] = (float)u[i * 2];
            
            if (i < nNodes - 1)
            {
                double v1 = u[i * 2];
                double theta1 = u[i * 2 + 1];
                double v2 = u[(i + 1) * 2];
                double theta2 = u[(i + 1) * 2 + 1];
                
                data.momentPoints[i] = (float)(EI * (6.0 / Le / Le * (v2 - v1) - 2.0 / Le * (2.0 * theta1 + theta2)));
                data.shearPoints[i] = (float)(EI * (12.0 / Le / Le / Le * (v2 - v1) - 6.0 / Le / Le * (theta1 + theta2)));
                data.stressPoints[i] = Mathf.Abs(data.momentPoints[i]) * 0.05f;
            }
        }
        
        for (int i = nNodes; i < resolution; i++)
        {
            data.deflectionPoints[i] = data.deflectionPoints[nNodes - 1];
            data.momentPoints[i] = data.momentPoints[nNodes - 1];
            data.shearPoints[i] = data.shearPoints[nNodes - 1];
            data.stressPoints[i] = data.stressPoints[nNodes - 1];
        }
        
        return data;
    }

    private static BeamData CreateEmptyData(int resolution)
    {
        BeamData data = new BeamData
        {
            momentPoints = new float[resolution],
            shearPoints = new float[resolution],
            deflectionPoints = new float[resolution],
            stressPoints = new float[resolution]
        };
        return data;
    }

    private static void AddEquation_V(double[,] A, double[] B, int s, float z, double val, ref int r) 
    { 
        if (r >= A.GetLength(0)) return;
        double zDouble = (double)z;
        A[r, s * 4 + 0] = zDouble * zDouble * zDouble / 6.0; 
        A[r, s * 4 + 1] = zDouble * zDouble / 2.0; 
        A[r, s * 4 + 2] = zDouble; 
        A[r, s * 4 + 3] = 1.0; 
        B[r] = val; 
        r++; 
    }
    
    private static void AddEquation_M(double[,] A, double[] B, int s, float z, double val, ref int r) 
    { 
        if (r >= A.GetLength(0)) return;
        A[r, s * 4 + 0] = (double)z; 
        A[r, s * 4 + 1] = 1.0; 
        B[r] = val; 
        r++; 
    }
    
    private static void AddEquation_T(double[,] A, double[] B, int s, float z, double val, ref int r) 
    { 
        if (r >= A.GetLength(0)) return;
        A[r, s * 4 + 0] = 1.0; 
        B[r] = val; 
        r++; 
    }
    
    private static void AddContinuity_V(double[,] A, double[] B, int s1, int s2, float L1, ref int r) 
    { 
        if (r >= A.GetLength(0)) return;
        double L1d = (double)L1;
        A[r, s1 * 4 + 0] = L1d * L1d * L1d / 6.0; 
        A[r, s1 * 4 + 1] = L1d * L1d / 2.0; 
        A[r, s1 * 4 + 2] = L1d; 
        A[r, s1 * 4 + 3] = 1.0; 
        A[r, s2 * 4 + 3] = -1.0; 
        r++; 
    }
    
    private static void AddContinuity_Phi(double[,] A, double[] B, int s1, int s2, float L1, ref int r) 
    { 
        if (r >= A.GetLength(0)) return;
        double L1d = (double)L1;
        A[r, s1 * 4 + 0] = L1d * L1d / 2.0; 
        A[r, s1 * 4 + 1] = L1d; 
        A[r, s1 * 4 + 2] = 1.0; 
        A[r, s2 * 4 + 2] = -1.0; 
        r++; 
    }
    
    private static void AddContinuity_M(double[,] A, double[] B, int s1, int s2, float L1, ref int r) 
    { 
        if (r >= A.GetLength(0)) return;
        A[r, s1 * 4 + 0] = (double)L1; 
        A[r, s1 * 4 + 1] = 1.0; 
        A[r, s2 * 4 + 1] = -1.0; 
        r++; 
    }
    
    private static void AddEquilibrium_T(double[,] A, double[] B, int s1, int s2, float L1, double P, ref int r) 
    { 
        if (r >= A.GetLength(0)) return;
        A[r, s1 * 4 + 0] = 1.0; 
        A[r, s2 * 4 + 0] = -1.0; 
        B[r] = -P; 
        r++; 
    }

    private static double[,] GetLocalFEMStiffness(float l, float EI)
    {
        double ld = (double)l;
        double EI_d = (double)EI;
        double l2 = ld * ld; 
        double l3 = l2 * ld;
        return new double[4, 4] {
            { 12.0 * EI_d / l3, 6.0 * EI_d / l2, -12.0 * EI_d / l3, 6.0 * EI_d / l2 },
            { 6.0 * EI_d / l2,  4.0 * EI_d / ld,  -6.0 * EI_d / l2,  2.0 * EI_d / ld },
            { -12.0 * EI_d / l3, -6.0 * EI_d / l2, 12.0 * EI_d / l3, -6.0 * EI_d / l2 },
            { 6.0 * EI_d / l2,  2.0 * EI_d / ld,  -6.0 * EI_d / l2,  4.0 * EI_d / ld }
        };
    }

    private static double[] SolveLinearSystem(double[,] A, double[] b, int n)
    {
        if (n == 0) return null;
        
        double[,] Acopy = new double[n, n];
        double[] bcopy = new double[n];
        for (int i = 0; i < n; i++)
        {
            bcopy[i] = b[i];
            for (int j = 0; j < n; j++)
                Acopy[i, j] = A[i, j];
        }
        
        try
        {
            for (int i = 0; i < n; i++)
            {
                int pivot = i;
                double maxAbs = System.Math.Abs(Acopy[i, i]);
                for (int j = i + 1; j < n; j++)
                {
                    if (System.Math.Abs(Acopy[j, i]) > maxAbs)
                    {
                        maxAbs = System.Math.Abs(Acopy[j, i]);
                        pivot = j;
                    }
                }
                
                if (maxAbs < 1e-10) 
                {
                    continue;
                }
                
                if (pivot != i)
                {
                    for (int k = i; k < n; k++)
                    {
                        double t = Acopy[i, k];
                        Acopy[i, k] = Acopy[pivot, k];
                        Acopy[pivot, k] = t;
                    }
                    double tb = bcopy[i];
                    bcopy[i] = bcopy[pivot];
                    bcopy[pivot] = tb;
                }
                
                double pivotVal = Acopy[i, i];
                for (int k = i; k < n; k++)
                    Acopy[i, k] /= pivotVal;
                bcopy[i] /= pivotVal;
                
                for (int j = 0; j < n; j++)
                {
                    if (j != i && System.Math.Abs(Acopy[j, i]) > 1e-12)
                    {
                        double factor = Acopy[j, i];
                        for (int k = i; k < n; k++)
                            Acopy[j, k] -= factor * Acopy[i, k];
                        bcopy[j] -= factor * bcopy[i];
                    }
                }
            }
            
            return bcopy;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Solver failed: {e.Message}");
            return null;
        }
    }
}