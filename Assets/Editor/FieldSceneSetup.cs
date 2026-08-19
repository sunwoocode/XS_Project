using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[InitializeOnLoad]
public static class FieldSceneSetup
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const string MaterialFolder = "Assets/Materials";
    private const string PrefabFolder = "Assets/Prefabs";
    private const string CardPrefabFolder = PrefabFolder + "/Cards";
    private const string FieldMaterialPath = MaterialFolder + "/FieldGrid.mat";
    private const string UnitMaterialPath = MaterialFolder + "/Unit.mat";
    private const string EnemyMaterialPath = MaterialFolder + "/Enemy.mat";
    private const string TerrainMaterialPath = MaterialFolder + "/Terrain.mat";
    private const string UnitPrefabPath = PrefabFolder + "/Unit_Player.prefab";
    private const string EnemyPrefabPath = PrefabFolder + "/Unit_Enemy.prefab";
    private const string Obstacle1x1PrefabPath = PrefabFolder + "/Terrain_Block_1x1.prefab";
    private const string Obstacle2x1PrefabPath = PrefabFolder + "/Terrain_Block_2x1.prefab";
    private const string CardPrefabPath = CardPrefabFolder + "/Card.prefab";
    private const string FieldShaderName = "XS Project/Field Grid";
    private const string SessionKey = "XSProject.FieldSceneSetup.UnitSelectedGridV8.Completed";

    static FieldSceneSetup()
    {
        EditorApplication.delayCall += SetupFieldOnce;
    }

    private static void SetupFieldOnce()
    {
        if (SessionState.GetBool(SessionKey, false) || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        SessionState.SetBool(SessionKey, true);
        SetupField();
    }

    [MenuItem("XS Project/Setup 10x10 Field")]
    public static void SetupField()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        EnsureAssetFolders();

        Material fieldMaterial = EnsureMaterial(
            FieldMaterialPath,
            "FieldGrid",
            FieldShaderName,
            new Color(0.22f, 0.50f, 0.27f, 1f),
            0.08f);
        Material unitMaterial = EnsureMaterial(
            UnitMaterialPath,
            "Unit",
            "Universal Render Pipeline/Lit",
            new Color(0.16f, 0.48f, 0.92f, 1f),
            0.28f);
        Material enemyMaterial = EnsureMaterial(
            EnemyMaterialPath,
            "Enemy",
            "Universal Render Pipeline/Lit",
            new Color(0.78f, 0.16f, 0.14f, 1f),
            0.24f);
        Material terrainMaterial = EnsureMaterial(
            TerrainMaterialPath,
            "Terrain",
            "Universal Render Pipeline/Lit",
            new Color(0.42f, 0.30f, 0.18f, 1f),
            0.08f);

        if (fieldMaterial == null || unitMaterial == null || enemyMaterial == null || terrainMaterial == null)
        {
            return;
        }

        GameObject unitPrefab = BuildUnitPrefab(
            "Unit_Player",
            GridUnitTeam.Player,
            unitMaterial,
            UnitPrefabPath);
        GameObject enemyPrefab = BuildUnitPrefab(
            "Unit_Enemy",
            GridUnitTeam.Enemy,
            enemyMaterial,
            EnemyPrefabPath);
        GameObject obstacle1x1Prefab = BuildObstaclePrefab(
            "Terrain_Block_1x1",
            1,
            1,
            terrainMaterial,
            Obstacle1x1PrefabPath);
        GameObject obstacle2x1Prefab = BuildObstaclePrefab(
            "Terrain_Block_2x1",
            2,
            1,
            terrainMaterial,
            Obstacle2x1PrefabPath);
        GameObject cardPrefab = BuildCardPrefab(CardPrefabPath);

        DestroySceneObject("Field_10x10");
        DestroyAllSceneUnits();
        DestroySceneObject("Terrain_Block_01");
        DestroySceneObject("Terrain_Block_1x1");
        DestroySceneObject("Terrain_Block_2x1");
        DestroySceneObject("UnitSelectionController");
        DestroySceneObject("TurnUI");

        GameObject fieldObject = new("Field_10x10");
        fieldObject.transform.position = Vector3.zero;
        FieldGenerator field = fieldObject.AddComponent<FieldGenerator>();
        field.SetGridMaterial(fieldMaterial);
        field.GenerateField();

        Camera camera = ConfigureCamera();
        ConfigureLight();

        Vector2Int[] startingCells =
        {
            new(1, 1),
            new(2, 1),
            new(1, 2)
        };
        GridUnit[] units = new GridUnit[startingCells.Length];
        for (int i = 0; i < startingCells.Length; i++)
        {
            GameObject unitObject = (GameObject)PrefabUtility.InstantiatePrefab(unitPrefab, scene);
            unitObject.name = $"Unit_Player_{i + 1}";
            unitObject.transform.position = field.GetCellCenterWorld(startingCells[i].x, startingCells[i].y, 0f);
            units[i] = unitObject.GetComponent<GridUnit>();
            units[i].Configure(i + 1);
            PrefabUtility.RecordPrefabInstancePropertyModifications(unitObject.transform);
            PrefabUtility.RecordPrefabInstancePropertyModifications(units[i]);
        }

        GameObject enemyObject = (GameObject)PrefabUtility.InstantiatePrefab(enemyPrefab, scene);
        enemyObject.name = "Unit_Enemy_1";
        enemyObject.transform.position = field.GetCellCenterWorld(8, 8, 0f);
        GridUnit enemyUnit = enemyObject.GetComponent<GridUnit>();
        enemyUnit.Configure(1, GridUnitTeam.Enemy);
        PrefabUtility.RecordPrefabInstancePropertyModifications(enemyObject.transform);
        PrefabUtility.RecordPrefabInstancePropertyModifications(enemyUnit);

        GameObject obstacle1x1 = (GameObject)PrefabUtility.InstantiatePrefab(obstacle1x1Prefab, scene);
        obstacle1x1.transform.position = field.GetCellCenterWorld(5, 4, 0f);

        GameObject obstacle2x1 = (GameObject)PrefabUtility.InstantiatePrefab(obstacle2x1Prefab, scene);
        obstacle2x1.transform.position = field.GetCellCenterWorld(2, 7, 0f);

        GameObject controllerObject = new("UnitSelectionController");
        UnitSelectionController controller = controllerObject.AddComponent<UnitSelectionController>();
        controller.Configure(field, camera, units);
        BuildTurnUI(controller, units, cardPrefab);
        EnsureEventSystem();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Selection.activeGameObject = fieldObject;
        Debug.Log("XS Project: player units, non-controllable enemy prefab, and turn UI configured.");
    }

    private static GameObject BuildUnitPrefab(
        string objectName,
        GridUnitTeam team,
        Material unitMaterial,
        string prefabPath)
    {
        GameObject unit = new(objectName);
        unit.transform.position = Vector3.zero;
        unit.transform.rotation = Quaternion.identity;
        unit.transform.localScale = Vector3.one;
        GridUnit gridUnit = unit.AddComponent<GridUnit>();
        gridUnit.Configure(1, team);

        CapsuleCollider unitCollider = unit.AddComponent<CapsuleCollider>();
        unitCollider.height = GridUnit.HeightMeters;
        unitCollider.radius = GridUnit.RadiusMeters;
        unitCollider.center = new Vector3(0f, GridUnit.HeightMeters * 0.5f, 0f);
        unitCollider.direction = 1;

        GameObject visualRoot = CreateChild(unit.transform, "VisualRoot");
        GameObject preview = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        preview.name = "CapsulePreview";
        preview.transform.SetParent(visualRoot.transform, false);
        preview.transform.localPosition = new Vector3(0f, GridUnit.HeightMeters * 0.5f, 0f);
        preview.transform.localScale = new Vector3(
            GridUnit.DiameterMeters,
            GridUnit.HeightMeters * 0.5f,
            GridUnit.DiameterMeters);
        Object.DestroyImmediate(preview.GetComponent<Collider>());
        preview.GetComponent<Renderer>().sharedMaterial = unitMaterial;

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(unit, prefabPath);
        Object.DestroyImmediate(unit);
        return prefab;
    }

    private static GameObject BuildObstaclePrefab(
        string objectName,
        int widthCells,
        int depthCells,
        Material terrainMaterial,
        string prefabPath)
    {
        GameObject obstacle = new(objectName);
        obstacle.transform.position = Vector3.zero;
        obstacle.transform.rotation = Quaternion.identity;
        obstacle.transform.localScale = Vector3.one;

        GridObstacle gridObstacle = obstacle.AddComponent<GridObstacle>();
        gridObstacle.Configure(widthCells, depthCells);

        float widthMeters = widthCells * FieldGenerator.CellSizeMeters;
        float depthMeters = depthCells * FieldGenerator.CellSizeMeters;
        Vector3 center = new(
            (widthMeters - FieldGenerator.CellSizeMeters) * 0.5f,
            GridObstacle.HeightMeters * 0.5f,
            (depthMeters - FieldGenerator.CellSizeMeters) * 0.5f);

        BoxCollider obstacleCollider = obstacle.AddComponent<BoxCollider>();
        obstacleCollider.size = new Vector3(widthMeters, GridObstacle.HeightMeters, depthMeters);
        obstacleCollider.center = center;

        GameObject visualRoot = CreateChild(obstacle.transform, "VisualRoot");
        GameObject preview = GameObject.CreatePrimitive(PrimitiveType.Cube);
        preview.name = "TerrainPreview";
        preview.transform.SetParent(visualRoot.transform, false);
        preview.transform.localPosition = center;
        preview.transform.localScale = new Vector3(widthMeters, GridObstacle.HeightMeters, depthMeters);
        Object.DestroyImmediate(preview.GetComponent<Collider>());
        preview.GetComponent<Renderer>().sharedMaterial = terrainMaterial;

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(obstacle, prefabPath);
        Object.DestroyImmediate(obstacle);
        return prefab;
    }

    private static GameObject CreateChild(Transform parent, string childName)
    {
        GameObject child = new(childName);
        child.transform.SetParent(parent, false);
        child.transform.localPosition = Vector3.zero;
        child.transform.localRotation = Quaternion.identity;
        child.transform.localScale = Vector3.one;
        return child;
    }

    private static GameObject BuildCardPrefab(string prefabPath)
    {
        GameObject existingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (existingPrefab != null)
        {
            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
            ConfigureCardInteraction(prefabRoot);
            PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
            PrefabUtility.UnloadPrefabContents(prefabRoot);
            return AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        }

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        Image card = CreateImage(null, "Card", new Color(0.68f, 0.18f, 0.15f, 1f));
        SetRect(
            card.rectTransform,
            new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f),
            Vector2.zero,
            new Vector2(164f, 226f));
        card.raycastTarget = true;
        card.gameObject.AddComponent<CardHoverUI>();

        Image body = CreateImage(card.transform, "CardBody", new Color(0.10f, 0.11f, 0.13f, 1f));
        SetRect(body.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        body.rectTransform.offsetMin = new Vector2(6f, 6f);
        body.rectTransform.offsetMax = new Vector2(-6f, -6f);
        body.raycastTarget = false;

        Text title = CreateText(card.transform, "Title", font, 19, TextAnchor.MiddleCenter);
        SetRect(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(12f, -10f), new Vector2(120f, 38f));
        title.color = Color.white;
        title.fontStyle = FontStyle.Bold;
        title.raycastTarget = false;

        Image costBadge = CreateImage(card.transform, "Cost", new Color(0.12f, 0.58f, 0.82f, 1f));
        SetRect(costBadge.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(8f, -8f), new Vector2(38f, 38f));
        costBadge.raycastTarget = false;
        Text costText = CreateText(costBadge.transform, "Value", font, 24, TextAnchor.MiddleCenter);
        SetRect(costText.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        costText.color = Color.white;
        costText.fontStyle = FontStyle.Bold;
        costText.raycastTarget = false;

        Image symbolPanel = CreateImage(card.transform, "SymbolPanel", new Color(0.374f, 0.099f, 0.083f, 1f));
        SetRect(symbolPanel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 4f), new Vector2(122f, 104f));
        symbolPanel.raycastTarget = false;
        Text symbolText = CreateText(symbolPanel.transform, "Symbol", font, 44, TextAnchor.MiddleCenter);
        SetRect(symbolText.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        symbolText.color = Color.white;
        symbolText.fontStyle = FontStyle.Bold;
        symbolText.raycastTarget = false;

        Text footer = CreateText(card.transform, "Footer", font, 14, TextAnchor.MiddleCenter);
        SetRect(footer.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 14f), new Vector2(132f, 34f));
        footer.text = "NO EFFECT";
        footer.color = new Color(0.78f, 0.80f, 0.84f, 1f);
        footer.raycastTarget = false;

        CardView cardView = card.gameObject.AddComponent<CardView>();
        cardView.SetViewReferences(card, symbolPanel, title, costText, symbolText);
        cardView.Configure("CARD", 1, "?", new Color(0.68f, 0.18f, 0.15f, 1f));
        ConfigureCardInteraction(card.gameObject);

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(card.gameObject, prefabPath);
        Object.DestroyImmediate(card.gameObject);
        return prefab;
    }

    private static void ConfigureCardInteraction(GameObject cardObject)
    {
        CardView cardView = cardObject.GetComponent<CardView>();
        CardHoverUI hoverUI = cardObject.GetComponent<CardHoverUI>();
        Outline outline = cardObject.GetComponent<Outline>();
        if (outline == null)
        {
            outline = cardObject.AddComponent<Outline>();
            outline.effectColor = new Color(1f, 0.78f, 0.18f, 1f);
            outline.effectDistance = new Vector2(4f, -4f);
            outline.useGraphicAlpha = true;
        }

        outline.enabled = false;
        if (cardView != null)
        {
            cardView.SetInteractionReferences(hoverUI, outline);
        }
    }

    private static void BuildTurnUI(UnitSelectionController controller, GridUnit[] units, GameObject cardPrefab)
    {
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        GameObject canvasObject = new("TurnUI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 20;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        Text turnText = CreateText(canvasObject.transform, "TurnNumber", font, 34, TextAnchor.MiddleCenter);
        SetRect(turnText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(320f, 64f));
        turnText.text = "TURN 1";
        turnText.color = Color.white;

        Image panel = CreateImage(canvasObject.transform, "UnitMovePanel", new Color(0.08f, 0.09f, 0.10f, 0.88f));
        SetRect(panel.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f, -20f), new Vector2(250f, 236f));

        Text header = CreateText(panel.transform, "Header", font, 22, TextAnchor.MiddleCenter);
        SetRect(header.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -10f), new Vector2(220f, 42f));
        header.text = "UNIT MOVE";
        header.color = Color.white;
        header.raycastTarget = false;

        Button[] statusButtons = new Button[units.Length];
        for (int i = 0; i < units.Length; i++)
        {
            Image row = CreateImage(panel.transform, $"UnitStatus_{i + 1}", new Color(0.28f, 0.30f, 0.32f, 0.94f));
            SetRect(row.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -58f - i * 56f), new Vector2(218f, 46f));
            Button statusButton = row.gameObject.AddComponent<Button>();
            statusButton.targetGraphic = row;
            statusButton.transition = Selectable.Transition.None;

            Text label = CreateText(row.transform, "Label", font, 20, TextAnchor.MiddleLeft);
            SetRect(label.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            label.rectTransform.offsetMin = new Vector2(16f, 0f);
            label.rectTransform.offsetMax = new Vector2(-12f, 0f);
            label.text = $"UNIT {units[i].UnitNumber}  MOVE";
            label.color = Color.white;
            label.raycastTarget = false;
            statusButtons[i] = statusButton;
        }

        Image buttonImage = CreateImage(canvasObject.transform, "EndTurnButton", new Color(0.22f, 0.24f, 0.27f, 0.98f));
        SetRect(
            buttonImage.rectTransform,
            new Vector2(1f, 1f),
            new Vector2(1f, 1f),
            new Vector2(1f, 1f),
            new Vector2(-24f, -24f),
            new Vector2(190f, 64f));
        Button endTurnButton = buttonImage.gameObject.AddComponent<Button>();
        endTurnButton.targetGraphic = buttonImage;
        ColorBlock buttonColors = endTurnButton.colors;
        buttonColors.highlightedColor = new Color(0.35f, 0.38f, 0.42f, 1f);
        buttonColors.pressedColor = new Color(0.15f, 0.17f, 0.20f, 1f);
        buttonColors.disabledColor = new Color(0.15f, 0.15f, 0.15f, 0.65f);
        endTurnButton.colors = buttonColors;

        Text buttonLabel = CreateText(buttonImage.transform, "Label", font, 24, TextAnchor.MiddleCenter);
        SetRect(buttonLabel.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        buttonLabel.text = "END TURN";
        buttonLabel.color = Color.white;
        buttonLabel.raycastTarget = false;

        Image costPanel = CreateImage(canvasObject.transform, "CardCostPanel", new Color(0.10f, 0.36f, 0.62f, 0.96f));
        SetRect(
            costPanel.rectTransform,
            new Vector2(1f, 1f),
            new Vector2(1f, 1f),
            new Vector2(1f, 1f),
            new Vector2(-24f, -96f),
            new Vector2(190f, 52f));
        costPanel.raycastTarget = false;

        Text costText = CreateText(costPanel.transform, "Value", font, 24, TextAnchor.MiddleCenter);
        SetRect(costText.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        costText.text = "COST 3 / 3";
        costText.color = Color.white;
        costText.fontStyle = FontStyle.Bold;
        costText.raycastTarget = false;

        CardView[] cards = BuildCardUI(canvasObject.transform, font, cardPrefab);

        controller.ConfigureUI(turnText, endTurnButton, statusButtons, cards, costPanel, costText);
    }

    private static CardView[] BuildCardUI(Transform canvasRoot, Font font, GameObject cardPrefab)
    {
        RectTransform hand = CreateRectContainer(canvasRoot, "CardHand");
        SetRect(
            hand,
            new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f),
            new Vector2(0f, 18f),
            new Vector2(720f, 310f));

        string[] names = { "STRIKE", "GUARD", "ADVANCE", "FOCUS", "RECOVER" };
        string[] symbols = { "X", "[]", ">>", "*", "+" };
        int[] costs = { 1, 1, 1, 2, 1 };
        Color[] frameColors =
        {
            new(0.68f, 0.18f, 0.15f, 1f),
            new(0.16f, 0.38f, 0.68f, 1f),
            new(0.18f, 0.54f, 0.30f, 1f),
            new(0.48f, 0.24f, 0.64f, 1f),
            new(0.68f, 0.52f, 0.16f, 1f)
        };
        float[] xPositions = { -220f, -110f, 0f, 110f, 220f };
        float[] yPositions = { 8f, 28f, 38f, 28f, 8f };
        float[] rotations = { 10f, 5f, 0f, -5f, -10f };
        CardView[] cards = new CardView[names.Length];

        for (int i = 0; i < names.Length; i++)
        {
            GameObject cardObject = (GameObject)PrefabUtility.InstantiatePrefab(cardPrefab, hand);
            cardObject.name = $"Card_{i + 1}_{names[i]}";
            Image card = cardObject.GetComponent<Image>();
            SetRect(
                card.rectTransform,
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(xPositions[i], yPositions[i]),
                new Vector2(164f, 226f));
            card.rectTransform.localRotation = Quaternion.Euler(0f, 0f, rotations[i]);
            CardView cardView = cardObject.GetComponent<CardView>();
            cardView.Configure(names[i], costs[i], symbols[i], frameColors[i]);
            cards[i] = cardView;

            foreach (Component component in cardObject.GetComponentsInChildren<Component>(true))
            {
                PrefabUtility.RecordPrefabInstancePropertyModifications(component);
            }
        }

        BuildCardPile(
            canvasRoot,
            font,
            "DeckPile",
            "DECK",
            new Vector2(0f, 0f),
            new Vector2(0f, 0f),
            new Vector2(30f, 24f),
            new Color(0.12f, 0.25f, 0.48f, 1f),
            false);
        BuildCardPile(
            canvasRoot,
            font,
            "DiscardPile",
            "DISCARD",
            new Vector2(1f, 0f),
            new Vector2(1f, 0f),
            new Vector2(-24f, 24f),
            new Color(0.28f, 0.29f, 0.31f, 1f),
            true);

        return cards;
    }

    private static void BuildCardPile(
        Transform canvasRoot,
        Font font,
        string objectName,
        string labelText,
        Vector2 anchor,
        Vector2 pivot,
        Vector2 position,
        Color color,
        bool scattered)
    {
        RectTransform pile = CreateRectContainer(canvasRoot, objectName);
        SetRect(pile, anchor, anchor, pivot, position, new Vector2(120f, 156f));

        for (int i = 0; i < 3; i++)
        {
            Image layer = CreateImage(pile, $"CardLayer_{i + 1}", Color.Lerp(color, Color.black, 0.08f * (2 - i)));
            SetRect(
                layer.rectTransform,
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2((i - 1) * 6f, 30f + i * 5f),
                new Vector2(90f, 112f));
            layer.rectTransform.localRotation = Quaternion.Euler(0f, 0f, scattered ? (i - 1) * 8f : (i - 1) * 2f);
            layer.raycastTarget = i == 2;
        }

        Text label = CreateText(pile, "Label", font, 18, TextAnchor.MiddleCenter);
        SetRect(label.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(120f, 28f));
        label.text = labelText;
        label.color = Color.white;
        label.fontStyle = FontStyle.Bold;
        label.raycastTarget = false;
    }

    private static RectTransform CreateRectContainer(Transform parent, string objectName)
    {
        GameObject container = new(objectName, typeof(RectTransform));
        container.transform.SetParent(parent, false);
        return container.GetComponent<RectTransform>();
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

    private static void EnsureEventSystem()
    {
        EventSystem[] eventSystems = Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None);
        EventSystem eventSystem;
        if (eventSystems.Length == 0)
        {
            GameObject eventSystemObject = new("EventSystem");
            eventSystem = eventSystemObject.AddComponent<EventSystem>();
        }
        else
        {
            eventSystem = eventSystems[0];
            eventSystem.gameObject.name = "EventSystem";
            for (int i = 1; i < eventSystems.Length; i++)
            {
                Object.DestroyImmediate(eventSystems[i].gameObject);
            }
        }

        StandaloneInputModule[] legacyModules = eventSystem.GetComponents<StandaloneInputModule>();
        foreach (StandaloneInputModule legacyModule in legacyModules)
        {
            Object.DestroyImmediate(legacyModule);
        }

        InputSystemUIInputModule inputModule = eventSystem.GetComponent<InputSystemUIInputModule>();
        if (inputModule == null)
        {
            inputModule = eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
        }

        if (inputModule.actionsAsset == null)
        {
            inputModule.AssignDefaultActions();
        }
    }

    private static void DestroyAllSceneUnits()
    {
        GridUnit[] existingUnits = Object.FindObjectsByType<GridUnit>(FindObjectsSortMode.None);
        foreach (GridUnit unit in existingUnits)
        {
            if (!EditorUtility.IsPersistent(unit))
            {
                Object.DestroyImmediate(unit.gameObject);
            }
        }
    }

    private static void DestroySceneObject(string objectName)
    {
        GameObject existing = GameObject.Find(objectName);
        if (existing != null && !EditorUtility.IsPersistent(existing))
        {
            Object.DestroyImmediate(existing);
        }
    }

    private static void EnsureAssetFolders()
    {
        if (!AssetDatabase.IsValidFolder(MaterialFolder))
        {
            AssetDatabase.CreateFolder("Assets", "Materials");
        }

        if (!AssetDatabase.IsValidFolder(PrefabFolder))
        {
            AssetDatabase.CreateFolder("Assets", "Prefabs");
        }

        if (!AssetDatabase.IsValidFolder(CardPrefabFolder))
        {
            AssetDatabase.CreateFolder(PrefabFolder, "Cards");
        }
    }

    private static Material EnsureMaterial(
        string path,
        string materialName,
        string shaderName,
        Color color,
        float smoothness)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null)
        {
            return material;
        }

        Shader shader = Shader.Find(shaderName);
        if (shader == null)
        {
            Debug.LogError($"Shader '{shaderName}' was not found.");
            return null;
        }

        material = new Material(shader)
        {
            name = materialName
        };
        material.SetColor("_BaseColor", color);
        material.SetFloat("_Smoothness", smoothness);
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    private static Camera ConfigureCamera()
    {
        Camera camera = Camera.main;
        if (camera == null)
        {
            GameObject cameraObject = new("Main Camera");
            cameraObject.tag = "MainCamera";
            camera = cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<AudioListener>();
        }

        camera.transform.position = new Vector3(0f, 12f, 0f);
        camera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        camera.orthographic = true;
        camera.orthographicSize = 5.75f;
        camera.nearClipPlane = 0.1f;
        camera.farClipPlane = 100f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.055f, 0.09f, 0.07f, 1f);
        return camera;
    }

    private static void ConfigureLight()
    {
        Light light = Object.FindFirstObjectByType<Light>();
        if (light == null)
        {
            GameObject lightObject = new("Directional Light");
            light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
        }

        light.transform.rotation = Quaternion.Euler(52f, -32f, 0f);
        light.color = new Color(1f, 0.96f, 0.86f, 1f);
        light.intensity = 1.25f;
        light.shadows = LightShadows.Soft;
    }
}
