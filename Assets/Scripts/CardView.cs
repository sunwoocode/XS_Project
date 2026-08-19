using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class CardView : MonoBehaviour, IPointerClickHandler
{
    [Header("Card Data")]
    [SerializeField] private string cardName = "CARD";
    [SerializeField, Min(0)] private int cost = 1;
    [SerializeField] private string symbol = "?";
    [SerializeField] private Color frameColor = Color.white;

    [Header("View References")]
    [SerializeField] private Image frameImage;
    [SerializeField] private Image symbolPanel;
    [SerializeField] private Text titleText;
    [SerializeField] private Text costText;
    [SerializeField] private Text symbolText;
    [SerializeField] private CardHoverUI hoverUI;
    [SerializeField] private Outline selectionOutline;

    private UnitSelectionController selectionController;
    private int handIndex = -1;

    public int Cost => cost;

    public void Configure(string newCardName, int newCost, string newSymbol, Color newFrameColor)
    {
        cardName = newCardName;
        cost = Mathf.Max(0, newCost);
        symbol = newSymbol;
        frameColor = newFrameColor;
        Refresh();
    }

    public void SetViewReferences(
        Image newFrameImage,
        Image newSymbolPanel,
        Text newTitleText,
        Text newCostText,
        Text newSymbolText)
    {
        frameImage = newFrameImage;
        symbolPanel = newSymbolPanel;
        titleText = newTitleText;
        costText = newCostText;
        symbolText = newSymbolText;
        Refresh();
    }

    public void SetInteractionReferences(CardHoverUI newHoverUI, Outline newSelectionOutline)
    {
        hoverUI = newHoverUI;
        selectionOutline = newSelectionOutline;
        SetSelected(false);
    }

    public void BindSelection(UnitSelectionController controller, int index)
    {
        selectionController = controller;
        handIndex = index;
    }

    public void SetSelected(bool selected)
    {
        if (selectionOutline != null)
        {
            selectionOutline.enabled = selected;
        }

        if (hoverUI != null)
        {
            hoverUI.SetSelected(selected);
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left && selectionController != null)
        {
            selectionController.SelectCardByIndex(handIndex);
        }
    }

    private void Awake()
    {
        Refresh();
    }

    private void OnValidate()
    {
        cost = Mathf.Max(0, cost);
        Refresh();
    }

    private void Refresh()
    {
        if (frameImage != null)
        {
            frameImage.color = frameColor;
        }

        if (symbolPanel != null)
        {
            symbolPanel.color = new Color(
                frameColor.r * 0.55f,
                frameColor.g * 0.55f,
                frameColor.b * 0.55f,
                frameColor.a);
        }

        if (titleText != null)
        {
            titleText.text = cardName;
        }

        if (costText != null)
        {
            costText.text = cost.ToString();
        }

        if (symbolText != null)
        {
            symbolText.text = symbol;
        }
    }
}
