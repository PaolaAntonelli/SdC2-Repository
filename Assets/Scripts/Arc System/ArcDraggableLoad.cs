using UnityEngine;
using UnityEngine.InputSystem;

public class ArcDraggableLoad : MonoBehaviour
{
    public ArcBeamController arcController;
    private bool isDragging = false;
    private Camera mainCamera;
    private float initialY;
    private float initialZ;

    void Awake()
    {
        mainCamera = Camera.main;
    }
    
    void Start()
    {
        initialY = transform.position.y;
        initialZ = transform.position.z;
        
        if (arcController == null)
            arcController = FindFirstObjectByType<ArcBeamController>();
    }

    void Update()
    {
        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            Ray ray = mainCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
            if (Physics.Raycast(ray, out RaycastHit hit) && 
                (hit.transform == transform || hit.transform.IsChildOf(transform)))
            {
                isDragging = true;
            }
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

        // Clamp alla lunghezza dell'arco
        float cX = Mathf.Clamp(wPos.x, arcController.ArcStartX, 
                               arcController.ArcStartX + arcController.ArcLength);
        
        transform.position = new Vector3(cX, initialY, initialZ);
    }
}