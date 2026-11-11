using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Finite-state enemy AI shared by all regular enemies:
/// Idle -> Patrol -> Chase -> Attack -> Search -> Return, plus Stagger and Dead.
/// The attack style decides how it fights: melee (Skeleton Warrior, Brute),
/// ranged (Skeleton Archer) or caster (Dark Mage, keeps distance and teleports).
/// </summary>
[RequireComponent(typeof(NavMeshAgent), typeof(Health))]
public class EnemyAI : MonoBehaviour
{
    public enum State { Idle, Patrol, Chase, Attack, Search, Return, Stagger, Dead }
    public enum AttackStyle { Melee, Ranged, Caster }

    [Header("Identity")]
    public string displayName = "Skeleton Warrior";
    public AttackStyle style = AttackStyle.Melee;
    public int scoreValue = 100;

    [Header("Senses")]
    public float sightRange = 12f;
    [Range(10f, 360f)] public float fieldOfView = 140f;
    public float hearingRange = 3.5f;
    public float loseTargetTime = 4f;
    public LayerMask sightBlockers = ~0;

    [Header("Movement")]
    public float patrolSpeed = 1.6f;
    public float chaseSpeed = 3.6f;
    public Transform[] patrolPoints;
    public float idleTimeAtPoint = 2f;

    [Header("Attack")]
    public float attackRange = 1.8f;
    public float preferredDistance = 0f;      // ranged/caster keep this distance
    public float attackDamage = 12f;
    public float attackWindup = 0.55f;         // telegraph time the player can read
    public float attackRecovery = 0.6f;
    public float attackCooldown = 1.2f;
    public float attackRadius = 1.1f;
    public float attackKnockback = 0.5f;
    public bool heavyAttack = false;

    [Header("Ranged / Caster")]
    public Projectile projectilePrefab;
    public Transform firePoint;
    public float teleportDistance = 6f;       // caster: blink away when cornered
    public float teleportCooldown = 6f;

    [Header("Reactions")]
    [Tooltip("Hits needed in a short time to stagger. 0 = never staggers from light hits.")]
    public int poiseHits = 2;
    public float staggerTime = 0.5f;
    public float corpseLifetime = 6f;

    [Header("Loot")]
    public Vector2Int coinDrop = new Vector2Int(3, 8);
    [Range(0f, 1f)] public float potionDropChance = 0.08f;

    [Header("Sounds (Resources/Audio/SFX names)")]
    public string growlSound = "skeleton_growl";
    public string attackSound = "swing";
    public string deathSound = "skeleton_die";
    float nextGrowl;

    [Header("Optional visuals")]
    public Animator animator;
    public Renderer[] flashRenderers;

    public State CurrentState { get; private set; } = State.Idle;
    public Health Health { get; private set; }
    public NavMeshAgent Agent { get; private set; }
    public Transform Target { get; private set; }

    Vector3 homePosition;
    Quaternion homeRotation;
    Vector3 lastKnownPosition;
    float lastSeenTime;
    float nextAttackTime;
    float nextTeleportTime;
    float stateTimer;
    int patrolIndex;
    int recentHits;
    float recentHitTime;
    Coroutine attackRoutine;
    readonly HashSet<IDamageable> hitSet = new HashSet<IDamageable>();

    static readonly int SpeedHash = Animator.StringToHash("Speed");
    static readonly int AttackHash = Animator.StringToHash("Attack");
    static readonly int HitHash = Animator.StringToHash("Hit");
    static readonly int DieHash = Animator.StringToHash("Die");

    public event System.Action<EnemyAI> Died;

    protected virtual void Awake()
    {
        Agent = GetComponent<NavMeshAgent>();
        Health = GetComponent<Health>();
        Health.SetTeam(Team.Enemy);
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (flashRenderers == null || flashRenderers.Length == 0) flashRenderers = GetComponentsInChildren<Renderer>();
        homePosition = transform.position;
        homeRotation = transform.rotation;
    }

    protected virtual void OnEnable()
    {
        Health.Damaged += OnDamaged;
        Health.Died += OnDied;
    }

