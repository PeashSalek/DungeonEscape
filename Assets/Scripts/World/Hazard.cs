using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Environmental hazards the player reacts to:
/// Spikes (damage on a timer), Blade (damage + knockback on contact),
/// PoisonWater (slows movement and drains health while inside).
/// Needs a trigger collider.
/// </summary>
public class Hazard : MonoBehaviour
{
    public enum Kind { Spikes, Blade, PoisonWater }
    public Kind kind = Kind.Spikes;
    public float damage = 15f;
    public float damageInterval = 1f;
    public float slowMultiplier = 0.5f;
    public float knockback = 1.2f;

    [Header("Spikes")]
    public Transform spikesVisual;
    public float cycle = 2.5f;          // seconds per up/down cycle
    public float activeFraction = 0.4f; // share of the cycle the spikes are up

    [Header("Blade")]
    public Transform bladePivot;
    public float swingAngle = 60f;
    public float swingSpeed = 1.6f;

    readonly Dictionary<Health, float> nextTick = new Dictionary<Health, float>();
    float phaseOffset;

    void Awake()
    {
        phaseOffset = Random.value * cycle;
        foreach (var c in GetComponents<Collider>()) c.isTrigger = true;
    }

    bool SpikesUp => ((Time.time + phaseOffset) % cycle) / cycle < activeFraction;

    void Update()
    {
        if (kind == Kind.Spikes && spikesVisual != null)
        {
            float targetY = SpikesUp ? 0f : -0.6f;
            var lp = spikesVisual.localPosition;
            lp.y = Mathf.MoveTowards(lp.y, targetY, 8f * Time.deltaTime);
            spikesVisual.localPosition = lp;
        }
        if (kind == Kind.Blade && bladePivot != null)
            bladePivot.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(Time.time * swingSpeed + phaseOffset) * swingAngle);
    }

    void OnTriggerStay(Collider other)
    {
        var health = other.GetComponentInParent<Health>();
        if (health == null || health.IsDead || health.Team != Team.Player) return;

        if (kind == Kind.PoisonWater)
        {
            var move = health.GetComponent<PlayerMovement>();
            if (move != null) move.environmentSpeedMultiplier = slowMultiplier;
        }
        if (kind == Kind.Spikes && !SpikesUp) return;

        nextTick.TryGetValue(health, out float next);
        if (Time.time < next) return;
        nextTick[health] = Time.time + damageInterval;

        Vector3 dir = health.transform.position - transform.position; dir.y = 0f;
        health.TakeDamage(new DamageInfo(damage, gameObject, dir.sqrMagnitude > 0.01f ? dir.normalized : Vector3.forward,
            kind == Kind.PoisonWater ? 0f : knockback));
    }

    void OnTriggerExit(Collider other)
    {
        if (kind != Kind.PoisonWater) return;
        var move = other.GetComponentInParent<PlayerMovement>();
        if (move != null) move.environmentSpeedMultiplier = 1f;
    }
}
