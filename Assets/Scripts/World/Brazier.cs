using UnityEngine;

/// <summary>
/// Checkpoint brazier. Lighting it heals the player, refills a potion,
/// sets the respawn point and saves the run.
/// </summary>
public class Brazier : MonoBehaviour, IInteractable
{
    public string checkpointId = "checkpoint";
    public Light fireLight;
    public GameObject fireEffect;
    public Transform respawnPoint;

    public bool IsLit { get; private set; }

    void Start()
    {
        SetVisual(false);
        if (SaveSystem.CurrentRun != null && SaveSystem.CurrentRun.checkpointId == checkpointId)
            SetVisual(true);
    }

    public string Prompt(PlayerInteractor p) => IsLit ? "Rest at brazier" : "Light the brazier";
    public bool CanInteract(PlayerInteractor p) => true;

    public void Interact(PlayerInteractor p)
    {
        bool first = !IsLit;
        AudioManager.Play("magic", transform.position);
        SetVisual(true);
        var stats = p.Stats;
        stats.Health.Heal(stats.Health.maxHealth);
        if (first) stats.AddPotion(1);
        var pos = respawnPoint != null ? respawnPoint : transform;
        Respawner.SetCheckpoint(pos.position + pos.forward * 1.2f, pos.rotation);
        SaveSystem.SaveCheckpoint(checkpointId, stats);
        GameEvents.Notify(first ? "Brazier lit - progress saved" : "Rested - progress saved");
    }

    void SetVisual(bool lit)
    {
        IsLit = lit;
        if (fireLight != null) fireLight.enabled = lit;
        if (fireEffect != null) fireEffect.SetActive(lit);
    }
}
