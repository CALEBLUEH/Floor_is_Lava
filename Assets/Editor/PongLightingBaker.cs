using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class PongLightingBaker
{
    private const string SceneName = "Level_Pong";
    private const string SettingsPath = "Assets/Lighting/Level_Pong Lighting Settings.lighting";
    private const string LightRootName = "Generated Area Lighting";

    [MenuItem("Tools/ACG/Pong/Prepare Baked Lighting")]
    private static void PrepareBakedLighting()
    {
        if (!TryGetPongScene(out Scene scene)) return;
        LightingSettings settings = GetOrCreateSettings();
        Lightmapping.lightingSettings = settings;

        int prepared = 0;
        GameObject root = GameObject.Find(LightRootName);
        if (root != null)
        {
            foreach (Light lightSource in root.GetComponentsInChildren<Light>(true))
            {
                lightSource.lightmapBakeType = LightmapBakeType.Baked;
                lightSource.shadows = LightShadows.Soft;
                EditorUtility.SetDirty(lightSource);
                prepared++;
            }
        }

        if (prepared == 0)
            Debug.LogWarning("No Pong area lights were found under '" + LightRootName + "'. Existing scene objects were left unchanged.");

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        if (root != null) Selection.activeGameObject = root;
        Debug.Log("Pong baked lighting is prepared. The Directional Light was not enabled or modified. Area lights prepared: " + prepared);
    }

    [MenuItem("Tools/ACG/Pong/Generate Baked Lighting")]
    private static void GenerateBakedLighting()
    {
        if (!TryGetPongScene(out Scene scene)) return;
        PrepareBakedLighting();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Lightmapping.BakeAsync();
    }

    [MenuItem("Tools/ACG/Pong/Select Lighting Settings")]
    private static void SelectLightingSettings()
    {
        Object settings = AssetDatabase.LoadAssetAtPath<Object>(SettingsPath);
        if (settings == null) return;
        Selection.activeObject = settings;
        EditorGUIUtility.PingObject(settings);
    }

    private static LightingSettings GetOrCreateSettings()
    {
        LightingSettings settings = AssetDatabase.LoadAssetAtPath<LightingSettings>(SettingsPath);
        if (settings != null) return settings;
        settings = new LightingSettings { name = "Level_Pong Lighting Settings" };
        AssetDatabase.CreateAsset(settings, SettingsPath);
        return settings;
    }

    private static bool TryGetPongScene(out Scene scene)
    {
        scene = SceneManager.GetActiveScene();
        if (scene.name == SceneName) return true;
        EditorUtility.DisplayDialog("Pong Lighting",
            "Open Level_Pong before preparing or generating its baked lighting.", "OK");
        return false;
    }
}
