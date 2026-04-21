using UnityEngine;
using System.Linq; // AGGIUNTO per supportare Max() sugli array

public class StressVisualizer : MonoBehaviour
{
    [Header("Visualization Settings")]
    public VisualizationMode mode = VisualizationMode.None;
    public float maxExpectedStress = 100f;
    public float diagramScale = 0.1f;
    public float shearScaleMultiplier = 1f;
    public float momentScaleMultiplier = 0.1f;
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
        float[] axialForces = CalculateAxialForces(results, isArch);
        float scale = beamController != null ? beamController.shearDiagramScale : diagramScale;
        RenderDiagram(axialForceLine, axialForces, scale, axialColor, isArch);
    }

    private void VisualizeShearForce(BeamData results, bool isArch)
    {
        float scale = beamController != null ? beamController.shearDiagramScale : diagramScale;
        RenderDiagram(shearForceLine, results.shearPoints, scale, shearColor, isArch);
    }

    private void VisualizeBendingMoment(BeamData results, bool isArch)
    {
        float scale = beamController != null ? beamController.momentDiagramScale : diagramScale * momentScaleMultiplier;
        RenderDiagram(bendingMomentLine, results.momentPoints, scale, momentColor, isArch);
    }

    private void VisualizeCombinedStress(BeamData results, bool isArch)
    {
        if (archController != null && isArch)
        {
            float[] combinedStress = new float[results.stressPoints.Length];
            
            for (int i = 0; i < combinedStress.Length; i++)
            {
                float t = (float)i / (combinedStress.Length - 1);
                float worldX = Mathf.Lerp(archController.ArchStartX, archController.ArchEndX, t);
                
                // Considera sia momento che sforzo normale
                float M = results.momentPoints[i];
                float N = CalculateAxialForceAtX(results, worldX, isArch);
                
                // Formula di Navier semplificata
                combinedStress[i] = Mathf.Abs(N) * 0.05f + Mathf.Abs(M) * 0.15f;
            }
            
            float maxStress = combinedStress.Max();
            if (maxStress < 0.1f) maxStress = 1f;
            
            archController.VisualizeStress(combinedStress, maxStress);
        }
        else
        {
            float scale = beamController != null ? beamController.momentDiagramScale : diagramScale;
            RenderDiagram(bendingMomentLine, results.momentPoints, scale, momentColor, false);
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
            return axialForces;
        }

        for (int i = 0; i < n; i++)
        {
            float t = (float)i / (n - 1);
            float worldX = Mathf.Lerp(archController.ArchStartX, archController.ArchEndX, t);
            axialForces[i] = CalculateAxialForceAtX(results, worldX, isArch);
        }

        return axialForces;
    }

    private float CalculateAxialForceAtX(BeamData results, float worldX, bool isArch)
    {
        if (!isArch || archController == null) return 0;
        
        Vector3 tangent = archController.GetArchTangentAtX(worldX);
        float cosTheta = tangent.x;
        float sinTheta = tangent.y;
        
        // Calcola H dalla geometria dell'arco
        float H = 10f; // Valore di fallback
        
        // Stima di H dal momento massimo
        if (results.momentPoints.Length > 0)
        {
            float maxMoment = results.momentPoints.Max();
            H = maxMoment / archController.archHeight;
        }
        
        // Interpola il taglio alla posizione x
        float t = Mathf.InverseLerp(archController.ArchStartX, archController.ArchEndX, worldX);
        int idx = Mathf.Clamp(Mathf.RoundToInt(t * (results.shearPoints.Length - 1)), 0, results.shearPoints.Length - 1);
        float V = results.shearPoints[idx];
        
        return -H * cosTheta - V * sinTheta;
    }

    private void RenderDiagram(LineRenderer line, float[] values, float scale, Color color, bool useArchY = false)
    {
        if (line == null) return;

        line.positionCount = values.Length;
        line.startColor = color;
        line.endColor = color;

        float startX = beamController.BeamStartX;
        float length = beamController.BeamLength;

        if (archController != null && archController.IsActive)
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

            Vector3 pos = new Vector3(x, y + values[i] * scale, transform.position.z);
            line.SetPosition(i, pos);
        }
    }

    private void HideAllDiagrams()
    {
        if (axialForceLine != null) axialForceLine.positionCount = 0;
        if (shearForceLine != null) shearForceLine.positionCount = 0;
        if (bendingMomentLine != null) bendingMomentLine.positionCount = 0;
    }

    public void SetMode(int modeIndex) => mode = (VisualizationMode)modeIndex;
    
    public void SetDiagramScale(float scale)
    {
        diagramScale = scale;
    }
}