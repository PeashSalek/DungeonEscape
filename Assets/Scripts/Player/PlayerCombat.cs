using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Sword combat: a 3-hit light combo, a heavy attack, potion use, lock-on,
/// hit reactions and death. Attacks are timed (wind-up, active, recovery) so
/// they work with or without animation clips.
/// </summary>
[RequireComponent(typeof(PlayerMovement), typeof(PlayerStats), typeof(Health))]
public class PlayerCombat : MonoBehaviour
{
    [System.Serializable]
    public class AttackData
    {
        public float windup = 0.12f;
        public float active = 0.14f;
        public float recovery = 0.22f;
        public float range = 1.6f;
        public float radius = 1.1f;
        public float knockback = 0.4f;
        public float forwardStep = 1.5f; // lunge speed during wind-up
    }

    public AttackData[] lightCombo =
    {
        new AttackData(),
        new AttackData { windup = 0.1f, active = 0.14f, recovery = 0.22f },
        new AttackData { windup = 0.18f, active = 0.16f, recovery = 0.35f, radius = 1.3f, knockback = 0.8f },
    };
    public AttackData heavy = new AttackData { windup = 0.45f, active = 0.18f, recovery = 0.45f, range = 1.8f, radius = 1.5f, knockback = 1.5f, forwardStep = 2.5f };

    [Tooltip("Time after an attack in which the next combo input is accepted.")]
    public float comboWindow = 0.45f;
    public float lockOnRange = 14f;
    public float hitStunTime = 0.35f;

    [Header("Optional visuals")]
    [Tooltip("Swung by code when there is no attack animation.")]
    public Transform weaponPivot;
    public Animator animator;

    public bool IsAttacking { get; private set; }
    public Health LockedTarget { get; private set; }

    PlayerMovement movement;
    PlayerStats stats;
    Health health;
    int comboIndex;
    float lastAttackEnd = -10f;
    bool queuedLight;
    bool currentIsHeavy;
    float stunnedUntil;
    Coroutine attackRoutine;
    readonly HashSet<IDamageable> hitThisSwing = new HashSet<IDamageable>();

    static readonly int AttackHash = Animator.StringToHash("Attack");
    static readonly int ComboHash = Animator.StringToHash("Combo");
    static readonly int HeavyHash = Animator.StringToHash("HeavyAttack");
    static readonly int HitHash = Animator.StringToHash("Hit");
    static readonly int DieHash = Animator.StringToHash("Die");

    public event System.Action<Health> HitEnemy;

    void Awake()
    {
        movement = GetComponent<PlayerMovement>();
        stats = GetComponent<PlayerStats>();
        health = GetComponent<Health>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
        GameInput.Init();
    }

    void OnEnable()
    {
        health.Damaged += OnDamaged;
        health.Died += OnDied;
    }

    void OnDisable()
    {
        health.Damaged -= OnDamaged;
        health.Died -= OnDied;
    }

    void Update()
    {
        if (health.IsDead || Time.timeScale == 0f) return;

        HandleLockOn();

        if (GameInput.UsePotion.WasPressedThisFrame() && !IsAttacking)
            stats.UsePotion();

        if (Time.time < stunnedUntil || movement.IsDodging) return;

        if (GameInput.LightAttack.WasPressedThisFrame())
        {
            if (IsAttacking) queuedLight = true;
            else StartLight();
        }
        else if (GameInput.HeavyAttack.WasPressedThisFrame() && !IsAttacking)
        {
            if (stats.TryUseStamina(stats.heavyAttackCost))
                attackRoutine = StartCoroutine(DoAttack(heavy, stats.heavyDamage, true, -1));
        }
    }

    void StartLight()
    {
        if (!stats.TryUseStamina(stats.lightAttackCost)) return;
        if (Time.time - lastAttackEnd > comboWindow) comboIndex = 0;
        var data = lightCombo[comboIndex];
        float damage = stats.lightDamage * (comboIndex == lightCombo.Length - 1 ? 1.4f : 1f);
        attackRoutine = StartCoroutine(DoAttack(data, damage, false, comboIndex));
        comboIndex = (comboIndex + 1) % lightCombo.Length;
    }

