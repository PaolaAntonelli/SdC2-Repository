using UnityEngine;
using UnityEngine.InputSystem;

public class ArcDraggableLoad : MonoBehaviour
{
    public ArcBeamController arcController;
    private bool isDragging = false;
    private Camera mainCamera;
    private Vector3 dragOffset;
    
    void Awake()
    {
        mainCamera = Camera.main;
        
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }
    }
    
    void Update()
    {
        if (arcController == null) return;
        
        // Gestione mouse
        if (Mouse.current != null)
        {
            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                Ray ray = mainCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
                if (Physics.Raycast(ray, out RaycastHit hit) && hit.transform == transform)
                {
                    isDragging = true;
                    // Calcola offset per trascinamento fluido
                    Vector3 hitPoint = hit.point;
                    dragOffset = transform.position - hitPoint;
                }
            }
            
            if (isDragging && Mouse.current.leftButton.isPressed)
            {
                Drag();
            }
            else if (isDragging && !Mouse.current.leftButton.isPressed)
            {
                isDragging = false;
            }
        }
    }
    
    void Drag()
    {
        if (arcController == null || mainCamera == null) return;
        
        // Ottieni posizione del mouse nel mondo
        Ray ray = mainCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
        Plane dragPlane = new Plane(Vector3.forward, transform.position);
        
        if (dragPlane.Raycast(ray, out float distance))
        {
            Vector3 hitPoint = ray.GetPoint(distance);
            Vector3 targetWorldPos = hitPoint + dragOffset;
            
            // Proietta sulla linea di trascinamento
            Vector3 constrainedPos = arcController.ProjectOnDragLine(targetWorldPos);
            
            // Applica la posizione
            transform.position = constrainedPos;
            
            // Opzionale: aggiungi un indicatore visivo sul punto dell'arco corrispondente
            Vector3 arcPoint = arcController.GetArcPointFromDragPosition(constrainedPos);
            Debug.DrawLine(constrainedPos, arcPoint, Color.yellow, 0.02f);
        }
    }
    
    // Per supporto touch
    void OnMouseDown()
    {
        isDragging = true;
        Vector3 mousePos = Mouse.current.position.ReadValue();
        Ray ray = mainCamera.ScreenPointToRay(mousePos);
        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            dragOffset = transform.position - hit.point;
        }
    }
    
    void OnMouseUp()
    {
        isDragging = false;
    }
}