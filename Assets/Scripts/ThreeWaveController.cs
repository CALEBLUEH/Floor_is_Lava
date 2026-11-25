using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class ThreeWaveController : MonoBehaviour
{
    [SerializeField] private GameObject[] waveOneObjectives;
    [SerializeField] private GameObject[] waveTwoObjectives;
    [SerializeField] private GameObject[] waveThreeObjectives;
    [SerializeField, Min(1)] private int celebrationFlashes = 3;
    [SerializeField, Min(.05f)] private float flashDuration = .24f;
    private int currentWave = 1;
    private bool transitioning;
    public int CurrentWave => currentWave;

    private void Awake()
    {
        RandomizeObjectiveOrder();
        SetObjectivesEnabled(waveOneObjectives, true);
        SetObjectivesEnabled(waveTwoObjectives, false);
        SetObjectivesEnabled(waveThreeObjectives, false);
    }
    private void Start() => ApplyWave(1);

    public bool TryAdvanceWave()
    {
        if (transitioning || currentWave >= 3) return transitioning;
        StartCoroutine(AdvanceRoutine()); return true;
    }

    private IEnumerator AdvanceRoutine()
    {
        transitioning = true;
        FindAnyObjectByType<LaserTilePatternController>()?.StopAndClear();
        FindAnyObjectByType<MovingLaserFieldController>()?.StopAndHide();
        TileGrid grid = FindAnyObjectByType<TileGrid>();
        FloorTile[] tiles = grid != null
            ? grid.GetComponentsInChildren<FloorTile>(true)
            : FindObjectsByType<FloorTile>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int flash = 0; flash < celebrationFlashes; flash++)
        {
            foreach (FloorTile tile in tiles) tile.SetCelebrationGreen(true, this);
            yield return new WaitForSeconds(flashDuration);
            foreach (FloorTile tile in tiles) tile.SetCelebrationGreen(false, this);
            yield return new WaitForSeconds(flashDuration);
        }
        SetObjectivesEnabled(currentWave == 1 ? waveOneObjectives : waveTwoObjectives, false);
        currentWave++;
        SetObjectivesEnabled(currentWave == 2 ? waveTwoObjectives : waveThreeObjectives, true);
        FindAnyObjectByType<PlayerLife>()?.GrantInvincibility(2f);
        ApplyWave(currentWave); GameManager.Instance?.RecountObjectives(); transitioning = false;
    }

    private void ApplyWave(int wave)
    {
        FindAnyObjectByType<PongArenaController>()?.SetWave(wave);
        FindAnyObjectByType<LaserTilePatternController>()?.SetWave(wave);
        FindAnyObjectByType<MovingLaserFieldController>()?.SetWave(wave);
        foreach (RisingWallObstacleController walls in FindObjectsByType<RisingWallObstacleController>()) walls.SetWave(wave);
        GameManager.Instance?.SetWave(wave, 3);
    }

    private static void SetObjectivesEnabled(GameObject[] targets, bool enabledState)
    {
        if (targets == null) return;
        foreach (GameObject target in targets)
        {
            if (target == null) continue;
            target.SetActive(true);
            target.GetComponent<FloorTile>()?.SetObjectiveEnabled(enabledState);
        }
    }

    private void RandomizeObjectiveOrder()
    {
        int firstCount = waveOneObjectives?.Length ?? 0;
        int secondCount = waveTwoObjectives?.Length ?? 0;
        int thirdCount = waveThreeObjectives?.Length ?? 0;
        GameObject[] shuffled = CombineUnique(waveOneObjectives, waveTwoObjectives, waveThreeObjectives);
        for (int i = shuffled.Length - 1; i > 0; i--)
        {
            int swap = Random.Range(0, i + 1);
            (shuffled[i], shuffled[swap]) = (shuffled[swap], shuffled[i]);
        }
        waveOneObjectives = Slice(shuffled, 0, firstCount);
        waveTwoObjectives = Slice(shuffled, firstCount, secondCount);
        waveThreeObjectives = Slice(shuffled, firstCount + secondCount, thirdCount);
    }

    private static GameObject[] CombineUnique(params GameObject[][] groups)
    {
        var result = new System.Collections.Generic.List<GameObject>();
        var seen = new System.Collections.Generic.HashSet<GameObject>();
        foreach (GameObject[] group in groups)
            if (group != null)
                foreach (GameObject objective in group)
                    if (objective != null && seen.Add(objective)) result.Add(objective);
        return result.ToArray();
    }

    private static GameObject[] Slice(GameObject[] source, int start, int count)
    {
        int available = Mathf.Max(0, Mathf.Min(count, source.Length - start));
        var result = new GameObject[available];
        System.Array.Copy(source, start, result, 0, available);
        return result;
    }

#if UNITY_EDITOR
    public void ConfigurePrefab(GameObject[] first, GameObject[] second, GameObject[] third)
    {
        waveOneObjectives = first;
        waveTwoObjectives = second;
        waveThreeObjectives = third;
        flashDuration = .4f;
    }
#endif
}
