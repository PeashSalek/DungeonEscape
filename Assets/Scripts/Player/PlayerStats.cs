using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Stamina, inventory (coins, potions, keys, lockpicks) and combat stats of the player.
/// Blessings and merchant upgrades change the multipliers here.
/// </summary>
[RequireComponent(typeof(Health))]
public class PlayerStats : MonoBehaviour
{
    [Header("Stamina")]
    public float maxStamina = 100f;
    public float staminaRegenPerSecond = 28f;
    public float staminaRegenDelay = 0.8f;

    [Header("Costs")]
    public float sprintCostPerSecond = 14f;
    public float dodgeCost = 25f;
    public float lightAttackCost = 10f;
    public float heavyAttackCost = 25f;

    [Header("Combat")]
    public float lightDamage = 20f;
    public float heavyDamage = 45f;
    [Tooltip("Multiplies all damage dealt by the player.")]
    public float damageMultiplier = 1f;
    [Tooltip("Fraction of damage dealt that heals the player (Bloodthirst blessing).")]
    public float lifeSteal = 0f;

    [Header("Potions")]
    public int potions = 2;
    public int maxPotions = 5;
    public float potionHealFraction = 0.5f;
    [Tooltip("Multiplies potion healing (curses can lower it).")]
    public float potionMultiplier = 1f;

    [Header("Inventory")]
    public int coins;
    public int lockpicks = 1;
    readonly HashSet<string> keys = new HashSet<string>();

    public float Stamina { get; private set; }
    public float StaminaNormalized => maxStamina > 0 ? Stamina / maxStamina : 0f;
    public IEnumerable<string> Keys => keys;
    public int KeyCount => keys.Count;

    public Health Health { get; private set; }

    public event Action Changed;                 // inventory changed (for HUD)
    public event Action<int> CoinsGained;        // amount

    float lastStaminaUse;

    void Awake()
    {
        Health = GetComponent<Health>();
        Health.SetTeam(Team.Player);
        Stamina = maxStamina;
    }

    void Update()
    {
        if (Time.time - lastStaminaUse > staminaRegenDelay && Stamina < maxStamina)
            Stamina = Mathf.Min(maxStamina, Stamina + staminaRegenPerSecond * Time.deltaTime);
    }

    public bool HasStamina(float amount) => Stamina >= amount * 0.5f; // allow acting when nearly empty

    public bool TryUseStamina(float amount)
    {
        if (!HasStamina(amount)) return false;
        Stamina = Mathf.Max(0f, Stamina - amount);
        lastStaminaUse = Time.time;
        return true;
    }

    public void DrainStamina(float amount)
    {
        Stamina = Mathf.Max(0f, Stamina - amount);
        lastStaminaUse = Time.time;
    }

    public void AddCoins(int amount)
    {
        if (amount == 0) return;
        coins = Mathf.Max(0, coins + amount);
        if (amount > 0) CoinsGained?.Invoke(amount);
        Changed?.Invoke();
    }

    public bool SpendCoins(int amount)
    {
        if (coins < amount) return false;
        coins -= amount;
        Changed?.Invoke();
        return true;
    }

    public bool AddPotion(int amount = 1)
    {
        if (potions >= maxPotions) return false;
        potions = Mathf.Min(maxPotions, potions + amount);
        Changed?.Invoke();
        return true;
    }

    public bool UsePotion()
    {
        if (potions <= 0 || Health.IsDead || Health.CurrentHealth >= Health.maxHealth) return false;
        potions--;
        Health.Heal(Health.maxHealth * potionHealFraction * potionMultiplier);
        AudioManager.Play("bubble", transform.position, 0.8f);
        Changed?.Invoke();
        return true;
    }

    public void AddKey(string id)
    {
        if (string.IsNullOrEmpty(id)) return;
        keys.Add(id);
        Changed?.Invoke();
    }

    public bool HasKey(string id) => !string.IsNullOrEmpty(id) && keys.Contains(id);

    public void UseKey(string id)
    {
        if (keys.Remove(id)) Changed?.Invoke();
    }

    public void AddLockpicks(int amount)
    {
        lockpicks = Mathf.Max(0, lockpicks + amount);
        Changed?.Invoke();
    }

    public bool UseLockpick()
    {
        if (lockpicks <= 0) return false;
        lockpicks--;
        Changed?.Invoke();
        return true;
    }

    /// <summary>Called by the player's attacks so life steal can heal.</summary>
    public void OnDealtDamage(float amount)
    {
        if (lifeSteal > 0f) Health.Heal(amount * lifeSteal);
    }

    public void NotifyChanged() => Changed?.Invoke();
}