    protected virtual void OnDisable()
    {
        Health.Damaged -= OnDamaged;
        Health.Died -= OnDied;
    }

    protected virtual void Start()
    {
        FindPlayer();
        SetState(patrolPoints != null && patrolPoints.Length > 0 ? State.Patrol : State.Idle);
    }

    void FindPlayer()
    {
        var player = FindAnyObjectByType<PlayerStats>();
        if (player != null) Target = player.transform;
    }

    protected virtual void Update()
    {
        if (CurrentState == State.Dead) return;
        if (Target == null) FindPlayer();
        bool sees = CanSeeTarget();
        if (sees) { lastKnownPosition = Target.position; lastSeenTime = Time.time; }

        stateTimer += Time.deltaTime;
        switch (CurrentState)
        {
            case State.Idle:
                if (sees) SetState(State.Chase);
                else if (patrolPoints != null && patrolPoints.Length > 0 && stateTimer > idleTimeAtPoint) SetState(State.Patrol);
                break;

            case State.Patrol:
                if (sees) { SetState(State.Chase); break; }
                if (!Agent.pathPending && Agent.remainingDistance < 0.4f)
                {
                    patrolIndex = (patrolIndex + 1) % patrolPoints.Length;
                    SetState(State.Idle);
                }
                break;

            case State.Chase:
                UpdateChase(sees);
                break;

            case State.Search:
                if (sees) { SetState(State.Chase); break; }
                if (!Agent.pathPending && Agent.remainingDistance < 0.5f)
                {
                    // Look around for a moment, then give up.
                    transform.Rotate(0f, 90f * Time.deltaTime, 0f);
                    if (stateTimer > 3.5f) SetState(State.Return);
                }
                break;

            case State.Return:
                if (sees) { SetState(State.Chase); break; }
                if (!Agent.pathPending && Agent.remainingDistance < 0.4f)
                {
                    transform.rotation = homeRotation;
                    SetState(patrolPoints != null && patrolPoints.Length > 0 ? State.Patrol : State.Idle);
                }
                break;

            case State.Attack:
            case State.Stagger:
                FaceTarget(8f);
                break;
        }

        if (animator != null && animator.runtimeAnimatorController != null)
            animator.SetFloat(SpeedHash, Agent.velocity.magnitude, 0.1f, Time.deltaTime);
    }

    void UpdateChase(bool sees)
    {
        if (Target == null) { SetState(State.Return); return; }
        var targetHealth = Target.GetComponent<Health>();
        if (targetHealth != null && targetHealth.IsDead) { SetState(State.Return); return; }

        if (!sees && Time.time - lastSeenTime > loseTargetTime) { SetState(State.Search); return; }

        float dist = Vector3.Distance(transform.position, Target.position);

        if (style == AttackStyle.Melee)
        {
            Agent.SetDestination(sees ? Target.position : lastKnownPosition);
            if (sees && dist <= attackRange && Time.time >= nextAttackTime) BeginAttack();
            return;
        }

        // Ranged and casters try to hold a preferred distance.
        float keep = preferredDistance > 0f ? preferredDistance : attackRange * 0.75f;
        if (style == AttackStyle.Caster && dist < 3f && Time.time >= nextTeleportTime)
        {
            TeleportAway();
            return;
        }
        if (!sees || dist > attackRange) Agent.SetDestination(sees ? Target.position : lastKnownPosition);
        else if (dist < keep * 0.6f)
        {
            Vector3 away = (transform.position - Target.position).normalized;
            Vector3 dest = transform.position + away * 3f;
            if (NavMesh.SamplePosition(dest, out NavMeshHit hit, 2f, NavMesh.AllAreas)) Agent.SetDestination(hit.position);
        }
        else Agent.ResetPath();

        FaceTarget(6f);
        if (sees && dist <= attackRange && Time.time >= nextAttackTime) BeginAttack();
    }

