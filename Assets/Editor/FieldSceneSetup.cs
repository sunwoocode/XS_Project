using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class FieldSceneSetup
{
    private const string ScenePath = "Assets/Scenes/MainScene.unity";
    private const string CardCsvPath = "Assets/Resources/CardData/cards.csv";
    private const string MaterialFolder = "Assets/Materials";
    private const string PrefabFolder = "Assets/Prefabs";
    private const string CardPrefabFolder = PrefabFolder + "/Cards";
    private const string FieldMaterialPath = MaterialFolder + "/FieldGrid.mat";
    private const string BackgroundFieldMaterialPath = MaterialFolder + "/BackgroundFieldGrid.mat";
    private const string UnitMaterialPath = MaterialFolder + "/Unit.mat";
    private const string EnemyMaterialPath = MaterialFolder + "/Enemy.mat";
    private const string TerrainMaterialPath = MaterialFolder + "/Terrain.mat";
    private const string UnitPrefabPath = PrefabFolder + "/Unit_Player.prefab";
    private const string EnemyPrefabPath = PrefabFolder + "/Unit_Enemy.prefab";
    private const string Obstacle1x1PrefabPath = PrefabFolder + "/Terrain_Block_1x1.prefab";
    private const string Obstacle2x1PrefabPath = PrefabFolder + "/Terrain_Block_2x1.prefab";
    private const string CardPrefabPath = CardPrefabFolder + "/Card.prefab";
    private const string FieldShaderName = "XS Project/Field Grid";

    [MenuItem("XS Project/Setup 20x30 Field")]
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
        Material backgroundFieldMaterial = EnsureBackgroundFieldMaterial(fieldMaterial);
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

        if (fieldMaterial == null || backgroundFieldMaterial == null || unitMaterial == null || enemyMaterial == null || terrainMaterial == null)
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
        DestroySceneObject("Field_20x30");
        DestroySceneObject("Battle_Field");
        DestroySceneObject("Background_Field");
        DestroyAllSceneUnits();
        DestroySceneObject("Terrain_Block_01");
        DestroySceneObject("Terrain_Block_1x1");
        DestroySceneObject("Terrain_Block_2x1");
        DestroySceneObject("UnitSelectionController");
        DestroySceneObject("TurnUI");

        GameObject fieldObject = new("Battle_Field");
        fieldObject.transform.position = Vector3.zero;
        FieldGenerator field = fieldObject.AddComponent<FieldGenerator>();
        field.SetGridMaterial(fieldMaterial);
        field.SetDimensions(20, 30);
        BuildBackgroundField(backgroundFieldMaterial);

        Camera camera = ConfigureCamera(field);
        ConfigureLight();

        Vector2Int[] startingCells =
        {
            new(2, 2),
            new(3, 2),
            new(2, 3)
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
        enemyObject.transform.position = field.GetCellCenterWorld(17, 27, 0f);
        GridUnit enemyUnit = enemyObject.GetComponent<GridUnit>();
        enemyUnit.Configure(1, GridUnitTeam.Enemy);
        PrefabUtility.RecordPrefabInstancePropertyModifications(enemyObject.transform);
        PrefabUtility.RecordPrefabInstancePropertyModifications(enemyUnit);

        GameObject obstacle1x1 = (GameObject)PrefabUtility.InstantiatePrefab(obstacle1x1Prefab, scene);
        obstacle1x1.transform.position = field.GetCellCenterWorld(10, 12, 0f);

        GameObject obstacle2x1 = (GameObject)PrefabUtility.InstantiatePrefab(obstacle2x1Prefab, scene);
        obstacle2x1.transform.position = field.GetCellCenterWorld(5, 21, 0f);

        GameObject controllerObject = new("UnitSelectionController");
        UnitSelectionController controller = controllerObject.AddComponent<UnitSelectionController>();
        controller.Configure(field, camera, units);
        BuildTurnUI(controller, units, cardPrefab);
        EnsureEventSystem();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Selection.activeGameObject = fieldObject;
        Debug.Log("XS Project: 20x30 field, distributed units, card-name UI, and right-drag camera pan configured.");
    }

    [MenuItem("XS Project/Refresh Battle and Background Fields")]
    public static void RefreshBattleAndBackgroundFields()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        EnsureAssetFolders();

        Material fieldMaterial = EnsureMaterial(
            FieldMaterialPath,
            "FieldGrid",
            FieldShaderName,
            new Color(0.22f, 0.50f, 0.27f, 1f),
            0.08f);
        Material backgroundFieldMaterial = EnsureBackgroundFieldMaterial(fieldMaterial);
        if (fieldMaterial == null || backgroundFieldMaterial == null)
        {
            Debug.LogError("XS Project: 필드 머티리얼을 준비하지 못해 필드를 갱신하지 못했습니다.");
            return;
        }

        GameObject battleFieldObject = GameObject.Find("Battle_Field");
        if (battleFieldObject == null)
        {
            battleFieldObject = GameObject.Find("Field_20x30");
        }

        FieldGenerator battleField = battleFieldObject != null
            ? battleFieldObject.GetComponent<FieldGenerator>()
            : Object.FindFirstObjectByType<FieldGenerator>();
        if (battleField == null)
        {
            Debug.LogError("XS Project: 기존 Battle Field의 FieldGenerator를 찾지 못했습니다.");
            return;
        }

        battleFieldObject = battleField.gameObject;
        battleFieldObject.name = "Battle_Field";
        battleFieldObject.transform.position = Vector3.zero;
        battleField.SetGridMaterial(fieldMaterial);
        battleField.SetDimensions(20, 30);

        DestroySceneObject("Field_10x10");
        DestroySceneObject("Field_20x30");
        DestroySceneObject("Background_Field");
        BuildBackgroundField(backgroundFieldMaterial);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Selection.activeGameObject = battleFieldObject;
        Debug.Log("XS Project: Battle_Field 20x30 및 비이동 Background_Field 40x50 구성을 갱신했습니다.");
    }

    [MenuItem("XS Project/Refresh Turn UI")]
    public static void RefreshTurnUI()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        UnitSelectionController controller = Object.FindFirstObjectByType<UnitSelectionController>();
        GameObject cardPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CardPrefabPath);
        if (controller == null || cardPrefab == null)
        {
            Debug.LogError("XS Project: UnitSelectionController 또는 카드 프리팹을 찾을 수 없어 턴 UI를 갱신하지 못했습니다.");
            return;
        }

        GridUnit[] units = System.Array.FindAll(
            Object.FindObjectsByType<GridUnit>(FindObjectsSortMode.None),
            unit => unit != null && unit.IsPlayerControlled);
        System.Array.Sort(units, (left, right) => left.UnitNumber.CompareTo(right.UnitNumber));

        DestroySceneObject("TurnUI");
        BuildTurnUI(controller, units, cardPrefab);
        EnsureEventSystem();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("XS Project: unit AP status UI refreshed.");
    }

    [MenuItem("XS Project/Refresh Battle Settings Menu")]
    public static void RefreshBattleSettingsMenu()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        GameObject turnUI = GameObject.Find("TurnUI");
        UnitSelectionController controller = Object.FindFirstObjectByType<UnitSelectionController>();
        FieldCameraPan cameraPan = Object.FindFirstObjectByType<FieldCameraPan>();
        Canvas canvas = turnUI != null ? turnUI.GetComponent<Canvas>() : null;
        if (canvas == null || controller == null || cameraPan == null)
        {
            Debug.LogError("XS Project: TurnUI, UnitSelectionController 또는 FieldCameraPan을 찾지 못해 설정 메뉴를 갱신하지 못했습니다.");
            return;
        }

        Transform existingMenu = turnUI.transform.Find("BattleSettingsMenu");
        if (existingMenu != null)
        {
            Object.DestroyImmediate(existingMenu.gameObject);
        }

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        BuildBattleSettingsMenu(turnUI.transform, font, controller, cameraPan);
        EnsureEventSystem();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("XS Project: MainScene ESC settings menu refreshed.");
    }

    [MenuItem("XS Project/Refresh Quarter View Camera")]
    public static void RefreshQuarterViewCamera()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        FieldGenerator field = Object.FindFirstObjectByType<FieldGenerator>();
        if (field == null)
        {
            Debug.LogError("XS Project: FieldGenerator를 찾을 수 없어 쿼터뷰 카메라를 갱신하지 못했습니다.");
            return;
        }

        ConfigureCamera(field);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("XS Project: X-COM style quarter-view camera refreshed.");
    }

    [MenuItem("XS Project/Refresh DNA Card Prefab")]
    public static void RefreshDnaCardPrefab()
    {
        EnsureAssetFolders();
        GameObject cardPrefab = BuildCardPrefab(CardPrefabPath);
        if (cardPrefab == null)
        {
            Debug.LogError("XS Project: DNA 카드 프리팹을 갱신하지 못했습니다.");
            return;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("XS Project: DNA card prefab refreshed.");
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
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        Image card = CreateImage(null, "Card", CardData.DefaultRaceColor);
        SetRect(
            card.rectTransform,
            new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f),
            Vector2.zero,
            new Vector2(164f, 268f));
        card.raycastTarget = true;
        card.gameObject.AddComponent<CardHoverUI>();

        Image body = CreateImage(card.transform, "EffectPanel", CardData.DefaultRaceColor);
        SetRect(body.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 5f), new Vector2(154f, 110f));
        body.raycastTarget = false;

        Image tagBadge = CreateImage(card.transform, "TagPoint", new Color32(0x34, 0x34, 0x38, 0xff));
        SetRect(tagBadge.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(5f, -5f), new Vector2(31f, 28f));
        tagBadge.raycastTarget = false;
        Text tagText = CreateText(tagBadge.transform, "Value", font, 18, TextAnchor.MiddleCenter);
        SetRect(tagText.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        tagText.text = CardData.DefaultTagPoint.ToString();
        tagText.color = Color.white;
        tagText.fontStyle = FontStyle.Bold;
        tagText.raycastTarget = false;

        Image costBadge = CreateImage(card.transform, "Cost", new Color32(0x16, 0x8b, 0xc6, 0xff));
        SetRect(costBadge.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-5f, -5f), new Vector2(31f, 28f));
        costBadge.raycastTarget = false;
        Text costText = CreateText(costBadge.transform, "Value", font, 18, TextAnchor.MiddleCenter);
        SetRect(costText.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        costText.text = CardData.DefaultCost.ToString();
        costText.color = Color.white;
        costText.fontStyle = FontStyle.Bold;
        costText.raycastTarget = false;

        Image nameBadge = CreateImage(card.transform, "NameHeader", new Color32(0xcb, 0xa4, 0x3a, 0xff));
        SetRect(nameBadge.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(36f, -5f), new Vector2(92f, 28f));
        nameBadge.raycastTarget = false;
        Text cardNameText = CreateText(nameBadge.transform, "CardName", font, 16, TextAnchor.MiddleCenter);
        SetRect(cardNameText.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        cardNameText.rectTransform.offsetMin = new Vector2(4f, 1f);
        cardNameText.rectTransform.offsetMax = new Vector2(-4f, -1f);
        cardNameText.text = CardData.DefaultCardName;
        cardNameText.color = Color.white;
        cardNameText.fontStyle = FontStyle.Bold;
        cardNameText.resizeTextForBestFit = true;
        cardNameText.resizeTextMinSize = 10;
        cardNameText.resizeTextMaxSize = 16;
        cardNameText.horizontalOverflow = HorizontalWrapMode.Wrap;
        cardNameText.verticalOverflow = VerticalWrapMode.Truncate;
        cardNameText.raycastTarget = false;

        Image artwork = CreateImage(card.transform, "Artwork", Color.white);
        SetRect(artwork.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -33f), new Vector2(120f, 120f));
        artwork.preserveAspect = true;
        artwork.raycastTarget = false;
        artwork.enabled = false;

        Text effectText = CreateText(body.transform, "EffectText", font, 16, TextAnchor.MiddleLeft);
        SetRect(effectText.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        effectText.rectTransform.offsetMin = new Vector2(10f, 8f);
        effectText.rectTransform.offsetMax = new Vector2(-10f, -8f);
        effectText.text = CardData.DefaultEffectText;
        effectText.color = Color.black;
        effectText.resizeTextForBestFit = true;
        effectText.resizeTextMinSize = 11;
        effectText.resizeTextMaxSize = 16;
        effectText.horizontalOverflow = HorizontalWrapMode.Wrap;
        effectText.verticalOverflow = VerticalWrapMode.Truncate;
        effectText.raycastTarget = false;

        CardView cardView = card.gameObject.AddComponent<CardView>();
        cardView.SetViewReferences(card, body, cardNameText, tagText, costText, artwork, effectText);
        cardView.Configure(new CardData());
        ConfigureCardInteraction(card.gameObject);

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(card.gameObject, prefabPath);
        Object.DestroyImmediate(card.gameObject);
        return prefab;
    }

    private static void ConfigureCardInteraction(GameObject cardObject)
    {
        CardView cardView = cardObject.GetComponent<CardView>();
        CardHoverUI hoverUI = cardObject.GetComponent<CardHoverUI>();
        if (cardView != null)
        {
            cardView.SetInteractionReferences(hoverUI, null);
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

        Image panel = CreateImage(canvasObject.transform, "UnitStatusPanel", new Color(0.08f, 0.09f, 0.10f, 0.88f));
        SetRect(panel.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f, -20f), new Vector2(300f, 250f));
        CanvasGroup panelGroup = panel.gameObject.AddComponent<CanvasGroup>();

        Text header = CreateText(panel.transform, "Header", font, 22, TextAnchor.MiddleCenter);
        SetRect(header.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -10f), new Vector2(270f, 42f));
        header.text = "UNIT";
        header.color = Color.white;
        header.raycastTarget = false;

        UnitStatusView[] statusViews = new UnitStatusView[units.Length];
        for (int i = 0; i < units.Length; i++)
        {
            Image row = CreateImage(panel.transform, $"UnitStatus_{i + 1}", new Color(0.28f, 0.30f, 0.32f, 0.94f));
            SetRect(row.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -58f - i * 58f), new Vector2(268f, 48f));
            Button statusButton = row.gameObject.AddComponent<Button>();
            statusButton.targetGraphic = row;
            statusButton.transition = Selectable.Transition.None;

            Outline selectionOutline = row.gameObject.AddComponent<Outline>();
            selectionOutline.effectColor = new Color(1f, 0.78f, 0.16f, 1f);
            selectionOutline.effectDistance = new Vector2(3f, -3f);
            selectionOutline.useGraphicAlpha = false;
            selectionOutline.enabled = false;

            Text label = CreateText(row.transform, "Label", font, 20, TextAnchor.MiddleLeft);
            SetRect(label.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            label.rectTransform.offsetMin = new Vector2(16f, 0f);
            label.rectTransform.offsetMax = new Vector2(-112f, 0f);
            label.text = units[i].DisplayName.ToUpperInvariant();
            label.color = Color.white;
            label.raycastTarget = false;

            Image[] actionPointSlots = new Image[GridUnit.MaxActionPoints];
            for (int actionPoint = 0; actionPoint < actionPointSlots.Length; actionPoint++)
            {
                Image slot = CreateImage(row.transform, $"AP_{actionPoint + 1}", new Color(0.31f, 0.61f, 0.88f, 1f));
                SetRect(
                    slot.rectTransform,
                    new Vector2(1f, 0.5f),
                    new Vector2(1f, 0.5f),
                    new Vector2(1f, 0.5f),
                    new Vector2(-12f - (actionPointSlots.Length - 1 - actionPoint) * 48f, 0f),
                    new Vector2(40f, 32f));
                slot.raycastTarget = false;
                actionPointSlots[actionPoint] = slot;
            }

            UnitStatusView statusView = row.gameObject.AddComponent<UnitStatusView>();
            statusView.Configure(statusButton, row, label, actionPointSlots, selectionOutline);
            statusViews[i] = statusView;
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

        UnitSelectionDetailView selectionDetailView = BuildUnitSelectionDetailUI(canvasObject.transform, font);
        CardView[] cards = BuildCardUI(canvasObject.transform, font, cardPrefab);

        controller.ConfigureUI(
            turnText,
            endTurnButton,
            panelGroup,
            statusViews,
            cards,
            costPanel,
            costText,
            selectionDetailView);

        FieldCameraPan cameraPan = Object.FindFirstObjectByType<FieldCameraPan>();
        BuildBattleSettingsMenu(canvasObject.transform, font, controller, cameraPan);
    }

    private static BattleSettingsMenu BuildBattleSettingsMenu(
        Transform canvasRoot,
        Font font,
        UnitSelectionController controller,
        FieldCameraPan cameraPan)
    {
        RectTransform menuRoot = CreateRectContainer(canvasRoot, "BattleSettingsMenu");
        SetRect(menuRoot, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

        Image overlay = CreateImage(menuRoot, "Overlay", new Color(0.015f, 0.02f, 0.025f, 0.78f));
        SetRect(overlay.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        overlay.raycastTarget = true;

        Image panel = CreateImage(overlay.transform, "SettingsPanel", new Color(0.08f, 0.09f, 0.10f, 0.98f));
        SetRect(
            panel.rectTransform,
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            new Vector2(440f, 360f));

        Outline panelOutline = panel.gameObject.AddComponent<Outline>();
        panelOutline.effectColor = new Color(1f, 0.78f, 0.16f, 1f);
        panelOutline.effectDistance = new Vector2(3f, -3f);
        panelOutline.useGraphicAlpha = false;

        Text title = CreateText(panel.transform, "Title", font, 42, TextAnchor.MiddleCenter);
        SetRect(
            title.rectTransform,
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0f, -38f),
            new Vector2(380f, 72f));
        title.text = "SETTINGS";
        title.color = Color.white;
        title.fontStyle = FontStyle.Bold;
        title.raycastTarget = false;

        Button resumeButton = CreateSettingsMenuButton(panel.transform, font, "ResumeButton", "RESUME", new Vector2(0f, 20f));
        Button lobbyButton = CreateSettingsMenuButton(panel.transform, font, "LobbyButton", "LOBBY", new Vector2(0f, -82f));

        BattleSettingsMenu settingsMenu = menuRoot.gameObject.AddComponent<BattleSettingsMenu>();
        settingsMenu.Configure(overlay.gameObject, resumeButton, lobbyButton, controller, cameraPan);
        overlay.gameObject.SetActive(false);
        menuRoot.SetAsLastSibling();
        return settingsMenu;
    }

    private static Button CreateSettingsMenuButton(
        Transform parent,
        Font font,
        string objectName,
        string labelText,
        Vector2 position)
    {
        Image buttonImage = CreateImage(parent, objectName, new Color(0.22f, 0.24f, 0.27f, 1f));
        SetRect(
            buttonImage.rectTransform,
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            position,
            new Vector2(280f, 76f));

        Outline outline = buttonImage.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(1f, 0.78f, 0.16f, 1f);
        outline.effectDistance = new Vector2(3f, -3f);
        outline.useGraphicAlpha = false;

        Button button = buttonImage.gameObject.AddComponent<Button>();
        button.targetGraphic = buttonImage;
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.3f, 1.3f, 1.3f, 1f);
        colors.selectedColor = new Color(1.3f, 1.3f, 1.3f, 1f);
        colors.pressedColor = new Color(0.68f, 0.70f, 0.74f, 1f);
        colors.disabledColor = new Color(0.45f, 0.45f, 0.45f, 0.6f);
        button.colors = colors;

        Text label = CreateText(buttonImage.transform, "Label", font, 28, TextAnchor.MiddleCenter);
        SetRect(label.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        label.text = labelText;
        label.color = Color.white;
        label.fontStyle = FontStyle.Bold;
        label.raycastTarget = false;
        return button;
    }

    private static UnitSelectionDetailView BuildUnitSelectionDetailUI(Transform canvasRoot, Font font)
    {
        RectTransform root = CreateRectContainer(canvasRoot, "UnitSelectionDetailUI");
        SetRect(root, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

        RectTransform tagPanel = CreateRectContainer(root, "TagSlotPanel");
        SetRect(
            tagPanel,
            new Vector2(0f, 0.5f),
            new Vector2(0f, 0.5f),
            new Vector2(0f, 0.5f),
            new Vector2(14f, 0f),
            new Vector2(390f, 294f));

        const int tagSlotCount = 3;
        Text[] tagNameTexts = new Text[tagSlotCount];
        Text[] tagContentTexts = new Text[tagSlotCount];
        Text[] tagPointTexts = new Text[tagSlotCount];
        for (int i = 0; i < tagSlotCount; i++)
        {
            Image border = CreateImage(tagPanel, $"TagSlot_{i + 1}", Color.white);
            SetRect(
                border.rectTransform,
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -i * 98f),
                new Vector2(390f, 94f));
            border.raycastTarget = false;

            Image slot = CreateImage(border.transform, "Background", new Color(0.18f, 0.19f, 0.20f, 0.96f));
            SetRect(slot.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            slot.rectTransform.offsetMin = new Vector2(2f, 2f);
            slot.rectTransform.offsetMax = new Vector2(-2f, -2f);
            slot.raycastTarget = false;

            Image nameBox = CreateImage(slot.transform, "NameBox", new Color(0.54f, 0.55f, 0.56f, 1f));
            SetRect(nameBox.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(8f, -6f), new Vector2(102f, 24f));
            nameBox.raycastTarget = false;
            Text nameText = CreateText(nameBox.transform, "Value", font, 16, TextAnchor.MiddleLeft);
            SetRect(nameText.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            nameText.rectTransform.offsetMin = new Vector2(7f, 0f);
            nameText.rectTransform.offsetMax = new Vector2(-4f, 0f);
            nameText.text = "name";
            nameText.color = Color.white;
            nameText.raycastTarget = false;
            tagNameTexts[i] = nameText;

            Image contentBox = CreateImage(slot.transform, "ContentBox", new Color(0.54f, 0.55f, 0.56f, 1f));
            SetRect(contentBox.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(8f, -36f), new Vector2(304f, 48f));
            contentBox.raycastTarget = false;
            Text contentText = CreateText(contentBox.transform, "Value", font, 16, TextAnchor.MiddleLeft);
            SetRect(contentText.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            contentText.rectTransform.offsetMin = new Vector2(7f, 0f);
            contentText.rectTransform.offsetMax = new Vector2(-4f, 0f);
            contentText.text = "content";
            contentText.color = Color.white;
            contentText.raycastTarget = false;
            tagContentTexts[i] = contentText;

            Image tagPointBox = CreateImage(slot.transform, "TagPointBox", new Color(0.48f, 0.49f, 0.51f, 1f));
            SetRect(tagPointBox.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-8f, -36f), new Vector2(54f, 48f));
            tagPointBox.raycastTarget = false;
            Text tagPointText = CreateText(tagPointBox.transform, "Value", font, 16, TextAnchor.MiddleCenter);
            SetRect(tagPointText.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            tagPointText.text = "TP";
            tagPointText.color = Color.white;
            tagPointText.raycastTarget = false;
            tagPointTexts[i] = tagPointText;
        }

        RectTransform actionPanel = CreateRectContainer(root, "ActionButtonPanel");
        SetRect(
            actionPanel,
            new Vector2(1f, 0.5f),
            new Vector2(1f, 0.5f),
            new Vector2(1f, 0.5f),
            new Vector2(-24f, 0f),
            new Vector2(132f, 220f));

        string[] actionLabels = { "MAIN", "SUB", "OVER\nWATCH" };
        Button[] actionButtons = new Button[actionLabels.Length];
        for (int i = 0; i < actionLabels.Length; i++)
        {
            Image actionImage = CreateImage(actionPanel, $"Action_{i + 1}", new Color(0.31f, 0.32f, 0.34f, 0.98f));
            SetRect(
                actionImage.rectTransform,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0f, 76f - i * 76f),
                new Vector2(120f, 64f));

            Button actionButton = actionImage.gameObject.AddComponent<Button>();
            actionButton.targetGraphic = actionImage;
            ColorBlock actionColors = actionButton.colors;
            actionColors.normalColor = Color.white;
            actionColors.highlightedColor = new Color(1.12f, 1.12f, 1.12f, 1f);
            actionColors.pressedColor = new Color(0.74f, 0.74f, 0.76f, 1f);
            actionColors.disabledColor = new Color(0.48f, 0.48f, 0.50f, 0.55f);
            actionButton.colors = actionColors;

            Text actionLabel = CreateText(actionImage.transform, "Label", font, i == 2 ? 19 : 26, TextAnchor.MiddleCenter);
            SetRect(actionLabel.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            actionLabel.text = actionLabels[i];
            actionLabel.color = Color.white;
            actionLabel.fontStyle = FontStyle.Bold;
            actionLabel.raycastTarget = false;
            actionButtons[i] = actionButton;
        }

        UnitSelectionDetailView detailView = root.gameObject.AddComponent<UnitSelectionDetailView>();
        detailView.Configure(
            tagPanel.gameObject,
            actionPanel.gameObject,
            tagNameTexts,
            tagContentTexts,
            tagPointTexts,
            actionButtons);
        return detailView;
    }

    private static CardView[] BuildCardUI(Transform canvasRoot, Font font, GameObject cardPrefab)
    {
        RectTransform hand = CreateRectContainer(canvasRoot, "CardHand");
        SetRect(
            hand,
            new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f),
            Vector2.zero,
            new Vector2(720f, 310f));

        CardCsvLoader loader = hand.gameObject.AddComponent<CardCsvLoader>();
        TextAsset cardCsv = AssetDatabase.LoadAssetAtPath<TextAsset>(CardCsvPath);
        if (cardCsv == null)
        {
            Debug.LogError($"카드 CSV 에셋을 찾을 수 없습니다: {CardCsvPath}");
        }

        loader.Configure(
            cardCsv,
            cardPrefab.GetComponent<CardView>(),
            Object.FindFirstObjectByType<UnitSelectionController>());

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

        return new CardView[0];
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

    private static GameObject BuildBackgroundField(Material backgroundMaterial)
    {
        GameObject backgroundField = new("Background_Field");
        backgroundField.transform.position = Vector3.zero;

        GameObject surface = GameObject.CreatePrimitive(PrimitiveType.Cube);
        surface.name = "FieldSurface";
        surface.transform.SetParent(backgroundField.transform, false);
        surface.transform.localPosition = new Vector3(0f, -0.22f, 0f);
        surface.transform.localRotation = Quaternion.identity;
        surface.transform.localScale = new Vector3(40f, 0.1f, 50f);

        Collider surfaceCollider = surface.GetComponent<Collider>();
        if (surfaceCollider != null)
        {
            Object.DestroyImmediate(surfaceCollider);
        }

        Renderer surfaceRenderer = surface.GetComponent<Renderer>();
        surfaceRenderer.sharedMaterial = backgroundMaterial;
        return backgroundField;
    }

    private static Material EnsureBackgroundFieldMaterial(Material fieldMaterial)
    {
        if (fieldMaterial == null)
        {
            return null;
        }

        Material material = AssetDatabase.LoadAssetAtPath<Material>(BackgroundFieldMaterialPath);
        if (material == null)
        {
            material = new Material(fieldMaterial)
            {
                name = "BackgroundFieldGrid"
            };
            AssetDatabase.CreateAsset(material, BackgroundFieldMaterialPath);
        }
        else
        {
            material.CopyPropertiesFromMaterial(fieldMaterial);
            material.shader = fieldMaterial.shader;
        }

        material.SetVector("_GridSize", new Vector4(40f, 50f, 0f, 0f));
        material.SetVector("_HighlightCell", new Vector4(-1f, -1f, 0f, 0f));
        material.SetFloat("_GridVisible", 0f);
        EditorUtility.SetDirty(material);
        return material;
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

    private static Camera ConfigureCamera(FieldGenerator field)
    {
        Camera camera = Camera.main;
        if (camera == null)
        {
            GameObject cameraObject = new("Main Camera");
            cameraObject.tag = "MainCamera";
            camera = cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<AudioListener>();
        }

        camera.orthographic = false;
        camera.fieldOfView = 42f;
        camera.nearClipPlane = 0.1f;
        camera.farClipPlane = 100f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.055f, 0.09f, 0.07f, 1f);
        FieldCameraPan cameraPan = camera.GetComponent<FieldCameraPan>();
        if (cameraPan == null)
        {
            cameraPan = camera.gameObject.AddComponent<FieldCameraPan>();
        }

        cameraPan.Configure(field);
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
