using UnityEngine;

[DefaultExecutionOrder(-900)]
public class SceneSkybox : MonoBehaviour
{
    public Material skyboxMaterial;
    [Range(-80f, 80f)] public float verticalRotation = 24f;
    [Range(-2f, 2f)] public float horizontalSpeed = .18f;
    [Range(0f, 2f)] public float verticalDriftAmount = .3f;
    [Range(.01f, 1f)] public float verticalDriftSpeed = .08f;

    private Material previousSkybox;
    private Material runtimeSkybox;
    private float horizontalRotation;

    private void Start()
    {
        previousSkybox = RenderSettings.skybox;
        ApplySkybox();
    }

    private void LateUpdate()
    {
        if (runtimeSkybox == null) return;

        horizontalRotation = Mathf.Repeat(horizontalRotation + horizontalSpeed * Time.unscaledDeltaTime, 360f);
        float driftingVertical = verticalRotation + Mathf.Sin(Time.unscaledTime * verticalDriftSpeed) * verticalDriftAmount;
        runtimeSkybox.SetFloat("_Rotation", horizontalRotation);
        runtimeSkybox.SetFloat("_VerticalRotation", driftingVertical);
    }

    private void ApplySkybox()
    {
        if (skyboxMaterial == null) return;
        Shader verticalShader = Shader.Find("ACG/Skybox Vertical Cubemap");
        if (verticalShader == null)
        {
            RenderSettings.skybox = skyboxMaterial;
        }
        else
        {
            runtimeSkybox = new Material(verticalShader) { name = skyboxMaterial.name + " (Vertical)" };
            runtimeSkybox.CopyPropertiesFromMaterial(skyboxMaterial);
            runtimeSkybox.SetFloat("_VerticalRotation", verticalRotation);
            horizontalRotation = runtimeSkybox.HasProperty("_Rotation") ? runtimeSkybox.GetFloat("_Rotation") : 0f;
            RenderSettings.skybox = runtimeSkybox;
        }
        DynamicGI.UpdateEnvironment();
    }

    private void OnDestroy()
    {
        RenderSettings.skybox = previousSkybox;
        if (runtimeSkybox != null) Destroy(runtimeSkybox);
        DynamicGI.UpdateEnvironment();
    }
}
