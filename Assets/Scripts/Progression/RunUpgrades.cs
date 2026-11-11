using UnityEngine;

/// <summary>Permanent upgrades bought from the Ghost Merchant during the current run.</summary>
public static class RunUpgrades
{
    public static int swordLevel, armorLevel, vitalityLevel;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    public static void Reset() { swordLevel = armorLevel = vitalityLevel = 0; }

    public static void Apply(PlayerStats s)
    {
        s.damageMultiplier *= 1f + 0.15f * swordLevel;
        s.Health.damageTakenMultiplier *= Mathf.Pow(0.9f, armorLevel);
        if (vitalityLevel > 0) s.Health.SetMaxHealth(s.Health.maxHealth + 20f * vitalityLevel, true);
    }
}
