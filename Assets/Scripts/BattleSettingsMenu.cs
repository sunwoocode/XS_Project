using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class BattleSettingsMenu : MonoBehaviour
{
    private const string LobbySceneName = "LobbyScene";

    [SerializeField] private GameObject menuOverlay;
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button lobbyButton;
    [SerializeField] private UnitSelectionController selectionController;
    [SerializeField] private FieldCameraPan fieldCameraPan;

    private GameObject previouslySelectedObject;
    private float previousTimeScale = 1f;
    private bool selectionControllerWasEnabled;
    private bool fieldCameraPanWasEnabled;
    private bool isOpen;

    public bool IsOpen => isOpen;

    public void Configure(
        GameObject targetMenuOverlay,
        Button targetResumeButton,
        Button targetLobbyButton,
        UnitSelectionController targetSelectionController,
        FieldCameraPan targetFieldCameraPan)
    {
        menuOverlay = targetMenuOverlay;
        resumeButton = targetResumeButton;
        lobbyButton = targetLobbyButton;
        selectionController = targetSelectionController;
        fieldCameraPan = targetFieldCameraPan;

        if (!Application.isPlaying && menuOverlay != null)
        {
            menuOverlay.SetActive(false);
        }
    }

    private void Awake()
    {
        selectionController ??= FindFirstObjectByType<UnitSelectionController>();
        fieldCameraPan ??= FindFirstObjectByType<FieldCameraPan>();

        if (menuOverlay != null)
        {
            menuOverlay.SetActive(false);
        }

        resumeButton?.onClick.AddListener(CloseMenu);
        lobbyButton?.onClick.AddListener(ReturnToLobby);
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
        {
            ToggleMenu();
        }
    }

    private void OnDestroy()
    {
        resumeButton?.onClick.RemoveListener(CloseMenu);
        lobbyButton?.onClick.RemoveListener(ReturnToLobby);

        if (isOpen)
        {
            isOpen = false;
            RestoreGameplayState();
        }
    }

    private void OnDisable()
    {
        if (!isOpen)
        {
            return;
        }

        menuOverlay?.SetActive(false);
        isOpen = false;
        RestoreGameplayState();
    }

    public void OpenMenu()
    {
        if (isOpen || menuOverlay == null)
        {
            return;
        }

        selectionController ??= FindFirstObjectByType<UnitSelectionController>();
        fieldCameraPan ??= FindFirstObjectByType<FieldCameraPan>();
        selectionController?.ClearSelectedCard();

        previousTimeScale = Time.timeScale;
        selectionControllerWasEnabled = selectionController != null && selectionController.enabled;
        fieldCameraPanWasEnabled = fieldCameraPan != null && fieldCameraPan.enabled;
        previouslySelectedObject = EventSystem.current != null
            ? EventSystem.current.currentSelectedGameObject
            : null;

        if (selectionController != null)
        {
            selectionController.enabled = false;
        }

        if (fieldCameraPan != null)
        {
            fieldCameraPan.enabled = false;
        }

        Time.timeScale = 0f;
        isOpen = true;
        menuOverlay.SetActive(true);
        menuOverlay.transform.SetAsLastSibling();

        if (EventSystem.current != null && resumeButton != null)
        {
            EventSystem.current.SetSelectedGameObject(resumeButton.gameObject);
        }
    }

    public void CloseMenu()
    {
        if (!isOpen)
        {
            return;
        }

        menuOverlay?.SetActive(false);
        isOpen = false;
        RestoreGameplayState();

        if (EventSystem.current != null)
        {
            GameObject selectionToRestore = previouslySelectedObject != null && previouslySelectedObject.activeInHierarchy
                ? previouslySelectedObject
                : null;
            EventSystem.current.SetSelectedGameObject(selectionToRestore);
        }

        previouslySelectedObject = null;
    }

    public void ToggleMenu()
    {
        if (isOpen)
        {
            CloseMenu();
        }
        else
        {
            OpenMenu();
        }
    }

    public void ReturnToLobby()
    {
        if (isOpen)
        {
            menuOverlay?.SetActive(false);
            isOpen = false;
            RestoreGameplayState();
        }

        Time.timeScale = 1f;
        SceneManager.LoadScene(LobbySceneName);
    }

    private void RestoreGameplayState()
    {
        Time.timeScale = previousTimeScale;

        if (selectionController != null)
        {
            selectionController.enabled = selectionControllerWasEnabled;
        }

        if (fieldCameraPan != null)
        {
            fieldCameraPan.enabled = fieldCameraPanWasEnabled;
        }
    }
}