    bool CanSeeTarget()
    {
        if (Target == null) return false;
        Vector3 eye = transform.position + Vector3.up * 1.5f;
        Vector3 targetPoint = Target.position + Vector3.up * 1.0f;
        Vector3 to = targetPoint - eye;
        float dist = to.magnitude;
        if (dist <= hearingRange) return true;
        if (dist > sightRange) return false;
        if (Vector3.Angle(transform.forward, to) > fieldOfView * 0.5f && CurrentState != State.Chase) return false;
        if (Physics.Raycast(eye, to / dist, out RaycastHit hit, dist, sightBlockers, QueryTriggerInteraction.Ignore))
            return hit.transform == Target || hit.transform.IsChildOf(Target);
        return true;
    }

    void SetState(State s)
    {
        CurrentState = s;
        stateTimer = 0f;
        if (!Agent.isOnNavMesh) return;
        switch (s)
        {
            case State.Idle:
                Agent.ResetPath();
                break;
            case State.Patrol:
                Agent.speed = patrolSpeed;
                Agent.SetDestination(patrolPoints[patrolIndex].position);
                break;
            case State.Chase:
                Agent.speed = chaseSpeed;
                if (Time.time > nextGrowl) { AudioManager.Play(growlSound, transform.position, 0.8f); nextGrowl = Time.time + 8f; }
                break;
            case State.Search:
                Agent.speed = patrolSpeed * 1.3f;
                Agent.SetDestination(lastKnownPosition);
                break;
            case State.Return:
                Agent.speed = patrolSpeed;
                Agent.SetDestination(homePosition);
                break;
            case State.Attack:
            case State.Stagger:
            case State.Dead:
                Agent.ResetPath();
                break;
        }
    }

    void FaceTarget(float speed)
    {
        if (Target == null) return;
        Vector3 to = Target.position - transform.position;
        to.y = 0f;
        if (to.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(to), speed * Time.deltaTime);
    }

    void BeginAttack()
    {
        SetState(State.Attack);
        attackRoutine = StartCoroutine(AttackRoutine());
    }

    protected virtual IEnumerator AttackRoutine()
    {
        if (animator != null && animator.runtimeAnimatorController != null) animator.SetTrigger(AttackHash);
        StartCoroutine(Telegraph(attackWindup));
        if (style != AttackStyle.Melee) AudioManager.Play(style == AttackStyle.Caster ? "mage_cast" : "swing", transform.position, 0.7f);
        yield return new WaitForSeconds(attackWindup);
        if (CurrentState != State.Attack) yield break;

        switch (style)
        {
            case AttackStyle.Melee:
                AudioManager.Play(attackSound, transform.position, 0.8f);
                hitSet.Clear();
                Vector3 center = transform.position + Vector3.up + transform.forward * attackRange * 0.6f;
                Combat.MeleeSweep(center, attackRadius, Team.Enemy,
                    new DamageInfo(attackDamage, gameObject, transform.forward, attackKnockback, heavyAttack), hitSet, gameObject);
                break;
            default:
                FireProjectile();
                break;
        }

        yield return new WaitForSeconds(attackRecovery);
        nextAttackTime = Time.time + attackCooldown * Random.Range(0.85f, 1.25f);
        attackRoutine = null;
        if (CurrentState == State.Attack) SetState(State.Chase);
    }

    protected void FireProjectile()
    {
        if (projectilePrefab == null || Target == null) return;
        Vector3 origin = firePoint != null ? firePoint.position : transform.position + Vector3.up * 1.4f + transform.forward * 0.6f;
        Vector3 aim = (Target.position + Vector3.up * 1.0f) - origin;
        var p = Instantiate(projectilePrefab, origin, Quaternion.LookRotation(aim));
        p.owner = gameObject;
        p.team = Team.Enemy;
        p.damage = attackDamage;
        if (style == AttackStyle.Caster) p.homingTarget = Target;
    }

