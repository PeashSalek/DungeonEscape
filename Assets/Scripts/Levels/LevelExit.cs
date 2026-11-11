using UnityEngine;

/// <summary>Walk into this trigger to finish the level (optionally only after a condition).</summary>
[RequireComponent(typeof(BoxCollider))]
public class LevelExit : MonoBehaviour
{
    public string requiredKeyId = "";
    public string lockedMessage = "The way is sealed.";
    bool used;
    float nextMsg;

    void Awake() { GetComponent<BoxCollider>().isTrigger = true; }

    void OnTriggerEnter(Collider other)
    {
        var p = other.GetComponentInParent<PlayerStats>();
        if (p == null || used || p.Health.IsDead) return;
        if (!string.IsNullOrEmpty(requiredKeyId) && !p.HasKey(requiredKeyId))
        {
            if (Time.time > nextMsg) { GameEvents.Notify(lockedMessage); nextMsg = Time.time + 3f; }
            return;
        }
        used = true;
        GameFlow.CompleteLevel(p);
    }
}
