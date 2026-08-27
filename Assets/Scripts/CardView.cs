using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class CardView : MonoBehaviour, IPointerClickHandler
{
    private const string CardImageResourceFolder = "CardImages/";
    private const float FrameColorMultiplier = 0.62f;
    private const float BodyNeutralBlend = 0.45f;
    private static readonly Color BodyNeutralColor = new(0.82f, 0.82f, 0.82f, 1f);
    private static readonly Dictionary<string, Sprite> ArtworkCache = new(StringComparer.Ordinal);
    private static readonly HashSet<string> MissingArtworkWarnings = new(StringComparer.Ordinal);

    [Header("Card Data")]
    [SerializeField] private string cardName = CardData.DefaultCardName;
    [SerializeField, Min(0)] private int tagPoint = CardData.DefaultTagPoint;
    [SerializeField, Min(0)] private int cost = CardData.DefaultCost;
    [SerializeField] private string imagePath = "";
    [SerializeField, TextArea(2, 5)] private string effectText = CardData.DefaultEffectText;
    [SerializeField] private Color raceColor = new(179f / 255f, 175f / 255f, 175f / 255f, 1f);

    [Header("View References")]
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Image effectBackgroundImage;
    [SerializeField] private Text cardNameText;
    [SerializeField] private Text tagPointText;
    [SerializeField] private Text costText;
    [SerializeField] private Image artworkImage;
    [SerializeField] private Text effectDescriptionText;
    [SerializeField] private CardHoverUI hoverUI;
    [SerializeField] private Outline selectionOutline;

    private UnitSelectionController selectionController;
    private int handIndex = -1;
    private bool defaultRaycastTarget;
    private bool hasCachedRaycastTarget;

    public int TagPoint => tagPoint;
    public int Cost => cost;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetArtworkCache()
    {
        ArtworkCache.Clear();
        MissingArtworkWarnings.Clear();
    }

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
        Image newEffectBackgroundImage,
        Text newCardNameText,
        Text newTagPointText,
        Text newCostText,
        Image newArtworkImage,
        Text newEffectDescriptionText)
    {
        backgroundImage = newBackgroundImage;
        hasCachedRaycastTarget = false;
        CacheDefaultRaycastTarget();
        effectBackgroundImage = newEffectBackgroundImage;
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
        ApplySelectionState(selected, null);
    }

    public void SetSelected(bool selected, Vector2 screenPosition)
    {
        ApplySelectionState(selected, screenPosition);
    }

    private void ApplySelectionState(bool selected, Vector2? screenPosition)
    {
        if (selectionOutline != null)
        {
            selectionOutline.enabled = false;
        }

        CacheDefaultRaycastTarget();
        if (backgroundImage != null)
        {
            backgroundImage.raycastTarget = selected ? false : defaultRaycastTarget;
        }

        if (hoverUI != null)
        {
            if (screenPosition.HasValue)
            {
                hoverUI.SetSelected(selected, screenPosition.Value);
            }
            else
            {
                hoverUI.SetSelected(selected);
            }
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left && selectionController != null)
        {
            selectionController.SelectCardByIndex(handIndex, eventData.position);
        }
    }

    private void Awake()
    {
        CacheDefaultRaycastTarget();
        Refresh();
    }

    private void OnDisable()
    {
        if (selectionOutline != null)
        {
            selectionOutline.enabled = false;
        }

        if (backgroundImage != null && hasCachedRaycastTarget)
        {
            backgroundImage.raycastTarget = defaultRaycastTarget;
        }
    }

    private void CacheDefaultRaycastTarget()
    {
        if (!hasCachedRaycastTarget && backgroundImage != null)
        {
            defaultRaycastTarget = backgroundImage.raycastTarget;
            hasCachedRaycastTarget = true;
        }
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
            backgroundImage.color = GetFrameColor(raceColor);
        }

        if (effectBackgroundImage != null)
        {
            effectBackgroundImage.color = GetBodyColor(raceColor);
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
            Sprite artwork = LoadArtwork(imagePath);
            artworkImage.sprite = artwork;
            artworkImage.enabled = artwork != null;
            artworkImage.preserveAspect = true;
        }
    }

    private static Color GetFrameColor(Color source)
    {
        return new Color(
            Mathf.Clamp01(source.r * FrameColorMultiplier),
            Mathf.Clamp01(source.g * FrameColorMultiplier),
            Mathf.Clamp01(source.b * FrameColorMultiplier),
            1f);
    }

    private static Color GetBodyColor(Color source)
    {
        source.a = 1f;
        Color bodyColor = Color.Lerp(source, BodyNeutralColor, BodyNeutralBlend);
        bodyColor.a = 1f;
        return bodyColor;
    }

    private Sprite LoadArtwork(string configuredPath)
    {
        string resourcePath = NormalizeArtworkPath(configuredPath);
        if (string.IsNullOrEmpty(resourcePath))
        {
            return null;
        }

        if (ArtworkCache.TryGetValue(resourcePath, out Sprite cachedArtwork))
        {
            return cachedArtwork;
        }

        Sprite artwork = Resources.Load<Sprite>(resourcePath);
        if (artwork == null)
        {
            Texture2D texture = Resources.Load<Texture2D>(resourcePath);
            if (texture != null)
            {
                artwork = Sprite.Create(
                    texture,
                    new Rect(0f, 0f, texture.width, texture.height),
                    new Vector2(0.5f, 0.5f),
                    100f);
                artwork.name = $"{texture.name}_RuntimeSprite";
                artwork.hideFlags = HideFlags.HideAndDontSave;
            }
        }

        ArtworkCache[resourcePath] = artwork;
        if (artwork == null && Application.isPlaying && MissingArtworkWarnings.Add(resourcePath))
        {
            Debug.LogWarning($"카드 이미지 Resources/{resourcePath}를 찾을 수 없습니다.", this);
        }

        return artwork;
    }

    private static string NormalizeArtworkPath(string configuredPath)
    {
        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            return string.Empty;
        }

        string normalized = configuredPath.Trim().Replace('\\', '/').TrimStart('/');
        int lastSlash = normalized.LastIndexOf('/');
        int extensionSeparator = normalized.LastIndexOf('.');
        if (extensionSeparator > lastSlash)
        {
            normalized = normalized.Substring(0, extensionSeparator);
        }

        if (normalized.StartsWith(CardImageResourceFolder, StringComparison.OrdinalIgnoreCase))
        {
            return CardImageResourceFolder + normalized.Substring(CardImageResourceFolder.Length);
        }

        return CardImageResourceFolder + normalized;
    }
}
