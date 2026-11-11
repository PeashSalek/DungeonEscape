using UnityEngine;

/// <summary>
/// Mouse-orbit third-person camera (extended from the original prototype):
/// wall collision so it never clips into dungeon walls, smooth lock-on framing,
/// and screen shake.
/// </summary>
public class ThirdPersonCamera : MonoBehaviour
{
    [Header("Target")]
    public Transform target;

    [Header("Camera Settings")]
    public float distance = 6f;
    public float height = 2f;
    public float mouseSensitivity = 0.15f;

    [Header("Vertical Rotation")]
    public float minVerticalAngle = -20f;
    public float maxVerticalAngle = 60f;

    [Header("Collision")]
    public float collisionRadius = 0.25f;
    public LayerMask collisionMask = ~0;

    private float yaw = 0f;
    private float pitch = 20f;
    float currentDistance;
    float shakeAmount, shakeUntil;
    PlayerMovement playerMovement;

    public static ThirdPersonCamera Instance { get; private set; }

    void Awake()
    {
        Instance = this;
        currentDistance = distance;
        GameInput.Init();
    }

    void Start()
    {
        SetCursorLocked(true);
        if (target != null)
        {
            yaw = target.eulerAngles.y;
            playerMovement = target.GetComponent<PlayerMovement>();
        }
    }

    public static void SetCursorLocked(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }

    void LateUpdate()
    {
        if (target == null)
            return;

        if (Time.timeScale > 0f && Cursor.lockState == CursorLockMode.Locked)
            HandleCameraRotation();
        UpdateCameraPosition();
    }

    void HandleCameraRotation()
    {
        Vector2 mouseDelta = GameInput.Look.ReadValue<Vector2>();
        float sens = mouseSensitivity * PlayerPrefs.GetFloat("mouse_sensitivity", 1f);
        yaw += mouseDelta.x * sens;
        pitch -= mouseDelta.y * sens;
        pitch = Mathf.Clamp(pitch, minVerticalAngle, maxVerticalAngle);

        // While locked on, gently steer the camera behind the player toward the enemy.
        if (playerMovement != null && playerMovement.LockTarget != null)
        {
            Vector3 to = playerMovement.LockTarget.position - target.position;
            to.y = 0f;
            if (to.sqrMagnitude > 0.01f)
            {
                float wantedYaw = Quaternion.LookRotation(to).eulerAngles.y;
                yaw = Mathf.LerpAngle(yaw, wantedYaw, 6f * Time.unscaledDeltaTime);
                pitch = Mathf.Lerp(pitch, 15f, 3f * Time.unscaledDeltaTime);
            }
        }
    }

    void UpdateCameraPosition()
    {
        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 targetPoint = target.position + Vector3.up * height;
        Vector3 back = rotation * Vector3.back;

        float wanted = distance;
        if (Physics.SphereCast(targetPoint, collisionRadius, back, out RaycastHit hit, distance,
                collisionMask, QueryTriggerInteraction.Ignore))
        {
            if (!hit.transform.IsChildOf(target))
                wanted = Mathf.Max(0.6f, hit.distance - 0.1f);
        }
        // Snap in fast (avoid clipping), ease out slowly.
        currentDistance = wanted < currentDistance ? wanted : Mathf.Lerp(currentDistance, wanted, 5f * Time.unscaledDeltaTime);

        Vector3 cameraPosition = targetPoint + back * currentDistance;
        if (Time.unscaledTime < shakeUntil)
            cameraPosition += Random.insideUnitSphere * shakeAmount;

        transform.position = cameraPosition;
        transform.LookAt(targetPoint);
    }

    public void Shake(float amount, float duration)
    {
        shakeAmount = Mathf.Max(amount, Time.unscaledTime < shakeUntil ? shakeAmount : 0f);
        shakeUntil = Time.unscaledTime + duration;
    }
}
