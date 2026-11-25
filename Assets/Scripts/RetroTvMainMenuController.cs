using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class RetroTvMainMenuController : MonoBehaviour
{
    [SerializeField] private Camera menuCamera;
    [SerializeField] private Transform televisionTarget;
    [SerializeField] private CanvasGroup promptGroup;
    [SerializeField] private Image fadeImage;
    [SerializeField] private string nextScene = "Interface_LevelSelection";
    [SerializeField, Min(.1f)] private float zoomDuration = 1.35f;
    [SerializeField, Min(.05f)] private float fadeDuration = .35f;
    private bool starting;

    private void Update()
    {
        if (!starting && Input.GetKeyDown(KeyCode.Escape))
        {
            Application.Quit();
            return;
        }
        if (!starting && (Input.anyKeyDown || Input.GetMouseButtonDown(0) || TouchStarted()))
            StartCoroutine(EnterTelevision());
    }

    private static bool TouchStarted()
    {
        for (int i = 0; i < Input.touchCount; i++) if (Input.GetTouch(i).phase == TouchPhase.Began) return true;
        return false;
    }

    private IEnumerator EnterTelevision()
    {
        starting = true;
        Vector3 startPosition = menuCamera.transform.position;
        Quaternion startRotation = menuCamera.transform.rotation;
        float startFov = menuCamera.fieldOfView;
        Vector3 direction = (televisionTarget.position - startPosition).normalized;
        Vector3 endPosition = televisionTarget.position - direction * .12f;
        Quaternion endRotation = Quaternion.LookRotation(direction, Vector3.up);
        for (float elapsed = 0; elapsed < zoomDuration; elapsed += Time.unscaledDeltaTime)
        {
            float t = Mathf.SmoothStep(0, 1, elapsed / zoomDuration);
            menuCamera.transform.SetPositionAndRotation(Vector3.Lerp(startPosition, endPosition, t), Quaternion.Slerp(startRotation, endRotation, t));
            menuCamera.fieldOfView = Mathf.Lerp(startFov, 30f, t);
            if (promptGroup != null) promptGroup.alpha = 1f - t;
            yield return null;
        }
        for (float elapsed = 0; elapsed < fadeDuration; elapsed += Time.unscaledDeltaTime)
        {
            Color color = fadeImage.color; color.a = Mathf.Clamp01(elapsed / fadeDuration); fadeImage.color = color;
            yield return null;
        }
        SceneManager.LoadScene(nextScene);
    }

#if UNITY_EDITOR
    public void Configure(Camera camera, Transform target, CanvasGroup prompt, Image fade, string scene)
    { menuCamera = camera; televisionTarget = target; promptGroup = prompt; fadeImage = fade; nextScene = scene; }
#endif
}
