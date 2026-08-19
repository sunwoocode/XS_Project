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
    private const string ShaderName = "XS Project/Field Grid";
    private const string SessionKey = "XSProject.FieldSceneSetup.GridShaderV1.Completed";

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

        ConfigureCamera();
        ConfigureLight();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject = field;
        Debug.Log("XS Project: single-surface 10x10 shader grid created in SampleScene.");
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

    private static void ConfigureCamera()
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
