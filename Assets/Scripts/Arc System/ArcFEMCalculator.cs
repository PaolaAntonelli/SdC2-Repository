using UnityEngine;
using System.Collections.Generic;

public struct ArcBeamData
{
    public float[] axialForcePoints;
    public float[] shearPoints;
    public float[] momentPoints;
    public float[] horizontalDeflection;
    public float[] verticalDeflection;
}

public static class ArcFEMCalculator
{
    // Parametri del materiale per un arco in scala Unity (1 unità = 1 metro)
    // Valori realistici per acciaio strutturale, ma scalati per Unity
    private const float E = 210f;           // Modulo di Young scalato (GPa → unità Unity)
    private const float I = 0.000675f;      // Momento d'inerzia per sezione 30cm x 30cm
    private const float A = 0.09f;          // Area sezione trasversale
    private const float EI = E * I;
    private const float EA = E * A;

    public static ArcBeamData CalculateArcFEM(
    Vector3[] arcPoints,
    List<float> loadPositions,
    List<float> loadMagnitudes,
    List<float> supportPositions,
    int visualizationResolution)
    {
    int nNodes = arcPoints.Length;
    int nElements = nNodes - 1;
    int dofPerNode = 3;  // u_x, u_y, θ_z
    int totalDof = nNodes * dofPerNode;
    
    double[,] K = new double[totalDof, totalDof];
    double[] F = new double[totalDof];
    bool[] constrainedDofs = new bool[totalDof];
    
    // Calcola lunghezze e angoli degli elementi
    float[] elementLengths = new float[nElements];
    float[] elementAngles = new float[nElements];
    
    for (int i = 0; i < nElements; i++)
    {
        Vector3 delta = arcPoints[i + 1] - arcPoints[i];
        elementLengths[i] = delta.magnitude;
        elementAngles[i] = Mathf.Atan2(delta.y, delta.x);
    }
    
    // Assemblaggio matrice di rigidezza globale
    for (int e = 0; e < nElements; e++)
    {
        double L = elementLengths[e];
        double angle = elementAngles[e];
        
        double[,] kLocal = GetLocalStiffnessMatrix(L);
        double[,] T = GetTransformationMatrix(angle);
        double[,] Tt = TransposeMatrix(T);
        double[,] kGlobal = MultiplyMatrices(MultiplyMatrices(Tt, kLocal), T);
        
        int[] dofs = new int[6];
        for (int j = 0; j < 3; j++)
        {
            dofs[j] = e * dofPerNode + j;
            dofs[j + 3] = (e + 1) * dofPerNode + j;
        }
        
        for (int i = 0; i < 6; i++)
        {
            for (int j = 0; j < 6; j++)
            {
                K[dofs[i], dofs[j]] += kGlobal[i, j];
            }
        }
    }
    
    // Applica carichi (forze concentrate) usando la mappatura X reale
    for (int i = 0; i < loadPositions.Count; i++)
    {
        float relPos = Mathf.Clamp01(loadPositions[i]);
        ApplyLoadAtPosition(F, relPos, loadMagnitudes[i], arcPoints, dofPerNode);
    }
    
    // CORREZIONE 1: Penalizzazione scalata a 1e10 per evitare instabilità numerica
    double penalty = 1e10; 
    foreach (float supportPos in supportPositions)
    {
        float relPos = Mathf.Clamp01(supportPos);
        int[] constrainedDofIndices = GetConstrainedDofsAtPosition(relPos, arcPoints, dofPerNode);
        
        foreach (int dof in constrainedDofIndices)
        {
            K[dof, dof] += penalty;
            constrainedDofs[dof] = true;
        }
    }
    
    // Risolvi il sistema
    double[] displacements = SolveSystem(K, F);
    
    // Azzera gli spostamenti vincolati
    for (int i = 0; i < totalDof; i++)
    {
        if (constrainedDofs[i])
            displacements[i] = 0;
    }
    
    return CalculateResults(displacements, arcPoints, elementLengths, elementAngles, 
                          visualizationResolution, dofPerNode);
    }
    
