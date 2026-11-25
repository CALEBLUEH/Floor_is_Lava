using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(SurvivalEnemy))]
public sealed class SurvivalEnemyHealthBar : MonoBehaviour
{
    [SerializeField] private GameObject barRoot;
    [SerializeField] private Image fillImage;
    [SerializeField] private RectTransform fillRect;

    private SurvivalEnemy enemy;
    private Camera viewCamera;

    private void Awake()
    {
        enemy = GetComponent<SurvivalEnemy>();
        viewCamera = Camera.main;
        if (fillRect == null && fillImage != null)
        {
            fillRect = fillImage.rectTransform;
            fillRect.pivot = new Vector2(0f, .5f);
        }
        Refresh();
    }

    private void LateUpdate()
    {
        Refresh();
        if (barRoot == null || !barRoot.activeSelf) return;
        if (viewCamera == null) viewCamera = Camera.main;
        if (viewCamera != null) barRoot.transform.rotation = viewCamera.transform.rotation;
    }

    public void Refresh()
    {
        if (enemy == null) enemy = GetComponent<SurvivalEnemy>();
        if (enemy == null || barRoot == null) return;

        bool visible = enemy.CurrentHealth > 0 && enemy.CurrentHealth < enemy.MaxHealth;
        if (barRoot.activeSelf != visible) barRoot.SetActive(visible);
        float normalizedHealth = enemy.MaxHealth > 0
            ? Mathf.Clamp01((float)enemy.CurrentHealth / enemy.MaxHealth)
            : 0f;
        if (fillImage != null) fillImage.fillAmount = normalizedHealth;
        if (fillRect != null)
            fillRect.localScale = new Vector3(normalizedHealth, 1f, 1f);
    }

    public float DisplayedHealthNormalized => fillRect != null
        ? fillRect.localScale.x
        : fillImage != null ? fillImage.fillAmount : 0f;

#if UNITY_EDITOR
    public void ConfigureForEditor(GameObject root, Image fill, RectTransform fillTransform)
    {
        barRoot = root;
        fillImage = fill;
        fillRect = fillTransform;
    }
#endif
}
