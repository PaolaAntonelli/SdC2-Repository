using UnityEngine;
using UnityEngine.InputSystem;

public class DraggableLoad : MonoBehaviour
{
    public BeamController beamController;
    private bool isDragging = false;
    private Camera mainCamera;
    private float initialY;

    void Awake() => mainCamera = Camera.main;
    void Start() => initialY = transform.position.y;

    void Update()
    {
        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            Ray ray = mainCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
            if (Physics.Raycast(ray, out RaycastHit hit) && hit.transform == transform) isDragging = true;
        }

        if (!Mouse.current.leftButton.isPressed) isDragging = false;

        if (isDragging) Drag();
    }

    void Drag()
    {
        if (beamController == null) return;
        Vector3 mPos = Mouse.current.position.ReadValue();
        float dist = Mathf.Abs(mainCamera.transform.position.z - transform.position.z);
        Vector3 wPos = mainCamera.ScreenToWorldPoint(new Vector3(mPos.x, mPos.y, dist));

        // Clamping alla lunghezza della trave
        float cX = Mathf.Clamp(wPos.x, beamController.BeamStartX, beamController.BeamStartX + beamController.BeamLength);
        transform.position = new Vector3(cX, initialY, transform.position.z);
    }
}