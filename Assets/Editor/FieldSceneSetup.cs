using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class FieldSceneSetup
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const string MaterialFolder = "Assets/Materials";
    private const string MaterialPath = MaterialFolder + "/FieldGrid.mat";
    private const string UnitMaterialPath = MaterialFolder + "/Unit.mat";
    private const string ShaderName = "XS Project/Field Grid";
    private const string SessionKey = "XSProject.FieldSceneSetup.MetricPlayerV1.Completed";

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
        Material gridMaterial = EnsureGridMaterial();
        if (gridMaterial == null)
        {
            return;
        }

        GameObject existingField = GameObject.Find("Field_10x10");
        if (existingField != null)
        {
            Object.DestroyImmediate(existingField);
        }

        GameObject field = new("Field_10x10");
        field.transform.position = Vector3.zero;
        FieldGenerator generator = field.AddComponent<FieldGenerator>();
        generator.SetGridMaterial(gridMaterial);
        generator.GenerateField();

        Camera camera = ConfigureCamera();
        ConfigureLight();
        CreateUnit(generator, camera);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject = field;
        Debug.Log("XS Project: single-surface 10x10 shader grid created in SampleScene.");
    }

    private static void CreateUnit(FieldGenerator field, Camera camera)
    {
        GameObject existingCapsule = GameObject.Find("Unit_Capsule");
        if (existingCapsule != null)
        {
            Object.DestroyImmediate(existingCapsule);
        }

        GameObject existingPlayer = GameObject.Find("Unit_Player");
        if (existingPlayer != null)
        {
            Object.DestroyImmediate(existingPlayer);
        }

        GameObject existingController = GameObject.Find("UnitSelectionController");
        if (existingController != null)
        {
            Object.DestroyImmediate(existingController);
        }

        GameObject unit = new("Unit_Player");
        unit.transform.position = field.GetCellCenterWorld(4, 4, 0f);
        unit.transform.rotation = Quaternion.identity;
        unit.transform.localScale = Vector3.one;
        unit.AddComponent<GridUnit>();

        CapsuleCollider unitCollider = unit.AddComponent<CapsuleCollider>();
        unitCollider.height = GridUnit.HeightMeters;
        unitCollider.radius = GridUnit.RadiusMeters;
        unitCollider.center = new Vector3(0f, GridUnit.HeightMeters * 0.5f, 0f);
        unitCollider.direction = 1;

        GameObject visualRoot = new("VisualRoot");
        visualRoot.transform.SetParent(unit.transform, false);
        visualRoot.transform.localPosition = Vector3.zero;
        visualRoot.transform.localRotation = Quaternion.identity;
        visualRoot.transform.localScale = Vector3.one;

        GameObject preview = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        preview.name = "CapsulePreview";
        preview.transform.SetParent(visualRoot.transform, false);
        preview.transform.localPosition = new Vector3(0f, GridUnit.HeightMeters * 0.5f, 0f);
        preview.transform.localRotation = Quaternion.identity;
        preview.transform.localScale = new Vector3(
            GridUnit.DiameterMeters,
            GridUnit.HeightMeters * 0.5f,
            GridUnit.DiameterMeters);
        Object.DestroyImmediate(preview.GetComponent<Collider>());

        Material unitMaterial = EnsureUnitMaterial();
        if (unitMaterial != null)
        {
            preview.GetComponent<Renderer>().sharedMaterial = unitMaterial;
        }

        GameObject controllerObject = new("UnitSelectionController");
        UnitSelectionController controller = controllerObject.AddComponent<UnitSelectionController>();
        controller.Configure(field, camera);
    }

    private static Material EnsureGridMaterial()
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material != null)
        {
            return material;
        }

        Shader shader = Shader.Find(ShaderName);
        if (shader == null)
        {
            Debug.LogError($"Shader '{ShaderName}' was not found. Reimport Assets/Shaders/FieldGrid.shader and try again.");
            return null;
        }

        if (!AssetDatabase.IsValidFolder(MaterialFolder))
        {
            AssetDatabase.CreateFolder("Assets", "Materials");
        }

        material = new Material(shader)
        {
            name = "FieldGrid"
        };
        AssetDatabase.CreateAsset(material, MaterialPath);
        AssetDatabase.SaveAssets();
        return material;
    }

    private static Material EnsureUnitMaterial()
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(UnitMaterialPath);
        if (material != null)
        {
            return material;
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            Debug.LogError("Universal Render Pipeline/Lit shader was not found.");
            return null;
        }

        material = new Material(shader)
        {
            name = "Unit"
        };
        material.SetColor("_BaseColor", new Color(0.16f, 0.48f, 0.92f, 1f));
        material.SetFloat("_Smoothness", 0.28f);
        AssetDatabase.CreateAsset(material, UnitMaterialPath);
        AssetDatabase.SaveAssets();
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

        camera.transform.position = new Vector3(8.5f, 10f, -8.5f);
        camera.transform.rotation = Quaternion.LookRotation(Vector3.zero - camera.transform.position, Vector3.up);
        camera.fieldOfView = 42f;
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
