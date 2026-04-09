using UnityEngine;
using System;

[Serializable]
public class UIPanel
{
    public string name;      // Un ID semplice (es. "Intro", "Tutorial", "Fine")
    public GameObject panel; // Il riferimento al GameObject del pannello
}