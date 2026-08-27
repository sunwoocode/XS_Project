using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public sealed class UnitSelectionController : MonoBehaviour
{
    private static readonly Color CostAvailableColor = new(0.10f, 0.36f, 0.62f, 0.96f);
    private static readonly Color CostWaitingColor = new(0.16f, 0.18f, 0.21f, 0.82f);

    [SerializeField] private FieldGenerator field;
    [SerializeField] private Camera selectionCamera;
    [SerializeField] private FieldCameraPan fieldCameraPan;
    [SerializeField] private GridUnit[] units = System.Array.Empty<GridUnit>();
    [SerializeField, Min(1)] private int movementRange = 3;
    [SerializeField, Min(0.01f)] private float secondsPerCell = 0.12f;
    [SerializeField, Min(0.1f)] private float opponentTurnSeconds = 3f;
    [SerializeField, Min(1)] private int maxCardCost = 3;
    [SerializeField, Min(0)] private int remainingCardCost = 3;
    [SerializeField] private Text turnText;
    [SerializeField] private Button endTurnButton;
    [SerializeField] private Image cardCostPanel;
    [SerializeField] private Text cardCostText;
    [SerializeField] private CanvasGroup unitStatusPanelGroup;
    [SerializeField] private UnitStatusView[] unitStatusViews = System.Array.Empty<UnitStatusView>();
    [SerializeField] private UnitSelectionDetailView unitSelectionDetailView;
    [SerializeField] private CardView[] cards = System.Array.Empty<CardView>();

    private GridUnit selectedUnit;
    private int selectedCardIndex = -1;
    private UnityAction[] unitButtonActions = System.Array.Empty<UnityAction>();
    private bool isMoving;
    private bool isPlayerTurn = true;
    private bool endTurnQueued;
    private int currentTurn = 1;

    public int MaxCardCost => maxCardCost;
    public int RemainingCardCost => remainingCardCost;
    public event System.Action PlayerTurnStarted;

    public void Configure(FieldGenerator targetField, Camera targetCamera, GridUnit[] controlledUnits)
    {
        field = targetField;
        selectionCamera = targetCamera;
        fieldCameraPan = targetCamera != null ? targetCamera.GetComponent<FieldCameraPan>() : null;
        units = controlledUnits ?? System.Array.Empty<GridUnit>();
    }

    public void ConfigureUI(
        Text targetTurnText,
        Button targetEndTurnButton,
        CanvasGroup targetUnitStatusPanelGroup,
        UnitStatusView[] statusViews,
        CardView[] handCards,
        Image targetCardCostPanel,
        Text targetCardCostText)
    {
        ConfigureUI(
            targetTurnText,
            targetEndTurnButton,
            targetUnitStatusPanelGroup,
            statusViews,
            handCards,
            targetCardCostPanel,
            targetCardCostText,
            unitSelectionDetailView);
    }

    public void ConfigureUI(
        Text targetTurnText,
        Button targetEndTurnButton,
        CanvasGroup targetUnitStatusPanelGroup,
        UnitStatusView[] statusViews,
        CardView[] handCards,
        Image targetCardCostPanel,
        Text targetCardCostText,
        UnitSelectionDetailView targetUnitSelectionDetailView)
    {
        turnText = targetTurnText;
        endTurnButton = targetEndTurnButton;
        unitStatusPanelGroup = targetUnitStatusPanelGroup;
        unitStatusViews = statusViews ?? System.Array.Empty<UnitStatusView>();
        cards = handCards ?? System.Array.Empty<CardView>();
        cardCostPanel = targetCardCostPanel;
        cardCostText = targetCardCostText;
        unitSelectionDetailView = targetUnitSelectionDetailView;
        RefreshUI();
    }

    private void Awake()
    {
        field ??= FindFirstObjectByType<FieldGenerator>();
        selectionCamera ??= Camera.main;
        fieldCameraPan ??= selectionCamera != null ? selectionCamera.GetComponent<FieldCameraPan>() : null;
        if (units == null || units.Length == 0)
        {
            units = FindObjectsByType<GridUnit>(FindObjectsSortMode.None);
            System.Array.Sort(units, (a, b) => a.UnitNumber.CompareTo(b.UnitNumber));
        }
    }

    private void Start()
    {
        remainingCardCost = maxCardCost;
        ResetControlledUnitActionPoints();
        if (endTurnButton != null)
        {
            endTurnButton.onClick.RemoveListener(EndPlayerTurn);
            endTurnButton.onClick.AddListener(EndPlayerTurn);
        }

        BindSelectionUI();

        RefreshUI();
        PlayerTurnStarted?.Invoke();
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
        maxCardCost = Mathf.Max(1, maxCardCost);
        remainingCardCost = Mathf.Clamp(remainingCardCost, 0, maxCardCost);
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        Mouse mouse = Mouse.current;
        if (mouse != null && mouse.rightButton.wasPressedThisFrame && selectedCardIndex >= 0)
        {
            ClearSelectedCard();
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
            SelectUnit(clickedUnit);
            return;
        }

        if (selectedUnit == null || !field.IsFieldCollider(hit.collider))
        {
            return;
        }

        if (!CanMoveSelectedUnit())
        {
            return;
        }

        if (field.TryGetCell(hit.point, out Vector2Int targetCell) &&
            field.TryBuildPath(targetCell, out List<Vector2Int> path) &&
            selectedUnit.TrySpendActionPoint())
        {
            RefreshUI();
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
        if (unit == null || !IsControlledUnit(unit))
        {
            return;
        }

        SelectUnit(unit);
    }

    public void SelectCardByIndex(int index)
    {
        Vector2 screenPosition = Mouse.current != null
            ? Mouse.current.position.ReadValue()
            : new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        SelectCardByIndex(index, screenPosition);
    }

    public void SelectCardByIndex(int index, Vector2 screenPosition)
    {
        if (!isPlayerTurn || isMoving || index < 0 || index >= cards.Length || cards[index] == null)
        {
            return;
        }

        int nextIndex = selectedCardIndex == index ? -1 : index;
        ClearSelectedCard();
        if (nextIndex >= 0)
        {
            selectedCardIndex = nextIndex;
            cards[selectedCardIndex].SetSelected(true, screenPosition);
        }
    }

    public bool CanAffordCard(int cost)
    {
        return cost >= 0 && remainingCardCost >= cost;
    }

    public bool TrySpendCardCost(int cost)
    {
        if (!isPlayerTurn || isMoving || !CanAffordCard(cost))
        {
            return false;
        }

        remainingCardCost -= cost;
        RefreshUI();
        return true;
    }

    public void SetCards(CardView[] handCards)
    {
        ClearSelectedCard();
        cards = handCards ?? System.Array.Empty<CardView>();
        for (int i = 0; i < cards.Length; i++)
        {
            cards[i]?.BindSelection(this, i);
        }

        RefreshUI();
    }

    private void SelectUnit(GridUnit unit)
    {
        if (unit == selectedUnit)
        {
            CancelUnitSelection();
            return;
        }

        selectedUnit = unit;
        FocusSelectedUnit();
        RefreshSelectedUnitMovementRange();
        RefreshUI();
    }

    private void RefreshSelectedUnitMovementRange()
    {
        if (!CanMoveSelectedUnit())
        {
            field?.ClearHighlights();
            return;
        }

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
            if (otherUnit != null && otherUnit != selectedUnit &&
                field.TryGetCell(otherUnit.transform.position, out Vector2Int occupiedCell))
            {
                blockedCells.Add(occupiedCell);
            }
        }

        field.ShowMovementRange(selectedUnit.transform.position, movementRange, blockedCells);
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

        isMoving = false;
        RefreshSelectedUnitMovementRange();
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
        remainingCardCost = 0;
        endTurnQueued = false;
        ClearAllSelections();
        RefreshUI();

        yield return new WaitForSeconds(opponentTurnSeconds);

        currentTurn++;
        ResetControlledUnitActionPoints();
        remainingCardCost = maxCardCost;
        isPlayerTurn = true;
        RefreshUI();
        PlayerTurnStarted?.Invoke();
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

    private bool CanMoveSelectedUnit()
    {
        return selectedUnit != null &&
               IsControlledUnit(selectedUnit) &&
               selectedUnit.RemainingActionPoints > 0;
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

        if (cardCostText != null)
        {
            cardCostText.text = $"COST {remainingCardCost} / {maxCardCost}";
            cardCostText.color = isPlayerTurn ? Color.white : new Color(0.62f, 0.65f, 0.70f, 1f);
        }

        if (cardCostPanel != null)
        {
            cardCostPanel.color = isPlayerTurn ? CostAvailableColor : CostWaitingColor;
        }

        if (unitStatusPanelGroup != null)
        {
            unitStatusPanelGroup.alpha = isPlayerTurn ? 1f : 0.48f;
            unitStatusPanelGroup.interactable = isPlayerTurn && !isMoving;
            unitStatusPanelGroup.blocksRaycasts = isPlayerTurn && !isMoving;
        }

        if (unitSelectionDetailView != null)
        {
            unitSelectionDetailView.Refresh(selectedUnit, isPlayerTurn, isPlayerTurn && !isMoving);
        }

        if (unitStatusViews == null)
        {
            return;
        }

        int unitCount = units?.Length ?? 0;
        for (int i = 0; i < unitStatusViews.Length; i++)
        {
            UnitStatusView statusView = unitStatusViews[i];
            if (statusView == null)
            {
                continue;
            }

            GridUnit unit = i < unitCount ? units[i] : null;
            bool canSelect = isPlayerTurn && !isMoving && unit != null && IsControlledUnit(unit);
            statusView.Refresh(unit, unit == selectedUnit, canSelect);
        }
    }

    private bool HandleKeyboardShortcuts(Keyboard keyboard)
    {
        if (keyboard.spaceKey.wasPressedThisFrame && FocusSelectedUnit()) { return true; }
        if (keyboard.digit1Key.wasPressedThisFrame || keyboard.numpad1Key.wasPressedThisFrame) { SelectCardByIndex(0); return true; }
        if (keyboard.digit2Key.wasPressedThisFrame || keyboard.numpad2Key.wasPressedThisFrame) { SelectCardByIndex(1); return true; }
        if (keyboard.digit3Key.wasPressedThisFrame || keyboard.numpad3Key.wasPressedThisFrame) { SelectCardByIndex(2); return true; }
        if (keyboard.qKey.wasPressedThisFrame) { SelectUnitByIndex(0); return true; }
        if (keyboard.wKey.wasPressedThisFrame) { SelectUnitByIndex(1); return true; }
        if (keyboard.eKey.wasPressedThisFrame) { SelectUnitByIndex(2); return true; }
        if (keyboard.rKey.wasPressedThisFrame) { SelectUnitByIndex(3); return true; }
        if (keyboard.tKey.wasPressedThisFrame) { SelectUnitByIndex(4); return true; }
        return false;
    }

    private bool FocusSelectedUnit()
    {
        if (selectedUnit == null || fieldCameraPan == null)
        {
            return false;
        }

        Vector3 visualCenter = selectedUnit.transform.position + Vector3.up * (GridUnit.HeightMeters * 0.5f);
        fieldCameraPan.FocusOn(visualCenter);
        return true;
    }

    public void ClearSelectedCard()
    {
        ClearCardSelection();
    }

    private void BindSelectionUI()
    {
        UnbindUnitButtons();
        unitButtonActions = new UnityAction[unitStatusViews.Length];
        for (int i = 0; i < unitStatusViews.Length; i++)
        {
            Button button = unitStatusViews[i]?.SelectionButton;
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
        if (unitStatusViews == null || unitButtonActions == null)
        {
            return;
        }

        for (int i = 0; i < unitStatusViews.Length && i < unitButtonActions.Length; i++)
        {
            Button button = unitStatusViews[i]?.SelectionButton;
            UnityAction action = unitButtonActions[i];
            if (button != null && action != null)
            {
                button.onClick.RemoveListener(action);
            }
        }

        unitButtonActions = System.Array.Empty<UnityAction>();
    }

    private void ResetControlledUnitActionPoints()
    {
        if (units == null)
        {
            return;
        }

        foreach (GridUnit unit in units)
        {
            if (unit != null && IsControlledUnit(unit))
            {
                unit.ResetActionPoints();
            }
        }
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
        ClearSelectedCard();
    }
}
