using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public struct ArcBeamData
{
    public float[] axialForcePoints;      // Sforzo normale
    public float[] shearPoints;           // Taglio
    public float[] momentPoints;          // Momento flettente
    public float[] horizontalDeflection;  // Deformazione orizzontale
    public float[] verticalDeflection;    // Deformazione verticale
}

public static class ArcFEMCalculator
{
    // Proprietà del materiale
    private const float E = 210000000f;    // Modulo di Young (Pa) - acciaio
    private const float I = 0.0001f;       // Momento d'inerzia (m^4)
    private const float A = 0.01f;         // Area della sezione (m^2)
    private const float EI = E * I;
    private const float EA = E * A;

    public static ArcBeamData CalculateArcFEM(
        Vector3[] arcPoints,              // Punti che definiscono la geometria dell'arco
        List<float> loadPositions,        // Posizioni relative dei carichi (0-1 lungo l'arco)
        List<float> loadMagnitudes,       // Magnitudine dei carichi (positivo = verso il basso)
        List<float> supportPositions,     // Posizioni relative degli appoggi (0-1 lungo l'arco)
        int visualizationResolution)      // Risoluzione per la visualizzazione
    {
        int nNodes = arcPoints.Length;
        int nElements = nNodes - 1;
        
        // 3 gradi di libertà per nodo: u_x, u_y, theta_z
        int dofPerNode = 3;
        int totalDof = nNodes * dofPerNode;
        
        // Inizializza matrici globali
        double[,] K = new double[totalDof, totalDof];
        double[] F = new double[totalDof];
        
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
            
            // Matrice di rigidezza locale (6x6 per elemento trave 2D)
            double[,] kLocal = GetLocalStiffnessMatrix(L);
            
            // Matrice di trasformazione
            double[,] T = GetTransformationMatrix(angle);
            
            // Trasforma in coordinate globali: K_global = T^T * K_local * T
            double[,] kGlobal = MultiplyMatrices(TransposeMatrix(T), MultiplyMatrices(kLocal, T));
            
            // Assembla nella matrice globale
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
        
        // Applica carichi
        for (int i = 0; i < loadPositions.Count; i++)
        {
            float relPos = Mathf.Clamp01(loadPositions[i]);
            ApplyLoadAtPosition(K, F, relPos, loadMagnitudes[i], arcPoints, nNodes, dofPerNode);
        }
        
        // Applica vincoli (appoggi)
        foreach (float supportPos in supportPositions)
        {
            float relPos = Mathf.Clamp01(supportPos);
            ApplySupportAtPosition(K, relPos, arcPoints, nNodes, dofPerNode);
        }
        
        // Risolvi il sistema
        double[] displacements = SolveSystem(K, F);
        
        // Calcola risultati per la visualizzazione
        return CalculateResults(displacements, arcPoints, elementLengths, elementAngles, 
                              visualizationResolution);
    }
    
    private static double[,] GetLocalStiffnessMatrix(double L)
    {
        double L2 = L * L;
        double L3 = L2 * L;
        
        // Matrice di rigidezza locale 6x6 per elemento trave 2D
        // Ordine DOF: [u1, v1, θ1, u2, v2, θ2]
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
        {
            for (int j = 0; j < colsB; j++)
            {
                result[i, j] = 0;
                for (int k = 0; k < colsA; k++)
                {
                    result[i, j] += A[i, k] * B[k, j];
                }
            }
        }
        
        return result;
    }
    
    private static double[,] TransposeMatrix(double[,] matrix)
    {
        int rows = matrix.GetLength(0);
        int cols = matrix.GetLength(1);
        double[,] result = new double[cols, rows];
        
        for (int i = 0; i < rows; i++)
        {
            for (int j = 0; j < cols; j++)
            {
                result[j, i] = matrix[i, j];
            }
        }
        
        return result;
    }
    
    private static void ApplyLoadAtPosition(double[,] K, double[] F, float relPos, 
        float magnitude, Vector3[] arcPoints, int nNodes, int dofPerNode)
    {
        // Trova l'elemento più vicino alla posizione del carico
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
        
        // Applica carico distribuito sull'elemento come carichi nodali equivalenti
        int node1 = elementIndex;
        int node2 = elementIndex + 1;
        float L = Vector3.Distance(arcPoints[node1], arcPoints[node2]);
        float a = localDist;
        float b = L - a;
        
        // Carichi nodali equivalenti per carico concentrato su elemento trave
        double P = magnitude;
        
        // Forze verticali (in coordinate locali, poi trasformate)
        F[node2 * dofPerNode + 1] += -P * a * a * (3 * a + b) / (L * L * L);  // Fy2
        F[node1 * dofPerNode + 1] += -P * b * b * (a + 3 * b) / (L * L * L);  // Fy1
        F[node2 * dofPerNode + 2] += -P * a * a * b / (L * L);                 // M2
        F[node1 * dofPerNode + 2] += P * a * b * b / (L * L);                  // M1
    }
    
