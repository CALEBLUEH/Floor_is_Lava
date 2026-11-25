using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(TMP_Text))]
public sealed class BestTimeLabel : MonoBehaviour
{
    [SerializeField] private string sceneName;
    private TMP_Text label;

    private void Awake() { label = GetComponent<TMP_Text>(); Refresh(); }
    private void OnEnable() { if (label == null) label = GetComponent<TMP_Text>(); Refresh(); }

    private void Refresh()
    {
        if (label == null) return;
        label.text = BestTimeRecord.TryGet(sceneName, out float seconds, out bool perfect)
            ? $"BEST  {BestTimeRecord.Format(seconds)}{(perfect ? "  •  PERFECT" : string.Empty)}"
            : "BEST  --:--.--";
    }

#if UNITY_EDITOR
    public void ConfigurePrefab(string targetScene)
    {
        sceneName = targetScene;
        if (label == null) label = GetComponent<TMP_Text>();
        Refresh();
    }
#endif
}
