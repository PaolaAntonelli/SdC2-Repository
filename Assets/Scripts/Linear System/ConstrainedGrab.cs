using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

// Ereditiamo da XRGrabInteractable per mantenerne tutte le funzionalit�
public class ConstrainedGrab : UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable
{
    private BeamController beamController;
    private float initialY;
    private float initialZ;

    protected override void Awake()
    {
        base.Awake();
        beamController = Object.FindFirstObjectByType<BeamController>();
        
        // Salviamo l'assegnazione iniziale per evitare micro-spostamenti verticali
        initialY = transform.position.y;
        initialZ = transform.position.z;
    }

    // Questo � il cuore di XRI: viene chiamato pi� volte per frame
    public override void ProcessInteractable(XRInteractionUpdateOrder.UpdatePhase phase)
    {
        // 1. Lasciamo che Unity calcoli il movimento verso la mano virtuale
        base.ProcessInteractable(phase);

        // 2. Subito dopo (nella fase della fisica o del rendering), correggiamo la posizione
        if (phase == XRInteractionUpdateOrder.UpdatePhase.Fixed || phase == XRInteractionUpdateOrder.UpdatePhase.Dynamic)
        {
            if (beamController == null) return;

            Vector3 pos = transform.position;
            float minX = beamController.BeamStartX;
            float maxX = beamController.BeamStartX + beamController.BeamLength;

            // Se stiamo per uscire, blocchiamo la X
            if (pos.x < minX || pos.x > maxX)
            {
                pos.x = Mathf.Clamp(pos.x, minX, maxX);
                
                // Manteniamo rigorosamente Y e Z fissi
                pos.y = initialY;
                pos.z = initialZ;
                transform.position = pos;

                // Azzeriamo la velocit� fisica per eliminare ogni tremolio residuo
                Rigidbody rb = GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }
            }
        }
    }
}