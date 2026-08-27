using UnityEngine;
using UnityEngine.UI;

public sealed class UnitSelectionDetailView : MonoBehaviour
{
    [SerializeField] private GameObject tagSlotPanel;
    [SerializeField] private GameObject actionButtonPanel;
    [SerializeField] private Text[] tagNameTexts = System.Array.Empty<Text>();
    [SerializeField] private Text[] tagContentTexts = System.Array.Empty<Text>();
    [SerializeField] private Text[] tagPointTexts = System.Array.Empty<Text>();
    [SerializeField] private Button[] actionButtons = System.Array.Empty<Button>();

    public void Configure(
        GameObject targetTagSlotPanel,
        GameObject targetActionButtonPanel,
        Text[] targetTagNameTexts,
        Text[] targetTagContentTexts,
        Text[] targetTagPointTexts,
        Button[] targetActionButtons)
    {
        tagSlotPanel = targetTagSlotPanel;
        actionButtonPanel = targetActionButtonPanel;
        tagNameTexts = targetTagNameTexts ?? System.Array.Empty<Text>();
        tagContentTexts = targetTagContentTexts ?? System.Array.Empty<Text>();
        tagPointTexts = targetTagPointTexts ?? System.Array.Empty<Text>();
        actionButtons = targetActionButtons ?? System.Array.Empty<Button>();
        Refresh(null, false, false);
    }

    public void Refresh(GridUnit selectedUnit, bool isPlayerTurn, bool actionsEnabled)
    {
        bool isVisible = selectedUnit != null && selectedUnit.IsPlayerControlled && isPlayerTurn;
        if (tagSlotPanel != null)
        {
            tagSlotPanel.SetActive(isVisible);
        }

        if (actionButtonPanel != null)
        {
            actionButtonPanel.SetActive(isVisible);
        }

        foreach (Button actionButton in actionButtons)
        {
            if (actionButton != null)
            {
                actionButton.interactable = isVisible && actionsEnabled;
            }
        }
    }
}
