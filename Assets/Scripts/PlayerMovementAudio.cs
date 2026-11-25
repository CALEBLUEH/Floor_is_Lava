using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerMovementAudio : MonoBehaviour
{
    private TopDownPlayer gameplayPlayer;
    private ElevatorFirstPersonMover elevatorPlayer;
    private CharacterController character;
    private Rigidbody body;
    [SerializeField] private AudioSource effects;
    private AudioClip[] steps;
    private AudioClip jumpVoice;
    private float nextStep;
    private float nextJumpSound;
    private bool wasGrounded;
    private bool airborne;
    private bool initialized;

    private void Awake()
    {
        PlayerMovementAudio[] movementAudio = GetComponents<PlayerMovementAudio>();
        if (movementAudio.Length > 1 && movementAudio[0] != this)
        {
            enabled = false;
            return;
        }

        gameplayPlayer = GetComponent<TopDownPlayer>();
        elevatorPlayer = GetComponent<ElevatorFirstPersonMover>();
        character = GetComponent<CharacterController>();
        body = GetComponent<Rigidbody>();
        if (effects == null)
        {
            Debug.LogError($"{nameof(PlayerMovementAudio)} on '{name}' needs an authored AudioSource reference.", this);
            enabled = false;
            return;
        }
        steps = new AudioClip[4];
        for (int i = 0; i < steps.Length; i++)
            steps[i] = Resources.Load<AudioClip>("SceneAudio/Footstep0" + (i + 1));
        jumpVoice = Resources.Load<AudioClip>("SceneAudio/JumpVoice");
    }

#if UNITY_EDITOR
    public void ConfigureAuthoredSource(AudioSource source)
    {
        effects = source;
    }
#endif

    private void Update()
    {
        effects.volume = 0.7f * GameSettings.SfxVolume;
        if (Time.timeScale <= 0f) return;

        bool grounded = gameplayPlayer != null ? gameplayPlayer.IsGrounded :
            character != null && character.isGrounded;
        bool moving = gameplayPlayer != null ? gameplayPlayer.IsMoving :
            elevatorPlayer != null && elevatorPlayer.IsMoving;
        bool sprinting = gameplayPlayer != null ? gameplayPlayer.IsSprinting :
            elevatorPlayer != null && elevatorPlayer.IsSprinting;

        if (!initialized)
        {
            initialized = true;
            wasGrounded = grounded;
        }

        if (gameplayPlayer != null)
        {
            if (wasGrounded && !grounded)
            {
                airborne = true;
                if (body != null && body.linearVelocity.y > 0.35f && Time.unscaledTime >= nextJumpSound)
                {
                    nextJumpSound = Time.unscaledTime + .65f;
                    if (jumpVoice != null)
                    {
                        effects.Stop();
                        effects.PlayOneShot(jumpVoice, .55f);
                    }
                }
            }
            else if (airborne && grounded)
            {
                airborne = false;
            }
        }

        if (moving && grounded && Time.time >= nextStep)
        {
            AudioClip step = steps[Random.Range(0, steps.Length)];
            if (step != null) effects.PlayOneShot(step, sprinting ? 0.52f : 0.42f);
            nextStep = Time.time + (sprinting ? 0.28f : 0.42f);
        }
        if (!moving) nextStep = Time.time;
        wasGrounded = grounded;
    }
}
