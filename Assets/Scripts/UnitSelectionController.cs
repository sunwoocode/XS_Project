using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public sealed class UnitSelectionController : MonoBehaviour
{
    private static readonly Color AvailableColor = new(0.28f, 0.30f, 0.32f, 0.94f);
    private static readonly Color MovedColor = new(0.72f, 0.12f, 0.12f, 0.96f);
    private static readonly Color SelectedColor = new(0.82f, 0.62f, 0.12f, 0.98f);

    [SerializeField] private FieldGenerator field;
    [SerializeField] private Camera selectionCamera;
    [SerializeField] private GridUnit[] units = System.Array.Empty<GridUnit>();
    [SerializeField, Min(1)] private int movementRange = 3;
    [SerializeField, Min(0.01f)] private float secondsPerCell = 0.12f;
    [SerializeField, Min(0.1f)] private float opponentTurnSeconds = 3f;
    [SerializeField] private Text turnText;
    [SerializeField] private Button endTurnButton;
    [SerializeField] private Button[] unitStatusButtons = System.Array.Empty<Button>();
    [SerializeField] private CardView[] cards = System.Array.Empty<CardView>();

    private readonly HashSet<GridUnit> movedUnits = new();
    private GridUnit selectedUnit;
    private int selectedCardIndex = -1;
    private UnityAction[] unitButtonActions = System.Array.Empty<UnityAction>();
    private bool isMoving;
    private bool isPlayerTurn = true;
    private bool endTurnQueued;
    private int currentTurn = 1;

    public void Configure(FieldGenerator targetField, Camera targetCamera, GridUnit[] controlledUnits)
    {
        field = targetField;
        selectionCamera = targetCamera;
        units = controlledUnits ?? System.Array.Empty<GridUnit>();
    }

    public void ConfigureUI(
        Text targetTurnText,
        Button targetEndTurnButton,
        Button[] statusButtons,
        CardView[] handCards)
    {
        turnText = targetTurnText;
        endTurnButton = targetEndTurnButton;
        unitStatusButtons = statusButtons ?? System.Array.Empty<Button>();
        cards = handCards ?? System.Array.Empty<CardView>();
        RefreshUI();
    }

    private void Awake()
    {
        field ??= FindFirstObjectByType<FieldGenerator>();
        selectionCamera ??= Camera.main;
        if (units == null || units.Length == 0)
        {
            units = FindObjectsByType<GridUnit>(FindObjectsSortMode.None);
            System.Array.Sort(units, (a, b) => a.UnitNumber.CompareTo(b.UnitNumber));
        }
    }

    private void Start()
    {
        if (endTurnButton != null)
        {
            endTurnButton.onClick.RemoveListener(EndPlayerTurn);
            endTurnButton.onClick.AddListener(EndPlayerTurn);
        }

        BindSelectionUI();

        RefreshUI();
    }

    private void OnDestroy()
    {
        if (endTurnButton != null)
        {
            endTurnButton.onClick.RemoveListener(EndPlayerTurn);
        }

        UnbindUnitButtons();
    }

    private void OnValidate()
    {
        movementRange = Mathf.Max(1, movementRange);
        secondsPerCell = Mathf.Max(0.01f, secondsPerCell);
        opponentTurnSeconds = Mathf.Max(0.1f, opponentTurnSeconds);
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
        {
            ClearAllSelections();
            return;
        }

        if (!isPlayerTurn || isMoving)
        {
            return;
        }

        if (keyboard != null && HandleKeyboardShortcuts(keyboard))
        {
            return;
        }

        Mouse mouse = Mouse.current;
        if (mouse == null || !mouse.leftButton.wasPressedThisFrame)
        {
            return;
        }

        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
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
            if (IsControlledUnit(clickedUnit) && !movedUnits.Contains(clickedUnit))
            {
                SelectUnit(clickedUnit);
            }
            else
            {
                CancelUnitSelection();
            }

            return;
        }

        if (selectedUnit == null || !field.IsFieldCollider(hit.collider))
        {
            return;
        }

        if (field.TryGetCell(hit.point, out Vector2Int targetCell) &&
            field.TryBuildPath(targetCell, out List<Vector2Int> path))
        {
            StartCoroutine(MoveSelectedUnit(path));
        }
    }

    public void EndPlayerTurn()
    {
        if (!isPlayerTurn)
        {
            return;
        }

        if (isMoving)
        {
            endTurnQueued = true;
            return;
        }

        StartCoroutine(WaitForNextPlayerTurn());
    }

    public void SelectUnitByIndex(int index)
    {
        if (!isPlayerTurn || isMoving || index < 0 || index >= units.Length)
        {
            return;
        }

        GridUnit unit = units[index];
        if (unit == null || !IsControlledUnit(unit) || movedUnits.Contains(unit))
        {
            return;
        }

        SelectUnit(unit);
    }

    public void SelectCardByIndex(int index)
    {
        if (!isPlayerTurn || isMoving || index < 0 || index >= cards.Length || cards[index] == null)
        {
            return;
        }

        int nextIndex = selectedCardIndex == index ? -1 : index;
        ClearCardSelection();
        if (nextIndex >= 0)
        {
            selectedCardIndex = nextIndex;
            cards[selectedCardIndex].SetSelected(true);
        }
    }

    private void SelectUnit(GridUnit unit)
    {
        selectedUnit = unit;
        HashSet<Vector2Int> blockedCells = new();
        GridObstacle[] obstacles = FindObjectsByType<GridObstacle>(FindObjectsSortMode.None);
        foreach (GridObstacle obstacle in obstacles)
        {
            if (obstacle.isActiveAndEnabled)
            {
                obstacle.AddOccupiedCells(field, blockedCells);
            }
        }

        GridUnit[] allUnits = FindObjectsByType<GridUnit>(FindObjectsSortMode.None);
        foreach (GridUnit otherUnit in allUnits)
        {
            if (otherUnit != null && otherUnit != unit &&
                field.TryGetCell(otherUnit.transform.position, out Vector2Int occupiedCell))
            {
                blockedCells.Add(occupiedCell);
            }
        }

        field.ShowMovementRange(unit.transform.position, movementRange, blockedCells);
        RefreshUI();
    }

    private IEnumerator MoveSelectedUnit(IReadOnlyList<Vector2Int> path)
    {
        isMoving = true;
        RefreshUI();
        GridUnit movingUnit = selectedUnit;
        float localHeight = field.transform.InverseTransformPoint(movingUnit.transform.position).y;

        foreach (Vector2Int pathCell in path)
        {
            Vector3 startPosition = movingUnit.transform.position;
            Vector3 targetPosition = field.GetCellCenterWorld(pathCell.x, pathCell.y, localHeight);
            float elapsed = 0f;

            while (elapsed < secondsPerCell)
            {
                elapsed += Time.deltaTime;
                float progress = Mathf.Clamp01(elapsed / secondsPerCell);
                float smoothedProgress = Mathf.SmoothStep(0f, 1f, progress);
                movingUnit.transform.position = Vector3.Lerp(startPosition, targetPosition, smoothedProgress);
                yield return null;
            }

            movingUnit.transform.position = targetPosition;
        }

        movedUnits.Add(movingUnit);
        isMoving = false;
        CancelUnitSelection();
        RefreshUI();

        if (endTurnQueued)
        {
            endTurnQueued = false;
            StartCoroutine(WaitForNextPlayerTurn());
        }
    }

    private IEnumerator WaitForNextPlayerTurn()
    {
        isPlayerTurn = false;
        endTurnQueued = false;
        ClearAllSelections();
        RefreshUI();

        yield return new WaitForSeconds(opponentTurnSeconds);

        currentTurn++;
        movedUnits.Clear();
        isPlayerTurn = true;
        RefreshUI();
    }

    private bool IsControlledUnit(GridUnit unit)
    {
        if (!unit.IsPlayerControlled)
        {
            return false;
        }

        foreach (GridUnit controlledUnit in units)
        {
            if (controlledUnit == unit)
            {
                return true;
            }
        }

        return false;
    }

    private void RefreshUI()
    {
        if (turnText != null)
        {
            turnText.text = isPlayerTurn ? $"TURN {currentTurn}" : $"TURN {currentTurn}  WAIT";
        }

        if (endTurnButton != null)
        {
            endTurnButton.interactable = isPlayerTurn;
        }

        if (unitStatusButtons == null)
        {
            return;
        }

        int unitCount = units?.Length ?? 0;
        for (int i = 0; i < unitStatusButtons.Length; i++)
        {
            Button statusButton = unitStatusButtons[i];
            if (statusButton == null)
            {
                continue;
            }

            GridUnit unit = i < unitCount ? units[i] : null;
            bool hasMoved = unit != null && movedUnits.Contains(unit);
            bool isAvailable = unit != null && IsControlledUnit(unit) && !hasMoved;
            statusButton.interactable = isPlayerTurn && !isMoving && isAvailable;
            statusButton.image.color = hasMoved
                ? MovedColor
                : unit == selectedUnit ? SelectedColor : AvailableColor;
        }
    }

    private bool HandleKeyboardShortcuts(Keyboard keyboard)
    {
        if (keyboard.digit1Key.wasPressedThisFrame || keyboard.numpad1Key.wasPressedThisFrame) { SelectCardByIndex(0); return true; }
        if (keyboard.digit2Key.wasPressedThisFrame || keyboard.numpad2Key.wasPressedThisFrame) { SelectCardByIndex(1); return true; }
        if (keyboard.digit3Key.wasPressedThisFrame || keyboard.numpad3Key.wasPressedThisFrame) { SelectCardByIndex(2); return true; }
        if (keyboard.digit4Key.wasPressedThisFrame || keyboard.numpad4Key.wasPressedThisFrame) { SelectCardByIndex(3); return true; }
        if (keyboard.digit5Key.wasPressedThisFrame || keyboard.numpad5Key.wasPressedThisFrame) { SelectCardByIndex(4); return true; }
        if (keyboard.qKey.wasPressedThisFrame) { SelectUnitByIndex(0); return true; }
        if (keyboard.wKey.wasPressedThisFrame) { SelectUnitByIndex(1); return true; }
        if (keyboard.eKey.wasPressedThisFrame) { SelectUnitByIndex(2); return true; }
        if (keyboard.rKey.wasPressedThisFrame) { SelectUnitByIndex(3); return true; }
        if (keyboard.tKey.wasPressedThisFrame) { SelectUnitByIndex(4); return true; }
        return false;
    }

    private void BindSelectionUI()
    {
        UnbindUnitButtons();
        unitButtonActions = new UnityAction[unitStatusButtons.Length];
        for (int i = 0; i < unitStatusButtons.Length; i++)
        {
            Button button = unitStatusButtons[i];
            if (button == null)
            {
                continue;
            }

            int index = i;
            UnityAction action = () => SelectUnitByIndex(index);
            unitButtonActions[i] = action;
            button.onClick.AddListener(action);
        }

        for (int i = 0; i < cards.Length; i++)
        {
            cards[i]?.BindSelection(this, i);
        }
    }

    private void UnbindUnitButtons()
    {
        if (unitStatusButtons == null || unitButtonActions == null)
        {
            return;
        }

        for (int i = 0; i < unitStatusButtons.Length && i < unitButtonActions.Length; i++)
        {
            Button button = unitStatusButtons[i];
            UnityAction action = unitButtonActions[i];
            if (button != null && action != null)
            {
                button.onClick.RemoveListener(action);
            }
        }

        unitButtonActions = System.Array.Empty<UnityAction>();
    }

    private void CancelUnitSelection()
    {
        selectedUnit = null;
        if (field != null)
        {
            field.ClearHighlights();
        }

        RefreshUI();
    }

    private void ClearCardSelection()
    {
        if (selectedCardIndex >= 0 && selectedCardIndex < cards.Length && cards[selectedCardIndex] != null)
        {
            cards[selectedCardIndex].SetSelected(false);
        }

        selectedCardIndex = -1;
    }

    private void ClearAllSelections()
    {
        CancelUnitSelection();
        ClearCardSelection();
    }
}
