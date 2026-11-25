using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class RisingWallObstacleController : MonoBehaviour
{
    [SerializeField] private Transform[] walls;
    [SerializeField, Min(.1f)] private float riseHeight = 2.2f;
    [SerializeField, Min(.1f)] private float moveDuration = .45f;
    [SerializeField, Min(.1f)] private float raisedDuration = 2.1f;
    private Vector3[] loweredPositions;
    private Coroutine routine;
    private int wave = 1;

    public bool TryGetRaisedWallHit(Vector3 from, Vector3 to, float puckRadius, out Vector3 hit)
    {
        hit = default;
        if (walls == null) return false;
        Vector3 travel = to - from;
        float distance = travel.magnitude;
        if (distance < .001f) return false;
        Ray ray = new Ray(from, travel / distance);
        float nearest = float.PositiveInfinity;
        foreach (Transform wall in walls)
        {
            if (wall == null) continue;
            Collider collider = wall.GetComponent<Collider>();
            if (collider == null || !collider.enabled || collider.bounds.max.y < from.y + .25f) continue;
            Bounds bounds = collider.bounds;
            bounds.Expand(new Vector3(puckRadius * 2f, 0f, puckRadius * 2f));
            bounds.center = new Vector3(bounds.center.x, from.y, bounds.center.z);
            bounds.size = new Vector3(bounds.size.x, 2f, bounds.size.z);
            if (bounds.IntersectRay(ray, out float entry) && entry <= distance && entry < nearest)
            {
                nearest = entry;
                hit = from + ray.direction * Mathf.Max(0f, entry - .03f);
            }
        }
        return nearest < float.PositiveInfinity;
    }

    private void Awake()
    {
        loweredPositions = new Vector3[walls == null ? 0 : walls.Length];
        for (int i = 0; i < loweredPositions.Length; i++) loweredPositions[i] = walls[i].localPosition;
    }

    public void SetWave(int newWave)
    {
        wave = Mathf.Clamp(newWave, 1, 3);
        if (routine != null) StopCoroutine(routine);
        for (int i = 0; i < loweredPositions.Length; i++)
            if (walls[i] != null) walls[i].localPosition = loweredPositions[i];
        routine = StartCoroutine(Run());
    }

    private IEnumerator Run()
    {
        yield return new WaitForSeconds(wave == 1 ? 6f : 3f);
        while (true)
        {
            int count = wave == 3 ? 2 : 1;
            int first = Random.Range(0, walls.Length);
            int second = walls.Length > 1 ? (first + Random.Range(1, walls.Length)) % walls.Length : first;
            yield return MovePair(first, count > 1 ? second : -1, true);
            yield return new WaitForSeconds(raisedDuration);
            yield return MovePair(first, count > 1 ? second : -1, false);
            yield return new WaitForSeconds(wave == 1 ? 5f : wave == 2 ? 3.2f : 1.8f);
        }
    }

    private IEnumerator Move(int index, bool raise)
    {
        Transform wall = walls[index];
        Vector3 from = wall.localPosition;
        Vector3 to = loweredPositions[index] + Vector3.up * (raise ? riseHeight : 0f);
        for (float t = 0; t < 1f; t += Time.deltaTime / moveDuration)
        {
            wall.localPosition = Vector3.Lerp(from, to, Mathf.SmoothStep(0, 1, t));
            yield return null;
        }
        wall.localPosition = to;
    }

    private IEnumerator MovePair(int first, int second, bool raise)
    {
        Transform a = walls[first];
        Transform b = second >= 0 ? walls[second] : null;
        Vector3 aFrom = a.localPosition;
        Vector3 bFrom = b != null ? b.localPosition : Vector3.zero;
        Vector3 aTo = loweredPositions[first] + Vector3.up * (raise ? riseHeight : 0f);
        Vector3 bTo = b != null ? loweredPositions[second] + Vector3.up * (raise ? riseHeight : 0f) : Vector3.zero;
        for (float t = 0f; t < 1f; t += Time.deltaTime / moveDuration)
        {
            float eased = Mathf.SmoothStep(0f, 1f, t);
            a.localPosition = Vector3.Lerp(aFrom, aTo, eased);
            if (b != null) b.localPosition = Vector3.Lerp(bFrom, bTo, eased);
            yield return null;
        }
        a.localPosition = aTo;
        if (b != null) b.localPosition = bTo;
    }

#if UNITY_EDITOR
    public void ConfigurePrefab(Transform[] targets) { walls = targets; }
#endif
}
