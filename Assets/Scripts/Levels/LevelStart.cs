using UnityEngine;

/// <summary>
/// Runs when a level loads: restores the player's inventory (from the previous level
/// or a save), applies blessings and merchant upgrades, and moves the player to the
/// saved checkpoint when continuing.
/// </summary>
public class LevelStart : MonoBehaviour
{
    public string levelTitle = "The Prison";
    public string levelSubtitle = "";

    void Start()
    {
        var stats = FindAnyObjectByType<PlayerStats>();
        if (stats == null) return;

        if (BlessingSystem.Instance == null) new GameObject("BlessingSystem").AddComponent<BlessingSystem>();
        if (RunStats.Instance == null) new GameObject("RunStats").AddComponent<RunStats>();

        var save = SaveSystem.CurrentRun;
        if (save != null && save.scene == gameObject.scene.name)
        {
            SaveSystem.ApplyToPlayer(save, stats);
            foreach (var b in FindObjectsByType<Brazier>())
                if (b.checkpointId == save.checkpointId)
                {
                    var t = b.respawnPoint != null ? b.respawnPoint : b.transform;
                    var pos = t.position + t.forward * 1.2f;
                    stats.GetComponent<PlayerMovement>().Teleport(pos, t.rotation);
                    Respawner.SetCheckpoint(pos, t.rotation);
                }
        }
        else PlayerCarryOver.Restore(stats);

        RunUpgrades.Apply(stats);
        BlessingSystem.Instance.ApplyAll(stats);
        GameEvents.Notify(levelTitle);
        if (!string.IsNullOrEmpty(levelSubtitle)) GameEvents.Notify(levelSubtitle);
        ThirdPersonCamera.SetCursorLocked(true);
    }
}
