using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Camera))]
public sealed class FieldCameraPan : MonoBehaviour
{
    [SerializeField] private FieldGenerator field;

    private Camera panCamera;
    private Vector2 previousPointerPosition;
    private bool isDragging;

    public void Configure(FieldGenerator targetField)
    {
        field = targetField;
        EnsureCamera();
        SnapToPlayerSide();
    }

    private void Awake()
    {
        EnsureCamera();
        field ??= FindFirstObjectByType<FieldGenerator>();
        ClampToField();
    }

    private void Update()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null || panCamera == null || field == null || !panCamera.orthographic)
        {
            isDragging = false;
            return;
        }

        Vector2 pointerPosition = mouse.position.ReadValue();
        if (mouse.rightButton.wasPressedThisFrame)
        {
            isDragging = EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject();
            previousPointerPosition = pointerPosition;
        }

        if (mouse.rightButton.wasReleasedThisFrame)
        {
            isDragging = false;
        }

        if (!isDragging || !mouse.rightButton.isPressed)
        {
            return;
        }

        Vector2 pointerDelta = pointerPosition - previousPointerPosition;
        previousPointerPosition = pointerPosition;
        float worldUnitsPerPixel = panCamera.orthographicSize * 2f / Mathf.Max(1, Screen.height);
        transform.position += new Vector3(
            -pointerDelta.x * worldUnitsPerPixel,
            0f,
            -pointerDelta.y * worldUnitsPerPixel);
        ClampToField();
    }

    private void EnsureCamera()
    {
        panCamera ??= GetComponent<Camera>();
    }

    private void SnapToPlayerSide()
    {
        if (panCamera == null || field == null)
        {
            return;
        }

        Vector3 position = transform.position;
        position.x = field.transform.position.x;
        position.z = field.transform.position.z - field.Depth * FieldGenerator.CellSizeMeters * 0.5f + panCamera.orthographicSize;
        transform.position = position;
        ClampToField();
    }

    private void ClampToField()
    {
        if (panCamera == null || field == null || !panCamera.orthographic)
        {
            return;
        }

        Vector3 center = field.transform.position;
        float halfFieldWidth = field.Width * FieldGenerator.CellSizeMeters * 0.5f;
        float halfFieldDepth = field.Depth * FieldGenerator.CellSizeMeters * 0.5f;
        float halfViewDepth = panCamera.orthographicSize;
        float halfViewWidth = halfViewDepth * Mathf.Max(0.01f, panCamera.aspect);

        Vector3 position = transform.position;
        position.x = ClampAxis(position.x, center.x, halfFieldWidth, halfViewWidth);
        position.z = ClampAxis(position.z, center.z, halfFieldDepth, halfViewDepth);
        transform.position = position;
    }

    private static float ClampAxis(float value, float center, float halfField, float halfView)
    {
        if (halfView >= halfField)
        {
            return center;
        }

        return Mathf.Clamp(value, center - halfField + halfView, center + halfField - halfView);
    }
}
