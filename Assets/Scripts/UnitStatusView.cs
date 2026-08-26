using UnityEngine;
using UnityEngine.UI;

public sealed class UnitStatusView : MonoBehaviour
{
    private static readonly Color AvailableColor = new(0.28f, 0.30f, 0.32f, 0.94f);
    private static readonly Color EndedColor = new(0.72f, 0.12f, 0.12f, 0.96f);
    private static readonly Color ActionPointAvailableColor = new(0.31f, 0.61f, 0.88f, 1f);
    private static readonly Color ActionPointSpentColor = new(0.16f, 0.18f, 0.21f, 0.92f);

    [SerializeField] private Button selectionButton;
    [SerializeField] private Image background;
    [SerializeField] private Text unitNameText;
    [SerializeField] private Image[] actionPointSlots = System.Array.Empty<Image>();
    [SerializeField] private Outline selectionOutline;

    public Button SelectionButton => selectionButton;

    public void Configure(
        Button button,
        Image targetBackground,
        Text targetUnitNameText,
        Image[] targetActionPointSlots,
        Outline targetSelectionOutline)
    {
        selectionButton = button;
        background = targetBackground;
        unitNameText = targetUnitNameText;
        actionPointSlots = targetActionPointSlots ?? System.Array.Empty<Image>();
        selectionOutline = targetSelectionOutline;
    }

    public void Refresh(GridUnit unit, bool isSelected, bool canSelect)
    {
        bool hasUnit = unit != null;
        gameObject.SetActive(hasUnit);
        if (!hasUnit)
        {
            return;
        }

        bool hasEndedAction = unit.RemainingActionPoints <= 0;
        if (background != null)
        {
            background.color = hasEndedAction ? EndedColor : AvailableColor;
        }

        if (unitNameText != null)
        {
            unitNameText.text = unit.DisplayName.ToUpperInvariant();
        }

        if (selectionButton != null)
        {
            selectionButton.interactable = canSelect;
        }

        if (selectionOutline != null)
        {
            selectionOutline.enabled = isSelected;
        }

        for (int i = 0; i < actionPointSlots.Length; i++)
        {
            Image slot = actionPointSlots[i];
            if (slot != null)
            {
                slot.color = i < unit.RemainingActionPoints
                    ? ActionPointAvailableColor
                    : ActionPointSpentColor;
            }
        }
    }
}
