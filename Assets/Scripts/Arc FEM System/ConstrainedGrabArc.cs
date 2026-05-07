using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class ConstrainedGrabArc : XRGrabInteractable
{
    private ArcController arcController;
    private float initialY;
    private float initialZ;

    protected override void Awake()
    {
        base.Awake();
        arcController = Object.FindFirstObjectByType<ArcController>();
        initialY = transform.position.y;
        initialZ = transform.position.z;
    }

    public override void ProcessInteractable(XRInteractionUpdateOrder.UpdatePhase phase)
    {
        base.ProcessInteractable(phase);

        if (phase == XRInteractionUpdateOrder.UpdatePhase.Fixed || 
            phase == XRInteractionUpdateOrder.UpdatePhase.Dynamic)
        {
            if (arcController == null) return;

            Vector3 pos = transform.position;
            float minX = arcController.ArcStartX;
            float maxX = arcController.ArcStartX + arcController.ArcSpan;

            if (pos.x < minX || pos.x > maxX)
            {
                pos.x = Mathf.Clamp(pos.x, minX, maxX);
                pos.y = initialY;
                pos.z = initialZ;
                transform.position = pos;

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