    void TeleportAway()
    {
        nextTeleportTime = Time.time + teleportCooldown;
        for (int i = 0; i < 8; i++)
        {
            Vector2 r = Random.insideUnitCircle.normalized * teleportDistance;
            Vector3 candidate = Target.position + new Vector3(r.x, 0f, r.y);
            if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, 2f, NavMesh.AllAreas))
            {
                Agent.Warp(hit.position);
                FaceTarget(100f);
                return;
            }
        }
    }

    IEnumerator Telegraph(float duration)
    {
        // Flash red during wind-up so the player can read the attack.
        float end = Time.time + duration;
        var block = new MaterialPropertyBlock();
        while (Time.time < end && CurrentState == State.Attack)
        {
            float k = Mathf.PingPong(Time.time * 8f, 1f);
            SetEmission(block, new Color(1f, 0.15f, 0.05f) * k * 1.5f);
            yield return null;
        }
        SetEmission(block, Color.black);
    }

    void SetEmission(MaterialPropertyBlock block, Color c)
    {
        foreach (var r in flashRenderers)
        {
            if (r == null) continue;
            r.GetPropertyBlock(block);
            block.SetColor("_EmissionColor", c);
            r.SetPropertyBlock(block);
        }
    }

    void OnDamaged(DamageInfo info)
    {
        if (CurrentState == State.Dead) return;
        AudioManager.Play("hit", info.point, 0.9f);
        lastKnownPosition = Target != null ? Target.position : lastKnownPosition;
        lastSeenTime = Time.time;
        if (CurrentState == State.Idle || CurrentState == State.Patrol || CurrentState == State.Return || CurrentState == State.Search)
            SetState(State.Chase);

        if (Time.time - recentHitTime > 1.5f) recentHits = 0;
        recentHits++;
        recentHitTime = Time.time;

        bool stagger = info.heavy || (poiseHits > 0 && recentHits >= poiseHits);
        if (info.knockback > 0f && Agent.isOnNavMesh)
            Agent.Move(info.direction * info.knockback * (stagger ? 1f : 0.4f));
        if (stagger)
        {
            recentHits = 0;
            if (attackRoutine != null) { StopCoroutine(attackRoutine); attackRoutine = null; }
            StartCoroutine(StaggerRoutine());
        }
        if (animator != null && animator.runtimeAnimatorController != null) animator.SetTrigger(HitHash);
    }

    IEnumerator StaggerRoutine()
    {
        SetState(State.Stagger);
        yield return new WaitForSeconds(staggerTime);
        if (CurrentState == State.Stagger)
        {
            nextAttackTime = Mathf.Max(nextAttackTime, Time.time + 0.4f);
            SetState(State.Chase);
        }
    }

    protected virtual void OnDied()
    {
        StopAllCoroutines();
        SetState(State.Dead);
        Agent.enabled = false;
        foreach (var c in GetComponentsInChildren<Collider>()) c.enabled = false;
        if (animator != null && animator.runtimeAnimatorController != null) animator.SetTrigger(DieHash);
        else StartCoroutine(FallOver());

        DropLoot();
        Died?.Invoke(this);
        GameEvents.RaiseEnemyKilled(this);
        Destroy(gameObject, corpseLifetime);
    }

    IEnumerator FallOver()
    {
        // Placeholder death animation for models without clips: tip over and sink.
        Quaternion start = transform.rotation;
        Quaternion end = start * Quaternion.Euler(-90f, 0f, 0f);
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime * 2.5f;
            transform.rotation = Quaternion.Slerp(start, end, Mathf.SmoothStep(0, 1, t));
            yield return null;
        }
        yield return new WaitForSeconds(corpseLifetime - 2f);
        float sink = 0f;
        while (sink < 1.5f) { sink += Time.deltaTime; transform.position += Vector3.down * Time.deltaTime; yield return null; }
    }

    void DropLoot()
    {
        int coins = Mathf.RoundToInt(Random.Range(coinDrop.x, coinDrop.y + 1) * BlessingSystem.coinMultiplier);
        if (coins > 0) Pickup.SpawnCoins(transform.position + Vector3.up * 0.5f, coins);
        if (Random.value < potionDropChance) Pickup.Spawn(PickupType.Potion, transform.position + Vector3.up * 0.5f, 1);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, sightRange);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}
