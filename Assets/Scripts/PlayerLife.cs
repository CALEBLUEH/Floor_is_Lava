using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
[RequireComponent(typeof(TopDownPlayer))]
public class PlayerLife : MonoBehaviour
{
    [SerializeField, Min(1)] private int maxLives = 3;
    [SerializeField, Min(0f)] private float invulnerabilityDuration = 2f;
    [SerializeField, Tooltip("When disabled, non-fatal hits only remove HP and grant invulnerability; the player remains where they were hit.")]
    private bool respawnAfterNonFatalDamage = true;
    [SerializeField, Tooltip("Optional fixed respawn marker. If empty, the player's initial transform is used.")]
    private Transform respawnPoint;
    [SerializeField, Tooltip("Authored child renderer used for the invincibility pulse.")]
    private Renderer invincibilityAura;

    private TopDownPlayer player;
    private Vector3 spawnPosition;
    private Quaternion spawnRotation;
    private float invulnerableUntil;
    private int lastDamageFrame = -1;
    private MaterialPropertyBlock auraProperties;
    private Coroutine auraRoutine;
    private Renderer[] characterRenderers;
    private Material[][] originalCharacterMaterials;
    private Material[][] transparentCharacterMaterials;

    public int MaxLives => maxLives;
    public int CurrentLives { get; private set; }
    public bool IsDefeated => CurrentLives <= 0;

    private void Awake()
    {
        player = GetComponent<TopDownPlayer>();
        spawnPosition = respawnPoint != null ? respawnPoint.position : transform.position;
        spawnRotation = respawnPoint != null ? respawnPoint.rotation : transform.rotation;
        CurrentLives = maxLives;
        BuildTransparentCharacterMaterials();
        if (invincibilityAura != null) invincibilityAura.gameObject.SetActive(false);
    }

    public bool TryTakeDamage(Object source)
    {
        if (IsDefeated || Time.frameCount == lastDamageFrame || Time.unscaledTime < invulnerableUntil ||
            GameManager.Instance != null && GameManager.Instance.IsLevelComplete)
        {
            return false;
        }

        // Reserve the hit immediately, before any life-change callbacks can report
        // another hazard in this frame. Keep a short grace period on every stage.
        lastDamageFrame = Time.frameCount;
        GrantInvincibility(Mathf.Max(.75f, invulnerabilityDuration));
        CurrentLives = Mathf.Max(0, CurrentLives - 1);

        if (CurrentLives == 0)
        {
            player.SetControlEnabled(false);
            GameManager.Instance?.PlayerDefeated();
            Debug.LogWarning($"Player ran out of lives after being hit by '{GetSourceName(source)}'.", this);
            return true;
        }

        string sceneName = SceneManager.GetActiveScene().name;
        bool keepCurrentPosition = !respawnAfterNonFatalDamage || sceneName == "Level_Laser" || sceneName == "Level_Pong";
        if (!keepCurrentPosition)
        {
            transform.rotation = spawnRotation;
            player.TeleportTo(spawnPosition);
        }
        Debug.LogWarning(
            $"Player was hit by '{GetSourceName(source)}'. {CurrentLives}/{maxLives} lives remain.",
            this);
        return true;
    }

    public bool Heal(int amount)
    {
        if (amount <= 0 || IsDefeated || CurrentLives >= maxLives) return false;
        CurrentLives = Mathf.Min(maxLives, CurrentLives + amount);
        return true;
    }

    public void GrantInvincibility(float duration)
    {
        if (IsDefeated || duration <= 0f) return;
        invulnerableUntil = Mathf.Max(invulnerableUntil, Time.unscaledTime + duration);
        if (auraRoutine == null) auraRoutine = StartCoroutine(ShowInvincibilityAura());
    }

    private IEnumerator ShowInvincibilityAura()
    {
        if (invincibilityAura != null) invincibilityAura.gameObject.SetActive(true);
        UseTransparentCharacterMaterials(true);
        while (Time.unscaledTime < invulnerableUntil)
        {
            float pulse = (Mathf.Sin(Time.unscaledTime * 12f) + 1f) * .5f;
            Color glow = new Color(.18f, .82f, 1f, Mathf.Lerp(.12f, .34f, pulse));
            UpdateAura(glow, pulse);
            UpdateCharacterTransparency(Mathf.Lerp(.38f, .55f, pulse), pulse);
            yield return null;
        }
        if (invincibilityAura != null) invincibilityAura.gameObject.SetActive(false);
        UseTransparentCharacterMaterials(false);
        auraRoutine = null;
    }

