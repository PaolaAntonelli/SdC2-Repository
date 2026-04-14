using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class BeamBounds : MonoBehaviour
{
    private BeamController beamController;
    private Rigidbody rb;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        
        // Trova in automatico il BeamController nella scena
        beamController = Object.FindFirstObjectByType<BeamController>();
    }

    void FixedUpdate()
    {
        if (beamController == null) return;

        Vector3 pos = rb.position;
        
        // Calcoliamo i limiti della trave
        float minX = beamController.BeamStartX;
        float maxX = beamController.BeamStartX + beamController.BeamLength;

        // Se l'oggetto cerca di uscire dai bordi, lo blocchiamo
        if (pos.x < minX || pos.x > maxX)
        {
            pos.x = Mathf.Clamp(pos.x, minX, maxX);
            
            // Usiamo rb.position invece di transform.position 
            // per non creare conflitti (jitter) con l'XR Grab
            rb.position = pos; 
            
            // Azzeriamo la velocità su X per fermare l'inerzia contro il "muro" invisibile
            rb.linearVelocity = Vector3.zero; 
        }
    }
}