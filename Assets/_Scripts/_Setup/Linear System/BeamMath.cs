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
        // Crea lista nodi includendo estremità, carichi e supporti
        List<float> nodes = new List<float> { 0, L };
        nodes.AddRange(loadX);
        nodes.AddRange(suppX);
        nodes = nodes.Where(n => n >= 0 && n <= L).OrderBy(n => n).Distinct().ToList();

        // Pulisci nodi troppo vicini (tolleranza)
        List<float> cleanNodes = new List<float>();
        if (nodes.Count > 0)
        {
            cleanNodes.Add(nodes[0]);
            foreach (var n in nodes)
            {
                if (n - cleanNodes.Last() > 0.01f) cleanNodes.Add(n);
            }
        }

        int nSeg = cleanNodes.Count - 1;
        if (nSeg < 1) return CreateEmptyData(resolution);

        int dim = nSeg * 4;
        double[,] A = new double[dim, dim];
        double[] B = new double[dim];
        int row = 0;

        for (int i = 0; i < cleanNodes.Count; i++)
        {
            float x = cleanNodes[i];
            bool isSupport = suppX.Any(sx => Mathf.Abs(sx - x) < 0.02f);
            bool isLoad = loadX.Any(lx => Mathf.Abs(lx - x) < 0.02f);
            float pValue = isLoad ? loadP[loadX.FindIndex(lx => Mathf.Abs(lx - x) < 0.02f)] : 0f;

            if (i == 0) // Nodo sinistro
            {
                if (isSupport)
                {
                    Add_V(A, B, 0, 0, 0, ref row);
                    Add_M(A, B, 0, 0, 0, ref row);
                }
                else
                {
                    // Estremità libera: momento = 0, taglio = -P (solo se c'è carico)
                    Add_M(A, B, 0, 0, 0, ref row);
                    Add_T(A, B, 0, 0, -pValue, ref row);
                }
            }
            else if (i == cleanNodes.Count - 1) // Nodo destro
            {
                float L_last = cleanNodes[i] - cleanNodes[i - 1];
                if (isSupport)
                {
                    Add_V(A, B, nSeg - 1, L_last, 0, ref row);
                    Add_M(A, B, nSeg - 1, L_last, 0, ref row);
                }
                else
                {
                    Add_M(A, B, nSeg - 1, L_last, 0, ref row);
                    Add_T(A, B, nSeg - 1, L_last, -pValue, ref row);
                }
            }
            else // Nodo interno
            {
                float L_left = cleanNodes[i] - cleanNodes[i - 1];
                
                // Continuità spostamento
                Add_Cont_V(A, B, i - 1, i, L_left, ref row);
                // Continuità rotazione
                Add_Cont_Phi(A, B, i - 1, i, L_left, ref row);
                // Continuità momento
                Add_Cont_M(A, B, i - 1, i, L_left, ref row);
                
                // Equilibrio taglio (con eventuale carico concentrato)
                if (isSupport)
                {
                    Add_V(A, B, i, 0, 0, ref row);
                }
                else
                {
                    Add_Cont_T(A, B, i - 1, i, L_left, pValue, ref row);
                }
            }
        }

        double[] coeffs = Solve(A, B);
        
        // Se la soluzione è nulla, usa FEM come fallback
        if (coeffs == null || coeffs.All(c => Mathf.Abs((float)c) < 1e-10))
        {
            Debug.LogWarning("Analytic solver failed, falling back to FEM");
            return CalculateFEM(L, loadX, loadP, suppX, resolution);
        }

        return InterpolateResults(cleanNodes, coeffs, L, resolution);
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
            
            // Trova il segmento contenente x
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
                // v(z) = (1/EI) * (c0*z³/6 + c1*z²/2 + c2*z + c3)
                data.deflectionPoints[j] = (float)((1.0 / EI) * (
                    coeffs[c + 0] * z * z * z / 6.0 + 
                    coeffs[c + 1] * z * z / 2.0 + 
                    coeffs[c + 2] * z + 
                    coeffs[c + 3]));
                
                // M(z) = -(c0*z + c1)
                data.momentPoints[j] = -(float)(coeffs[c + 0] * z + coeffs[c + 1]);
                
                // T(z) = -c0
                data.shearPoints[j] = -(float)(coeffs[c + 0]);
                
                data.stressPoints[j] = Mathf.Abs(data.momentPoints[j]) * 0.1f;
            }
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

    // ... (resto dei metodi Add_V, Add_M, Add_T, Add_Cont_V, Add_Cont_Phi, Add_Cont_M, Add_Cont_T rimangono uguali)

    public static BeamData CalculateFEM(float L, List<float> loadX, List<float> loadP, List<float> suppX, int resolution)
    {
        int nNodes = resolution;
        int nElems = nNodes - 1;
        float Le = L / nElems;
        int ndof = nNodes * 2;

        double[,] K = new double[ndof, ndof];
        double[] F = new double[ndof];

        // Assembla matrice di rigidezza
        for (int e = 0; e < nElems; e++)
        {
            double[,] ke = GetLocalFEMStiffness(Le, EI);
            int[] idx = { e * 2, e * 2 + 1, (e + 1) * 2, (e + 1) * 2 + 1 };
            for (int i = 0; i < 4; i++)
                for (int j = 0; j < 4; j++) 
                    K[idx[i], idx[j]] += ke[i, j];
        }

        // Applica carichi concentrati
        for (int i = 0; i < loadX.Count; i++)
        {
            int nodeIdx = Mathf.Clamp(Mathf.RoundToInt((loadX[i] / L) * nElems), 0, nNodes - 1);
            F[nodeIdx * 2] += loadP[i];  // Nota: segno corretto
        }

        // Applica condizioni al contorno (supporti)
        foreach (float sx in suppX)
        {
            int nodeIdx = Mathf.Clamp(Mathf.RoundToInt((sx / L) * nElems), 0, nNodes - 1);
            // Penale per vincolare lo spostamento
            K[nodeIdx * 2, nodeIdx * 2] += 1e12;
        }

        double[] u = Solve(K, F);
        
        if (u == null) return CreateEmptyData(resolution);

        BeamData data = new BeamData
        {
            momentPoints = new float[resolution],
            shearPoints = new float[resolution],
            deflectionPoints = new float[resolution],
            stressPoints = new float[resolution]
        };
        
        for (int i = 0; i < nNodes; i++)
        {
            data.deflectionPoints[i] = (float)u[i * 2];
            
            if (i < nNodes - 1)
            {
                // Calcola momento e taglio dallo strain
                double v1 = u[i * 2];
                double theta1 = u[i * 2 + 1];
                double v2 = u[(i + 1) * 2];
                double theta2 = u[(i + 1) * 2 + 1];
                
                data.momentPoints[i] = (float)(EI * (6/Le/Le * (v2 - v1) - 2/Le * (2*theta1 + theta2)));
                data.shearPoints[i] = (float)(EI * (12/Le/Le/Le * (v2 - v1) - 6/Le/Le * (theta1 + theta2)));
                data.stressPoints[i] = Mathf.Abs(data.momentPoints[i]) * 0.1f;
            }
        }
        
        // Propaga ultimo valore
        if (resolution > 1)
        {
            data.momentPoints[resolution - 1] = data.momentPoints[resolution - 2];
            data.shearPoints[resolution - 1] = data.shearPoints[resolution - 2];
            data.stressPoints[resolution - 1] = data.stressPoints[resolution - 2];
        }
        
        return data;
    }

    // Mantieni i metodi helper esistenti...
    static void Add_V(double[,] A, double[] B, int s, float z, double val, ref int r) 
    { 
        A[r, s * 4 + 0] = z * z * z / 6.0; 
        A[r, s * 4 + 1] = z * z / 2.0; 
        A[r, s * 4 + 2] = z; 
        A[r, s * 4 + 3] = 1; 
        B[r] = val; 
        r++; 
    }
    
    static void Add_M(double[,] A, double[] B, int s, float z, double val, ref int r) 
    { 
        A[r, s * 4 + 0] = z; 
        A[r, s * 4 + 1] = 1; 
        B[r] = val; 
        r++; 
    }
    
    static void Add_T(double[,] A, double[] B, int s, float z, double val, ref int r) 
    { 
        A[r, s * 4 + 0] = 1; 
        B[r] = val; 
        r++; 
    }
    
    static void Add_Cont_V(double[,] A, double[] B, int s1, int s2, float L1, ref int r) 
    { 
        A[r, s1 * 4 + 0] = L1 * L1 * L1 / 6.0; 
        A[r, s1 * 4 + 1] = L1 * L1 / 2.0; 
        A[r, s1 * 4 + 2] = L1; 
        A[r, s1 * 4 + 3] = 1; 
        A[r, s2 * 4 + 3] = -1; 
        r++; 
    }
    
    static void Add_Cont_Phi(double[,] A, double[] B, int s1, int s2, float L1, ref int r) 
    { 
        A[r, s1 * 4 + 0] = L1 * L1 / 2.0; 
        A[r, s1 * 4 + 1] = L1; 
        A[r, s1 * 4 + 2] = 1; 
        A[r, s2 * 4 + 2] = -1; 
        r++; 
    }
    
    static void Add_Cont_M(double[,] A, double[] B, int s1, int s2, float L1, ref int r) 
    { 
        A[r, s1 * 4 + 0] = L1; 
        A[r, s1 * 4 + 1] = 1; 
        A[r, s2 * 4 + 1] = -1; 
        r++; 
    }
    
    static void Add_Cont_T(double[,] A, double[] B, int s1, int s2, float L1, double P, ref int r) 
    { 
        A[r, s1 * 4 + 0] = 1; 
        A[r, s2 * 4 + 0] = -1; 
        B[r] = -P; 
        r++; 
    }

    private static double[,] GetLocalFEMStiffness(float l, float EI)
    {
        double l2 = l * l; 
        double l3 = l2 * l;
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
        if (n == 0) return null;
        
        // Crea copie per non modificare gli originali
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
                // Pivot parziale
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
                
                if (maxAbs < 1e-12) continue;
                
                // Scambia righe
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
                
                // Eliminazione
                for (int j = i + 1; j < n; j++)
                {
                    double factor = Acopy[j, i] / Acopy[i, i];
                    bcopy[j] -= factor * bcopy[i];
                    for (int k = i; k < n; k++)
                        Acopy[j, k] -= factor * Acopy[i, k];
                }
            }
            
            // Back substitution
            double[] x = new double[n];
            for (int i = n - 1; i >= 0; i--)
            {
                double sum = 0;
                for (int j = i + 1; j < n; j++)
                    sum += Acopy[i, j] * x[j];
                if (System.Math.Abs(Acopy[i, i]) > 1e-12)
                    x[i] = (bcopy[i] - sum) / Acopy[i, i];
                else
                    x[i] = 0;
            }
            return x;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Solver failed: {e.Message}");
            return null;
        }
    }
}