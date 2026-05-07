using UnityEngine;
using UnityEngine.InputSystem;

public class DraggableLoadArc : MonoBehaviour
{
    public ArcController arcController;
    private bool isDragging = false;
    private Camera mainCamera;
    private float initialZ;

    void Awake() 
    { 
        mainCamera = Camera.main; 
    }

    void Start() 
    { 
        initialZ = transform.position.z; 
    }

    void Update()
    {
        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            Ray ray = mainCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
            if (Physics.Raycast(ray, out RaycastHit hit) && hit.transform == transform)
                isDragging = true;
        }

        if (!Mouse.current.leftButton.isPressed)
            isDragging = false;

        if (isDragging)
            Drag();
    }

    void Drag()
    {
        if (arcController == null) return;
        
        Vector3 mPos = Mouse.current.position.ReadValue();
        float dist = Mathf.Abs(mainCamera.transform.position.z - transform.position.z);
        Vector3 wPos = mainCamera.ScreenToWorldPoint(new Vector3(mPos.x, mPos.y, dist));

        // Calcola la posizione X relativa all'arco
        float relativeX = Mathf.Clamp(wPos.x - arcController.ArcStartX, 0, arcController.ArcSpan);
        
        // Ottieni l'altezza dell'arco in quella posizione
        float arcY = GetArcHeightAtX(relativeX);
        
        // Determina se questo è un supporto o un carico in base al tag
        float verticalOffset = 0f;
        if (CompareTag("Support"))
            verticalOffset = arcController.supportVerticalOffset;
        else if (CompareTag("Load"))
            verticalOffset = arcController.loadVerticalOffset;
        
        float finalY = arcY + verticalOffset;
        
        transform.position = new Vector3(arcController.ArcStartX + relativeX, finalY, initialZ);
    }

    float GetArcHeightAtX(float xRelative)
    {
        float t = Mathf.Clamp01(xRelative / arcController.ArcSpan);
        float y = 4f * arcController.riseHeight * t * (1f - t);
        return arcController.arcObject.transform.position.y + y;
    }
}