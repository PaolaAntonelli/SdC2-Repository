using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

public class ArcConstrainedGrab : UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable
{
    private ArcBeamController arcController;
    private float offsetFromCurve;
    private Vector3 initialOffset;
    
    protected override void Awake()
    {
        base.Awake();
        arcController = Object.FindFirstObjectByType<ArcBeamController>();
    }
    
    protected override void OnSelectEntered(SelectEnterEventArgs args)
    {
        base.OnSelectEntered(args);
        
        // Calcola l'offset dalla curva quando viene afferrato
        if (arcController != null)
        {
            Vector3 nearestPoint = ProjectOntoArc(transform.position);
            Vector3 normal = GetArcNormal(nearestPoint);
            
            // Offset vettoriale dalla curva
            Vector3 offset = transform.position - nearestPoint;
            offsetFromCurve = Vector3.Dot(offset, normal);
            initialOffset = offset;
        }
    }
    
    public override void ProcessInteractable(XRInteractionUpdateOrder.UpdatePhase phase)
    {
        base.ProcessInteractable(phase);
        
        if ((phase == XRInteractionUpdateOrder.UpdatePhase.Fixed || 
             phase == XRInteractionUpdateOrder.UpdatePhase.Dynamic) && 
            arcController != null && isSelected)
        {
            ConstrainToArc();
        }
    }
    
    void ConstrainToArc()
    {
        // Proietta sulla curva dell'arco
        Vector3 worldPos = transform.position;
        Vector3 closestPoint = ProjectOntoArc(worldPos);
        Vector3 normal = GetArcNormal(closestPoint);
        
        // Mantieni l'offset originale dalla curva
        Vector3 constrainedPos = closestPoint + normal * offsetFromCurve;
        
        // Aggiorna posizione
        transform.position = constrainedPos;
        
        // Smorza velocità per evitare oscillazioni
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.Lerp(rb.linearVelocity, Vector3.zero, 0.5f);
            rb.angularVelocity = Vector3.Lerp(rb.angularVelocity, Vector3.zero, 0.5f);
        }
    }
    
    Vector3 ProjectOntoArc(Vector3 worldPoint)
    {
        Vector3[] curvePoints = arcController.GetWorldCurvePoints();
        
        if (curvePoints.Length < 2)
            return transform.position;
        
        float minDist = float.MaxValue;
        Vector3 closestPoint = transform.position;
        
        // Cerca su tutti i segmenti
        for (int i = 0; i < curvePoints.Length - 1; i++)
        {
            Vector3 projected = ProjectPointOnSegment(
                worldPoint, 
                curvePoints[i], 
                curvePoints[i + 1]
            );
            
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
        float t = Vector3.Dot(point - start, segmentDir) / segmentLength;
        t = Mathf.Clamp01(t);
        
        return start + t * segment;
    }
    
    Vector3 GetArcNormal(Vector3 pointOnCurve)
    {
        Vector3 tangent = GetArcTangent(pointOnCurve);
        // Normale perpendicolare alla tangente nel piano XY
        return Vector3.Cross(tangent, Vector3.forward).normalized;
    }
    
    Vector3 GetArcTangent(Vector3 pointOnCurve)
    {
        Vector3[] curvePoints = arcController.GetWorldCurvePoints();
        
        // Trova l'indice del punto più vicino
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
        
        // Calcola tangente
        if (closestIndex == 0)
            return (curvePoints[1] - curvePoints[0]).normalized;
        else if (closestIndex == curvePoints.Length - 1)
            return (curvePoints[closestIndex] - curvePoints[closestIndex - 1]).normalized;
        else
            return (curvePoints[closestIndex + 1] - curvePoints[closestIndex - 1]).normalized;
    }
    
    // Opzionale: feedback visivo durante il grab
    void OnDrawGizmosSelected()
    {
        if (arcController == null || !isSelected) return;
        
        Gizmos.color = Color.cyan;
        Vector3 nearestPoint = ProjectOntoArc(transform.position);
        Gizmos.DrawLine(transform.position, nearestPoint);
        Gizmos.DrawWireSphere(nearestPoint, 0.1f);
    }
}