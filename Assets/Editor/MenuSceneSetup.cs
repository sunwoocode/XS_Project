using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class MenuSceneSetup
{
    private const string TitleScenePath = "Assets/Scenes/TitleScene.unity";
    private const string LobbyScenePath = "Assets/Scenes/LobbyScene.unity";
    private const string BattleScenePath = "Assets/Scenes/SampleScene.unity";

    private static readonly Color BackgroundColor = new(0.055f, 0.065f, 0.075f, 1f);
    private static readonly Color PanelColor = new(0.08f, 0.09f, 0.10f, 0.96f);
    private static readonly Color ButtonColor = new(0.22f, 0.24f, 0.27f, 1f);
    private static readonly Color GoldColor = new(1f, 0.78f, 0.16f, 1f);

    [MenuItem("XS Project/Setup Menu Scenes")]
    public static void SetupMenuScenes()
    {
        EnsureScenesFolder();
        BuildTitleScene();
        BuildLobbyScene();
        ConfigureBuildSettings();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(TitleScenePath);
        EditorSceneManager.OpenScene(TitleScenePath, OpenSceneMode.Single);
        Debug.Log("TitleScene and LobbyScene setup completed.");
    }

    private static void BuildTitleScene()
    {
        Scene scene = CreateBaseScene("TitleCanvas", out Transform canvasRoot, out Font font, out SceneNavigation navigation, out EventSystem eventSystem);

        Image panel = CreateImage(canvasRoot, "TitlePanel", PanelColor);
        SetRect(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(620f, 360f));

        Text title = CreateText(panel.transform, "Title", font, 72, TextAnchor.MiddleCenter);
        SetRect(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 72f), new Vector2(560f, 110f));
        title.text = "XS PROJECT";
        title.color = Color.white;
        title.fontStyle = FontStyle.Bold;
        title.raycastTarget = false;

        Button startButton = CreateButton(panel.transform, "StartButton", "START", font, new Vector2(0f, -70f));
        UnityEventTools.AddPersistentListener(startButton.onClick, navigation.OpenLobby);
        eventSystem.firstSelectedGameObject = startButton.gameObject;

        EditorSceneManager.SaveScene(scene, TitleScenePath);
    }

    private static void BuildLobbyScene()
    {
        Scene scene = CreateBaseScene("LobbyCanvas", out Transform canvasRoot, out Font font, out SceneNavigation navigation, out EventSystem eventSystem);

        Text heading = CreateText(canvasRoot, "Heading", font, 42, TextAnchor.MiddleCenter);
        SetRect(heading.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 140f), new Vector2(420f, 72f));
        heading.text = "LOBBY";
        heading.color = Color.white;
        heading.fontStyle = FontStyle.Bold;
        heading.raycastTarget = false;

        Button battleButton = CreateButton(canvasRoot, "BattleButton", "Battle", font, Vector2.zero);
        UnityEventTools.AddPersistentListener(battleButton.onClick, navigation.StartBattle);
        eventSystem.firstSelectedGameObject = battleButton.gameObject;

        EditorSceneManager.SaveScene(scene, LobbyScenePath);
    }

    private static Scene CreateBaseScene(
        string canvasName,
        out Transform canvasRoot,
        out Font font,
        out SceneNavigation navigation,
        out EventSystem eventSystem)
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        GameObject cameraObject = new("Main Camera", typeof(Camera), typeof(AudioListener));
        cameraObject.tag = "MainCamera";
        Camera camera = cameraObject.GetComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = BackgroundColor;
        camera.transform.position = new Vector3(0f, 0f, -10f);

        GameObject navigationObject = new("SceneNavigation", typeof(SceneNavigation));
        navigation = navigationObject.GetComponent<SceneNavigation>();

        GameObject canvasObject = new(canvasName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1024f, 768f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        canvasRoot = canvasObject.transform;

        Image background = CreateImage(canvasRoot, "Background", BackgroundColor);
        SetRect(background.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        background.raycastTarget = false;
        background.transform.SetAsFirstSibling();

        eventSystem = EnsureEventSystem();
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        return scene;
    }

    private static Button CreateButton(Transform parent, string objectName, string labelText, Font font, Vector2 position)
    {
        Image buttonImage = CreateImage(parent, objectName, ButtonColor);
        SetRect(buttonImage.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), position, new Vector2(260f, 76f));

        Outline outline = buttonImage.gameObject.AddComponent<Outline>();
        outline.effectColor = GoldColor;
        outline.effectDistance = new Vector2(3f, -3f);
        outline.useGraphicAlpha = false;

        Button button = buttonImage.gameObject.AddComponent<Button>();
        button.targetGraphic = buttonImage;
        ColorBlock colors = button.colors;
        colors.normalColor = ButtonColor;
        colors.highlightedColor = new Color(0.34f, 0.36f, 0.39f, 1f);
        colors.selectedColor = new Color(0.34f, 0.36f, 0.39f, 1f);
        colors.pressedColor = new Color(0.14f, 0.16f, 0.19f, 1f);
        colors.disabledColor = new Color(0.15f, 0.15f, 0.15f, 0.65f);
        colors.colorMultiplier = 1f;
        button.colors = colors;

        Text label = CreateText(buttonImage.transform, "Label", font, 30, TextAnchor.MiddleCenter);
        SetRect(label.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        label.text = labelText;
        label.color = Color.white;
        label.fontStyle = FontStyle.Bold;
        label.raycastTarget = false;
        return button;
    }

    private static Image CreateImage(Transform parent, string objectName, Color color)
    {
        GameObject imageObject = new(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        imageObject.transform.SetParent(parent, false);
        Image image = imageObject.GetComponent<Image>();
        image.color = color;
        return image;
    }

    private static Text CreateText(Transform parent, string objectName, Font font, int fontSize, TextAnchor alignment)
    {
        GameObject textObject = new(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        textObject.transform.SetParent(parent, false);
        Text text = textObject.GetComponent<Text>();
        text.font = font;
        text.fontSize = fontSize;
        text.alignment = alignment;
        return text;
    }

    private static void SetRect(
        RectTransform rect,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 pivot,
        Vector2 anchoredPosition,
        Vector2 sizeDelta)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;
    }

    private static EventSystem EnsureEventSystem()
    {
        GameObject eventSystemObject = new("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        return eventSystemObject.GetComponent<EventSystem>();
    }

    private static void ConfigureBuildSettings()
    {
        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene(TitleScenePath, true),
            new EditorBuildSettingsScene(LobbyScenePath, true),
            new EditorBuildSettingsScene(BattleScenePath, true)
        };
    }

    private static void EnsureScenesFolder()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
        {
            AssetDatabase.CreateFolder("Assets", "Scenes");
        }
    }
}
