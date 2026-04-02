using UnityEngine;

public class FollowCameraYOnly : MonoBehaviour
{
    private Transform cameraTransform;
    
    [Header("Configurazione")]
    [SerializeField] private float distance = 3.0f; // Distanza dal visore
    [SerializeField] private float smoothSpeed = 5.0f; // Velocità di inseguimento (opzionale)

    void Start() 
    {
        if (Camera.main != null)
            cameraTransform = Camera.main.transform;
    }

    void LateUpdate()
    {
        if (cameraTransform == null) return;

        // 1. Calcoliamo la posizione target davanti alla camera
        // Prendiamo la posizione della camera e aggiungiamo il suo vettore "forward" moltiplicato per la distanza
        Vector3 targetPosition = cameraTransform.position + (cameraTransform.forward * distance);

        // 2. Vincoliamo l'altezza (Y) affinché sia uguale a quella della camera
        // (o puoi impostare un valore fisso se preferisci che non salga/scenda mai)
        targetPosition.y = cameraTransform.position.y;

        // 3. Applichiamo la posizione (con un leggero smoothing per evitare vibrazioni in VR)
        transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * smoothSpeed);

        // 4. Rotazione: puntiamo verso la camera annullando l'asse Y per evitare inclinazioni verticali
        Vector3 lookAtPos = cameraTransform.position;
        lookAtPos.y = transform.position.y;
        
        // Ruotiamo la canvas per guardare l'utente (invertendo se necessario a seconda della Canvas)
        transform.LookAt(lookAtPos);
        transform.Rotate(0, 180, 0); // Spesso necessario per le Canvas per non vederle al contrario
    }
}