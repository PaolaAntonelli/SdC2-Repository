using UnityEngine;
using UnityEngine.InputSystem;

public class DraggableLoad : MonoBehaviour
{
    public enum ElementType { Load, Support }
    
    public BeamController beamController;
    public ElementType elementType = ElementType.Load;
    
    private bool isDragging = false;
    private Camera mainCamera;
    private bool isArchSupport = false;

    void Awake() => mainCamera = Camera.main;
    
    void Start()
    {
        DetermineElementType();
        UpdateYOffset();
    }

    private void DetermineElementType()
    {
        if (CompareTag("Support")) elementType = ElementType.Support;
        else if (CompareTag("Load")) elementType = ElementType.Load;
    }

    public void ForceArchSupportUpdate()
    {
        if (beamController != null)
            isArchSupport = (elementType == ElementType.Support && 
                           beamController.currentStructure == BeamStructureType.Arch);
    }

    public void UpdateYOffset()
    {
        if (beamController == null)
        {
            Debug.LogWarning($"UpdateYOffset: beamController è null per {gameObject.name}");
            return;
        }
        
        ForceArchSupportUpdate();
        
        float newY = transform.position.y;
        
        if (elementType == ElementType.Load)
        {
            float offset = beamController.GetCurrentLoadHeightOffset();
            newY = beamController.beamObject.transform.position.y + offset;
            Debug.Log($"UpdateYOffset LOAD: {gameObject.name} -> Y={newY} (offset={offset}, structure={beamController.currentStructure})");
        }
        else if (elementType == ElementType.Support)
        {
            float offset = beamController.GetCurrentSupportHeightOffset();
            if (isArchSupport)
            {
                ArchController arch = beamController.GetComponent<ArchController>();
                if (arch != null)
                    newY = arch.GetArchHeightAtX(transform.position.x) + offset;
            }
            else
            {
                newY = beamController.beamObject.transform.position.y + offset;
            }
            Debug.Log($"UpdateYOffset SUPPORT: {gameObject.name} -> Y={newY} (offset={offset}, isArch={isArchSupport})");
        }
        
        transform.position = new Vector3(transform.position.x, newY, transform.position.z);
    }

    void Update()
    {
        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            Ray ray = mainCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
            if (Physics.Raycast(ray, out RaycastHit hit) && hit.transform == transform) 
                isDragging = true;
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

        float cX = Mathf.Clamp(wPos.x, beamController.BeamStartX, 
                               beamController.BeamStartX + beamController.BeamLength);
        
        float finalY = transform.position.y;
        
        if (elementType == ElementType.Load)
        {
            finalY = beamController.beamObject.transform.position.y + 
                    beamController.GetCurrentLoadHeightOffset();
        }
        else if (elementType == ElementType.Support && isArchSupport)
        {
            ArchController arch = beamController.GetComponent<ArchController>();
            if (arch != null)
                finalY = arch.GetArchHeightAtX(cX) + beamController.GetCurrentSupportHeightOffset();
        }
        else if (elementType == ElementType.Support)
        {
            finalY = beamController.beamObject.transform.position.y + 
                    beamController.GetCurrentSupportHeightOffset();
        }
        
        transform.position = new Vector3(cX, finalY, transform.position.z);
    }
}