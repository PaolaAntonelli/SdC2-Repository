using UnityEngine;

/// <summary>
/// Add to the support prefab (Appoggio ARCO).
/// Draws a pin-support symbol (inverted triangle) at runtime so the object
/// is always visible after Play without needing a pre-built 3D model.
/// Enforces the "Support" tag required by ArcBeamController.
/// </summary>
public class ArcSupport : MonoBehaviour
{
    [Header("Simbolo")]
    [Tooltip("Metà larghezza del triangolo")]
    public float halfWidth  = 0.4f;
    [Tooltip("Altezza del triangolo")]
    public float height     = 0.6f;
    [Tooltip("Materiale Unlit (assegnare dal progetto, es. LineUnlit)")]
    public Material lineMaterial;

    void Awake()
    {
        gameObject.tag = "Support";

        if (TryGetComponent<Rigidbody>(out var rb))
        {
            rb.isKinematic = true;
            rb.useGravity   = false;
        }
    }

    void Start()
    {
        BuildSymbol();
    }

    void BuildSymbol()
    {
        float w = halfWidth;
        float h = height;
        float lw = h * 0.08f; // line width proportional to size

        // Inverted triangle: apex at (0,0), base at ±w going down by h
        //
        //     * ← pivot (springer foot)
        //    / \
        //   /   \
        //  /     \
        // *-------*
        // =========  ← hatching

        // Triangle outline
        CreateLine("Triangle", lw, Color.white,
            new Vector3(0,    0,  0),
            new Vector3(-w,  -h,  0),
            new Vector3( w,  -h,  0),
            new Vector3(0,    0,  0));   // close the loop

        // Horizontal base line (slightly wider than triangle)
        CreateLine("Base", lw, Color.white,
            new Vector3(-w * 1.3f, -h, 0),
            new Vector3( w * 1.3f, -h, 0));

        // Three short hatching marks below the base
        float hatchW = w * 0.25f;
        float hatchH = h * 0.25f;
        for (int i = -1; i <= 1; i++)
        {
            CreateLine($"Hatch{i}", lw * 0.7f, Color.white,
                new Vector3(i * w * 0.55f,        -h,          0),
                new Vector3(i * w * 0.55f - hatchW, -h - hatchH, 0));
        }
    }

    void CreateLine(string goName, float lineWidth, Color color, params Vector3[] points)
    {
        var go = new GameObject(goName);
        go.transform.SetParent(transform, false);

        var lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace   = false;
        lr.positionCount   = points.Length;
        lr.SetPositions(points);
        lr.startWidth      = lineWidth;
        lr.endWidth        = lineWidth;
        lr.startColor      = color;
        lr.endColor        = color;
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
}
