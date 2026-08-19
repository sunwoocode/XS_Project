using UnityEngine;
using UnityEngine.EventSystems;

public sealed class CardHoverUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField, Min(0f)] private float hoverLift = 110f;
    [SerializeField, Min(1f)] private float hoverScale = 1.12f;
    [SerializeField, Min(0f)] private float selectedLift = 54f;
    [SerializeField, Min(1f)] private float selectedScale = 1.06f;
    [SerializeField, Min(1f)] private float transitionSpeed = 14f;

    private RectTransform rectTransform;
    private Vector2 homePosition;
    private Quaternion homeRotation;
    private Vector3 homeScale;
    private int homeSiblingIndex;
    private bool isHovered;
    private bool isSelected;

    private void Awake()
    {
        rectTransform = (RectTransform)transform;
        homePosition = rectTransform.anchoredPosition;
        homeRotation = rectTransform.localRotation;
        homeScale = rectTransform.localScale;
        homeSiblingIndex = rectTransform.GetSiblingIndex();
    }

    private void Update()
    {
        float blend = 1f - Mathf.Exp(-transitionSpeed * Time.unscaledDeltaTime);
        float lift = (isSelected ? selectedLift : 0f) + (isHovered ? hoverLift : 0f);
        float scale = (isSelected ? selectedScale : 1f) * (isHovered ? hoverScale : 1f);
        Vector2 targetPosition = homePosition + Vector2.up * lift;
        Quaternion targetRotation = isHovered || isSelected ? Quaternion.identity : homeRotation;
        Vector3 targetScale = homeScale * scale;

        rectTransform.anchoredPosition = Vector2.Lerp(rectTransform.anchoredPosition, targetPosition, blend);
        rectTransform.localRotation = Quaternion.Slerp(rectTransform.localRotation, targetRotation, blend);
        rectTransform.localScale = Vector3.Lerp(rectTransform.localScale, targetScale, blend);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        isHovered = true;
        rectTransform.SetAsLastSibling();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isHovered = false;
        if (!isSelected && rectTransform.parent != null)
        {
            rectTransform.SetSiblingIndex(Mathf.Min(homeSiblingIndex, rectTransform.parent.childCount - 1));
        }
    }

    public void SetSelected(bool selected)
    {
        isSelected = selected;
        if (rectTransform == null || rectTransform.parent == null)
        {
            return;
        }

        if (isSelected)
        {
            rectTransform.SetAsLastSibling();
        }
        else if (!isHovered)
        {
            rectTransform.SetSiblingIndex(Mathf.Min(homeSiblingIndex, rectTransform.parent.childCount - 1));
        }
    }

    private void OnDisable()
    {
        if (rectTransform == null)
        {
            return;
        }

        isHovered = false;
        isSelected = false;
        rectTransform.anchoredPosition = homePosition;
        rectTransform.localRotation = homeRotation;
        rectTransform.localScale = homeScale;
    }

    private void OnValidate()
    {
        hoverLift = Mathf.Max(0f, hoverLift);
        hoverScale = Mathf.Max(1f, hoverScale);
        selectedLift = Mathf.Max(0f, selectedLift);
        selectedScale = Mathf.Max(1f, selectedScale);
        transitionSpeed = Mathf.Max(1f, transitionSpeed);
    }
}
