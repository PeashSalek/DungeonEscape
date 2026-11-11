using System.Collections;
using UnityEngine;

/// <summary>Timed power-ups: Rage (double damage) and Ward (absorbs the next hit).</summary>
[RequireComponent(typeof(PlayerStats))]
public class PowerUps : MonoBehaviour
{
    public float rageDuration = 10f;

    public float RageRemaining { get; private set; }
    public bool WardActive { get; private set; }

    PlayerStats stats;
    Health health;
    Coroutine rage;

    void Awake()
    {
        stats = GetComponent<PlayerStats>();
        health = GetComponent<Health>();
        health.DamageFilter = _ => TryAbsorb();
    }

    public void Activate(PickupType type)
    {
        if (type == PickupType.PowerUpRage)
        {
            if (rage != null) StopCoroutine(rage);
            else stats.damageMultiplier *= 2f;
            rage = StartCoroutine(Rage());
            GameEvents.Notify("RAGE! Double damage");
        }
        else if (type == PickupType.PowerUpWard && !WardActive)
        {
            WardActive = true;
            GameEvents.Notify("Ward: the next hit is blocked");
        }
    }

    IEnumerator Rage()
    {
        RageRemaining = rageDuration;
        while (RageRemaining > 0f)
        {
            RageRemaining -= Time.deltaTime;
            yield return null;
        }
        stats.damageMultiplier *= 0.5f;
        rage = null;
    }

    /// <summary>Called by Health before damage applies; returns true if the hit was absorbed.</summary>
    public bool TryAbsorb()
    {
        if (!WardActive) return false;
        WardActive = false;
        GameEvents.Notify("Ward shattered!");
        return true;
    }
}
