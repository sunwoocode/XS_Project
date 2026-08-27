using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Camera))]
public sealed class FieldCameraPan : MonoBehaviour
{
    [SerializeField] private FieldGenerator field;
    [SerializeField, Range(20f, 80f)] private float pitchDegrees = 55f;
    [SerializeField, Range(-180f, 180f)] private float yawDegrees = 45f;
    [SerializeField, Min(1f)] private float focusDistance = 12f;
    [SerializeField, Range(20f, 80f)] private float fieldOfView = 42f;
    [SerializeField, Min(0f)] private float focusDuration = 0.25f;
    [SerializeField, Min(0.01f)] private float dragSensitivity = 1f;

    private Camera panCamera;
    private Vector2 previousPointerPosition;
    private Vector3 focusPoint;
    private Vector3 focusStartPoint;
    private Vector3 targetFocusPoint;
    private float focusElapsed;
    private bool isDragging;
    private bool isFocusing;
    private bool isInitialized;

    public void Configure(FieldGenerator targetField)
    {
        field = targetField;
        EnsureCamera();
        ApplyCameraSettings();
        SnapToPlayerSide();
    }

    public void FocusOn(Vector3 worldPosition)
    {
        EnsureInitialized();
        if (!isInitialized)
        {
            return;
        }

        focusStartPoint = focusPoint;
        targetFocusPoint = ClampFocusPoint(worldPosition);
        focusElapsed = 0f;
        isFocusing = focusDuration > 0f &&
                     (targetFocusPoint - focusStartPoint).sqrMagnitude > 0.000001f;

        if (!isFocusing)
        {
            focusPoint = targetFocusPoint;
            ApplyCameraTransform();
        }
    }

    private void Awake()
    {
        EnsureCamera();
        field ??= FindFirstObjectByType<FieldGenerator>();
        ApplyCameraSettings();
        InitializeFocusFromTransform();
    }

    private void OnValidate()
    {
        pitchDegrees = Mathf.Clamp(pitchDegrees, 20f, 80f);
        yawDegrees = Mathf.Clamp(yawDegrees, -180f, 180f);
        focusDistance = Mathf.Max(1f, focusDistance);
        fieldOfView = Mathf.Clamp(fieldOfView, 20f, 80f);
        focusDuration = Mathf.Max(0f, focusDuration);
        dragSensitivity = Mathf.Max(0.01f, dragSensitivity);

        if (!Application.isPlaying)
        {
            EnsureCamera();
            ApplyCameraSettings();
        }
    }

    private void Update()
    {
        EnsureInitialized();
        if (!isInitialized)
        {
            isDragging = false;
            return;
        }

        UpdateFocusTransition();
        UpdatePointerDrag();
    }

    private void UpdateFocusTransition()
    {
        if (!isFocusing)
        {
            return;
        }

        focusElapsed += Time.deltaTime;
        float progress = focusDuration <= 0f ? 1f : Mathf.Clamp01(focusElapsed / focusDuration);
        float smoothedProgress = Mathf.SmoothStep(0f, 1f, progress);
        focusPoint = Vector3.Lerp(focusStartPoint, targetFocusPoint, smoothedProgress);
        ApplyCameraTransform();

        if (progress >= 1f)
        {
            focusPoint = targetFocusPoint;
            isFocusing = false;
            ApplyCameraTransform();
        }
    }

    private void UpdatePointerDrag()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null)
        {
            isDragging = false;
            return;
        }

        Vector2 pointerPosition = mouse.position.ReadValue();
        if (mouse.middleButton.wasPressedThisFrame)
        {
            isDragging = EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject();
            previousPointerPosition = pointerPosition;
            if (isDragging)
            {
                CancelFocusTransition();
            }
        }

        if (mouse.middleButton.wasReleasedThisFrame)
        {
            isDragging = false;
        }

        if (!isDragging || !mouse.middleButton.isPressed)
        {
            return;
        }

        Vector2 pointerDelta = pointerPosition - previousPointerPosition;
        previousPointerPosition = pointerPosition;
        if (pointerDelta.sqrMagnitude <= 0f)
        {
            return;
        }

        float worldUnitsPerPixel = 2f * focusDistance *
                                   Mathf.Tan(fieldOfView * 0.5f * Mathf.Deg2Rad) /
                                   Mathf.Max(1, Screen.height);
        Vector3 groundRight = Vector3.ProjectOnPlane(transform.right, Vector3.up).normalized;
        Vector3 groundForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
        Vector3 movement = (-groundRight * pointerDelta.x - groundForward * pointerDelta.y) *
                           worldUnitsPerPixel * dragSensitivity;

        focusPoint = ClampFocusPoint(focusPoint + movement);
        targetFocusPoint = focusPoint;
        ApplyCameraTransform();
    }

    private void EnsureCamera()
    {
        panCamera ??= GetComponent<Camera>();
    }

    private void EnsureInitialized()
    {
        EnsureCamera();
        field ??= FindFirstObjectByType<FieldGenerator>();
        if (!isInitialized && panCamera != null && field != null)
        {
            ApplyCameraSettings();
            InitializeFocusFromTransform();
        }
    }

    private void ApplyCameraSettings()
    {
        if (panCamera == null)
        {
            return;
        }

        panCamera.orthographic = false;
        panCamera.fieldOfView = fieldOfView;
        transform.rotation = Quaternion.Euler(pitchDegrees, yawDegrees, 0f);
    }

    private void InitializeFocusFromTransform()
    {
        if (panCamera == null || field == null)
        {
            return;
        }

        focusPoint = ClampFocusPoint(transform.position + transform.forward * focusDistance);
        focusStartPoint = focusPoint;
        targetFocusPoint = focusPoint;
        focusElapsed = 0f;
        isFocusing = false;
        isInitialized = true;
        ApplyCameraTransform();
    }

    private void SnapToPlayerSide()
    {
        if (panCamera == null || field == null)
        {
            return;
        }

        Vector3 center = field.transform.position;
        float halfFieldDepth = field.Depth * FieldGenerator.CellSizeMeters * 0.5f;
        float playerSideOffset = Mathf.Min(4f, halfFieldDepth);
        focusPoint = new Vector3(center.x, center.y, center.z - halfFieldDepth + playerSideOffset);
        focusPoint = ClampFocusPoint(focusPoint);
        focusStartPoint = focusPoint;
        targetFocusPoint = focusPoint;
        focusElapsed = 0f;
        isFocusing = false;
        isInitialized = true;
        ApplyCameraTransform();
    }

    private Vector3 ClampFocusPoint(Vector3 point)
    {
        if (field == null)
        {
            return point;
        }

        Vector3 center = field.transform.position;
        float halfFieldWidth = field.Width * FieldGenerator.CellSizeMeters * 0.5f;
        float halfFieldDepth = field.Depth * FieldGenerator.CellSizeMeters * 0.5f;
        point.x = Mathf.Clamp(point.x, center.x - halfFieldWidth, center.x + halfFieldWidth);
        point.z = Mathf.Clamp(point.z, center.z - halfFieldDepth, center.z + halfFieldDepth);
        return point;
    }

    private void CancelFocusTransition()
    {
        isFocusing = false;
        focusStartPoint = focusPoint;
        targetFocusPoint = focusPoint;
        focusElapsed = 0f;
    }

    private void ApplyCameraTransform()
    {
        transform.rotation = Quaternion.Euler(pitchDegrees, yawDegrees, 0f);
        transform.position = focusPoint - transform.forward * focusDistance;
    }
}
