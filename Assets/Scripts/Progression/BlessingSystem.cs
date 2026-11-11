using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Curse-shrine blessings: each one gives a bonus and carries a small curse.
/// Three random blessings are offered at every shrine, so each run plays differently.
/// Active blessings are re-applied to the player when a level loads.
/// </summary>
public class BlessingSystem : MonoBehaviour
{
    public static BlessingSystem Instance { get; private set; }

    public class Blessing
    {
        public string id, title, boon, curse;
        public Action<PlayerStats> apply;
    }

    public static readonly List<Blessing> All = new List<Blessing>
    {
        new Blessing { id = "bloodthirst", title = "Bloodthirst", boon = "Your hits heal you for 8% of the damage dealt.", curse = "Potions heal 40% less.",
            apply = s => { s.lifeSteal += 0.08f; s.potionMultiplier *= 0.6f; } },
        new Blessing { id = "fleetfoot", title = "Fleet Foot", boon = "Dodging costs no stamina and you move 10% faster.", curse = "You take 15% more damage.",
            apply = s => { var m = s.GetComponent<PlayerMovement>(); m.freeDodge = true; m.speedMultiplier *= 1.1f; s.Health.damageTakenMultiplier *= 1.15f; } },
        new Blessing { id = "giantsblood", title = "Giant's Blood", boon = "+40 maximum health.", curse = "Stamina regenerates 25% slower.",
            apply = s => { s.Health.SetMaxHealth(s.Health.maxHealth + 40f, false); s.Health.Heal(40f); s.staminaRegenPerSecond *= 0.75f; } },
        new Blessing { id = "executioner", title = "Executioner", boon = "Heavy attacks deal 60% more damage.", curse = "Light attacks deal 15% less damage.",
            apply = s => { s.heavyDamage *= 1.6f; s.lightDamage *= 0.85f; } },
        new Blessing { id = "greed", title = "Greed of the King", boon = "Enemies drop twice as many coins.", curse = "You start each area with 20% less health.",
            apply = s => { coinMultiplier *= 2f; s.Health.SetCurrent(s.Health.CurrentHealth * 0.8f); } },
        new Blessing { id = "ironskin", title = "Ironskin", boon = "You take 20% less damage.", curse = "You move 10% slower.",
            apply = s => { s.Health.damageTakenMultiplier *= 0.8f; s.GetComponent<PlayerMovement>().speedMultiplier *= 0.9f; } },
        new Blessing { id = "fury", title = "Fury", boon = "All attacks deal 20% more damage.", curse = "Attacks cost 30% more stamina.",
            apply = s => { s.damageMultiplier *= 1.2f; s.lightAttackCost *= 1.3f; s.heavyAttackCost *= 1.3f; } },
        new Blessing { id = "secondwind", title = "Second Wind", boon = "+1 potion slot and a free potion.", curse = "Maximum health -15.",
            apply = s => { s.maxPotions += 1; s.AddPotion(1); s.Health.SetMaxHealth(s.Health.maxHealth - 15f, false); } },
        new Blessing { id = "thief", title = "Thief's Fingers", boon = "+3 lockpicks and wider lock sweet spots.", curse = "Chests hold 30% fewer coins.",
            apply = s => { s.AddLockpicks(3); lockpickBonus = true; chestCoinMultiplier *= 0.7f; } },
        new Blessing { id = "endurance", title = "Endless Breath", boon = "+50 maximum stamina.", curse = "Sprinting drains stamina 30% faster.",
            apply = s => { s.maxStamina += 50f; s.sprintCostPerSecond *= 1.3f; } },
    };

    // Global modifiers read by other systems.
    public static float coinMultiplier = 1f;
    public static float chestCoinMultiplier = 1f;
    public static bool lockpickBonus;

    public readonly List<string> active = new List<string>();

    public string ActiveSummary => active.Count == 0 ? "" :
        string.Join("  |  ", active.Select(id => All.First(b => b.id == id).title));

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        coinMultiplier = 1f; chestCoinMultiplier = 1f; lockpickBonus = false;
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>Three random blessings the player doesn't already have.</summary>
    public List<Blessing> RollChoices(int count = 3)
    {
        var pool = All.Where(b => !active.Contains(b.id)).ToList();
        var result = new List<Blessing>();
        while (result.Count < count && pool.Count > 0)
        {
            int i = UnityEngine.Random.Range(0, pool.Count);
            result.Add(pool[i]);
            pool.RemoveAt(i);
        }
        return result;
    }

    public void Choose(Blessing b, PlayerStats stats)
    {
        if (b == null || active.Contains(b.id)) return;
        active.Add(b.id);
        b.apply(stats);
        stats.NotifyChanged();
        GameEvents.Notify("Blessing: " + b.title);
    }

    /// <summary>Re-apply all active blessings to a freshly spawned player (new level).</summary>
    public void ApplyAll(PlayerStats stats)
    {
        ResetStatics();
        foreach (var id in active)
        {
            var b = All.FirstOrDefault(x => x.id == id);
            b?.apply(stats);
        }
    }

    public void ResetRun()
    {
        active.Clear();
        ResetStatics();
    }
}