    private static double[,] GetLocalStiffnessMatrix(double L)
    {
        double L2 = L * L;
        double L3 = L2 * L;
        
        return new double[6, 6]
        {
            {  EA/L,      0,          0,          -EA/L,      0,          0         },
            {  0,         12*EI/L3,   6*EI/L2,    0,          -12*EI/L3,  6*EI/L2   },
            {  0,         6*EI/L2,    4*EI/L,     0,          -6*EI/L2,   2*EI/L    },
            {  -EA/L,     0,          0,          EA/L,       0,          0         },
            {  0,         -12*EI/L3,  -6*EI/L2,   0,          12*EI/L3,   -6*EI/L2  },
            {  0,         6*EI/L2,    2*EI/L,     0,          -6*EI/L2,   4*EI/L    }
        };
    }
    
    private static double[,] GetTransformationMatrix(double angle)
    {
        double c = System.Math.Cos(angle);
        double s = System.Math.Sin(angle);
        
        return new double[6, 6]
        {
            {  c,  s,  0,  0,  0,  0 },
            { -s,  c,  0,  0,  0,  0 },
            {  0,  0,  1,  0,  0,  0 },
            {  0,  0,  0,  c,  s,  0 },
            {  0,  0,  0, -s,  c,  0 },
            {  0,  0,  0,  0,  0,  1 }
        };
    }
    
    private static double[,] MultiplyMatrices(double[,] A, double[,] B)
    {
        int rowsA = A.GetLength(0);
        int colsA = A.GetLength(1);
        int colsB = B.GetLength(1);
        
        double[,] result = new double[rowsA, colsB];
        
        for (int i = 0; i < rowsA; i++)
            for (int j = 0; j < colsB; j++)
            {
                double sum = 0;
                for (int k = 0; k < colsA; k++)
                    sum += A[i, k] * B[k, j];
                result[i, j] = sum;
            }
        
        return result;
    }
    
    private static double[,] TransposeMatrix(double[,] matrix)
    {
        int rows = matrix.GetLength(0);
        int cols = matrix.GetLength(1);
        double[,] result = new double[cols, rows];
        
        for (int i = 0; i < rows; i++)
            for (int j = 0; j < cols; j++)
                result[j, i] = matrix[i, j];
        
        return result;
    }
    
    // CORREZIONE 2 & 3: Ricerca dell'elemento tramite coordinata X e proiezione locale del carico
    private static void ApplyLoadAtPosition(double[] F, float relPos, 
        float magnitude, Vector3[] arcPoints, int dofPerNode)
    {
        float minX = arcPoints[0].x;
        float maxX = arcPoints[arcPoints.Length - 1].x;
        float targetX = Mathf.Lerp(minX, maxX, relPos);
        
        // Trova l'elemento corretto basandosi sulla X reale del cursore/carico
        int elementIndex = 0;
        for (int i = 0; i < arcPoints.Length - 1; i++)
        {
            if (targetX >= arcPoints[i].x && targetX <= arcPoints[i + 1].x)
            {
                elementIndex = i;
                break;
            }
        }
        
        int node1 = elementIndex;
        int node2 = elementIndex + 1;
        
        double L = Vector3.Distance(arcPoints[node1], arcPoints[node2]);
        double angle = Mathf.Atan2(arcPoints[node2].y - arcPoints[node1].y, arcPoints[node2].x - arcPoints[node1].x);
        
        // Calcola la distanza locale del carico dal primo nodo dell'elemento
        float t_x = (targetX - arcPoints[node1].x) / (arcPoints[node2].x - arcPoints[node1].x);
        double a = Mathf.Clamp01(t_x) * L;
        double b = L - a;
        
        double P_global_y = -magnitude; // Forza verticale verso il basso
        
        // Ruota il carico globale nel sistema di riferimento LOCALE dell'elemento inclinato
        double cosA = System.Math.Cos(angle);
        double sinA = System.Math.Sin(angle);
        
        double P_local_x = P_global_y * sinA; // Componente assiale locale
        double P_local_y = P_global_y * cosA; // Componente trasversale locale
        
        // Calcola le azioni d'incastro perfetto nel sistema locale
        double[] fLocal = new double[6];
        
        // Componente Assiale (ripartizione lineare)
        fLocal[0] = P_local_x * (b / L);
        fLocal[3] = P_local_x * (a / L);
        
        // Componente Trasversale (Formule di Hermite standard)
        fLocal[1] = P_local_y * (b * b * (L + 2 * a)) / (L * L * L);
        fLocal[2] = P_local_y * (a * b * b) / (L * L);
        fLocal[4] = P_local_y * (a * a * (L + 2 * b)) / (L * L * L);
        fLocal[5] = -P_local_y * (a * a * b) / (L * L);
        
        // Ruota le forze locali in coordinate GLOBALI (F_global = T^T * f_local)
        double[,] T = GetTransformationMatrix(angle);
        double[] fGlobal = new double[6];
        for (int i = 0; i < 6; i++)
        {
            double sum = 0;
            for (int k = 0; k < 6; k++)
                sum += T[k, i] * fLocal[k]; // Nota l'uso di T trasposta (T[k,i])
            fGlobal[i] = sum;
        }
        
        // Applica al vettore globale delle forze
        F[node1 * dofPerNode + 0] += fGlobal[0];
        F[node1 * dofPerNode + 1] += fGlobal[1];
        F[node1 * dofPerNode + 2] += fGlobal[2];
        
        F[node2 * dofPerNode + 0] += fGlobal[3];
        F[node2 * dofPerNode + 1] += fGlobal[4];
        F[node2 * dofPerNode + 2] += fGlobal[5];
    }
    
