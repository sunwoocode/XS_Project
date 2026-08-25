using System;
using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
public sealed class FieldGenerator : MonoBehaviour
{
    public const float CellSizeMeters = 1f;

    private const string SurfaceName = "FieldSurface";

    private static readonly int GridSizeId = Shader.PropertyToID("_GridSize");
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int LineColorId = Shader.PropertyToID("_LineColor");
    private static readonly int LineWidthId = Shader.PropertyToID("_LineWidth");
    private static readonly int HighlightCellId = Shader.PropertyToID("_HighlightCell");
    private static readonly int HighlightColorId = Shader.PropertyToID("_HighlightColor");
    private static readonly int MovementColorId = Shader.PropertyToID("_MovementColor");
    private static readonly int ReachabilityMapId = Shader.PropertyToID("_ReachabilityMap");
    private static readonly int GridVisibleId = Shader.PropertyToID("_GridVisible");

    private static readonly Vector2Int[] CardinalDirections =
    {
        Vector2Int.up,
        Vector2Int.right,
        Vector2Int.down,
        Vector2Int.left
    };

    [Header("Grid")]
    [SerializeField, Min(1)] private int width = 20;
    [SerializeField, Min(1)] private int depth = 30;
    [SerializeField, Min(0.02f)] private float tileHeight = 0.16f;

    [Header("Appearance")]
    [SerializeField] private Color baseColor = new(0.22f, 0.50f, 0.27f, 1f);
    [SerializeField] private Color lineColor = new(0.68f, 0.92f, 0.70f, 1f);
    [SerializeField] private Color highlightColor = new(1f, 0.72f, 0.18f, 1f);
    [SerializeField] private Color movementColor = new(0.18f, 0.62f, 0.95f, 1f);
    [SerializeField, Range(0.005f, 0.15f)] private float lineWidth = 0.035f;
    [SerializeField] private Material gridMaterial;

    private Material runtimeMaterial;
    private MaterialPropertyBlock propertyBlock;
    private Vector2Int highlightedCell = new(-1, -1);
    private bool highlightEnabled;
    private bool[,] reachableCells;
    private Vector2Int[,] previousCells;
    private Texture2D reachabilityTexture;

    public float TileSizeMeters => CellSizeMeters;
    public int Width => width;
    public int Depth => depth;
    public bool IsGridActive => highlightEnabled;

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

    public void SetDimensions(int newWidth, int newDepth)
    {
        width = Mathf.Max(1, newWidth);
        depth = Mathf.Max(1, newDepth);
        ClearHighlights();
        GenerateField();
    }

    public Vector3 GetCellCenterWorld(int column, int row, float surfaceOffset = 0f)
    {
        column = Mathf.Clamp(column, 0, width - 1);
        row = Mathf.Clamp(row, 0, depth - 1);

        float localX = (column - width * 0.5f + 0.5f) * CellSizeMeters;
        float localZ = (row - depth * 0.5f + 0.5f) * CellSizeMeters;
        return transform.TransformPoint(new Vector3(localX, surfaceOffset, localZ));
    }

    public bool TryGetCell(Vector3 worldPosition, out Vector2Int cell)
    {
        Vector3 localPosition = transform.InverseTransformPoint(worldPosition);
        float halfWidth = width * CellSizeMeters * 0.5f;
        float halfDepth = depth * CellSizeMeters * 0.5f;
        int column = Mathf.FloorToInt((localPosition.x + halfWidth) / CellSizeMeters);
        int row = Mathf.FloorToInt((localPosition.z + halfDepth) / CellSizeMeters);

        if (column < 0 || column >= width || row < 0 || row >= depth)
        {
            cell = new Vector2Int(-1, -1);
            return false;
        }

        cell = new Vector2Int(column, row);
        return true;
    }

    public bool IsValidCell(Vector2Int cell)
    {
        return cell.x >= 0 && cell.x < width && cell.y >= 0 && cell.y < depth;
    }

    public bool ShowMovementRange(
        Vector3 unitWorldPosition,
        int maxDistance,
        IReadOnlyCollection<Vector2Int> blockedCells)
    {
        if (!TryGetCell(unitWorldPosition, out Vector2Int unitCell))
        {
            ClearHighlights();
            return false;
        }

        highlightedCell = unitCell;
        highlightEnabled = true;
        BuildReachability(unitCell, Mathf.Max(0, maxDistance), blockedCells);
        UpdateReachabilityTexture();
        UpdateSurface();
        return true;
    }

    public bool IsCellInMovementRange(Vector2Int cell)
    {
        if (!highlightEnabled || !IsValidCell(cell) || reachableCells == null)
        {
            return false;
        }

        return reachableCells[cell.x, cell.y];
    }

