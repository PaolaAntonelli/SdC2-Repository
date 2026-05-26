using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Add to the load prefab (Carico ARCO).
/// Draws a downward arrow symbol at runtime so the object is always visible.
/// Enforces the "Load" tag, auto-adds a BoxCollider for mouse picking,
/// and handles dragging along the horizontal line (X-axis only, Y/Z fixed).
/// ArcBeamController sets arcController automatically on spawn.
/// </summary>
[DisallowMultipleComponent]
public class ArcLoad : MonoBehaviour
{
    [Header("Simbolo")]
    [Tooltip("Lunghezza totale della freccia")]
    public float arrowLength    = 1.5f;
    [Tooltip("Dimensione della punta della freccia")]
    public float arrowHeadSize  = 0.35f;
    [Tooltip("Materiale Unlit (assegnare dal progetto, es. LineUnlit)")]
    public Material lineMaterial;

    [HideInInspector] public ArcBeamController arcController;

    private Camera mainCamera;
    private bool   isDragging;
    private float  fixedY;
    private float  fixedZ;

    // ── Lifecycle ────────────────────────────────────────────────────────

    void Awake()
    {
        gameObject.tag = "Load";

        if (TryGetComponent<Rigidbody>(out var rb))
        {
            rb.isKinematic = true;
            rb.useGravity   = false;
        }

        // Collider added here so it exists before Start (Physics.Raycast needs it)
        if (GetComponent<Collider>() == null)
            gameObject.AddComponent<BoxCollider>();
    }

    void Start()
    {
        mainCamera = Camera.main;

        if (arcController == null)
            arcController = FindFirstObjectByType<ArcBeamController>();

        fixedY = transform.position.y;
        fixedZ = transform.position.z;

        SizeCollider();
        BuildArrow();
    }

    // ── Collider sizing ──────────────────────────────────────────────────

    void SizeCollider()
    {
        if (!TryGetComponent<BoxCollider>(out var col)) return;
        // Cover the entire arrow so the user can click anywhere on it
        col.center = new Vector3(0f, -arrowLength * 0.5f, 0f);
        col.size   = new Vector3(arrowHeadSize * 1.5f, arrowLength, arrowHeadSize * 1.5f);
    }

    // ── Arrow visual ─────────────────────────────────────────────────────

    void BuildArrow()
    {
        float lw = arrowHeadSize * 0.15f;

        // Vertical shaft: from (0,0) down to (0,-arrowLength)
        CreateLine("Shaft", lw, Color.green,
            new Vector3(0f,  0f,            0f),
            new Vector3(0f, -arrowLength,   0f));

        // Arrowhead: V shape at the tip
        float tip = -arrowLength;
        float hs  = arrowHeadSize;
        CreateLine("Head", lw, Color.green,
            new Vector3(-hs * 0.5f, tip + hs, 0f),
            new Vector3(0f,          tip,      0f),
            new Vector3( hs * 0.5f, tip + hs,  0f));
    }

    void CreateLine(string goName, float lineWidth, Color color, params Vector3[] points)
    {
        var go = new GameObject(goName);
        go.transform.SetParent(transform, false);

        var lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace    = false;
        lr.positionCount    = points.Length;
        lr.SetPositions(points);
        lr.startWidth       = lineWidth;
        lr.endWidth         = lineWidth;
        lr.startColor       = color;
        lr.endColor         = color;
        lr.numCornerVertices = 4;
        lr.numCapVertices    = 4;

        ApplyMaterial(lr);
    }

    void ApplyMaterial(LineRenderer lr)
    {
        if (lineMaterial != null) { lr.material = lineMaterial; return; }
        Shader s = Shader.Find("Universal Render Pipeline/Unlit")
                ?? Shader.Find("Unlit/Color")
                ?? Shader.Find("Sprites/Default");
        if (s != null) lr.material = new Material(s);
    }

    // ── Mouse drag ───────────────────────────────────────────────────────

    void Update()
    {
        if (mainCamera == null || Mouse.current == null) return;

        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            Ray ray = mainCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
            if (Physics.Raycast(ray, out RaycastHit hit) &&
                (hit.transform == transform || hit.transform.IsChildOf(transform)))
                isDragging = true;
        }

        if (!Mouse.current.leftButton.isPressed)
            isDragging = false;

        if (isDragging)
            DragAlongLine();
    }

    void DragAlongLine()
    {
        if (arcController == null) return;

        Vector2 mousePos = Mouse.current.position.ReadValue();
        float   depth    = Mathf.Abs(mainCamera.transform.position.z - fixedZ);
        Vector3 world    = mainCamera.ScreenToWorldPoint(new Vector3(mousePos.x, mousePos.y, depth));

        float clampedX = Mathf.Clamp(world.x,
            arcController.ArcStartX,
            arcController.ArcStartX + arcController.ArcLength);

        transform.position = new Vector3(clampedX, fixedY, fixedZ);
    }
}
