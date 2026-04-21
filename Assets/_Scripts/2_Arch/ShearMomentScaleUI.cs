using UnityEngine;
using UnityEngine.UI;

public class ShearMomentScaleUI : MonoBehaviour
{
    public BeamController beamController;
    public Slider shearScaleSlider;
    public Slider momentScaleSlider;
    public Text shearValueText;
    public Text momentValueText;
    public Toggle autoScaleToggle;

    void Start()
    {
        if (beamController == null)
            beamController = FindFirstObjectByType<BeamController>();

        // Inizializza slider con valori correnti e range ampliati
        if (shearScaleSlider != null)
        {
            shearScaleSlider.minValue = 0.01f;   // Era 0.001f
            shearScaleSlider.maxValue = 1.0f;    // Era 0.5f
            shearScaleSlider.value = beamController.shearDiagramScale;
            shearScaleSlider.onValueChanged.AddListener(OnShearScaleChanged);
        }

        if (momentScaleSlider != null)
        {
            momentScaleSlider.minValue = 0.001f;  // Era 0.0001f
            momentScaleSlider.maxValue = 0.2f;    // Era 0.1f
            momentScaleSlider.value = beamController.momentDiagramScale;
            momentScaleSlider.onValueChanged.AddListener(OnMomentScaleChanged);
        }

        if (autoScaleToggle != null)
        {
            autoScaleToggle.isOn = beamController.autoScaleDiagrams;
            autoScaleToggle.onValueChanged.AddListener(OnAutoScaleToggled);
        }

        UpdateTexts();
    }

    void OnShearScaleChanged(float value)
    {
        beamController.SetShearScale(value);
        UpdateTexts();
    }

    void OnMomentScaleChanged(float value)
    {
        beamController.SetMomentScale(value);
        UpdateTexts();
    }

    void OnAutoScaleToggled(bool isOn)
    {
        beamController.SetAutoScale(isOn);
        // Disabilita slider se auto-scale è attivo
        if (shearScaleSlider != null) shearScaleSlider.interactable = !isOn;
        if (momentScaleSlider != null) momentScaleSlider.interactable = !isOn;
    }

    void UpdateTexts()
    {
        if (shearValueText != null)
            shearValueText.text = $"Scala Taglio: {beamController.shearDiagramScale:F3}";
        if (momentValueText != null)
            momentValueText.text = $"Scala Momento: {beamController.momentDiagramScale:F4}";
    }

    void Update()
    {
        // Aggiorna slider se auto-scale è attivo e i valori cambiano
        if (beamController.autoScaleDiagrams)
        {
            if (shearScaleSlider != null && !Mathf.Approximately(shearScaleSlider.value, beamController.shearDiagramScale))
                shearScaleSlider.SetValueWithoutNotify(beamController.shearDiagramScale);
            if (momentScaleSlider != null && !Mathf.Approximately(momentScaleSlider.value, beamController.momentDiagramScale))
                momentScaleSlider.SetValueWithoutNotify(beamController.momentDiagramScale);
            UpdateTexts();
        }
    }
}