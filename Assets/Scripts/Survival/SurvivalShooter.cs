using UnityEngine;
using UnityEngine.EventSystems;

[DisallowMultipleComponent]
[RequireComponent(typeof(TopDownPlayer))]
public sealed class SurvivalShooter : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera aimCamera;
    [SerializeField] private SurvivalPlayerProjectile projectilePrefab;
    [SerializeField] private Transform projectileRoot;
    [SerializeField] private GameplayUIController gameplayUi;
    [SerializeField] private Transform combatPlaneReference;

    [Header("Shooting")]
    [SerializeField, Min(.05f)] private float shotsPerSecond = 4f;
    [SerializeField, Min(.1f)] private float projectileSpeed = 18f;
    [SerializeField, Min(.1f)] private float projectileLifetime = 4f;
    [SerializeField, Min(1)] private int projectileDamage = 1;
    [SerializeField] private Vector3 muzzleOffset = new(0f, .65f, 0f);
    [SerializeField, Min(0f)] private float combatPlaneHeight = 1.8f;

    private TopDownPlayer player;
    private PlayerLife playerLife;
    private float nextShotTime;

    private void Awake()
    {
        player = GetComponent<TopDownPlayer>();
        playerLife = GetComponent<PlayerLife>();
        if (aimCamera == null) aimCamera = Camera.main;
    }

    private void Update()
    {
        if (!Input.GetMouseButton(0) || Time.time < nextShotTime || !CanShoot()) return;
        if (!TryGetMouseDirection(out Vector3 direction)) return;

        Fire(direction);
    }

    private bool CanShoot()
    {
        if (projectilePrefab == null || aimCamera == null || PauseMenuController.IsGamePaused ||
            gameplayUi != null && !gameplayUi.IsGameStarted ||
            playerLife != null && playerLife.IsDefeated || !player.ControlsEnabled)
            return false;
        return EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject();
    }

    private bool TryGetMouseDirection(out Vector3 direction)
    {
        Vector3 origin = GetProjectileOrigin();
        Ray ray = aimCamera.ScreenPointToRay(Input.mousePosition);
        Plane plane = new(Vector3.up, origin);
        if (!plane.Raycast(ray, out float distance))
        {
            direction = Vector3.zero;
            return false;
        }

        direction = ray.GetPoint(distance) - origin;
        direction.y = 0f;
        direction = direction.normalized;
        return direction.sqrMagnitude > .001f;
    }

    private Vector3 GetProjectileOrigin()
    {
        Vector3 origin = transform.position + muzzleOffset;
        float referenceHeight = combatPlaneReference != null ? combatPlaneReference.position.y : 0f;
        origin.y = referenceHeight + combatPlaneHeight;
        return origin;
    }

    private void Fire(Vector3 direction)
    {
        Vector3 origin = GetProjectileOrigin();
        SurvivalPlayerProjectile projectile = Instantiate(
            projectilePrefab,
            origin,
            Quaternion.LookRotation(direction, Vector3.up),
            projectileRoot);
        projectile.Initialize(direction, projectileSpeed, projectileLifetime, projectileDamage);
        SceneAudioDirector.PlayPlayerShot();
        nextShotTime = Time.time + 1f / Mathf.Max(.05f, shotsPerSecond);
    }

#if UNITY_EDITOR
    public void ConfigureForEditor(Camera cameraReference, SurvivalPlayerProjectile prefab,
        Transform projectileParent, GameplayUIController ui, Transform planeReference)
    {
        aimCamera = cameraReference;
        projectilePrefab = prefab;
        projectileRoot = projectileParent;
        gameplayUi = ui;
        combatPlaneReference = planeReference;
    }

    public void FireAtForTest(Vector3 worldTarget)
    {
        Vector3 origin = GetProjectileOrigin();
        Vector3 direction = Vector3.ProjectOnPlane(worldTarget - origin, Vector3.up).normalized;
        if (direction.sqrMagnitude > .001f) Fire(direction);
    }
#endif
}
