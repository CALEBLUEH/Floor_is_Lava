using UnityEngine;

public interface IBossDamageTarget
{
    Transform TargetTransform { get; }
    bool IsDefeated { get; }
    void TakeDamage(int amount);
    void LaunchPlayerAttack(Vector3 launchPosition);
}
