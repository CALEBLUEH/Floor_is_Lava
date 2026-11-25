using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class SceneLoadButton : MonoBehaviour
{
    [SerializeField] private string sceneName;
    private void Awake() => GetComponent<Button>().onClick.AddListener(Load);
    public void Load() => SceneManager.LoadScene(sceneName);
#if UNITY_EDITOR
    public void Configure(string targetScene) => sceneName = targetScene;
#endif
}
