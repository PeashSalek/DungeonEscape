using System.Collections;
using UnityEngine;

/// <summary>When the player dies, wait, then respawn at the last checkpoint with full health.</summary>
public class Respawner : MonoBehaviour
{
    public float delay = 3f;

    static Vector3 checkpointPos;
    static Quaternion checkpointRot;
    static bool hasCheckpoint;

    PlayerStats stats;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { hasCheckpoint = false; }

    public static void SetCheckpoint(Vector3 pos, Quaternion rot)
    {
        checkpointPos = pos;
        checkpointRot = rot;
        hasCheckpoint = true;
    }

    public static void ClearCheckpoint() => hasCheckpoint = false;

    void Start()
    {
        stats = FindAnyObjectByType<PlayerStats>();
        if (stats == null) return;
        if (!hasCheckpoint) SetCheckpoint(stats.transform.position, stats.transform.rotation);
        stats.Health.Died += OnDied;
    }

    void OnDestroy()
    {
        if (stats != null) stats.Health.Died -= OnDied;
    }

    void OnDied()
    {
        GameEvents.RaisePlayerDied();
        StartCoroutine(Respawn());
    }

    IEnumerator Respawn()
    {
        yield return new WaitForSeconds(delay);
        var move = stats.GetComponent<PlayerMovement>();
        move.Teleport(checkpointPos, checkpointRot);
        move.MovementLocked = false;
        stats.Health.Revive(1f);
        var anim = stats.GetComponentInChildren<Animator>();
        if (anim != null && anim.runtimeAnimatorController != null) { anim.Rebind(); anim.Update(0f); }
        if (HUD.Instance != null) HUD.Instance.ShowDeath(false);
        if (RunStats.Instance != null) RunStats.Instance.deaths++;
    }
}
