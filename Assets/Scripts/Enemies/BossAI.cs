using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Multi-phase boss (the Forgotten King, and the Overlord in Trial of the King).
/// Phase 1: sword combos, a heavy slam and a charge.
/// Phase 2 (below 50% health): faster, adds magic orb volleys and summons minions
/// (the Chained Horror and skeletons) once.
/// Every attack is telegraphed with a red glow so the player can learn the patterns.
/// </summary>
[RequireComponent(typeof(NavMeshAgent), typeof(Health))]
public class BossAI : MonoBehaviour
{
    public string bossTitle = "Malgrath, the Forgotten King";
    public float moveSpeed = 3.2f;
    public float phase2SpeedMultiplier = 1.3f;
    public float meleeRange = 3f;
    public float comboDamage = 14f;
    public float slamDamage = 30f;
    public float slamRadius = 4.5f;
    public float chargeDamage = 22f;
    public float chargeSpeed = 14f;
    public float pauseBetweenAttacks = 1.1f;

    [Header("Phase 2")]
    public Projectile orbPrefab;
    public int orbsPerVolley = 5;
    public GameObject[] summonOnPhase2;
    public Transform[] summonPoints;
    public string roarSound = "king_roar";

    [Header("Visuals")]
    public Animator animator;
    public Renderer[] flashRenderers;
    public ParticleSystem slamEffect;

    public Health Health { get; private set; }
    public bool Active { get; private set; }
    public int Phase { get; private set; } = 1;
    public event System.Action Defeated;

    NavMeshAgent agent;
    Transform player;
    Health playerHealth;
    bool busy;
    readonly HashSet<IDamageable> hitSet = new HashSet<IDamageable>();
    readonly List<GameObject> summoned = new List<GameObject>();

