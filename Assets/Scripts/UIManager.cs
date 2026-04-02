using System.Collections.Generic;
using UnityEngine;

public class UIManager : MonoBehaviour
{
    [Header("Configurazione Pannelli")]
    public List<UIPanel> panels;
    public string startingPanel = "Intro";

    void Start()
    {
        ShowPanel(startingPanel);
    }

    // Metodo principale per cambiare pannello tramite nome
    public void ShowPanel(string panelName)
    {
        foreach (var p in panels)
        {
            if (p.panel != null)
            {
                p.panel.SetActive(p.name == panelName);
            }
        }
    }

    // Utile per i bottoni "Avanti" o "Indietro"
    public void ShowPanelByIndex(int index)
    {
        for (int i = 0; i < panels.Count; i++)
        {
            panels[i].panel.SetActive(i == index);
        }
    }
}