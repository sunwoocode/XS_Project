using UnityEngine;

public sealed class GridObstacle : MonoBehaviour
{
    public const float HeightMeters = 1f;

    [SerializeField, Min(1)] private int widthCells = 1;
    [SerializeField, Min(1)] private int depthCells = 1;

    public int WidthCells => widthCells;
    public int DepthCells => depthCells;

    public void Configure(int width, int depth)
    {
        widthCells = Mathf.Max(1, width);
        depthCells = Mathf.Max(1, depth);
    }

    public void AddOccupiedCells(FieldGenerator field, System.Collections.Generic.ICollection<Vector2Int> cells)
    {
        for (int z = 0; z < depthCells; z++)
        {
            for (int x = 0; x < widthCells; x++)
            {
                Vector3 samplePosition = transform.TransformPoint(
                    new Vector3(x * FieldGenerator.CellSizeMeters, 0f, z * FieldGenerator.CellSizeMeters));
                if (field.TryGetCell(samplePosition, out Vector2Int cell))
                {
                    cells.Add(cell);
                }
            }
        }
    }

    private void OnValidate()
    {
        widthCells = Mathf.Max(1, widthCells);
        depthCells = Mathf.Max(1, depthCells);
    }
}
