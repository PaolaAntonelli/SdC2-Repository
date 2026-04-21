using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// Gestisce lo switching tra trave e arco e coordina i componenti.
/// </summary>
public class StructureManager : MonoBehaviour
{
    [Header("UI References")]
    public Toggle beamToggle;
    public Toggle archToggle;
    public Dropdown visualizationDropdown;
    public Slider scaleSlider;
    
    [Header("Components")]
    public BeamController beamController;
    public ArchController archController;
    public StressVisualizer stressVisualizer;
    
    public enum StructureMode { Beam, Arch }
    private StructureMode currentMode = StructureMode.Beam;
    
    void Start()
    {
        // Trova componenti se non assegnati
        if (beamController == null)
            beamController = FindFirstObjectByType<BeamController>();
        if (archController == null)
            archController = GetComponent<ArchController>();
        if (stressVisualizer == null)
            stressVisualizer = GetComponent<StressVisualizer>();
        
        // Configura UI
        SetupUI();
        
        // Imposta modalità iniziale
        SwitchToBeam();
    }
    
    private void SetupUI()
    {
        if (beamToggle != null)
            beamToggle.onValueChanged.AddListener((val) => { if (val) SwitchToBeam(); });
        
        if (archToggle != null)
            archToggle.onValueChanged.AddListener((val) => { if (val) SwitchToArch(); });
        
        if (visualizationDropdown != null)
        {
            visualizationDropdown.options.Clear();
            visualizationDropdown.options.Add(new Dropdown.OptionData("Nessuna"));
            visualizationDropdown.options.Add(new Dropdown.OptionData("Forza Assiale"));
            visualizationDropdown.options.Add(new Dropdown.OptionData("Taglio"));
            visualizationDropdown.options.Add(new Dropdown.OptionData("Momento"));
            visualizationDropdown.options.Add(new Dropdown.OptionData("Stress Combinato"));
            visualizationDropdown.options.Add(new Dropdown.OptionData("Deflessione"));
            
            visualizationDropdown.onValueChanged.AddListener((val) =>
            {
                if (stressVisualizer != null)
                    stressVisualizer.SetMode(val);
            });
        }
        
        if (scaleSlider != null)
        {
            scaleSlider.onValueChanged.AddListener((val) =>
            {
                if (beamController != null)
                    beamController.SetDiagramScale(val);
                if (stressVisualizer != null)
                    stressVisualizer.SetDiagramScale(val);
            });
        }
    }
    
    public void SwitchToBeam()
    {
        if (currentMode == StructureMode.Beam) return;
        
        archController?.DeactivateArchMode();
        beamController.currentStructure = BeamStructureType.Beam;
        currentMode = StructureMode.Beam;
        
        Debug.Log("StructureManager: Modalità Trave");
    }
    
    public void SwitchToArch()
    {
        if (currentMode == StructureMode.Arch) return;
        
        archController?.ActivateArchMode();
        beamController.currentStructure = BeamStructureType.Arch;
        currentMode = StructureMode.Arch;
        
        Debug.Log("StructureManager: Modalità Arco");
    }
    
    /// <summary>
    /// Ottiene tutti gli elementi (supporti e carichi) nella scena
    /// </summary>
    private GameObject[] GetAllElements()
    {
        var list = new List<GameObject>();
        list.AddRange(GameObject.FindGameObjectsWithTag("Support"));
        list.AddRange(GameObject.FindGameObjectsWithTag("Load"));
        return list.ToArray();
    }
    
    /// <summary>
    /// Resetta la struttura rimuovendo tutti i carichi e supporti
    /// </summary>
    public void ResetStructure()
    {
        // Distruggi tutti i carichi e supporti esistenti
        foreach (var obj in GetAllElements())
        {
            if (obj != null)
                Destroy(obj);
        }
        
        // Resetta lo stato dei risultati
        if (beamController != null)
            beamController.ResetResults();  // Useremo un nuovo metodo pubblico
        
        if (archController != null)
            archController.ResetMesh();
        
        // Ricrea la configurazione iniziale dopo un breve delay
        Invoke(nameof(SetupInitialScenario), 0.1f);
    }
    
    /// <summary>
    /// Configura la situazione iniziale con due supporti e un carico centrale
    /// </summary>
    private void SetupInitialScenario()
    {
        if (beamController == null) return;
        
        beamController.UpdateBeamDimensions();
        beamController.SpawnSupport(beamController.BeamStartX);
        beamController.SpawnSupport(beamController.BeamStartX + beamController.BeamLength);
        beamController.SpawnLoad(beamController.BeamStartX + (beamController.BeamLength / 2f));
    }
}