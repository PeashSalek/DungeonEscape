using UnityEngine;

/// <summary>Arrows and magic orbs. Optional homing for the Dark Mage's orbs.</summary>
public class Projectile : MonoBehaviour
{
    public float speed = 14f;
    public float damage = 12f;
    public float lifetime = 5f;
    public float knockback = 0.3f;
    public float homingStrength = 0f;   // degrees per second toward target
    public Team team = Team.Enemy;
    public GameObject owner;
    public Transform homingTarget;
    public GameObject impactEffect;

    float dieAt;

    void Start()
    {
        dieAt = Time.time + lifetime;
    }

    void Update()
    {
        if (Time.time > dieAt) { Destroy(gameObject); return; }

        if (homingTarget != null && homingStrength > 0f)
        {
            Vector3 to = (homingTarget.position + Vector3.up) - transform.position;
            Quaternion want = Quaternion.LookRotation(to);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, want, homingStrength * Time.deltaTime);
        }

        float step = speed * Time.deltaTime;
        if (Physics.SphereCast(transform.position, 0.15f, transform.forward, out RaycastHit hit, step, ~0, QueryTriggerInteraction.Ignore))
        {
            if (owner != null && hit.transform.IsChildOf(owner.transform))
            {
                transform.position += transform.forward * step;
                return;
            }
            var target = hit.collider.GetComponentInParent<IDamageable>();
            if (target != null && !target.IsDead && target.Team != team)
                target.TakeDamage(new DamageInfo(damage, owner != null ? owner : gameObject, transform.forward, knockback) { point = hit.point });
            Impact(hit.point);
            return;
        }
        transform.position += transform.forward * step;
    }

    void Impact(Vector3 point)
    {
        if (impactEffect != null) Destroy(Instantiate(impactEffect, point, Quaternion.identity), 2f);
        Destroy(gameObject);
    }
}
