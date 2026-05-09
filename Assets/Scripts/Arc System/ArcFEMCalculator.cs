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
    // Parametri del materiale CALIBRATI per scala Unity (1 unità ≈ 1 metro)
    // Per un arco in acciaio di dimensioni realistiche
    private const float E = 200000f;        // Modulo di Young ridotto per scala (kPa invece di GPa)
    private const float I = 0.001f;         // Momento d'inerzia per sezione 20cm x 20cm
    private const float A = 0.04f;          // Area sezione 20cm x 20cm
    private const float EI = E * I;         // Rigidezza flessionale
    private const float EA = E * A;         // Rigidezza assiale

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
        bool[] constrainedDofs = new bool[totalDof];  // Traccia i DOF vincolati
        
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
            double[,] kGlobal = MultiplyMatrices(TransposeMatrix(T), MultiplyMatrices(kLocal, T));
            
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
        
        // Applica carichi (forze concentrate)
        for (int i = 0; i < loadPositions.Count; i++)
        {
            float relPos = Mathf.Clamp01(loadPositions[i]);
            ApplyLoadAtPosition(F, relPos, loadMagnitudes[i], arcPoints, nNodes, dofPerNode);
        }
        
        // Applica vincoli (appoggi) - metodo di penalizzazione
        double penalty = 1e25;
        foreach (float supportPos in supportPositions)
        {
            float relPos = Mathf.Clamp01(supportPos);
            int[] constrainedDofIndices = GetConstrainedDofsAtPosition(relPos, arcPoints, nNodes, dofPerNode);
            
            foreach (int dof in constrainedDofIndices)
            {
                K[dof, dof] += penalty;
                constrainedDofs[dof] = true;
            }
        }
        
        // Risolvi il sistema
        double[] displacements = SolveSystem(K, F);
        
        // Azzera gli spostamenti vincolati (per pulizia numerica)
        for (int i = 0; i < totalDof; i++)
        {
            if (constrainedDofs[i])
                displacements[i] = 0;
        }
        
        return CalculateResults(displacements, arcPoints, elementLengths, elementAngles, 
                              visualizationResolution, constrainedDofs, dofPerNode);
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
    
    private static void ApplyLoadAtPosition(double[] F, float relPos, 
        float magnitude, Vector3[] arcPoints, int nNodes, int dofPerNode)
    {
        float totalLength = CalculateArcLength(arcPoints);
        float targetDist = relPos * totalLength;
        
        float currentDist = 0;
        int elementIndex = 0;
        float localDist = 0;
        
        for (int i = 0; i < arcPoints.Length - 1; i++)
        {
            float segmentLength = Vector3.Distance(arcPoints[i], arcPoints[i + 1]);
            if (currentDist + segmentLength >= targetDist)
            {
                elementIndex = i;
                localDist = targetDist - currentDist;
                break;
            }
            currentDist += segmentLength;
        }
        
        // Applica il carico direttamente come forza verticale globale
        // (assumendo che il carico sia verticale verso il basso)
        int node1 = elementIndex;
        int node2 = elementIndex + 1;
        float L = Vector3.Distance(arcPoints[node1], arcPoints[node2]);
        float a = localDist;
        float b = L - a;
        
        double P = magnitude;  // Positivo verso il basso
        
        // Carichi nodali equivalenti per carico concentrato
        // Questi sono già in coordinate globali perché applichiamo forze verticali
        F[node1 * dofPerNode + 1] += -P * b * b * (a + 2 * b) / (L * L * L);
        F[node2 * dofPerNode + 1] += -P * a * a * (b + 2 * a) / (L * L * L);
        F[node1 * dofPerNode + 2] += -P * a * b * b / (L * L);
        F[node2 * dofPerNode + 2] += P * a * a * b / (L * L);
    }
    
    private static int[] GetConstrainedDofsAtPosition(float relPos, 
        Vector3[] arcPoints, int nNodes, int dofPerNode)
    {
        float totalLength = CalculateArcLength(arcPoints);
        float targetDist = relPos * totalLength;
        
        float currentDist = 0;
        int nodeIndex = 0;
        
        for (int i = 0; i < arcPoints.Length; i++)
        {
            if (i > 0)
                currentDist += Vector3.Distance(arcPoints[i-1], arcPoints[i]);
            
            if (currentDist >= targetDist || i == arcPoints.Length - 1)
            {
                nodeIndex = Mathf.Clamp(i, 0, nNodes - 1);
                break;
            }
        }
        
        // Per un appoggio semplice: blocca sia X che Y
        return new int[] 
        { 
            nodeIndex * dofPerNode + 0,  // Blocca spostamento orizzontale
            nodeIndex * dofPerNode + 1   // Blocca spostamento verticale
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
            
            for (int j = i + 1; j < n; j++)
            {
                double factor = A[j, i] / A[i, i];
                if (System.Math.Abs(factor) < 1e-15) continue;
                
                b[j] -= factor * b[i];
                for (int k = i; k < n; k++)
                    A[j, k] -= factor * A[i, k];
            }
        }
        
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
        float[] elementLengths, float[] elementAngles, int resolution, 
        bool[] constrainedDofs, int dofPerNode)
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
            
            // 1. Variabile normalizzata
            double t = localDist / L;
            double t2 = t * t;
            double t3 = t2 * t;

            // 2. Interpolazione spostamento assiale (lineare)
            double u_local = uLocal[0] * (1.0 - t) + uLocal[3] * t;

            // 3. Funzioni di forma di Hermite per flessione cubica
            double N1 = 1.0 - 3.0 * t2 + 2.0 * t3;
            double N2 = L * (t - 2.0 * t2 + t3);
            double N3 = 3.0 * t2 - 2.0 * t3;
            double N4 = L * (-t2 + t3);

            // 4. Interpolazione spostamento trasversale (cubico)
            double v_local = uLocal[1] * N1 + uLocal[2] * N2 + uLocal[4] * N3 + uLocal[5] * N4;

            // 5. Trasformazione dalle coordinate locali (elemento) a globali (x, y assoluti)
            double c = System.Math.Cos(angle);
            double s = System.Math.Sin(angle);
            
            double ux = u_local * c - v_local * s;
            double uy = u_local * s + v_local * c;
            
            // Calcola forze interne
            double x = localDist;
            double L2 = L * L;
            double L3 = L2 * L;
            
            // Sforzo normale
            double N = EA * (uLocal[3] - uLocal[0]) / L;
            
            // Taglio (costante nell'elemento per carichi concentrati ai nodi)
            double V = -EI * (
                12 * uLocal[1] / L3 + 6 * uLocal[2] / L2 
                - 12 * uLocal[4] / L3 + 6 * uLocal[5] / L2
            );
            
            // Momento (varia linearmente)
            double M = EI * (
                (6 / L2 - 12 * x / L3) * uLocal[1] 
                + (4 / L - 6 * x / L2) * uLocal[2]
                + (-6 / L2 + 12 * x / L3) * uLocal[4]
                + (2 / L - 6 * x / L2) * uLocal[5]
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