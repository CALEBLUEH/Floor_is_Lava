using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class MemoryGridLightingBaker
{
    private const string SceneName = "Level_MemoryGrid";
    private const string SettingsPath = "Assets/Lighting/Level_MemoryGrid Lighting Settings.lighting";

    [MenuItem("Tools/ACG/Memory Grid/Generate Baked Lighting")]
    private static void GenerateLighting()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.name != SceneName)
        {
            EditorUtility.DisplayDialog("Memory Grid Lighting",
                "Open Level_MemoryGrid before generating its lighting.", "OK");
            return;
        }

        LightingSettings settings = AssetDatabase.LoadAssetAtPath<LightingSettings>(SettingsPath);
        if (settings == null)
        {
            settings = new LightingSettings { name = "Level_MemoryGrid Lighting Settings" };
            AssetDatabase.CreateAsset(settings, SettingsPath);
        }

        Lightmapping.lightingSettings = settings;
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Lightmapping.BakeAsync();
    }

    [MenuItem("Tools/ACG/Memory Grid/Select Lighting Settings")]
    private static void SelectSettings()
    {
        Object settings = AssetDatabase.LoadAssetAtPath<Object>(SettingsPath);
        if (settings == null) return;
        Selection.activeObject = settings;
        EditorGUIUtility.PingObject(settings);
    }
}
