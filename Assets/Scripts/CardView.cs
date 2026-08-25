using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class CardView : MonoBehaviour, IPointerClickHandler
{
    [Header("Card Data")]
    [SerializeField] private string cardName = CardData.DefaultCardName;
    [SerializeField, Min(0)] private int tagPoint = CardData.DefaultTagPoint;
    [SerializeField, Min(0)] private int cost = CardData.DefaultCost;
    [SerializeField] private string imagePath = "";
    [SerializeField, TextArea(2, 5)] private string effectText = CardData.DefaultEffectText;
    [SerializeField] private Color raceColor = new(179f / 255f, 175f / 255f, 175f / 255f, 1f);

    [Header("View References")]
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Text cardNameText;
    [SerializeField] private Text tagPointText;
    [SerializeField] private Text costText;
    [SerializeField] private Image artworkImage;
    [SerializeField] private Text effectDescriptionText;
    [SerializeField] private CardHoverUI hoverUI;
    [SerializeField] private Outline selectionOutline;

    private UnitSelectionController selectionController;
    private int handIndex = -1;

    public int TagPoint => tagPoint;
    public int Cost => cost;

    public void Configure(CardData data)
    {
        data ??= new CardData();
        cardName = data.CardName;
        tagPoint = Mathf.Max(0, data.TagPoint);
        cost = Mathf.Max(0, data.Cost);
        imagePath = data.ImagePath;
        effectText = data.EffectText;
        raceColor = data.RaceColor;
        Refresh();
    }

    public void SetViewReferences(
        Image newBackgroundImage,
        Text newCardNameText,
        Text newTagPointText,
        Text newCostText,
        Image newArtworkImage,
        Text newEffectDescriptionText)
    {
        backgroundImage = newBackgroundImage;
        cardNameText = newCardNameText;
        tagPointText = newTagPointText;
        costText = newCostText;
        artworkImage = newArtworkImage;
        effectDescriptionText = newEffectDescriptionText;
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
        cardName = string.IsNullOrWhiteSpace(cardName) ? CardData.DefaultCardName : cardName;
        tagPoint = Mathf.Max(0, tagPoint);
        cost = Mathf.Max(0, cost);
        effectText = string.IsNullOrWhiteSpace(effectText) ? CardData.DefaultEffectText : effectText;
        Refresh();
    }

    private void Refresh()
    {
        if (backgroundImage != null)
        {
            backgroundImage.color = raceColor;
        }

        if (cardNameText != null)
        {
            cardNameText.text = cardName;
        }

        if (tagPointText != null)
        {
            tagPointText.text = tagPoint.ToString();
        }

        if (costText != null)
        {
            costText.text = cost.ToString();
        }

        if (effectDescriptionText != null)
        {
            effectDescriptionText.text = effectText;
        }

        if (artworkImage != null)
        {
            Sprite artwork = string.IsNullOrWhiteSpace(imagePath) ? null : Resources.Load<Sprite>(imagePath);
            artworkImage.sprite = artwork;
            artworkImage.enabled = artwork != null;
            artworkImage.preserveAspect = true;

            if (!string.IsNullOrWhiteSpace(imagePath) && artwork == null && Application.isPlaying)
            {
                Debug.LogWarning($"카드 이미지 Resources/{imagePath}를 찾을 수 없습니다.", this);
            }
        }
    }
}
