using System.Collections.Generic;
using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public class PongArenaController : MonoBehaviour
{
    [SerializeField] private TileGrid grid;
    [SerializeField] private Transform southPaddle;
    [SerializeField] private Transform northPaddle;
    [SerializeField] private Transform ball;

    [Header("Ball pass")]
    [SerializeField, Min(0.25f)] private float passDuration = 2.15f;
    [SerializeField, Min(0f)] private float pauseBeforeReturn = 0.2f;
    [SerializeField, Min(0f)] private float ballBaseHeight = 0.9f;
    [SerializeField, Min(0f)] private float ballArcHeight = 1.6f;
    [SerializeField, Range(0f, 1f)] private float targetEdgePadding = 0.12f;
    [SerializeField, Min(0f)] private float paddleContactOffset = 2.4f;

    [Header("Multi-ball and goals")]
    [SerializeField, Range(0f, .5f)] private float waveOneGoalChance = .08f;
    [SerializeField, Range(0f, .5f)] private float waveTwoGoalChance = .14f;
    [SerializeField, Range(0f, .5f)] private float waveThreeGoalChance = .22f;
    [SerializeField, Min(.2f)] private float goalDepth = 2.2f;
    [SerializeField, Min(.1f)] private float goalResetDelay = .65f;
    [SerializeField, Min(0f)] private float extraBallLaunchDelay = .18f;

    [Header("AI paddles")]
    [SerializeField, Min(0f)] private float paddleSpeed = 10f;

    private sealed class BallState
    {
        public Transform Actor;
        public Renderer[] Renderers;
        public Collider[] Colliders;
        public Vector3 PassStart;
        public Vector3 PassEnd;
        public float PassTime;
        public float PauseTime;
        public float Duration;
        public float ReboundCooldown;
        public bool TravellingNorth;
        public bool GoalShot;
        public bool WaitingAfterGoal;
    }

    private readonly List<BallState> balls = new();
    private RisingWallObstacleController raisedWalls;
    private float southZ;
    private float northZ;
    private float southContactZ;
    private float northContactZ;
    private float minX;
    private float maxX;
    private float southAimX;
    private float northAimX;
    private int wave = 1;
    private int northScore;
    private int southScore;
    private TextMeshPro worldScoreText;
    private bool initialized;

    private void Start()
    {
        if (!ResolveArena())
        {
            enabled = false;
            return;
        }

        raisedWalls = FindAnyObjectByType<RisingWallObstacleController>();
        GameObject scoreObject = GameObject.Find("Puck Score Text");
        if (scoreObject != null) worldScoreText = scoreObject.GetComponent<TextMeshPro>();
        RefreshWorldScore();
        float centerX = (minX + maxX) * .5f;
        southAimX = centerX;
        northAimX = centerX;
        southPaddle.position = WithX(southPaddle.position, centerX);
        northPaddle.position = WithX(northPaddle.position, centerX);

        initialized = true;
        EnsureBallCount(wave);
    }

    private void Update()
    {
        if (!initialized) return;

        UpdatePaddleTargets();
        UpdatePaddles();

        for (int i = 0; i < balls.Count; i++)
        {
            BallState state = balls[i];
            if (!state.Actor.gameObject.activeSelf) continue;
            UpdateBall(state);
        }
    }

    private void UpdateBall(BallState state)
    {
        if (state.PauseTime > 0f)
        {
            state.PauseTime -= Time.deltaTime;
            if (state.PauseTime > 0f) return;

            if (state.WaitingAfterGoal)
            {
                state.WaitingAfterGoal = false;
                state.TravellingNorth = !state.TravellingNorth;
                SetBallVisible(state, true);
                float serveZ = state.TravellingNorth ? southContactZ : northContactZ;
                BeginPass(state, ChooseGoalLaneX(), serveZ, true);
            }
        }

        state.PassTime += Time.deltaTime;
        state.ReboundCooldown = Mathf.Max(0f, state.ReboundCooldown - Time.deltaTime);
        float t = Mathf.Clamp01(state.PassTime / Mathf.Max(.01f, state.Duration));
        Vector3 position = Vector3.Lerp(state.PassStart, state.PassEnd, t);
        position.y = ballBaseHeight + Mathf.Sin(t * Mathf.PI) * ballArcHeight;

        if (!state.GoalShot && state.ReboundCooldown <= 0f && raisedWalls != null &&
            raisedWalls.TryGetRaisedWallHit(state.Actor.position, position, .9f, out Vector3 contact))
        {
            BounceFromWall(state, contact);
            return;
        }

        state.Actor.position = position;
        if (t < 1f) return;

        if (state.GoalShot)
        {
            if (state.TravellingNorth) southScore++;
            else northScore++;
            RefreshWorldScore();
            state.GoalShot = false;
            state.WaitingAfterGoal = true;
            state.PauseTime = goalResetDelay;
            SetBallVisible(state, false);
            return;
        }

        state.TravellingNorth = !state.TravellingNorth;
        state.PauseTime = pauseBeforeReturn;
        BeginPass(state, state.Actor.position.x, state.Actor.position.z, true);
    }

    private bool ResolveArena()
    {
        if (grid == null || southPaddle == null || northPaddle == null || ball == null)
        {
            Debug.LogWarning("Pong arena needs a grid, two paddles, and a ball.", this);
            return false;
        }

        Vector3 southWest = grid.transform.TransformPoint(Vector3.zero);
        Vector3 northEast = grid.transform.TransformPoint(new Vector3(
            (grid.Width - 1) * grid.CellSize,
            0f,
            (grid.Height - 1) * grid.CellSize));

        float padding = grid.CellSize * targetEdgePadding;
        minX = Mathf.Min(southWest.x, northEast.x) + padding;
        maxX = Mathf.Max(southWest.x, northEast.x) - padding;
        southZ = southPaddle.position.z;
        northZ = northPaddle.position.z;
        southContactZ = southZ + paddleContactOffset;
        northContactZ = northZ - paddleContactOffset;
        return true;
    }

    private void EnsureBallCount(int requestedCount)
    {
        requestedCount = Mathf.Clamp(requestedCount, 1, 3);
        if (balls.Count == 0)
        {
            balls.Add(CreateState(ball));
            LaunchBall(balls[0], 0);
        }

        while (balls.Count < requestedCount)
        {
            Transform clone = Instantiate(ball.gameObject, ball.parent).transform;
            clone.name = "Pong Hazard Ball " + (balls.Count + 1);
            BallState state = CreateState(clone);
            balls.Add(state);
            LaunchBall(state, balls.Count - 1);
        }

        for (int i = 0; i < balls.Count; i++)
        {
            bool active = i < requestedCount;
            balls[i].Actor.gameObject.SetActive(active);
            if (active && balls[i].Duration <= 0f) LaunchBall(balls[i], i);
        }
    }

    private BallState CreateState(Transform actor)
    {
        return new BallState
        {
            Actor = actor,
            Renderers = actor.GetComponentsInChildren<Renderer>(true),
            Colliders = actor.GetComponentsInChildren<Collider>(true)
        };
    }

    private void LaunchBall(BallState state, int index)
    {
        state.TravellingNorth = index % 2 == 0;
        state.WaitingAfterGoal = false;
        SetBallVisible(state, true);
        float launchX = Mathf.Lerp(minX, maxX, LaunchRatio(index));
        float launchZ = state.TravellingNorth ? southContactZ : northContactZ;
        BeginPass(state, launchX, launchZ, false);
        state.PauseTime = index * extraBallLaunchDelay;
    }

    private static float LaunchRatio(int index)
    {
        if (index == 0) return .5f;
        return index == 1 ? .3f : .7f;
    }

    private void BeginPass(BallState state, float fromX, float fromZ, bool allowGoal)
    {
        bool scoreAttempt = allowGoal && Random.value < CurrentGoalChance();
        float targetX = scoreAttempt ? ChooseGoalLaneX() : ChooseTargetX();
        float targetZ;
        if (state.TravellingNorth)
            targetZ = scoreAttempt ? northZ + goalDepth : northContactZ;
        else
            targetZ = scoreAttempt ? southZ - goalDepth : southContactZ;

        state.GoalShot = scoreAttempt;
        state.PassStart = new Vector3(Mathf.Clamp(fromX, minX, maxX), ballBaseHeight, fromZ);
        state.PassEnd = new Vector3(targetX, ballBaseHeight, targetZ);
        state.PassTime = 0f;
        float arenaLength = Mathf.Max(.1f, Mathf.Abs(northContactZ - southContactZ));
        state.Duration = Mathf.Max(.3f, passDuration * Mathf.Abs(targetZ - fromZ) / arenaLength);
        state.Actor.position = state.PassStart;
    }

    private void BounceFromWall(BallState state, Vector3 contact)
    {
        state.TravellingNorth = !state.TravellingNorth;
        state.ReboundCooldown = .35f;
        BeginPass(state, Mathf.Clamp(contact.x, minX, maxX), contact.z, false);
    }

    private void UpdatePaddleTargets()
    {
        float bestNorthZ = float.NegativeInfinity;
        float bestSouthZ = float.PositiveInfinity;
        for (int i = 0; i < balls.Count; i++)
        {
            BallState state = balls[i];
            if (!state.Actor.gameObject.activeSelf || state.WaitingAfterGoal) continue;

            if (state.TravellingNorth && state.Actor.position.z > bestNorthZ)
            {
                bestNorthZ = state.Actor.position.z;
                northAimX = state.GoalShot ? ChooseMissX(state.PassEnd.x) : state.PassEnd.x;
            }
            else if (!state.TravellingNorth && state.Actor.position.z < bestSouthZ)
            {
                bestSouthZ = state.Actor.position.z;
                southAimX = state.GoalShot ? ChooseMissX(state.PassEnd.x) : state.PassEnd.x;
            }
        }
    }

    private void UpdatePaddles()
    {
        Vector3 southPosition = southPaddle.position;
        southPosition.x = Mathf.MoveTowards(southPosition.x, Mathf.Clamp(southAimX, minX, maxX), paddleSpeed * Time.deltaTime);
        southPaddle.position = southPosition;

        Vector3 northPosition = northPaddle.position;
        northPosition.x = Mathf.MoveTowards(northPosition.x, Mathf.Clamp(northAimX, minX, maxX), paddleSpeed * Time.deltaTime);
        northPaddle.position = northPosition;
    }

    private float CurrentGoalChance()
    {
        return wave == 1 ? waveOneGoalChance : wave == 2 ? waveTwoGoalChance : waveThreeGoalChance;
    }

    private float ChooseTargetX() => Random.Range(minX, maxX);

    private float ChooseGoalLaneX()
    {
        return Random.Range(Mathf.Lerp(minX, maxX, .38f), Mathf.Lerp(minX, maxX, .62f));
    }

    private float ChooseMissX(float shotX)
    {
        float offset = Mathf.Max(2.5f, (maxX - minX) * .2f);
        float center = (minX + maxX) * .5f;
        return Mathf.Clamp(shotX + (shotX <= center ? offset : -offset), minX, maxX);
    }

    private static void SetBallVisible(BallState state, bool visible)
    {
        foreach (Renderer renderer in state.Renderers) renderer.enabled = visible;
        foreach (Collider collider in state.Colliders) collider.enabled = visible;
    }

    private void RefreshWorldScore()
    {
        if (worldScoreText == null) return;
        worldScoreText.text = $"<color=#55F5FF>CYAN</color>  {northScore:00}   <color=#FFFFFF>|</color>   {southScore:00}  <color=#FF646C>RED</color>";
    }

    public void SetWave(int newWave)
    {
        wave = Mathf.Clamp(newWave, 1, 3);
        passDuration = wave == 1 ? 1.9f : wave == 2 ? 1.55f : 1.2f;
        paddleSpeed = wave == 1 ? 16f : wave == 2 ? 20f : 25f;
        targetEdgePadding = wave == 1 ? .18f : wave == 2 ? .1f : .04f;
        if (ResolveArena() && initialized) EnsureBallCount(wave);
    }

    private static Vector3 WithX(Vector3 position, float x)
    {
        position.x = x;
        return position;
    }

#if UNITY_EDITOR
    public void ConfigurePrefab(TileGrid tileGrid, Transform south, Transform north, Transform pongBall)
    {
        grid = tileGrid;
        southPaddle = south;
        northPaddle = north;
        ball = pongBall;
    }
#endif
}
