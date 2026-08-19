using UnityEngine;

[ExecuteAlways]
public sealed class FieldGenerator : MonoBehaviour
{
    private const string SurfaceName = "FieldSurface";

    private static readonly int GridSizeId = Shader.PropertyToID("_GridSize");
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int LineColorId = Shader.PropertyToID("_LineColor");
    private static readonly int LineWidthId = Shader.PropertyToID("_LineWidth");

    [Header("Grid")]
    [SerializeField, Min(1)] private int width = 10;
    [SerializeField, Min(1)] private int depth = 10;
    [SerializeField, Min(0.1f)] private float tileSize = 1f;
    [SerializeField, Min(0.02f)] private float tileHeight = 0.16f;

    [Header("Appearance")]
    [SerializeField] private Color baseColor = new(0.22f, 0.50f, 0.27f, 1f);
    [SerializeField] private Color lineColor = new(0.68f, 0.92f, 0.70f, 1f);
    [SerializeField, Range(0.005f, 0.15f)] private float lineWidth = 0.035f;
    [SerializeField] private Material gridMaterial;

    private Material runtimeMaterial;
    private MaterialPropertyBlock propertyBlock;

#if UNITY_EDITOR
    private bool regenerationQueued;
#endif

    private void OnEnable()
    {
        if (!TryGetSurface(out _))
        {
            GenerateField();
        }
        else
        {
            UpdateSurface();
        }
    }

    private void OnValidate()
    {
        width = Mathf.Max(1, width);
        depth = Mathf.Max(1, depth);
        tileSize = Mathf.Max(0.1f, tileSize);
        tileHeight = Mathf.Max(0.02f, tileHeight);
        lineWidth = Mathf.Clamp(lineWidth, 0.005f, 0.15f);

#if UNITY_EDITOR
        if (!Application.isPlaying && isActiveAndEnabled && !regenerationQueued)
        {
            regenerationQueued = true;
            UnityEditor.EditorApplication.delayCall += RegenerateAfterValidation;
        }
#endif
    }

#if UNITY_EDITOR
    private void RegenerateAfterValidation()
    {
        regenerationQueued = false;

        if (this == null || Application.isPlaying || !isActiveAndEnabled)
        {
            return;
        }

        GenerateField();
        UnityEditor.EditorUtility.SetDirty(this);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
    }
#endif

    public void SetGridMaterial(Material material)
    {
        gridMaterial = material;
        UpdateSurface();
    }

    [ContextMenu("Generate Field")]
    public void GenerateField()
    {
        ClearField();

        GameObject surface = GameObject.CreatePrimitive(PrimitiveType.Cube);
        surface.name = SurfaceName;
        surface.transform.SetParent(transform, false);

        UpdateSurface(surface);
    }

    [ContextMenu("Clear Field")]
    public void ClearField()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            GameObject child = transform.GetChild(i).gameObject;
            if (Application.isPlaying)
            {
                Destroy(child);
            }
            else
            {
                DestroyImmediate(child);
            }
        }
    }

    private void UpdateSurface()
    {
        if (TryGetSurface(out GameObject surface))
        {
            UpdateSurface(surface);
        }
    }

    private void UpdateSurface(GameObject surface)
    {
        surface.transform.localPosition = new Vector3(0f, -tileHeight * 0.5f, 0f);
        surface.transform.localRotation = Quaternion.identity;
        surface.transform.localScale = new Vector3(width * tileSize, tileHeight, depth * tileSize);

        Renderer surfaceRenderer = surface.GetComponent<Renderer>();
        surfaceRenderer.sharedMaterial = ResolveMaterial();

        propertyBlock ??= new MaterialPropertyBlock();
        propertyBlock.Clear();
        propertyBlock.SetVector(GridSizeId, new Vector4(width, depth, 0f, 0f));
        propertyBlock.SetColor(BaseColorId, baseColor);
        propertyBlock.SetColor(LineColorId, lineColor);
        propertyBlock.SetFloat(LineWidthId, lineWidth);
        surfaceRenderer.SetPropertyBlock(propertyBlock);
    }

    private bool TryGetSurface(out GameObject surface)
    {
        if (transform.childCount == 1 && transform.GetChild(0).name == SurfaceName)
        {
            surface = transform.GetChild(0).gameObject;
            return surface.GetComponent<Renderer>() != null && surface.GetComponent<Collider>() != null;
        }

        surface = null;
        return false;
    }

    private Material ResolveMaterial()
    {
        if (gridMaterial != null)
        {
            return gridMaterial;
        }

        if (runtimeMaterial == null)
        {
            Shader shader = Shader.Find("XS Project/Field Grid");
            if (shader == null)
            {
                Debug.LogError("Field grid shader was not found.", this);
                return null;
            }

            runtimeMaterial = new Material(shader)
            {
                name = "Field Grid (Runtime)",
                hideFlags = HideFlags.HideAndDontSave
            };
        }

        return runtimeMaterial;
    }
}
