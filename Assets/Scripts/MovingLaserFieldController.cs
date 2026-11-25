using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class MovingLaserFieldController : MonoBehaviour
{
    [SerializeField] private Transform[] beams;
    [SerializeField] private Vector3 arenaCenter;
    [SerializeField] private Vector2 arenaSize = new(18f, 18f);
    private Vector3[] fixedPositions;
    private Quaternion[] fixedRotations;
    private Coroutine routine;
    private int wave = 1;

    private void Awake()
    {
        fixedPositions = new Vector3[beams == null ? 0 : beams.Length];
        fixedRotations = new Quaternion[fixedPositions.Length];
        for (int i = 0; i < fixedPositions.Length; i++)
        {
            fixedPositions[i] = beams[i].position;
            fixedRotations[i] = beams[i].rotation;
        }
        HideAll();
    }

    public void SetWave(int newWave)
    {
        wave = Mathf.Clamp(newWave, 1, 3);
        if (routine != null) StopCoroutine(routine);
        RestoreAnchors();
        routine = StartCoroutine(SweepContinuously());
    }

    public void StopAndHide()
    {
        if (routine != null) StopCoroutine(routine);
        routine = null;
        HideAll();
    }

    private IEnumerator SweepContinuously()
    {
        int activeCount = Mathf.Min(wave, beams.Length);
        for (int i = 0; i < beams.Length; i++)
        {
            beams[i].gameObject.SetActive(i < activeCount);
            if (i < activeCount) SetBeamLive(beams[i]);
        }

        float speed = wave == 1 ? .18f : wave == 2 ? .3f : .45f;
        while (true)
        {
            for (int i = 0; i < activeCount; i++)
            {
                bool horizontal = Mathf.Abs(Vector3.Dot(fixedRotations[i] * Vector3.right, Vector3.right)) > .5f;
                float direction = i % 2 == 0 ? 1f : -1f;
                float offset = Mathf.Sin(Time.time * speed + i * 1.9f) *
                               (horizontal ? arenaSize.y : arenaSize.x) * .42f * direction;
                Vector3 position = horizontal
                    ? new Vector3(arenaCenter.x, fixedPositions[i].y, arenaCenter.z + offset)
                    : new Vector3(arenaCenter.x + offset, fixedPositions[i].y, arenaCenter.z);
                beams[i].SetPositionAndRotation(position, fixedRotations[i]);
            }
            yield return null;
        }
    }

    private static void SetBeamLive(Transform beam)
    {
        Collider collider = beam.GetComponent<Collider>();
        if (collider != null) collider.enabled = true;
        foreach (Renderer renderer in beam.GetComponentsInChildren<Renderer>())
        {
            if (renderer.sharedMaterial == null) continue;
            Color red = new(1f, .015f, .005f, 1f);
            renderer.material.color = red;
            renderer.material.EnableKeyword("_EMISSION");
            renderer.material.SetColor("_EmissionColor", red * 5f);
        }
    }

    private void RestoreAnchors()
    {
        for (int i = 0; i < beams.Length && i < fixedPositions.Length; i++)
            beams[i].SetPositionAndRotation(fixedPositions[i], fixedRotations[i]);
    }

    private void HideAll()
    {
        if (beams == null) return;
        foreach (Transform beam in beams) if (beam != null) beam.gameObject.SetActive(false);
    }

#if UNITY_EDITOR
    public void ConfigurePrefab(Transform[] laserBeams, Vector3 center, Vector2 size)
    { beams = laserBeams; arenaCenter = center; arenaSize = size; }
#endif
}