    // CORREZIONE 3b: Trova il nodo di vincolo più vicino basandosi sulla X reale
    private static int[] GetConstrainedDofsAtPosition(float relPos, 
        Vector3[] arcPoints, int dofPerNode)
    {
        float minX = arcPoints[0].x;
        float maxX = arcPoints[arcPoints.Length - 1].x;
        float targetX = Mathf.Lerp(minX, maxX, relPos);
        
        int nodeIndex = 0;
        float minDelta = float.MaxValue;
        
        for (int i = 0; i < arcPoints.Length; i++)
        {
            float delta = Mathf.Abs(arcPoints[i].x - targetX);
            if (delta < minDelta)
            {
                minDelta = delta;
                nodeIndex = i;
            }
        }
        
        return new int[] 
        { 
            nodeIndex * dofPerNode + 0,  // u_x
            nodeIndex * dofPerNode + 1   // u_y
        };
    }
    
    private static float CalculateArcLength(Vector3[] points)
    {
        float length = 0;
        for (int i = 0; i < points.Length - 1; i++)
            length += Vector3.Distance(points[i], points[i + 1]);
        return length;
    }
    
    private static double[] SolveSystem(double[,] K, double[] F)
    {
        int n = F.Length;
        double[,] A = new double[n, n];
        double[] b = new double[n];
        
        for (int i = 0; i < n; i++)
        {
            b[i] = F[i];
            for (int j = 0; j < n; j++)
                A[i, j] = K[i, j];
        }
        
        // Eliminazione di Gauss con pivoting parziale
        for (int i = 0; i < n; i++)
        {
            // Pivoting
            int pivot = i;
            double maxVal = System.Math.Abs(A[i, i]);
            
            for (int j = i + 1; j < n; j++)
            {
                if (System.Math.Abs(A[j, i]) > maxVal)
                {
                    maxVal = System.Math.Abs(A[j, i]);
                    pivot = j;
                }
            }
            
            if (pivot != i)
            {
                for (int k = i; k < n; k++)
                {
                    double temp = A[i, k];
                    A[i, k] = A[pivot, k];
                    A[pivot, k] = temp;
                }
                double tempB = b[i];
                b[i] = b[pivot];
                b[pivot] = tempB;
            }
            
            if (System.Math.Abs(A[i, i]) < 1e-30)
                continue;
            
            // Eliminazione
            for (int j = i + 1; j < n; j++)
            {
                double factor = A[j, i] / A[i, i];
                if (System.Math.Abs(factor) < 1e-15) continue;
                
                b[j] -= factor * b[i];
                for (int k = i; k < n; k++)
                    A[j, k] -= factor * A[i, k];
            }
        }
        
        // Sostituzione all'indietro
        double[] x = new double[n];
        for (int i = n - 1; i >= 0; i--)
        {
            double sum = 0;
            for (int j = i + 1; j < n; j++)
                sum += A[i, j] * x[j];
            
            if (System.Math.Abs(A[i, i]) > 1e-30)
                x[i] = (b[i] - sum) / A[i, i];
            else
                x[i] = 0;
        }
        
        return x;
    }
    
