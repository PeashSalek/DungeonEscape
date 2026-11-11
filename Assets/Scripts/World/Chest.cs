using System.Collections;
using UnityEngine;

/// <summary>
/// Treasure chest. Locked chests start the lock-picking minigame;
/// opening spills coins and can drop a potion, lockpick or power-up.
/// </summary>
public class Chest : MonoBehaviour, IInteractable
{
    public bool locked = false;
    [Range(1, 5)] public int lockDifficulty = 2;
    public Vector2Int coins = new Vector2Int(15, 30);
    [Range(0f, 1f)] public float potionChance = 0.35f;
    [Range(0f, 1f)] public float lockpickChance = 0.25f;
    [Range(0f, 1f)] public float powerUpChance = 0.2f;
    [Tooltip("Optional guaranteed key (e.g. the key to the next door).")]
    public string guaranteedKeyId = "";
    public string guaranteedKeyName = "";
    public Transform lid;
    public AudioClip openSound;

    public bool IsOpen { get; private set; }

    public string Prompt(PlayerInteractor p)
    {
        if (IsOpen) return "";
        if (!locked) return "Open chest";
        return p.Stats.lockpicks > 0 ? "Pick the lock (" + p.Stats.lockpicks + " lockpicks)" : "Locked - you need a lockpick";
    }

    public bool CanInteract(PlayerInteractor p) => !IsOpen && (!locked || p.Stats.lockpicks > 0);

    public void Interact(PlayerInteractor p)
    {
        if (!CanInteract(p)) return;
        if (!locked) { Open(); return; }

        if (LockpickMinigame.Instance != null)
            LockpickMinigame.Instance.Begin(lockDifficulty, p.Stats, success => { if (success) { locked = false; Open(); } });
        else if (p.Stats.UseLockpick())
        {
            locked = false;
            Open();
        }
    }

    public void Open()
    {
        if (IsOpen) return;
        IsOpen = true;
        if (openSound != null) AudioSource.PlayClipAtPoint(openSound, transform.position);
        StartCoroutine(OpenRoutine());
    }

    IEnumerator OpenRoutine()
    {
        if (lid != null)
        {
            Quaternion a = lid.localRotation, b = a * Quaternion.Euler(-110f, 0f, 0f);
            for (float t = 0; t < 1f; t += Time.deltaTime * 2.5f)
            {
                lid.localRotation = Quaternion.Slerp(a, b, Mathf.SmoothStep(0, 1, t));
                yield return null;
            }
            lid.localRotation = b;
        }

        Vector3 spill = transform.position + Vector3.up * 0.8f + transform.forward * 0.3f;
        Pickup.SpawnCoins(spill, Mathf.Max(1, Mathf.RoundToInt(Random.Range(coins.x, coins.y + 1) * BlessingSystem.chestCoinMultiplier)));
        if (Random.value < potionChance) Launch(Pickup.Spawn(PickupType.Potion, spill, 1));
        if (Random.value < lockpickChance) Launch(Pickup.Spawn(PickupType.Lockpick, spill, 1));
        if (Random.value < powerUpChance)
            Launch(Pickup.Spawn(Random.value < 0.5f ? PickupType.PowerUpRage : PickupType.PowerUpWard, spill, 1));
        if (!string.IsNullOrEmpty(guaranteedKeyId))
        {
            var k = Pickup.Spawn(PickupType.Key, spill, 1);
            k.keyId = guaranteedKeyId;
            k.displayName = guaranteedKeyName;
            Launch(k);
        }
    }

    void Launch(Pickup p)
    {
        if (p != null) p.Launch(transform.forward * 2f + new Vector3(Random.Range(-1f, 1f), 4f, 0f));
    }
}
