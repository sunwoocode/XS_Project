using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-100)]
public sealed class CardCsvLoader : MonoBehaviour
{
    private const string DefaultCsvResourcePath = "CardData/cards";
    private const float HandWidth = 720f;
    private const float CardWidth = 164f;
    private const float CardHeight = 226f;
    private const float MaximumSpacing = 110f;
    private const int HandSize = 3;

    [SerializeField] private TextAsset cardCsv;
    [SerializeField] private CardView cardPrefab;
    [SerializeField] private UnitSelectionController selectionController;

    private CardDeck deck;
    private bool isSubscribed;

    public void Configure(CardView newCardPrefab, UnitSelectionController newSelectionController)
    {
        cardPrefab = newCardPrefab;
        selectionController = newSelectionController;
    }

    public void Configure(
        TextAsset newCardCsv,
        CardView newCardPrefab,
        UnitSelectionController newSelectionController)
    {
        cardCsv = newCardCsv;
        Configure(newCardPrefab, newSelectionController);
    }

    private void Awake()
    {
        cardCsv ??= Resources.Load<TextAsset>(DefaultCsvResourcePath);
        selectionController ??= FindFirstObjectByType<UnitSelectionController>();

        if (cardCsv == null)
        {
            Debug.LogError(
                $"CardCsvLoader의 cardCsv 참조가 비어 있고 " +
                $"Resources/{DefaultCsvResourcePath}.csv도 로드할 수 없습니다. " +
                "Assets/Resources/CardData/cards.csv 파일과 씬 참조를 확인하세요.",
                this);
            selectionController?.SetCards(System.Array.Empty<CardView>());
            return;
        }

        if (cardPrefab == null)
        {
            Debug.LogError("CardCsvLoader에 공통 CardView 프리팹이 연결되지 않았습니다.", this);
            selectionController?.SetCards(System.Array.Empty<CardView>());
            return;
        }

        List<CardData> cardData = CardCsvParser.Parse(cardCsv.text, message => Debug.LogWarning(message, this));
        deck = new CardDeck(cardData);
        if (deck.CardCount < HandSize)
        {
            Debug.LogWarning($"유효한 고유 카드가 {deck.CardCount}장뿐이므로 턴마다 가능한 카드만 지급합니다.", this);
        }
    }

    private void OnEnable()
    {
        SubscribeToTurnStart();
    }

    private void OnDisable()
    {
        UnsubscribeFromTurnStart();
    }

    private void SubscribeToTurnStart()
    {
        if (isSubscribed || selectionController == null)
        {
            return;
        }

        selectionController.PlayerTurnStarted += DrawHand;
        isSubscribed = true;
    }

    private void UnsubscribeFromTurnStart()
    {
        if (!isSubscribed || selectionController == null)
        {
            return;
        }

        selectionController.PlayerTurnStarted -= DrawHand;
        isSubscribed = false;
    }

    public void DrawHand()
    {
        if (deck == null)
        {
            selectionController?.SetCards(System.Array.Empty<CardView>());
            return;
        }

        RebuildHand(deck.DrawUniqueHand(HandSize));
    }

    private void RebuildHand(IReadOnlyList<CardData> data)
    {
        CardView[] existingCards = GetComponentsInChildren<CardView>(true);
        foreach (CardView existingCard in existingCards)
        {
            if (existingCard.transform.parent == transform)
            {
                existingCard.gameObject.SetActive(false);
                Destroy(existingCard.gameObject);
            }
        }

        CardView[] cards = new CardView[data.Count];
        float spacing = data.Count <= 1 ? 0f : Mathf.Min(MaximumSpacing, (HandWidth - CardWidth) / (data.Count - 1));

        for (int i = 0; i < data.Count; i++)
        {
            CardView card = Instantiate(cardPrefab, transform);
            card.name = $"Card_{i + 1}_{data[i].Index}";
            card.Configure(data[i]);

            RectTransform rect = (RectTransform)card.transform;
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.sizeDelta = new Vector2(CardWidth, CardHeight);

            float normalized = data.Count <= 1 ? 0f : i * 2f / (data.Count - 1f) - 1f;
            float x = (i - (data.Count - 1f) * 0.5f) * spacing;
            float y = 38f - 30f * Mathf.Abs(normalized);
            rect.anchoredPosition = new Vector2(x, y);
            rect.localRotation = Quaternion.Euler(0f, 0f, -10f * normalized);
            rect.localScale = Vector3.one;
            card.GetComponent<CardHoverUI>()?.CaptureHomePose();
            cards[i] = card;
        }

        selectionController?.SetCards(cards);
    }
}
