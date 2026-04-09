using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Componente modulare per la visualizzazione delle sollecitazioni.
/// Funziona sia con travi che con archi.
/// </summary>
public class StressVisualizer : MonoBehaviour
{
    [Header("Visualization Settings")]
    public VisualizationMode mode = VisualizationMode.None;
    public float maxExpectedStress = 100f;
    public float diagramScale = 0.1f;
    public float heatmapIntensity = 1f;
    
    [Header("Line Renderers")]
    public LineRenderer axialForceLine;
    public LineRenderer shearForceLine;
    public LineRenderer bendingMomentLine;
    
    [Header("Colors")]
    public Color axialColor = Color.green;
    public Color shearColor = Color.cyan;
    public Color momentColor = Color.magenta;
    
    private BeamController beamController;
    private ArchController archController;
    
    public enum VisualizationMode
    {
        None,
        AxialForce,
        ShearForce,
        BendingMoment,
        CombinedStress,
        Deflection
    }
    
    void Start()
    {
        beamController = GetComponent<BeamController>();
        archController = GetComponent<ArchController>();
    }
    
    void Update()
    {
        if (beamController == null || !beamController.HasResults) return;
        
        BeamData results = beamController.Results;
        bool isArch = (archController != null && archController.IsActive);
        
        switch (mode)
        {
            case VisualizationMode.AxialForce:
                VisualizeAxialForce(results, isArch);
                break;
            case VisualizationMode.ShearForce:
                VisualizeShearForce(results, isArch);
                break;
            case VisualizationMode.BendingMoment:
                VisualizeBendingMoment(results, isArch);
                break;
            case VisualizationMode.CombinedStress:
                VisualizeCombinedStress(results, isArch);
                break;
            case VisualizationMode.Deflection:
                VisualizeDeflection(results, isArch);
                break;
            default:
                HideAllDiagrams();
                break;
        }
    }
    
    private void VisualizeAxialForce(BeamData results, bool isArch)
    {
        // Per strutture ad arco, calcola la forza assiale
        float[] axialForces = CalculateAxialForces(results, isArch);
        RenderDiagram(axialForceLine, axialForces, axialColor);
    }
    
    private void VisualizeShearForce(BeamData results, bool isArch)
    {
        RenderDiagram(shearForceLine, results.shearPoints, shearColor, isArch);
    }
    
    private void VisualizeBendingMoment(BeamData results, bool isArch)
    {
        RenderDiagram(bendingMomentLine, results.momentPoints, momentColor, isArch);
    }
    
    private void VisualizeCombinedStress(BeamData results, bool isArch)
    {
        if (archController != null && isArch)
        {
            // Calcola lo stress combinato (N/A + M*y/I)
            float[] combinedStress = new float[results.stressPoints.Length];
            float[] axialForces = CalculateAxialForces(results, true);
            
            for (int i = 0; i < combinedStress.Length; i++)
            {
                // Formula semplificata per dimostrazione
                combinedStress[i] = Mathf.Abs(axialForces[i]) * 0.05f + results.stressPoints[i];
            }
            
            archController.VisualizeStress(combinedStress, maxExpectedStress);
        }
        else
        {
            // Per la trave, usa solo il momento
            RenderDiagram(bendingMomentLine, results.momentPoints, momentColor, false);
        }
    }
    
    private void VisualizeDeflection(BeamData results, bool isArch)
    {
        float visualScale = beamController.deflectionVisualScale;
        
        if (archController != null && isArch)
        {
            archController.ApplyDeflection(results.deflectionPoints, visualScale);
        }
    }
    
    private float[] CalculateAxialForces(BeamData results, bool isArch)
    {
        int n = results.momentPoints.Length;
        float[] axialForces = new float[n];
        
        if (!isArch || archController == null)
        {
            // Per travi, forza assiale è zero (carichi solo verticali)
            return axialForces;
        }
        
        // Per archi: N = H * cos(θ) + V * sin(θ)
        // Approssimazione semplificata per dimostrazione
        float H = 10f; // Spinta orizzontale (da calcolare dal solutore)
        
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / (n - 1);
            float worldX = Mathf.Lerp(archController.ArchStartX, archController.ArchEndX, t);
            Vector3 tangent = archController.GetArchTangentAtX(worldX);
            float cosTheta = tangent.x;
            float sinTheta = tangent.y;
            
            // Approssimazione
            axialForces[i] = -H * cosTheta - results.shearPoints[i] * sinTheta;
        }
        
        return axialForces;
    }
    
    private void RenderDiagram(LineRenderer line, float[] values, Color color, bool useArchY = false)
    {
        if (line == null) return;
        
        line.positionCount = values.Length;
        line.startColor = color;
        line.endColor = color;
        
        float startX = beamController.BeamStartX;
        float length = beamController.BeamLength;
        
        if (archController != null && archController.IsActive && useArchY)
        {
            startX = archController.ArchStartX;
            length = archController.ArchLength;
        }
        
        for (int i = 0; i < values.Length; i++)
        {
            float x = startX + (i * (length / (values.Length - 1)));
            float y = transform.position.y;
            
            if (archController != null && archController.IsActive && useArchY)
            {
                y = archController.GetArchHeightAtX(x);
            }
            
            Vector3 pos = new Vector3(x, y + values[i] * diagramScale, transform.position.z);
            line.SetPosition(i, pos);
        }
    }
    
    private void HideAllDiagrams()
    {
        if (axialForceLine != null) axialForceLine.positionCount = 0;
        if (shearForceLine != null) shearForceLine.positionCount = 0;
        if (bendingMomentLine != null) bendingMomentLine.positionCount = 0;
    }
    
    // Metodi pubblici per UI
    public void SetMode(int modeIndex) => mode = (VisualizationMode)modeIndex;
    public void SetDiagramScale(float scale) => diagramScale = scale;
}