    IEnumerator DoAttack(AttackData data, float damage, bool isHeavy, int combo)
    {
        IsAttacking = true;
        currentIsHeavy = isHeavy;
        queuedLight = false;
        movement.MovementLocked = true;
        hitThisSwing.Clear();
        FaceAttackDirection();

        if (animator != null && animator.runtimeAnimatorController != null)
        {
            if (isHeavy) animator.SetTrigger(HeavyHash);
            else { animator.SetInteger(ComboHash, combo); animator.SetTrigger(AttackHash); }
        }

        float total = data.windup + data.active + data.recovery;
        float t = 0f;
        bool swung = false;
        while (t < total)
        {
            if (health.IsDead) break;
            float dt = Time.deltaTime;
            t += dt;

            // Small lunge forward during wind-up so attacks feel weighty.
            if (t < data.windup + data.active)
                GetComponent<CharacterController>().Move(transform.forward * data.forwardStep * dt);

            AnimateWeapon(t / total, isHeavy);

            if (t >= data.windup && t <= data.windup + data.active)
            {
                Vector3 center = transform.position + Vector3.up * 1f + transform.forward * data.range * 0.6f;
                var info = new DamageInfo(damage * stats.damageMultiplier, gameObject, transform.forward, data.knockback, isHeavy);
                int before = hitThisSwing.Count;
                Combat.MeleeSweep(center, data.radius, Team.Player, info, hitThisSwing, gameObject);
                if (hitThisSwing.Count > before)
                {
                    stats.OnDealtDamage(info.amount * (hitThisSwing.Count - before));
                    foreach (var h in hitThisSwing)
                        if (h is Health hh) HitEnemy?.Invoke(hh);
                    if (!swung) { swung = true; StartCoroutine(HitStop(isHeavy ? 0.08f : 0.04f)); }
                }
            }

            // Allow chaining the next light attack during recovery.
            if (queuedLight && t >= data.windup + data.active && !isHeavy && combo >= 0 && combo < lightCombo.Length - 1)
                break;

            yield return null;
        }

        AnimateWeapon(0f, isHeavy);
        movement.MovementLocked = false;
        IsAttacking = false;
        lastAttackEnd = Time.time;
        attackRoutine = null;

        if (queuedLight && !health.IsDead)
        {
            queuedLight = false;
            StartLight();
        }
    }

    void FaceAttackDirection()
    {
        Vector3 dir;
        if (LockedTarget != null) dir = LockedTarget.transform.position - transform.position;
        else
        {
            Vector2 input = GameInput.Move.ReadValue<Vector2>();
            var cam = Camera.main;
            if (input.sqrMagnitude > 0.01f && cam != null)
            {
                Vector3 f = cam.transform.forward; f.y = 0; f.Normalize();
                Vector3 r = cam.transform.right; r.y = 0; r.Normalize();
                dir = f * input.y + r * input.x;
            }
            else dir = transform.forward;
        }
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(dir);
    }

    void AnimateWeapon(float progress, bool isHeavy)
    {
        if (weaponPivot == null) return;
        if (animator != null && animator.runtimeAnimatorController != null) return;
        // Simple code-driven swing: from raised-right to lowered-left.
        float swing = Mathf.Sin(progress * Mathf.PI);
        float yaw = Mathf.Lerp(70f, -70f, progress);
        weaponPivot.localRotation = progress <= 0f
            ? Quaternion.identity
            : Quaternion.Euler(isHeavy ? Mathf.Lerp(-80f, 60f, progress) : 20f * swing, isHeavy ? 0f : yaw, 0f);
    }

    IEnumerator HitStop(float duration)
    {
        if (Time.timeScale == 0f) yield break;
        float old = Time.timeScale;
        Time.timeScale = 0.05f;
        yield return new WaitForSecondsRealtime(duration);
        if (Time.timeScale > 0f) Time.timeScale = old;
    }

    void HandleLockOn()
    {
        if (LockedTarget != null && (LockedTarget.IsDead ||
            Vector3.Distance(LockedTarget.transform.position, transform.position) > lockOnRange * 1.3f))
            SetLock(null);

        if (!GameInput.LockOn.WasPressedThisFrame()) return;
        if (LockedTarget != null) { SetLock(null); return; }

        Health best = null;
        float bestScore = float.MaxValue;
        var cam = Camera.main;
        foreach (var h in FindObjectsByType<Health>())
        {
            if (h.Team != Team.Enemy || h.IsDead) continue;
            Vector3 to = h.transform.position - transform.position;
            float dist = to.magnitude;
            if (dist > lockOnRange) continue;
            float angle = cam != null ? Vector3.Angle(cam.transform.forward, to) : 0f;
            float score = dist + angle * 0.1f;
            if (score < bestScore) { bestScore = score; best = h; }
        }
        SetLock(best);
    }

    void SetLock(Health target)
    {
        LockedTarget = target;
        movement.LockTarget = target != null ? target.transform : null;
    }

    void OnDamaged(DamageInfo info)
    {
        // Light hits during a heavy attack don't interrupt it (hyper armor).
        if (IsAttacking && currentIsHeavy && !info.heavy) return;
        if (attackRoutine != null) { StopCoroutine(attackRoutine); attackRoutine = null; }
        IsAttacking = false;
        movement.MovementLocked = false;
        AnimateWeapon(0f, false);
        stunnedUntil = Time.time + hitStunTime;
        if (animator != null && animator.runtimeAnimatorController != null) animator.SetTrigger(HitHash);
    }

    void OnDied()
    {
        if (attackRoutine != null) StopCoroutine(attackRoutine);
        IsAttacking = false;
        movement.MovementLocked = true;
        SetLock(null);
        if (animator != null && animator.runtimeAnimatorController != null) animator.SetTrigger(DieHash);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position + Vector3.up + transform.forward * lightCombo[0].range * 0.6f, lightCombo[0].radius);
    }
}
