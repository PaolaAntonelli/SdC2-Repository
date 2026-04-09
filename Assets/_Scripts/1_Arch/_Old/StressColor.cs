using UnityEngine;

[RequireComponent(typeof(Renderer))]
public class StressColor : MonoBehaviour
{
    public Gradient stressGradient = new Gradient();
    private Renderer rend;
    private BeamController beam;
    
    void Start()
    {
        rend = GetComponent<Renderer>();
        beam = FindFirstObjectByType<BeamController>();
        
        if (stressGradient.colorKeys.Length == 0)
        {
            stressGradient.SetKeys(
                new GradientColorKey[] { 
                    new GradientColorKey(Color.green, 0),
                    new GradientColorKey(Color.yellow, 0.5f),
                    new GradientColorKey(Color.red, 1)
                },
                new GradientAlphaKey[] { 
                    new GradientAlphaKey(1, 0),
                    new GradientAlphaKey(1, 1)
                }
            );
        }
    }
    
    void Update()
    {
        // Use HasResults flag instead of comparing struct to null
        if (beam == null || beam.currentStructure != BeamStructureType.Arch) return;
        if (!beam.HasResults) return;  // <-- FIX: use the flag
        
        float maxStress = 0;
        if (beam.Results.stressPoints != null)
        {
            foreach (float s in beam.Results.stressPoints)
                maxStress = Mathf.Max(maxStress, Mathf.Abs(s));
        }
        
        float t = Mathf.Clamp01(maxStress / 1000f);
        rend.material.color = stressGradient.Evaluate(t);
    }
}