    private void BuildTransparentCharacterMaterials()
    {
        characterRenderers = GetComponentsInChildren<Renderer>(true);
        originalCharacterMaterials = new Material[characterRenderers.Length][];
        transparentCharacterMaterials = new Material[characterRenderers.Length][];
        for (int rendererIndex = 0; rendererIndex < characterRenderers.Length; rendererIndex++)
        {
            Material[] originals = characterRenderers[rendererIndex].sharedMaterials;
            originalCharacterMaterials[rendererIndex] = originals;
            transparentCharacterMaterials[rendererIndex] = new Material[originals.Length];
            for (int materialIndex = 0; materialIndex < originals.Length; materialIndex++)
            {
                if (originals[materialIndex] == null) continue;
                Material transparent = new Material(originals[materialIndex]);
                transparent.name = originals[materialIndex].name + " Invincible";
                ConfigureTransparent(transparent);
                transparentCharacterMaterials[rendererIndex][materialIndex] = transparent;
            }
        }
    }

    private static void ConfigureTransparent(Material material)
    {
        material.SetOverrideTag("RenderType", "Transparent");
        if (material.HasProperty("_Mode")) material.SetFloat("_Mode", 3f);
        if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
        if (material.HasProperty("_SrcBlend")) material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
        if (material.HasProperty("_DstBlend")) material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
        if (material.HasProperty("_ZWrite")) material.SetInt("_ZWrite", 0);
        material.DisableKeyword("_ALPHATEST_ON");
        material.EnableKeyword("_ALPHABLEND_ON");
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.EnableKeyword("_EMISSION");
        material.renderQueue = 3000;
    }

    private void UseTransparentCharacterMaterials(bool transparent)
    {
        if (characterRenderers == null) return;
        for (int i = 0; i < characterRenderers.Length; i++)
            if (characterRenderers[i] != null)
                characterRenderers[i].sharedMaterials = transparent ? transparentCharacterMaterials[i] : originalCharacterMaterials[i];
    }

    private void UpdateCharacterTransparency(float alpha, float pulse)
    {
        if (transparentCharacterMaterials == null) return;
        Color cyan = new(.18f, .82f, 1f, alpha);
        foreach (Material[] materials in transparentCharacterMaterials)
        {
            foreach (Material material in materials)
            {
                if (material == null) continue;
                if (material.HasProperty("_Color"))
                {
                    Color color = Color.Lerp(material.color, cyan, .28f);
                    color.a = alpha;
                    material.color = color;
                }
                if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", cyan);
                if (material.HasProperty("_EmissionColor")) material.SetColor("_EmissionColor", cyan * Mathf.Lerp(.7f, 1.8f, pulse));
            }
        }
    }

    private void UpdateAura(Color glow, float pulse)
    {
        if (invincibilityAura == null) return;
        auraProperties ??= new MaterialPropertyBlock();
        invincibilityAura.GetPropertyBlock(auraProperties);
        auraProperties.SetColor("_Color", glow);
        auraProperties.SetColor("_BaseColor", glow);
        auraProperties.SetColor("_EmissionColor",
            new Color(glow.r, glow.g, glow.b) * Mathf.Lerp(1.2f, 2.8f, pulse));
        invincibilityAura.SetPropertyBlock(auraProperties);
    }

    private void OnDestroy()
    {
        if (transparentCharacterMaterials == null) return;
        foreach (Material[] materials in transparentCharacterMaterials)
            foreach (Material material in materials)
                if (material != null) Destroy(material);
    }

    private static string GetSourceName(Object source)
    {
        return source != null ? source.name : "unknown source";
    }

#if UNITY_EDITOR
    public void ConfigureRespawnPointForEditor(Transform marker)
    {
        respawnPoint = marker;
    }

    public void ConfigureDamageRespawnForEditor(bool shouldRespawn)
    {
        respawnAfterNonFatalDamage = shouldRespawn;
    }

    public void ConfigureMaxLivesForEditor(int lives)
    {
        maxLives = Mathf.Max(1, lives);
    }

    public void ConfigureInvincibilityAuraForEditor(Renderer authoredAura)
    {
        invincibilityAura = authoredAura;
    }
#endif
}
