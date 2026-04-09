using UnityEngine;
using UnityEngine.UI;

public class StructureUI : MonoBehaviour
{
    public BeamController beamController;
    public Dropdown structureDropdown;
    public Toggle stressToggle;
    public Slider archHeightSlider;
    public Text archHeightText;
    
    void Start()
    {
        if (structureDropdown != null)
        {
            structureDropdown.ClearOptions();
            structureDropdown.AddOptions(new System.Collections.Generic.List<string> { "Beam", "Arch" });
            structureDropdown.onValueChanged.AddListener(OnStructureChanged);
        }
        
        if (stressToggle != null)
            stressToggle.onValueChanged.AddListener(OnStressToggled);
            
        if (archHeightSlider != null)
        {
            archHeightSlider.onValueChanged.AddListener(OnArchHeightChanged);
            archHeightSlider.gameObject.SetActive(false);
        }
    }
    
    void OnStructureChanged(int index)
    {
        if (beamController == null) return;
        
        beamController.currentStructure = (BeamStructureType)index;
        
        if (archHeightSlider != null)
            archHeightSlider.gameObject.SetActive(index == 1);
    }
    
    void OnStressToggled(bool isOn)
    {
        var stressColor = FindFirstObjectByType<StressColor>();
        if (stressColor != null)
            stressColor.enabled = isOn;
    }
    
    void OnArchHeightChanged(float value)
    {
        if (beamController != null)
        {
            beamController.archHeight = value;
            if (archHeightText != null)
                archHeightText.text = $"Arch Height: {value:F1}";
        }
    }
}