using UnityEngine;
using UnityEngine.InputSystem;

public class ArcDraggableLoad : MonoBehaviour
{
    public ArcBeamController arcController;
    private bool isDragging = false;
    private Camera mainCamera;
    private float offsetFromCurve;
    
    void Awake()
    {
        mainCamera = Camera.main;
    }
    
    void Start()
    {
        // Calcola l'offset iniziale dalla curva
        if (arcController != null)
        {
            Vector3 nearestPoint = ProjectOntoArc(transform.position);
            offsetFromCurve = Vector3.Dot(
                transform.position - nearestPoint,
                GetArcNormal(nearestPoint)
            );
        }
    }
    
    void Update()
    {
        // Supporto sia mouse che touch
        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            Ray ray = mainCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
            if (Physics.Raycast(ray, out RaycastHit hit) && hit.transform == transform)
            {
                isDragging = true;
            }
        }
        
        if (!Mouse.current.leftButton.isPressed)
        {
            isDragging = false;
        }
        
        if (isDragging && arcController != null)
        {
            Drag();
        }
    }
    
    void Drag()
    {
        Vector3 mousePos = Mouse.current.position.ReadValue();
        
        // Converti mouse in posizione mondo
        float distFromCamera = Mathf.Abs(mainCamera.transform.position.z - transform.position.z);
        Vector3 worldPos = mainCamera.ScreenToWorldPoint(
            new Vector3(mousePos.x, mousePos.y, distFromCamera)
        );
        
        // Proietta sulla curva e mantieni offset normale
        Vector3 closestPoint = ProjectOntoArc(worldPos);
        Vector3 normal = GetArcNormal(closestPoint);
        Vector3 constrainedPos = closestPoint + normal * offsetFromCurve;
        
        transform.position = constrainedPos;
    }
    
    Vector3 ProjectOntoArc(Vector3 worldPoint)
    {
        Vector3[] curvePoints = arcController.GetWorldCurvePoints();
        
        if (curvePoints.Length < 2)
            return transform.position;
        
        float minDist = float.MaxValue;
        Vector3 closestPoint = transform.position;
        
        // Cerca il punto più vicino sulla curva
        for (int i = 0; i < curvePoints.Length - 1; i++)
        {
            Vector3 segmentStart = curvePoints[i];
            Vector3 segmentEnd = curvePoints[i + 1];
            
            Vector3 projected = ProjectPointOnSegment(worldPoint, segmentStart, segmentEnd);
            float dist = Vector2.Distance(
                new Vector2(worldPoint.x, worldPoint.y),
                new Vector2(projected.x, projected.y)
            );
            
            if (dist < minDist)
            {
                minDist = dist;
                closestPoint = projected;
            }
        }
        
        return closestPoint;
    }
    
    Vector3 ProjectPointOnSegment(Vector3 point, Vector3 start, Vector3 end)
    {
        Vector3 segment = end - start;
        float segmentLength = segment.magnitude;
        
        if (segmentLength < 0.0001f)
            return start;
        
        Vector3 segmentDir = segment / segmentLength;
        Vector3 pointToStart = point - start;
        
        float t = Vector3.Dot(pointToStart, segmentDir);
        t = Mathf.Clamp01(t / segmentLength);
        
        return start + t * segment;
    }
    
    Vector3 GetArcNormal(Vector3 pointOnCurve)
    {
        Vector3 tangent = GetArcTangent(pointOnCurve);
        return Vector3.Cross(tangent, Vector3.forward).normalized;
    }
    
    Vector3 GetArcTangent(Vector3 pointOnCurve)
    {
        Vector3[] curvePoints = arcController.GetWorldCurvePoints();
        
        // Trova il segmento più vicino
        float minDist = float.MaxValue;
        int closestIndex = 0;
        
        for (int i = 0; i < curvePoints.Length; i++)
        {
            float dist = Vector2.Distance(
                new Vector2(pointOnCurve.x, pointOnCurve.y),
                new Vector2(curvePoints[i].x, curvePoints[i].y)
            );
            
            if (dist < minDist)
            {
                minDist = dist;
                closestIndex = i;
            }
        }
        
        // Calcola tangente usando punti adiacenti
        int prevIndex = Mathf.Max(0, closestIndex - 1);
        int nextIndex = Mathf.Min(curvePoints.Length - 1, closestIndex + 1);
        
        return (curvePoints[nextIndex] - curvePoints[prevIndex]).normalized;
    }
    
    void OnDrawGizmosSelected()
    {
        if (arcController == null) return;
        
        // Mostra la proiezione sulla curva
        Gizmos.color = Color.yellow;
        Vector3 projected = ProjectOntoArc(transform.position);
        Gizmos.DrawLine(transform.position, projected);
        Gizmos.DrawSphere(projected, 0.1f);
    }
}