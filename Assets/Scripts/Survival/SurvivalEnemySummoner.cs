using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(SurvivalEnemy))]
public sealed class SurvivalEnemySummoner : MonoBehaviour
{
    [SerializeField] private SurvivalEnemy summonPrefab;
    [SerializeField, Min(.1f)] private float summonInterval = 6f;
    [SerializeField, Min(0f)] private float initialDelay = 2f;
    [SerializeField, Min(1)] private int maximumLivingSummons = 3;

    private readonly List<SurvivalEnemy> livingSummons = new();
    private SurvivalEnemy owner;
    private float nextSummonTime;

    private void Awake() => owner = GetComponent<SurvivalEnemy>();

    public void Initialize()
    {
        nextSummonTime = Time.time + initialDelay;
    }

    private void Update()
    {
        if (owner == null || owner.Target == null || summonPrefab == null ||
            owner.CurrentHealth <= 0 || Time.time < nextSummonTime) return;

        livingSummons.RemoveAll(item => item == null || item.CurrentHealth <= 0);
        if (livingSummons.Count < maximumLivingSummons)
        {
            Vector3 offset = Random.insideUnitSphere;
            offset.y = 0f;
            if (offset.sqrMagnitude < .01f) offset = Vector3.right;
            Vector3 position = owner.ClampToArena(transform.position + offset.normalized * owner.Grid.CellSize * 1.5f);
            position.y = owner.CombatPlaneWorldHeight;
            SurvivalEnemy summon = Instantiate(summonPrefab, position, Quaternion.identity, owner.EnemyRoot);
            summon.Initialize(owner.Target, summon.MaxHealth, summon.MoveSpeed, owner.Grid, owner.EnemyRoot, owner.EffectRoot);
            livingSummons.Add(summon);
        }
        nextSummonTime = Time.time + summonInterval;
    }

#if UNITY_EDITOR
    public void ConfigureForEditor(SurvivalEnemy prefab, float interval, float delay, int maximum)
    {
        summonPrefab = prefab;
        summonInterval = Mathf.Max(.1f, interval);
        initialDelay = Mathf.Max(0f, delay);
        maximumLivingSummons = Mathf.Max(1, maximum);
    }
#endif
}