    public bool TryBuildPath(Vector2Int targetCell, out List<Vector2Int> path)
    {
        path = new List<Vector2Int>();
        if (!IsCellInMovementRange(targetCell) || previousCells == null)
        {
            return false;
        }

        Vector2Int current = targetCell;
        int safetyLimit = width * depth;
        while (current != highlightedCell && safetyLimit-- > 0)
        {
            path.Add(current);
            current = previousCells[current.x, current.y];
            if (!IsValidCell(current))
            {
                path.Clear();
                return false;
            }
        }

        path.Reverse();
        return path.Count > 0;
    }

    public bool IsFieldCollider(Collider targetCollider)
    {
        return targetCollider != null && targetCollider.transform.IsChildOf(transform);
    }

    public void ClearHighlights()
    {
        highlightEnabled = false;
        highlightedCell = new Vector2Int(-1, -1);
        reachableCells = null;
        previousCells = null;
        UpdateReachabilityTexture();
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
        surface.transform.localScale = new Vector3(width * CellSizeMeters, tileHeight, depth * CellSizeMeters);

        Renderer surfaceRenderer = surface.GetComponent<Renderer>();
        surfaceRenderer.sharedMaterial = ResolveMaterial();

        propertyBlock ??= new MaterialPropertyBlock();
        propertyBlock.Clear();
        propertyBlock.SetVector(GridSizeId, new Vector4(width, depth, 0f, 0f));
        propertyBlock.SetColor(BaseColorId, baseColor);
        propertyBlock.SetColor(LineColorId, lineColor);
        propertyBlock.SetColor(HighlightColorId, highlightColor);
        propertyBlock.SetColor(MovementColorId, movementColor);
        propertyBlock.SetFloat(LineWidthId, lineWidth);
        propertyBlock.SetFloat(GridVisibleId, highlightEnabled ? 1f : 0f);
        EnsureReachabilityTexture();
        propertyBlock.SetTexture(ReachabilityMapId, reachabilityTexture);
        propertyBlock.SetVector(
            HighlightCellId,
            new Vector4(highlightedCell.x, highlightedCell.y, highlightEnabled ? 1f : 0f, 0f));
        surfaceRenderer.SetPropertyBlock(propertyBlock);
    }

    private void BuildReachability(
        Vector2Int startCell,
        int maxDistance,
        IReadOnlyCollection<Vector2Int> blockedCells)
    {
        reachableCells = new bool[width, depth];
        previousCells = new Vector2Int[width, depth];
        int[,] distances = new int[width, depth];
        bool[,] blocked = new bool[width, depth];

        for (int x = 0; x < width; x++)
        {
            for (int z = 0; z < depth; z++)
            {
                distances[x, z] = -1;
                previousCells[x, z] = new Vector2Int(-1, -1);
            }
        }

        if (blockedCells != null)
        {
            foreach (Vector2Int blockedCell in blockedCells)
            {
                if (IsValidCell(blockedCell) && blockedCell != startCell)
                {
                    blocked[blockedCell.x, blockedCell.y] = true;
                }
            }
        }

        Queue<Vector2Int> frontier = new();
        frontier.Enqueue(startCell);
        distances[startCell.x, startCell.y] = 0;

        while (frontier.Count > 0)
        {
            Vector2Int current = frontier.Dequeue();
            int nextDistance = distances[current.x, current.y] + 1;
            if (nextDistance > maxDistance)
            {
                continue;
            }

            foreach (Vector2Int direction in CardinalDirections)
            {
                Vector2Int next = current + direction;
                if (!IsValidCell(next) || blocked[next.x, next.y] || distances[next.x, next.y] >= 0)
                {
                    continue;
                }

                distances[next.x, next.y] = nextDistance;
                previousCells[next.x, next.y] = current;
                reachableCells[next.x, next.y] = true;
                frontier.Enqueue(next);
            }
        }
    }

    private void EnsureReachabilityTexture()
    {
        if (reachabilityTexture != null &&
            reachabilityTexture.width == width &&
            reachabilityTexture.height == depth)
        {
            return;
        }

        if (reachabilityTexture != null)
        {
            if (Application.isPlaying)
            {
                Destroy(reachabilityTexture);
            }
            else
            {
                DestroyImmediate(reachabilityTexture);
            }
        }

        reachabilityTexture = new Texture2D(width, depth, TextureFormat.RGBA32, false, true)
        {
            name = "Field Reachability Map",
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };
        UpdateReachabilityTexture();
    }

    private void UpdateReachabilityTexture()
    {
        EnsureReachabilityTexture();
        Color32[] pixels = new Color32[width * depth];
        Color32 reachableColor = new(255, 255, 255, 255);
        Color32 blockedColor = new(0, 0, 0, 255);

        for (int z = 0; z < depth; z++)
        {
            for (int x = 0; x < width; x++)
            {
                bool reachable = reachableCells != null && reachableCells[x, z];
                pixels[z * width + x] = reachable ? reachableColor : blockedColor;
            }
        }

        reachabilityTexture.SetPixels32(pixels);
        reachabilityTexture.Apply(false, false);
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
