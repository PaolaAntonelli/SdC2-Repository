using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class ArcBounds : MonoBehaviour
{
    private ArcController arcController;
    private Rigidbody rb;
    private float initialZ;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        arcController = Object.FindFirstObjectByType<ArcController>();
        initialZ = transform.position.z;
    }

    void FixedUpdate()
    {
        if (arcController == null) return;

        Vector3 pos = rb.position;
        float relativeX = pos.x - arcController.ArcStartX;
        
        // Limita la posizione X alla lunghezza dell'arco
        if (relativeX < 0 || relativeX > arcController.ArcSpan)
        {
            relativeX = Mathf.Clamp(relativeX, 0, arcController.ArcSpan);
            pos.x = arcController.ArcStartX + relativeX;
            
            // Aggiorna l'altezza in base alla geometria dell'arco
            float t = relativeX / arcController.ArcSpan;
            float arcY = arcController.arcObject.transform.position.y + 
                         4f * arcController.riseHeight * t * (1f - t);
            
            float verticalOffset = CompareTag("Support") ? arcController.supportVerticalOffset : arcController.loadVerticalOffset;
            pos.y = arcY + verticalOffset;
            pos.z = initialZ;
            
            rb.position = pos;
            rb.linearVelocity = Vector3.zero;
        }
    }
}