    static readonly int SpeedHash = Animator.StringToHash("Speed");
    static readonly int AttackHash = Animator.StringToHash("Attack");
    static readonly int ComboHash = Animator.StringToHash("Combo");
    static readonly int DieHash = Animator.StringToHash("Die");
    static readonly int HitHash = Animator.StringToHash("Hit");

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        Health = GetComponent<Health>();
        Health.SetTeam(Team.Enemy);
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (flashRenderers == null || flashRenderers.Length == 0) flashRenderers = GetComponentsInChildren<Renderer>();
        agent.speed = moveSpeed;
    }

    void OnEnable() { Health.Died += OnDied; Health.Damaged += OnDamaged; }
    void OnDisable() { Health.Died -= OnDied; Health.Damaged -= OnDamaged; }

    public void Activate()
    {
        if (Active || Health.IsDead) return;
        var ps = FindAnyObjectByType<PlayerStats>();
        if (ps == null) return;
        player = ps.transform;
        playerHealth = ps.Health;
        Active = true;
        AudioManager.Play(roarSound, transform.position, 1f, 0f);
        AudioManager.PlayMusic("boss");
        if (HUD.Instance != null) HUD.Instance.ShowBoss(Health, bossTitle);
        StartCoroutine(Brain());
    }

    void Update()
    {
        if (animator != null && animator.runtimeAnimatorController != null)
            animator.SetFloat(SpeedHash, agent.enabled ? agent.velocity.magnitude : 0f, 0.1f, Time.deltaTime);
        if (Active && !busy && player != null) Face(player.position, 6f);
    }

    IEnumerator Brain()
    {
        yield return new WaitForSeconds(1.2f);
        while (Active && !Health.IsDead)
        {
            if (playerHealth == null || playerHealth.IsDead) { agent.ResetPath(); yield return new WaitForSeconds(0.5f); continue; }

            if (Phase == 1 && Health.Normalized <= 0.5f) yield return EnterPhase2();

            float dist = Vector3.Distance(transform.position, player.position);
            float roll = Random.value;
            if (dist > meleeRange * 2.2f)
            {
                if (Phase == 2 && roll < 0.45f) yield return OrbVolley();
                else if (roll < 0.75f) yield return Charge();
                else yield return Approach(2.5f);
            }
            else if (dist > meleeRange)
            {
                yield return Approach(1.5f);
            }
            else
            {
                if (roll < 0.55f) yield return Combo();
                else if (roll < 0.85f) yield return Slam();
                else if (Phase == 2) yield return OrbVolley();
                else yield return Combo();
            }
            yield return new WaitForSeconds(pauseBetweenAttacks / (Phase == 2 ? 1.4f : 1f));
        }
    }

    IEnumerator Approach(float maxTime)
    {
        float end = Time.time + maxTime;
        while (Time.time < end && player != null && Vector3.Distance(transform.position, player.position) > meleeRange * 0.9f)
        {
            if (agent.isOnNavMesh) agent.SetDestination(player.position);
            yield return null;
        }
        if (agent.isOnNavMesh) agent.ResetPath();
    }

    void Trigger(int combo)
    {
        if (animator == null || animator.runtimeAnimatorController == null) return;
        animator.SetInteger(ComboHash, combo);
        animator.SetTrigger(AttackHash);
    }

    IEnumerator Combo()
    {
        busy = true;
        Trigger(0);
        yield return Telegraph(0.45f);
        for (int i = 0; i < 3; i++)
        {
            Face(player.position, 100f);
            AudioManager.Play("swing", transform.position);
            Sweep(comboDamage, 2.2f, 0.6f, false);
            yield return new WaitForSeconds(0.38f);
        }
        busy = false;
    }

    IEnumerator Slam()
    {
        busy = true;
        Trigger(1);
        yield return Telegraph(0.9f);
        AudioManager.Play("clash", transform.position, 1f, 0f);
        if (ThirdPersonCamera.Instance != null) ThirdPersonCamera.Instance.Shake(0.4f, 0.4f);
        if (slamEffect != null) slamEffect.Play();
        hitSet.Clear();
        Combat.MeleeSweep(transform.position + Vector3.up * 0.5f, slamRadius, Team.Enemy,
            new DamageInfo(slamDamage, gameObject, transform.forward, 2f, true), hitSet, gameObject);
        yield return new WaitForSeconds(0.8f);
        busy = false;
    }

    IEnumerator Charge()
    {
        busy = true;
        Trigger(3);
        Face(player.position, 100f);
        yield return Telegraph(0.7f);
        Vector3 dir = player.position - transform.position; dir.y = 0f; dir.Normalize();
        hitSet.Clear();
        float t = 0f;
        while (t < 0.6f)
        {
            t += Time.deltaTime;
            if (agent.isOnNavMesh) agent.Move(dir * chargeSpeed * Time.deltaTime);
            Combat.MeleeSweep(transform.position + Vector3.up + dir, 1.4f, Team.Enemy,
                new DamageInfo(chargeDamage, gameObject, dir, 2.5f, true), hitSet, gameObject);
            yield return null;
        }
        yield return new WaitForSeconds(0.6f);
        busy = false;
    }

    IEnumerator OrbVolley()
    {
        if (orbPrefab == null) yield break;
        busy = true;
        Trigger(2);
        AudioManager.Play("mage_cast", transform.position);
        yield return Telegraph(0.8f);
        Vector3 origin = transform.position + Vector3.up * 2f + transform.forward;
        for (int i = 0; i < orbsPerVolley; i++)
        {
            float angle = (i - (orbsPerVolley - 1) * 0.5f) * 14f;
            Vector3 aim = Quaternion.Euler(0, angle, 0) * ((player.position + Vector3.up) - origin);
            var p = Instantiate(orbPrefab, origin, Quaternion.LookRotation(aim));
            p.owner = gameObject;
            p.team = Team.Enemy;
            p.damage = 12f;
            p.homingTarget = i == orbsPerVolley / 2 ? player : null;
        }
        yield return new WaitForSeconds(0.9f);
        busy = false;
    }

    IEnumerator EnterPhase2()
    {
        Phase = 2;
        busy = true;
        agent.speed = moveSpeed * phase2SpeedMultiplier;
        AudioManager.Play(roarSound, transform.position, 1f, 0f);
        GameEvents.Notify(bossTitle.Split(',')[0] + " calls upon the curse!");
        if (ThirdPersonCamera.Instance != null) ThirdPersonCamera.Instance.Shake(0.3f, 1.2f);
        yield return Telegraph(1.4f);
        for (int i = 0; i < summonOnPhase2.Length; i++)
        {
            if (summonOnPhase2[i] == null) continue;
            Vector3 pos = summonPoints != null && i < summonPoints.Length && summonPoints[i] != null
                ? summonPoints[i].position : transform.position + Random.insideUnitSphere.WithY(0) * 5f;
            if (NavMesh.SamplePosition(pos, out NavMeshHit hit, 3f, NavMesh.AllAreas)) pos = hit.position;
            var go = Instantiate(summonOnPhase2[i], pos, Quaternion.LookRotation(player.position - pos));
            go.SetActive(true);
            summoned.Add(go);
        }
        pauseBetweenAttacks *= 0.8f;
        busy = false;
    }

    void Sweep(float damage, float range, float knockback, bool heavy)
    {
        hitSet.Clear();
        Combat.MeleeSweep(transform.position + Vector3.up + transform.forward * range * 0.7f, range * 0.7f, Team.Enemy,
            new DamageInfo(damage, gameObject, transform.forward, knockback, heavy), hitSet, gameObject);
    }

    void Face(Vector3 target, float speed)
    {
        Vector3 to = target - transform.position; to.y = 0f;
        if (to.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(to), speed * Time.deltaTime);
    }

    IEnumerator Telegraph(float duration)
    {
        var block = new MaterialPropertyBlock();
        float end = Time.time + duration;
        while (Time.time < end)
        {
            float k = Mathf.PingPong(Time.time * 6f, 1f);
            foreach (var r in flashRenderers)
            {
                if (r == null) continue;
                r.GetPropertyBlock(block);
                block.SetColor("_EmissionColor", new Color(1f, 0.1f, 0.05f) * k * 2f);
                r.SetPropertyBlock(block);
            }
            if (player != null) Face(player.position, 3f);
            yield return null;
        }
        foreach (var r in flashRenderers)
        {
            if (r == null) continue;
            r.GetPropertyBlock(block);
            block.SetColor("_EmissionColor", Color.black);
            r.SetPropertyBlock(block);
        }
    }

    void OnDamaged(DamageInfo info)
    {
        AudioManager.Play("hit", info.point, 0.9f);
        if (!Active) Activate();
    }

    void OnDied()
    {
        Active = false;
        StopAllCoroutines();
        agent.enabled = false;
        foreach (var c in GetComponentsInChildren<Collider>()) c.enabled = false;
        if (animator != null && animator.runtimeAnimatorController != null) animator.SetTrigger(DieHash);
        AudioManager.Play("king_hurt", transform.position, 1f, 0f);
        foreach (var s in summoned)
            if (s != null) { var h = s.GetComponent<Health>(); if (h != null) h.Kill(); }
        Pickup.SpawnCoins(transform.position + Vector3.up, 120);
        if (HUD.Instance != null) HUD.Instance.ShowBoss(null, "");
        AudioManager.PlayMusic("ambience");
        Defeated?.Invoke();
    }
}

static class VectorExt
{
    public static Vector3 WithY(this Vector3 v, float y) { v.y = y; return v; }
}
