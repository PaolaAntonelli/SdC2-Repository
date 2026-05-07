using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public struct ArcData
{
    public float[] momentPoints;
    public float[] shearPoints;
    public float[] normalPoints;
    public float[] deflectionPoints;
    public float[] horizontalReaction;  // Reazione orizzontale (importante per arco)
    public float[] verticalReaction;
}

public static class ArcMath
{
    // FEM specifico per arco considerando flessione + sforzo normale
    public static ArcData CalculateFEM(float span, float rise, Vector3[] arcPath, 
                                        List<float> loadX, List<float> loadP, 
                                        List<float> suppX, int resolution,
                                        float EI, float EA)
    {
        int nNodes = resolution;
        int nElems = nNodes - 1;
        float Le = span / nElems;
        int ndof = nNodes * 2; // [v, theta] per nodo
        
        double[,] K = new double[ndof, ndof];
        double[] F = new double[ndof];
        
        // Assemblaggio matrice di rigidezza (flessione + assiale)
        for (int e = 0; e < nElems; e++)
        {
            double theta = GetArcAngle(arcPath[e], arcPath[e + 1]);
            double[,] ke = GetArcElementStiffness(Le, EI, EA, theta);
            int[] idx = { e * 2, e * 2 + 1, (e + 1) * 2, (e + 1) * 2 + 1 };
            for (int i = 0; i < 4; i++)
                for (int j = 0; j < 4; j++)
                    K[idx[i], idx[j]] += ke[i, j];
        }
        
        // Carichi verticali (convertiti in forze nodali)
        for (int i = 0; i < loadX.Count; i++)
        {
            int nodeIdx = Mathf.Clamp(Mathf.RoundToInt((loadX[i] / span) * nElems), 0, nNodes - 1);
            F[nodeIdx * 2] -= loadP[i];
        }
        
        // Vincoli (appoggi semplici agli estremi con possibilità orizzontale)
        foreach (float sx in suppX)
        {
            int nodeIdx = Mathf.Clamp(Mathf.RoundToInt((sx / span) * nElems), 0, nNodes - 1);
            
            if (Mathf.Abs(sx) < 0.01f) // Appoggio sinistro
            {
                K[nodeIdx * 2, nodeIdx * 2] += 1e12; // Blocca verticale
                // K[nodeIdx * 2 + 1, nodeIdx * 2 + 1] += 1e12; // Sblocca rotazione
            }
            else if (Mathf.Abs(sx - span) < 0.01f) // Appoggio destro
            {
                K[nodeIdx * 2, nodeIdx * 2] += 1e12; // Blocca verticale
                // Orizzontale libero per permettere dilatazione
            }
            else // Appoggio intermedio
            {
                K[nodeIdx * 2, nodeIdx * 2] += 1e12;
            }
        }
        
        // Risoluzione
        double[] u = Solve(K, F);
        
        // Calcolo risultati
        ArcData data = new ArcData
        {
            momentPoints = new float[resolution],
            shearPoints = new float[resolution],
            normalPoints = new float[resolution],
            deflectionPoints = new float[resolution],
            horizontalReaction = new float[2],
            verticalReaction = new float[2]
        };
        
        for (int i = 0; i < nNodes; i++)
        {
            data.deflectionPoints[i] = -(float)u[i * 2];
            
            if (i < nNodes - 1)
            {
                double[] ue = { u[i * 2], u[i * 2 + 1], u[(i + 1) * 2], u[(i + 1) * 2 + 1] };
                double theta = GetArcAngle(arcPath[i], arcPath[i + 1]);
                
                // Momento flettente
                data.momentPoints[i] = -(float)(EI * (ue[0] * (-6 / Le / Le) + ue[1] * (-4 / Le) + 
                                                       ue[2] * (6 / Le / Le) + ue[3] * (-2 / Le)));
                
                // Taglio (convertito in direzione locale dell'arco)
                data.shearPoints[i] = (float)(EI * (ue[0] * (12 / Le / Le / Le) + ue[1] * (6 / Le / Le) +
                                                     ue[2] * (-12 / Le / Le / Le) + ue[3] * (6 / Le / Le)));
                
                // Sforzo normale (dalla componente assiale)
                double axialStrain = (ue[2] - ue[0]) / Le;
                data.normalPoints[i] = (float)(EA * axialStrain);
            }
        }
        
        // Reazioni vincolari
        data.verticalReaction[0] = (float)(K[0, 0] * u[0] + K[0, 2] * u[2] + K[0, 4] * u[4]);
        data.verticalReaction[1] = (float)(K[(nNodes-1)*2, (nNodes-1)*2] * u[(nNodes-1)*2] + 
                                            K[(nNodes-1)*2, (nNodes-3)*2] * u[(nNodes-3)*2]);
        
        return data;
    }
    
