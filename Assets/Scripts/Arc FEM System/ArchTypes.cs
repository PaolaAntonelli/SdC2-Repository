// ============================================================
// File: Arch System/ArchTypes.cs
// Descrizione: Tipi esclusivi per il sistema ad arco
// ============================================================

using UnityEngine;
using System.Collections.Generic;

namespace ArchSystem
{
    public enum ArchSolverType
    {
        FEM_3DOF,   // Solo spostamento verticale
        FEM_6DOF    // Spostamento assiale + verticale
    }

    [System.Serializable]
    public struct ArchNodeData
    {
        public Vector2 position;    // Posizione nel piano XZ o XY
        public float u;            // Spostamento assiale
        public float v;            // Spostamento trasversale
        public float theta;        // Rotazione
    }

    [System.Serializable]
    public struct ArchResults
    {
        public float[] momentPoints;
        public float[] shearPoints;
        public float[] axialPoints;
        public float[] deflectionPoints;
        public float[] axialDisplacement;
        public Vector2[] deformedPositions;
    }

    [System.Serializable]
    public struct ArchConfig
    {
        public float radius;
        public float angleDegrees;
        public int segments;
        public float height;
        public float EI;
        public float EA;
    }
}