    private static void ApplySupportAtPosition(double[,] K, float relPos, 
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
        
        // Applica vincolo (penalizzazione)
        double penalty = 1e15;
        int dofV = nodeIndex * dofPerNode + 1;  // Blocca spostamento verticale
        int dofH = nodeIndex * dofPerNode + 0;  // Blocca spostamento orizzontale
        
        K[dofV, dofV] += penalty;
        K[dofH, dofH] += penalty;
    }
    
    private static float CalculateArcLength(Vector3[] points)
    {
        float length = 0;
        for (int i = 0; i < points.Length - 1; i++)
        {
            length += Vector3.Distance(points[i], points[i + 1]);
        }
        return length;
    }
    
    private static double[] SolveSystem(double[,] K, double[] F)
    {
        int n = F.Length;
        double[] x = new double[n];
        
        // Copia le matrici per non modificarle
        double[,] A = new double[n, n];
        double[] b = new double[n];
        
        for (int i = 0; i < n; i++)
        {
            b[i] = F[i];
            for (int j = 0; j < n; j++)
            {
                A[i, j] = K[i, j];
            }
        }
        
        // Eliminazione di Gauss con pivoting
        for (int i = 0; i < n; i++)
        {
            // Pivoting
            int pivot = i;
            for (int j = i + 1; j < n; j++)
            {
                if (System.Math.Abs(A[j, i]) > System.Math.Abs(A[pivot, i]))
                    pivot = j;
            }
            
            // Scambia righe
            for (int k = i; k < n; k++)
            {
                double temp = A[i, k];
                A[i, k] = A[pivot, k];
                A[pivot, k] = temp;
            }
            double tempB = b[i];
            b[i] = b[pivot];
            b[pivot] = tempB;
            
            if (System.Math.Abs(A[i, i]) < 1e-15)
                continue;
            
            // Eliminazione
            for (int j = i + 1; j < n; j++)
            {
                double factor = A[j, i] / A[i, i];
                b[j] -= factor * b[i];
                for (int k = i; k < n; k++)
                {
                    A[j, k] -= factor * A[i, k];
                }
            }
        }
        
        // Sostituzione all'indietro
        for (int i = n - 1; i >= 0; i--)
        {
            double sum = 0;
            for (int j = i + 1; j < n; j++)
            {
                sum += A[i, j] * x[j];
            }
            if (System.Math.Abs(A[i, i]) > 1e-15)
                x[i] = (b[i] - sum) / A[i, i];
            else
                x[i] = 0;
        }
        
        return x;
    }
    
    private static ArcBeamData CalculateResults(double[] displacements, Vector3[] arcPoints,
        float[] elementLengths, float[] elementAngles, int resolution)
    {
        int nNodes = arcPoints.Length;
        int dofPerNode = 3;
        
        ArcBeamData results = new ArcBeamData
        {
            axialForcePoints = new float[resolution],
            shearPoints = new float[resolution],
            momentPoints = new float[resolution],
            horizontalDeflection = new float[resolution],
            verticalDeflection = new float[resolution]
        };
        
        // Calcola lunghezza totale dell'arco
        float totalArcLength = 0;
        float[] cumulativeLengths = new float[nNodes];
        cumulativeLengths[0] = 0;
        
        for (int i = 1; i < nNodes; i++)
        {
            totalArcLength += elementLengths[i - 1];
            cumulativeLengths[i] = totalArcLength;
        }
        
        // Per ogni punto di visualizzazione
        for (int i = 0; i < resolution; i++)
        {
            float targetDist = (totalArcLength / (resolution - 1)) * i;
            
            // Trova l'elemento corrispondente
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
            
            // Estrai spostamenti nodali dell'elemento
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
                uLocal[j] = 0;
                for (int k = 0; k < 6; k++)
                {
                    uLocal[j] += T[j, k] * uGlobal[k];
                }
            }
            
            // Calcola forze interne in coordinate locali
            double x = localDist;
            double L2 = L * L;
            double L3 = L2 * L;
            
            // Sforzo normale (costante nell'elemento)
            double N = EA * (uLocal[3] - uLocal[0]) / L;
            
            // Taglio
            double V = EI * (
                12 * uLocal[1] / L3 + 6 * uLocal[2] / L2 
                - 12 * uLocal[4] / L3 + 6 * uLocal[5] / L2
            );
            
            // Momento
            double M = EI * (
                (6 / L2 - 12 * x / L3) * uLocal[1] 
                + (4 / L - 6 * x / L2) * uLocal[2]
                + (-6 / L2 + 12 * x / L3) * uLocal[4]
                + (2 / L - 6 * x / L2) * uLocal[5]
            );
            
            // Deformazioni (in coordinate globali)
            results.horizontalDeflection[i] = (float)uGlobal[0];  // u_x globale
            results.verticalDeflection[i] = (float)uGlobal[1];    // u_y globale
            results.axialForcePoints[i] = (float)N;
            results.shearPoints[i] = (float)V;
            results.momentPoints[i] = -(float)M;  // Convenzione di segno
        }
        
        return results;
    }
}