    // Metodo analitico approssimato per arco parabolico (teoria dell'arco a tre cerniere)
    public static ArcData CalculateAnalyticParabolic(float span, float rise, 
                                                       List<float> loadX, List<float> loadP,
                                                       List<float> suppX, int resolution,
                                                       float EI)
    {
        ArcData data = new ArcData
        {
            momentPoints = new float[resolution],
            shearPoints = new float[resolution],
            normalPoints = new float[resolution],
            deflectionPoints = new float[resolution]
        };
        
        // Calcola reazioni vincolari
        float totalLoad = 0;
        float momentAtCenter = 0;
        
        for (int i = 0; i < loadX.Count; i++)
        {
            totalLoad += loadP[i];
            momentAtCenter += loadP[i] * (span / 2 - loadX[i]);
        }
        
        float verticalReaction = totalLoad / 2;
        float horizontalThrust = momentAtCenter / rise;
        
        for (int i = 0; i < resolution; i++)
        {
            float x = (span / (resolution - 1)) * i;
            float y = 4 * rise * (x / span) * (1 - x / span);
            
            // Momento nell'arco (come trave + effetto spinta)
            float bendingMoment = 0;
            for (int j = 0; j < loadX.Count; j++)
                if (x > loadX[j])
                    bendingMoment += loadP[j] * (x - loadX[j]);
            
            bendingMoment -= verticalReaction * x;
            bendingMoment += horizontalThrust * y;
            
            data.momentPoints[i] = bendingMoment;
            
            // Taglio (derivata del momento lungo l'arco)
            if (i > 0)
                data.shearPoints[i] = -(data.momentPoints[i] - data.momentPoints[i-1]) / (span / resolution);
            
            // Sforzo normale (dalla geometria)
            float angle = Mathf.Atan2(4 * rise * (1 - 2 * x / span), span);
            data.normalPoints[i] = -(horizontalThrust * Mathf.Cos(angle) + verticalReaction * Mathf.Sin(angle));
            
            // Deformata approssimata
            data.deflectionPoints[i] = (bendingMoment * span * span) / (EI * 48) *
                                         (3 * (x / span) - 4 * Mathf.Pow(x / span, 3));
        }
        
        return data;
    }
    
    private static double[,] GetArcElementStiffness(float l, float EI, float EA, double theta)
    {
        double l2 = l * l;
        double l3 = l2 * l;
        double C = System.Math.Cos(theta);
        double S = System.Math.Sin(theta);
        
        // Matrice di rigidezza nel sistema locale (con flessione e assiale)
        double[,] kLocal = new double[4, 4]
        {
            { EA/l, 0, -EA/l, 0 },
            { 0, 12*EI/l3, 0, 6*EI/l2 },
            { -EA/l, 0, EA/l, 0 },
            { 0, 6*EI/l2, 0, 4*EI/l }
        };
        
        // Matrice di trasformazione
        double[,] T = new double[4, 4]
        {
            { C, S, 0, 0 },
            { -S, C, 0, 0 },
            { 0, 0, C, S },
            { 0, 0, -S, C }
        };
        
        // Trasformazione: k_global = T^T * k_local * T
        double[,] result = new double[4, 4];
        for (int i = 0; i < 4; i++)
            for (int j = 0; j < 4; j++)
                for (int k = 0; k < 4; k++)
                    for (int l_m = 0; l_m < 4; l_m++)
                        result[i, j] += T[k, i] * kLocal[k, l_m] * T[l_m, j];
        
        return result;
    }
    
    private static double GetArcAngle(Vector3 p1, Vector3 p2)
    {
        return System.Math.Atan2(p2.y - p1.y, p2.x - p1.x);
    }
    
    private static double[] Solve(double[,] A, double[] b)
    {
        int n = b.Length;
        double[,] A_copy = (double[,])A.Clone();
        double[] b_copy = (double[])b.Clone();
        
        for (int i = 0; i < n; i++)
        {
            int pivot = i;
            for (int j = i + 1; j < n; j++)
                if (System.Math.Abs(A_copy[j, i]) > System.Math.Abs(A_copy[pivot, i]))
                    pivot = j;
                    
            for (int k = i; k < n; k++)
            {
                double t = A_copy[i, k];
                A_copy[i, k] = A_copy[pivot, k];
                A_copy[pivot, k] = t;
            }
            
            double tempB = b_copy[i];
            b_copy[i] = b_copy[pivot];
            b_copy[pivot] = tempB;
            
            if (System.Math.Abs(A_copy[i, i]) < 1e-12) continue;
            
            for (int j = i + 1; j < n; j++)
            {
                double f = A_copy[j, i] / A_copy[i, i];
                b_copy[j] -= f * b_copy[i];
                for (int k = i; k < n; k++)
                    A_copy[j, k] -= f * A_copy[i, k];
            }
        }
        
        double[] x = new double[n];
        for (int i = n - 1; i >= 0; i--)
        {
            double s = 0;
            for (int j = i + 1; j < n; j++)
                s += A_copy[i, j] * x[j];
                
            if (System.Math.Abs(A_copy[i, i]) > 1e-12)
                x[i] = (b_copy[i] - s) / A_copy[i, i];
        }
        
        return x;
    }
}