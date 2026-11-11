using UnityEngine;

/// <summary>Starts the boss fight when the player enters, seals the arena, and opens the exit on victory.</summary>
[RequireComponent(typeof(BoxCollider))]
public class BossArena : MonoBehaviour
{
    public BossAI boss;
    public Door[] sealDoors;      // close behind the player (start open)
    public Door exitDoor;         // opens on victory
    public bool completeLevelOnVictory = true;
    public float victoryDelay = 4f;
    bool started;

    void Awake() { GetComponent<BoxCollider>().isTrigger = true; }

    void Start()
    {
        if (boss != null) boss.Defeated += OnVictory;
    }

    void OnTriggerEnter(Collider other)
    {
        if (started || other.GetComponentInParent<PlayerStats>() == null || boss == null) return;
        started = true;
        foreach (var d in sealDoors) if (d != null) d.Close();
        boss.Activate();
    }

    void OnVictory()
    {
        GameEvents.Notify(boss.bossTitle.Split(',')[0] + " has fallen!");
        if (exitDoor != null) exitDoor.Open();
        foreach (var d in sealDoors) if (d != null) d.Open();
        if (completeLevelOnVictory) Invoke(nameof(Complete), victoryDelay);
    }

    void Complete()
    {
        var p = FindAnyObjectByType<PlayerStats>();
        if (p != null && !p.Health.IsDead) GameFlow.CompleteLevel(p);
    }
}
