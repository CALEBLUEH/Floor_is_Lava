using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class BossAttackCoroutineHost : MonoBehaviour
{
    public Coroutine Run(IEnumerator attackRoutine)
    {
        return attackRoutine != null ? StartCoroutine(attackRoutine) : null;
    }

    public static BossAttackCoroutineHost GetOrAdd(Transform owner)
    {
        if (owner == null) return null;
        BossAttackCoroutineHost host = owner.GetComponent<BossAttackCoroutineHost>();
        return host != null ? host : owner.gameObject.AddComponent<BossAttackCoroutineHost>();
    }
}