    private static ArcBeamData CalculateResults(double[] displacements, Vector3[] arcPoints,
        float[] elementLengths, float[] elementAngles, int resolution, int dofPerNode)
    {
        int nNodes = arcPoints.Length;
        
        ArcBeamData results = new ArcBeamData
        {
            axialForcePoints = new float[resolution],
            shearPoints = new float[resolution],
            momentPoints = new float[resolution],
            horizontalDeflection = new float[resolution],
            verticalDeflection = new float[resolution]
        };
        
        float totalArcLength = 0;
        float[] cumulativeLengths = new float[nNodes];
        cumulativeLengths[0] = 0;
        
        for (int i = 1; i < nNodes; i++)
        {
            totalArcLength += elementLengths[i - 1];
            cumulativeLengths[i] = totalArcLength;
        }
        
        for (int i = 0; i < resolution; i++)
        {
            float targetDist = (totalArcLength / (resolution - 1)) * i;
            
            int elementIndex = 0;
            float localDist = 0;
            
            for (int j = 0; j < nNodes - 1; j++)
            {
                if (targetDist <= cumulativeLengths[j + 1] || j == nNodes - 2)
                {
                    elementIndex = j;
                    localDist = targetDist - cumulativeLengths[j];
                    break;
                }
            }
            
            if (elementIndex >= nNodes - 1)
                elementIndex = nNodes - 2;
            
            float L = elementLengths[elementIndex];
            float angle = elementAngles[elementIndex];
            
            // Estrai spostamenti nodali globali
            double[] uGlobal = new double[6];
            for (int j = 0; j < 3; j++)
            {
                uGlobal[j] = displacements[elementIndex * dofPerNode + j];
                uGlobal[j + 3] = displacements[(elementIndex + 1) * dofPerNode + j];
            }
            
            // Trasforma in coordinate locali
            double[,] T = GetTransformationMatrix(angle);
            double[] uLocal = new double[6];
            for (int j = 0; j < 6; j++)
            {
                double sum = 0;
                for (int k = 0; k < 6; k++)
                    sum += T[j, k] * uGlobal[k];
                uLocal[j] = sum;
            }
            
            // Calcola spostamenti in coordinate globali
            double c = System.Math.Cos(angle);
            double s = System.Math.Sin(angle);
            
            double t_val = localDist / L;
            
            // Spostamento assiale (lineare)
            double u_local = uLocal[0] * (1.0 - t_val) + uLocal[3] * t_val;
            
            // Spostamento trasversale (cubico - Hermite)
            double v_local = uLocal[1] * (1 - 3*t_val*t_val + 2*t_val*t_val*t_val) 
                           + uLocal[2] * L * (t_val - 2*t_val*t_val + t_val*t_val*t_val)
                           + uLocal[4] * (3*t_val*t_val - 2*t_val*t_val*t_val)
                           + uLocal[5] * L * (-t_val*t_val + t_val*t_val*t_val);
            
            // Trasformazione a coordinate globali
            double ux = u_local * c - v_local * s;
            double uy = u_local * s + v_local * c;
            
            // Calcolo sollecitazioni
            // Sforzo normale (costante nell'elemento)
            double N = EA * (uLocal[3] - uLocal[0]) / L;
            
            // Taglio (costante nell'elemento)
            double V = EI * (
                12 * (uLocal[1] - uLocal[4]) / (L * L * L) + 
                6 * (uLocal[2] + uLocal[5]) / (L * L)
            );
            
            // Momento (varia linearmente lungo l'elemento)
            double x = localDist;
            double M = EI * (
                uLocal[1] * (-6 + 12*x/L) / (L * L) +
                uLocal[2] * (-4 + 6*x/L) / L +
                uLocal[4] * (6 - 12*x/L) / (L * L) +
                uLocal[5] * (-2 + 6*x/L) / L
            );
            
            results.horizontalDeflection[i] = (float)ux;
            results.verticalDeflection[i] = (float)uy;
            results.axialForcePoints[i] = (float)N;
            results.shearPoints[i] = (float)V;
            results.momentPoints[i] = (float)M;
        }
        
        return results;
    }
}