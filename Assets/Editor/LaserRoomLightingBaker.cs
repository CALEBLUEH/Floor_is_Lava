using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class LaserRoomLightingBaker
{
    private const string SceneName = "Level_Laser";
    private const string LightName = "Laser Arena Baked Fill";
    private const string SettingsPath = "Assets/Lighting/Level_Laser Lighting Settings.lighting";

    [MenuItem("Tools/ACG/Laser Room/Prepare Center Baked Light")]
    private static void PrepareCenterLight()
    {
        if (!TryGetLaserScene(out Scene scene)) return;
        CreateOrUpdateCenterLight(scene);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject = GameObject.Find(LightName);
    }

    [MenuItem("Tools/ACG/Laser Room/Generate Baked Lighting")]
    private static void GenerateLighting()
    {
        if (!TryGetLaserScene(out Scene scene)) return;
        CreateOrUpdateCenterLight(scene);

        LightingSettings settings = AssetDatabase.LoadAssetAtPath<LightingSettings>(SettingsPath);
        if (settings == null)
        {
            settings = new LightingSettings { name = "Level_Laser Lighting Settings" };
            AssetDatabase.CreateAsset(settings, SettingsPath);
        }

        Lightmapping.lightingSettings = settings;
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Lightmapping.BakeAsync();
    }

    [MenuItem("Tools/ACG/Laser Room/Select Lighting Settings")]
    private static void SelectSettings()
    {
        Object settings = AssetDatabase.LoadAssetAtPath<Object>(SettingsPath);
        if (settings == null) return;
        Selection.activeObject = settings;
        EditorGUIUtility.PingObject(settings);
    }

    private static void CreateOrUpdateCenterLight(Scene scene)
    {
        GameObject lightObject = GameObject.Find(LightName);
        if (lightObject == null)
        {
            lightObject = new GameObject(LightName, typeof(Light));
            SceneManager.MoveGameObjectToScene(lightObject, scene);
            Undo.RegisterCreatedObjectUndo(lightObject, "Create Laser baked light");
        }

        Bounds arena = CalculateArena();
        lightObject.transform.position = arena.center + Vector3.up * 6.2f;
        Light light = lightObject.GetComponent<Light>();
        light.type = LightType.Point;
        light.lightmapBakeType = LightmapBakeType.Baked;
        light.color = new Color(.32f, .46f, .72f);
        light.intensity = 1.15f;
        light.range = Mathf.Max(20f, arena.size.magnitude * .72f);
        light.shadows = LightShadows.Soft;
        EditorUtility.SetDirty(light);
    }

    private static Bounds CalculateArena()
    {
        TileGrid grid = Object.FindAnyObjectByType<TileGrid>();
        if (grid == null) return new Bounds(Vector3.zero, new Vector3(22f, 2f, 28f));
        Renderer[] renderers = grid.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return new Bounds(grid.transform.position, new Vector3(22f, 2f, 28f));
        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        return bounds;
    }

    private static bool TryGetLaserScene(out Scene scene)
    {
        scene = SceneManager.GetActiveScene();
        if (scene.name == SceneName) return true;
        EditorUtility.DisplayDialog("Laser Room Lighting",
            "Open Level_Laser before preparing or generating its baked lighting.", "OK");
        return false;
    }
}
