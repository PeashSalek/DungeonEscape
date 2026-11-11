using System;
using System.Collections.Generic;
using UnityEngine;

public enum Team { Player, Enemy, Neutral }

public struct DamageInfo
{
    public float amount;
    public Vector3 point;
    public Vector3 direction;   // direction the hit travels (attacker -> target)
    public float knockback;     // metres of push-back
    public GameObject source;
    public bool heavy;          // heavy hits always stagger

    public DamageInfo(float amount, GameObject source, Vector3 direction, float knockback = 0f, bool heavy = false)
    {
        this.amount = amount;
        this.source = source;
        this.direction = direction;
        this.knockback = knockback;
        this.heavy = heavy;
        point = source != null ? source.transform.position : Vector3.zero;
    }
}

public interface IDamageable
{
    Team Team { get; }
    bool IsDead { get; }
    void TakeDamage(DamageInfo info);
}

/// <summary>Shared health component for the player, enemies and bosses.</summary>
public class Health : MonoBehaviour, IDamageable
{
    [SerializeField] Team team = Team.Enemy;
    public float maxHealth = 100f;
    [Tooltip("Multiplier applied to all incoming damage (curses and armor change this).")]
    public float damageTakenMultiplier = 1f;

    public float CurrentHealth { get; private set; }
    public Team Team => team;
    public bool IsDead { get; private set; }
    public float Normalized => maxHealth > 0 ? CurrentHealth / maxHealth : 0f;

    /// <summary>While Time.time is below this value, damage is ignored (dodge i-frames).</summary>
    [NonSerialized] public float invulnerableUntil;

    /// <summary>Optional filter; return true to cancel a hit (e.g. Ward power-up).</summary>
    public Func<DamageInfo, bool> DamageFilter;

    public event Action<DamageInfo> Damaged;
    public event Action<float, float> HealthChanged; // current, max
    public event Action Died;

    void Awake()
    {
        CurrentHealth = maxHealth;
    }

    public void SetTeam(Team t) => team = t;

    public void TakeDamage(DamageInfo info)
    {
        if (IsDead || Time.time < invulnerableUntil || info.amount <= 0f) return;
        if (DamageFilter != null && DamageFilter(info)) return;
        info.amount *= damageTakenMultiplier;
        CurrentHealth = Mathf.Max(0f, CurrentHealth - info.amount);
        Damaged?.Invoke(info);
        HealthChanged?.Invoke(CurrentHealth, maxHealth);
        if (CurrentHealth <= 0f) Die();
    }

    public void Heal(float amount)
    {
        if (IsDead || amount <= 0f) return;
        CurrentHealth = Mathf.Min(maxHealth, CurrentHealth + amount);
        HealthChanged?.Invoke(CurrentHealth, maxHealth);
    }

    public void SetMaxHealth(float newMax, bool fill)
    {
        maxHealth = Mathf.Max(1f, newMax);
        CurrentHealth = fill ? maxHealth : Mathf.Min(CurrentHealth, maxHealth);
        HealthChanged?.Invoke(CurrentHealth, maxHealth);
    }

    public void SetCurrent(float value)
    {
        CurrentHealth = Mathf.Clamp(value, 0f, maxHealth);
        HealthChanged?.Invoke(CurrentHealth, maxHealth);
    }

    public void Kill()
    {
        if (IsDead) return;
        CurrentHealth = 0f;
        HealthChanged?.Invoke(CurrentHealth, maxHealth);
        Die();
    }

    public void Revive(float fraction = 1f)
    {
        IsDead = false;
        CurrentHealth = maxHealth * Mathf.Clamp01(fraction);
        HealthChanged?.Invoke(CurrentHealth, maxHealth);
    }

    void Die()
    {
        if (IsDead) return;
        IsDead = true;
        Died?.Invoke();
    }
}

/// <summary>Helpers for melee sweeps used by both the player and enemies.</summary>
public static class Combat
{
    static readonly Collider[] buffer = new Collider[32];

    /// <summary>
    /// Damages every IDamageable of another team inside a sphere.
    /// alreadyHit stops one swing from hitting the same target twice.
    /// Returns the number of targets hit.
    /// </summary>
    public static int MeleeSweep(Vector3 center, float radius, Team attackerTeam, DamageInfo baseInfo,
        HashSet<IDamageable> alreadyHit, GameObject attacker)
    {
        int count = Physics.OverlapSphereNonAlloc(center, radius, buffer, ~0, QueryTriggerInteraction.Collide);
        int hits = 0;
        for (int i = 0; i < count; i++)
        {
            var col = buffer[i];
            if (attacker != null && col.transform.IsChildOf(attacker.transform)) continue;
            var target = col.GetComponentInParent<IDamageable>();
            if (target == null || target.IsDead) continue;
            if (target.Team == attackerTeam && attackerTeam != Team.Neutral) continue;
            if (alreadyHit != null && !alreadyHit.Add(target)) continue;

            var info = baseInfo;
            var targetPos = ((Component)target).transform.position;
            Vector3 dir = targetPos - (attacker != null ? attacker.transform.position : center);
            dir.y = 0f;
            info.direction = dir.sqrMagnitude > 0.001f ? dir.normalized : baseInfo.direction;
            info.point = col.ClosestPoint(center);
            target.TakeDamage(info);
            hits++;
        }
        return hits;
    }
}
