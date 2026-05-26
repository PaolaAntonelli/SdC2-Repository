using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

/// <summary>
/// XR grab interactable for arch loads.
/// Clamps movement to the arch X range while keeping Y and Z fixed,
/// so the user can slide the load left/right along the arch in VR.
/// Mirrors ConstrainedGrab.cs used for the linear beam.
/// </summary>
public class ArcConstrainedGrab : UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable
{
    private ArcBeamController arcController;
    private float initialY;
    private float initialZ;

    protected override void Awake()
    {
        base.Awake();
        arcController = Object.FindFirstObjectByType<ArcBeamController>();
        initialY = transform.position.y;
        initialZ = transform.position.z;
    }

    public override void ProcessInteractable(XRInteractionUpdateOrder.UpdatePhase phase)
    {
        base.ProcessInteractable(phase);

        if (phase != XRInteractionUpdateOrder.UpdatePhase.Fixed &&
            phase != XRInteractionUpdateOrder.UpdatePhase.Dynamic)
            return;

        if (arcController == null) return;

        Vector3 pos = transform.position;
        float minX = arcController.ArcStartX;
        float maxX = arcController.ArcStartX + arcController.ArcLength;

        if (pos.x < minX || pos.x > maxX)
        {
            pos.x = Mathf.Clamp(pos.x, minX, maxX);
            pos.y = initialY;
            pos.z = initialZ;
            transform.position = pos;

            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity    = Vector3.zero;
                rb.angularVelocity   = Vector3.zero;
            }
        }
    }
}
