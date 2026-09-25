using UnityEngine;

/// <summary>
/// A rounded, bordered backdrop quad generated at runtime and shaded by
/// UI/RoundedPanel. Nothing has to be dragged into the Inspector; the look
/// comes entirely from the PanelStyle asset it is given.
/// </summary>
public class RoundedPanelVisual
{
    public GameObject root;

    private MeshRenderer meshRenderer;
    private Material material;
    private PanelStyle style;
    private Vector2 size = new Vector2(1f, 1f);
    private float alpha = 1f;
    private bool drawOnTop = false;

    public Vector2 Size { get { return size; } }

    public RoundedPanelVisual(Transform parent, string name, PanelStyle panelStyle,
        bool renderOnTop = false)
    {
        drawOnTop = renderOnTop;
        style = panelStyle;
        if (style == null)
        {
            // A forgotten Inspector reference should cost the panel its styling,
            // not take the whole aid system down with a null reference.
            Debug.LogWarning("[RoundedPanelVisual] No PanelStyle assigned for '" + name +
                             "'. Using built-in defaults - drag a Panel Style asset onto " +
                             "ConceptualAidManager to control the look.");
            style = ScriptableObject.CreateInstance<PanelStyle>();
        }

        root = new GameObject(name);
        root.transform.SetParent(parent, false);

        MeshFilter filter = root.AddComponent<MeshFilter>();
        filter.mesh = BuildQuad();

        meshRenderer = root.AddComponent<MeshRenderer>();
        meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;

        Shader shader = Shader.Find("UI/RoundedPanel");
        if (shader == null)
        {
            // Without the shader the panel would render as opaque magenta and
            // hide the text behind it, so fall back to something harmless.
            Debug.LogWarning("[RoundedPanelVisual] Shader 'UI/RoundedPanel' not found. " +
                             "Make sure RoundedPanel.shader is in the project.");
            shader = Shader.Find("Sprites/Default");
        }

        material = new Material(shader);
        meshRenderer.material = material;

        Apply();
        root.SetActive(false);
    }

    private static Mesh BuildQuad()
    {
        Mesh mesh = new Mesh();
        mesh.name = "RoundedPanelQuad";
        mesh.vertices = new Vector3[]
        {
            new Vector3(-0.5f, -0.5f, 0f),
            new Vector3( 0.5f, -0.5f, 0f),
            new Vector3( 0.5f,  0.5f, 0f),
            new Vector3(-0.5f,  0.5f, 0f),
        };
        mesh.uv = new Vector2[]
        {
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(1f, 1f),
            new Vector2(0f, 1f),
        };
        mesh.triangles = new int[] { 0, 2, 1, 0, 3, 2 };
        mesh.RecalculateNormals();
        return mesh;
    }

    public void SetStyle(PanelStyle panelStyle)
    {
        style = panelStyle;
        Apply();
    }

    /// <summary>Resize in world metres. Keeps the corners circular.</summary>
    public void SetSize(float width, float height)
    {
        size = new Vector2(Mathf.Max(0.001f, width), Mathf.Max(0.001f, height));
        root.transform.localScale = new Vector3(size.x, size.y, 1f);
        Apply();
    }

    public void SetPose(Vector3 worldPosition, Quaternion rotation)
    {
        root.transform.position = worldPosition;
        root.transform.rotation = rotation;
    }

    public void SetAlpha(float a)
    {
        alpha = Mathf.Clamp01(a);
        if (material == null) return;
        material.SetFloat("_Alpha", alpha * (style != null ? style.opacity : 1f));
    }

    public void SetVisible(bool visible)
    {
        if (root != null) root.SetActive(visible);
    }

    public bool IsVisible { get { return root != null && root.activeSelf; } }

    /// <summary>Re-read the style asset. Safe to call every frame while tuning.</summary>
    public void Apply()
    {
        if (style == null || material == null) return;
        style.ApplyTo(material, size.x / size.y);
        material.SetFloat("_Alpha", alpha * style.opacity);
        // Match the text this backdrop sits behind: if the text is drawn on
        // top of the apparatus, so is the backdrop, and vice versa.
        material.SetFloat("_ZTest", drawOnTop
            ? (float)UnityEngine.Rendering.CompareFunction.Always
            : (float)UnityEngine.Rendering.CompareFunction.LessEqual);
    }

    public void Destroy()
    {
        if (root != null) Object.Destroy(root);
        root = null;
    }
}