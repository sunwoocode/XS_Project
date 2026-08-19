using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public sealed class UnitSelectionController : MonoBehaviour
{
    [SerializeField] private FieldGenerator field;
    [SerializeField] private Camera selectionCamera;
    [SerializeField, Min(1)] private int movementRange = 5;
    [SerializeField, Min(0.01f)] private float movementDuration = 0.3f;

    private GridUnit selectedUnit;
    private bool isMoving;

    public void Configure(FieldGenerator targetField, Camera targetCamera)
    {
        field = targetField;
        selectionCamera = targetCamera;
    }

    private void Awake()
    {
        field ??= FindFirstObjectByType<FieldGenerator>();
        selectionCamera ??= Camera.main;
    }

    private void OnValidate()
    {
        movementRange = Mathf.Max(1, movementRange);
        movementDuration = Mathf.Max(0.01f, movementDuration);
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            CancelSelection();
            return;
        }

        if (isMoving)
        {
            return;
        }

        Mouse mouse = Mouse.current;
        if (mouse == null || !mouse.leftButton.wasPressedThisFrame)
        {
            return;
        }

        if (field == null || selectionCamera == null)
        {
            return;
        }

        Ray ray = selectionCamera.ScreenPointToRay(mouse.position.ReadValue());
        if (!Physics.Raycast(ray, out RaycastHit hit))
        {
            return;
        }

        GridUnit clickedUnit = hit.collider.GetComponentInParent<GridUnit>();
        if (clickedUnit != null)
        {
            SelectUnit(clickedUnit);
            return;
        }

        if (selectedUnit == null || !field.IsFieldCollider(hit.collider))
        {
            return;
        }

        if (field.TryGetCell(hit.point, out Vector2Int targetCell) &&
            field.IsCellInMovementRange(targetCell))
        {
            StartCoroutine(MoveSelectedUnit(targetCell));
        }
    }

    private void SelectUnit(GridUnit unit)
    {
        selectedUnit = unit;
        field.ShowMovementRange(unit.transform.position, movementRange);
    }

    private IEnumerator MoveSelectedUnit(Vector2Int targetCell)
    {
        isMoving = true;
        GridUnit movingUnit = selectedUnit;
        Vector3 startPosition = movingUnit.transform.position;
        float localHeight = field.transform.InverseTransformPoint(startPosition).y;
        Vector3 targetPosition = field.GetCellCenterWorld(targetCell.x, targetCell.y, localHeight);
        float elapsed = 0f;

        while (elapsed < movementDuration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / movementDuration);
            float smoothedProgress = Mathf.SmoothStep(0f, 1f, progress);
            movingUnit.transform.position = Vector3.Lerp(startPosition, targetPosition, smoothedProgress);
            yield return null;
        }

        movingUnit.transform.position = targetPosition;
        isMoving = false;
        CancelSelection();
    }

    private void CancelSelection()
    {
        selectedUnit = null;
        if (field != null)
        {
            field.ClearHighlights();
        }
    }
}
