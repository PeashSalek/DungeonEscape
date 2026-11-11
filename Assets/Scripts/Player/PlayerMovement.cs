using UnityEngine;

/// <summary>
/// Third-person CharacterController movement: camera-relative walking and sprinting,
/// jumping, gravity, dodge roll with invulnerability, knockback and slow zones.
/// (Extended from the original prototype controller.)
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Movement")]
    public float walkSpeed = 4f;
    public float runSpeed = 7f;
    public float rotationSpeed = 10f;

    [Header("Gravity")]
    public float gravity = -9.81f;

    [Header("Jump")]
    public float jumpHeight = 1.5f;

    [Header("Dodge")]
    public float dodgeSpeed = 11f;
    public float dodgeDuration = 0.4f;
    public float dodgeInvulnerability = 0.35f;
    public float dodgeCooldown = 0.25f;

    [Header("Visuals (optional)")]
    public Animator animator;

    /// <summary>Multiplier from slow zones such as poison water (1 = normal).</summary>
    [System.NonSerialized] public float environmentSpeedMultiplier = 1f;
    /// <summary>Multiplier from blessings and curses.</summary>
    [System.NonSerialized] public float speedMultiplier = 1f;
    /// <summary>When true, dodging costs no stamina (Fleet Foot blessing).</summary>
    [System.NonSerialized] public bool freeDodge;

    public bool IsDodging => Time.time < dodgeEndTime;
    public bool IsGrounded => controller.isGrounded;
    public bool MovementLocked { get; set; }      // set by combat during attacks
    public Transform LockTarget { get; set; }     // set by lock-on
    public Vector3 Velocity { get; private set; }

    CharacterController controller;
    PlayerStats stats;
    Health health;
    Transform cameraTransform;
    Vector3 verticalVelocity;
    Vector3 knockbackVelocity;
    Vector3 dodgeDirection;
    float dodgeEndTime;
    float nextDodgeTime;

    static readonly int SpeedHash = Animator.StringToHash("Speed");
    static readonly int GroundedHash = Animator.StringToHash("Grounded");
    static readonly int DodgeHash = Animator.StringToHash("Dodge");
    static readonly int JumpHash = Animator.StringToHash("Jump");

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        stats = GetComponent<PlayerStats>();
        health = GetComponent<Health>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (Camera.main != null) cameraTransform = Camera.main.transform;
        GameInput.Init();
    }

    void OnEnable()
    {
        if (health != null) health.Damaged += OnDamaged;
    }

    void OnDisable()
    {
        if (health != null) health.Damaged -= OnDamaged;
    }

    void Update()
    {
        if (health != null && health.IsDead)
        {
            HandleGravity();
            return;
        }
        if (cameraTransform == null && Camera.main != null) cameraTransform = Camera.main.transform;

        HandleDodgeInput();
        HandleMovement();
        HandleJump();
        HandleGravity();
        UpdateAnimator();
    }

    Vector3 CameraRelative(Vector2 input)
    {
        if (cameraTransform == null) return new Vector3(input.x, 0f, input.y).normalized;
        Vector3 f = cameraTransform.forward; f.y = 0f; f.Normalize();
        Vector3 r = cameraTransform.right; r.y = 0f; r.Normalize();
        return Vector3.ClampMagnitude(f * input.y + r * input.x, 1f);
    }

    void HandleDodgeInput()
    {
        if (!GameInput.Dodge.WasPressedThisFrame() || IsDodging || Time.time < nextDodgeTime) return;
        if (!controller.isGrounded) return;
        if (!freeDodge && stats != null && !stats.TryUseStamina(stats.dodgeCost)) return;

        Vector3 dir = CameraRelative(GameInput.Move.ReadValue<Vector2>());
        if (dir.sqrMagnitude < 0.01f) dir = -transform.forward; // back-step when no direction
        dodgeDirection = dir.normalized;
        dodgeEndTime = Time.time + dodgeDuration;
        nextDodgeTime = dodgeEndTime + dodgeCooldown;
        if (health != null) health.invulnerableUntil = Time.time + dodgeInvulnerability;
        transform.rotation = Quaternion.LookRotation(dodgeDirection);
        MovementLocked = false;
        if (animator != null) animator.SetTrigger(DodgeHash);
    }

    void HandleMovement()
    {
        Vector3 move = Vector3.zero;

        if (IsDodging)
        {
            move = dodgeDirection * dodgeSpeed;
        }
        else if (!MovementLocked)
        {
            Vector3 direction = CameraRelative(GameInput.Move.ReadValue<Vector2>());
            if (direction.magnitude >= 0.1f)
            {
                bool sprinting = GameInput.Sprint.IsPressed() && LockTarget == null &&
                                 (stats == null || stats.Stamina > 1f);
                if (sprinting && stats != null) stats.DrainStamina(stats.sprintCostPerSecond * Time.deltaTime);

                float speed = (sprinting ? runSpeed : walkSpeed) * speedMultiplier * environmentSpeedMultiplier;
                move = direction * speed;

                Vector3 face = direction;
                if (LockTarget != null)
                {
                    face = LockTarget.position - transform.position;
                    face.y = 0f;
                }
                if (face.sqrMagnitude > 0.001f)
                {
                    Quaternion targetRotation = Quaternion.LookRotation(face);
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
                }
            }
            else if (LockTarget != null)
            {
                Vector3 face = LockTarget.position - transform.position; face.y = 0f;
                if (face.sqrMagnitude > 0.001f)
                    transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(face), rotationSpeed * Time.deltaTime);
            }
        }

        // Knockback decays quickly.
        move += knockbackVelocity;
        knockbackVelocity = Vector3.MoveTowards(knockbackVelocity, Vector3.zero, 25f * Time.deltaTime);

        Velocity = move;
        controller.Move(move * Time.deltaTime);
    }

    void HandleJump()
    {
        if (MovementLocked || IsDodging) return;
        if (GameInput.Jump.WasPressedThisFrame() && controller.isGrounded)
        {
            verticalVelocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            if (animator != null) animator.SetTrigger(JumpHash);
        }
    }

    void HandleGravity()
    {
        if (controller.isGrounded && verticalVelocity.y < 0f)
            verticalVelocity.y = -2f;

        verticalVelocity.y += gravity * Time.deltaTime;
        controller.Move(verticalVelocity * Time.deltaTime);
    }

    void UpdateAnimator()
    {
        if (animator == null || animator.runtimeAnimatorController == null) return;
        Vector3 flat = Velocity; flat.y = 0f;
        animator.SetFloat(SpeedHash, flat.magnitude, 0.1f, Time.deltaTime);
        animator.SetBool(GroundedHash, controller.isGrounded);
    }

    void OnDamaged(DamageInfo info)
    {
        if (info.knockback > 0f) AddKnockback(info.direction * info.knockback * 6f);
    }

    public void AddKnockback(Vector3 velocity)
    {
        velocity.y = 0f;
        knockbackVelocity += velocity;
    }

    /// <summary>Instantly move the player (respawn at checkpoint, level start).</summary>
    public void Teleport(Vector3 position, Quaternion rotation)
    {
        controller.enabled = false;
        transform.SetPositionAndRotation(position, rotation);
        controller.enabled = true;
        verticalVelocity = Vector3.zero;
        knockbackVelocity = Vector3.zero;
    }
}
