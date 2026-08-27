using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public sealed class CardHoverUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField, Min(0f)] private float hoverLift = 110f;
    [SerializeField, Min(1f)] private float hoverScale = 1.12f;
    [SerializeField, Min(1f)] private float transitionSpeed = 14f;

    private RectTransform rectTransform;
    private Vector2 homePosition;
    private Quaternion homeRotation;
    private Vector3 homeScale;
    private Vector2 selectedPosition;
    private int homeSiblingIndex;
    private bool isHovered;
    private bool isSelected;

    private void Awake()
    {
        CaptureHomePose();
    }

    public void CaptureHomePose()
    {
        rectTransform ??= (RectTransform)transform;
        homePosition = rectTransform.anchoredPosition;
        homeRotation = rectTransform.localRotation;
        homeScale = rectTransform.localScale;
        selectedPosition = homePosition;
        homeSiblingIndex = rectTransform.GetSiblingIndex();
    }

    private void Update()
    {
        if (isSelected && Mouse.current != null)
        {
            selectedPosition = GetAnchoredPositionForVisualCenter(Mouse.current.position.ReadValue());
        }

        float blend = 1f - Mathf.Exp(-transitionSpeed * Time.unscaledDeltaTime);
        float lift = !isSelected && isHovered ? hoverLift : 0f;
        float scale = !isSelected && isHovered ? hoverScale : 1f;
        Vector2 targetPosition = isSelected ? selectedPosition : homePosition + Vector2.up * lift;
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
        Vector2 screenPosition = Mouse.current != null
            ? Mouse.current.position.ReadValue()
            : new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        SetSelected(selected, screenPosition);
    }

    public void SetSelected(bool selected, Vector2 screenPosition)
    {
        isSelected = selected;
        if (rectTransform == null || rectTransform.parent == null)
        {
            return;
        }

        if (isSelected)
        {
            selectedPosition = GetAnchoredPositionForVisualCenter(screenPosition);
            rectTransform.SetAsLastSibling();
        }
        else
        {
            isHovered = false;
            rectTransform.SetSiblingIndex(Mathf.Min(homeSiblingIndex, rectTransform.parent.childCount - 1));
        }
    }

    private Vector2 GetAnchoredPositionForVisualCenter(Vector2 screenPosition)
    {
        if (rectTransform.parent is not RectTransform parentRect)
        {
            return homePosition;
        }

        Canvas canvas = GetComponentInParent<Canvas>();
        Camera eventCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.worldCamera
            : null;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parentRect,
                screenPosition,
                eventCamera,
                out Vector2 localPointerPosition))
        {
            return homePosition;
        }

        Vector2 anchorReference = new(
            Mathf.Lerp(parentRect.rect.xMin, parentRect.rect.xMax, rectTransform.anchorMin.x),
            Mathf.Lerp(parentRect.rect.yMin, parentRect.rect.yMax, rectTransform.anchorMin.y));
        Vector2 visualCenterFromPivot = Vector2.Scale(
            rectTransform.rect.center,
            new Vector2(homeScale.x, homeScale.y));
        return localPointerPosition - anchorReference - visualCenterFromPivot;
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
        transitionSpeed = Mathf.Max(1f, transitionSpeed);
    }
}
