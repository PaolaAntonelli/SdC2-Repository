using UnityEngine;

public class ArcInitializer : MonoBehaviour
{
    [Header("Setup Iniziale")]
    public ArcBeamController arcController;

    void Start()
    {
        if (arcController == null)
            arcController = FindFirstObjectByType<ArcBeamController>();

        // Lo scenario iniziale viene già configurato in ArcBeamController.Start()
    }

    // Metodo chiamabile da bottone UI per resettare
    public void ResetToDefault()
    {
        if (arcController != null)
            arcController.ResetStructure();
    }
}
