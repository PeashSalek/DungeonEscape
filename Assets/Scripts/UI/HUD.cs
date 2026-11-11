using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// In-game HUD built from code: health and stamina bars, coins, potions,
/// lockpicks, keys, interaction prompt, notifications, lock-on target bar,
/// boss bar and a "You Died" banner.
/// </summary>
public class HUD : MonoBehaviour
{
    public static HUD Instance { get; private set; }

    PlayerStats stats;
    PlayerInteractor interactor;
    PlayerCombat combat;
    PowerUps powerUps;

    Image healthFill, healthLag, staminaFill, targetFill, bossFill;
    Text coinsText, potionsText, lockpickText, keysText, promptText, noticeText, targetName, bossName, buffText, deathText;
    GameObject targetRoot, bossRoot, deathRoot;
    Health boss;
    readonly Queue<string> notices = new Queue<string>();
    float noticeUntil;
    float shownCoins;

    void Awake()
    {
        Instance = this;
        Build();
    }

    void OnEnable() { GameEvents.Notification += Enqueue; }
    void OnDisable() { GameEvents.Notification -= Enqueue; }

    void Start()
    {
        stats = FindAnyObjectByType<PlayerStats>();
        if (stats != null)
        {
            interactor = stats.GetComponent<PlayerInteractor>();
            combat = stats.GetComponent<PlayerCombat>();
            shownCoins = stats.coins;
            stats.Health.Died += () => ShowDeath(true);
        }
    }

    void Build()
    {
        var canvas = UIFactory.CreateCanvas("HUD Canvas", 10);
        canvas.transform.SetParent(transform, false);
        var root = canvas.transform;
        Vector2 tl = new Vector2(0, 1), tr = new Vector2(1, 1), bc = new Vector2(0.5f, 0), tc = new Vector2(0.5f, 1), c = new Vector2(0.5f, 0.5f);

        // Health + stamina (top-left)
        var hpBg = UIFactory.Bar(root, "HealthBar", new Color(0.75f, 0.1f, 0.1f), tl, tl, new Vector2(40, -40), new Vector2(460, 30));
        healthFill = hpBg;
        healthLag = UIFactory.CreateImage(hpBg.transform.parent, "Lag", new Color(1f, 0.85f, 0.4f, 0.8f), Vector2.zero, Vector2.one, new Vector2(0, 0.5f), Vector2.zero, Vector2.zero);
        healthLag.rectTransform.offsetMin = new Vector2(3, 3); healthLag.rectTransform.offsetMax = new Vector2(-3, -3);
        healthLag.type = Image.Type.Filled; healthLag.fillMethod = Image.FillMethod.Horizontal; healthLag.sprite = UIFactory.WhiteSprite;
        healthLag.transform.SetSiblingIndex(0);
        staminaFill = UIFactory.Bar(root, "StaminaBar", new Color(0.3f, 0.75f, 0.3f), tl, tl, new Vector2(40, -78), new Vector2(340, 16));

        buffText = UIFactory.CreateText(root, "Buffs", "", 22, TextAnchor.UpperLeft, UIFactory.Gold, tl, tl, tl, new Vector2(40, -104), new Vector2(600, 60));

        // Inventory (top-right)
        coinsText = UIFactory.CreateText(root, "Coins", "0", 34, TextAnchor.UpperRight, UIFactory.Gold, tr, tr, tr, new Vector2(-40, -30), new Vector2(400, 44));
        potionsText = UIFactory.CreateText(root, "Potions", "", 26, TextAnchor.UpperRight, UIFactory.Parchment, tr, tr, tr, new Vector2(-40, -78), new Vector2(400, 34));
        lockpickText = UIFactory.CreateText(root, "Lockpicks", "", 26, TextAnchor.UpperRight, UIFactory.Parchment, tr, tr, tr, new Vector2(-40, -112), new Vector2(400, 34));
        keysText = UIFactory.CreateText(root, "Keys", "", 26, TextAnchor.UpperRight, UIFactory.Parchment, tr, tr, tr, new Vector2(-40, -146), new Vector2(400, 34));

        // Interaction prompt + notifications
        promptText = UIFactory.CreateText(root, "Prompt", "", 30, TextAnchor.MiddleCenter, UIFactory.Parchment, bc, bc, bc, new Vector2(0, 170), new Vector2(900, 50));
        noticeText = UIFactory.CreateText(root, "Notice", "", 34, TextAnchor.MiddleCenter, UIFactory.Gold, tc, tc, tc, new Vector2(0, -170), new Vector2(1200, 50));

        // Lock-on target
        targetRoot = new GameObject("Target", typeof(RectTransform));
        targetRoot.transform.SetParent(root, false);
        targetName = UIFactory.CreateText(targetRoot.transform, "Name", "", 24, TextAnchor.MiddleCenter, UIFactory.Parchment, tc, tc, tc, new Vector2(0, -60), new Vector2(500, 30));
        targetFill = UIFactory.Bar(targetRoot.transform, "Bar", new Color(0.7f, 0.15f, 0.1f), tc, tc, new Vector2(0, -92), new Vector2(360, 14));

        // Boss bar
        bossRoot = new GameObject("Boss", typeof(RectTransform));
        bossRoot.transform.SetParent(root, false);
        bossName = UIFactory.CreateText(bossRoot.transform, "Name", "", 34, TextAnchor.MiddleCenter, UIFactory.Gold, bc, bc, bc, new Vector2(0, 92), new Vector2(1000, 44));
        bossFill = UIFactory.Bar(bossRoot.transform, "Bar", new Color(0.55f, 0.08f, 0.35f), bc, bc, new Vector2(0, 60), new Vector2(1100, 22));
        bossRoot.SetActive(false);

        // Death banner
        deathRoot = new GameObject("Death", typeof(RectTransform));
        deathRoot.transform.SetParent(root, false);
        var dim = UIFactory.CreateImage(deathRoot.transform, "Dim", new Color(0, 0, 0, 0.55f), Vector2.zero, Vector2.one, c, Vector2.zero, Vector2.zero);
        dim.rectTransform.sizeDelta = Vector2.zero;
        deathText = UIFactory.CreateText(deathRoot.transform, "Text", "YOU DIED", 110, TextAnchor.MiddleCenter, new Color(0.7f, 0.08f, 0.08f), c, c, c, Vector2.zero, new Vector2(1600, 200));
        deathRoot.SetActive(false);
    }

    void Update()
    {
        if (stats == null) { stats = FindAnyObjectByType<PlayerStats>(); return; }
        if (powerUps == null) powerUps = stats.GetComponent<PowerUps>();

        var h = stats.Health;
        healthFill.fillAmount = h.Normalized;
        healthLag.fillAmount = Mathf.MoveTowards(healthLag.fillAmount, h.Normalized, 0.5f * Time.unscaledDeltaTime);
        if (healthLag.fillAmount < h.Normalized) healthLag.fillAmount = h.Normalized;
        staminaFill.fillAmount = stats.StaminaNormalized;

        shownCoins = Mathf.MoveTowards(shownCoins, stats.coins, Mathf.Max(20f, Mathf.Abs(stats.coins - shownCoins) * 4f) * Time.unscaledDeltaTime);
        coinsText.text = Mathf.RoundToInt(shownCoins) + "  coins";
        potionsText.text = "Potions  " + stats.potions + "/" + stats.maxPotions + "   [" + GameInput.DisplayName(GameInput.UsePotion) + "]";
        lockpickText.text = "Lockpicks  " + stats.lockpicks;
        keysText.text = stats.KeyCount > 0 ? "Keys  " + stats.KeyCount : "";

        string buffs = "";
        if (powerUps != null)
        {
            if (powerUps.RageRemaining > 0f) buffs += "RAGE " + Mathf.CeilToInt(powerUps.RageRemaining) + "s   ";
            if (powerUps.WardActive) buffs += "WARD";
        }
        if (BlessingSystem.Instance != null) buffs += (buffs.Length > 0 ? "\n" : "") + BlessingSystem.Instance.ActiveSummary;
        buffText.text = buffs;

        promptText.text = interactor != null && !string.IsNullOrEmpty(interactor.CurrentPrompt)
            ? (interactor.Current != null && interactor.Current.CanInteract(interactor) ? "[" + GameInput.DisplayName(GameInput.Interact) + "]  " : "") + interactor.CurrentPrompt
            : "";

        var target = combat != null ? combat.LockedTarget : null;
        targetRoot.SetActive(target != null && target != boss);
        if (target != null)
        {
            var ai = target.GetComponent<EnemyAI>();
            targetName.text = ai != null ? ai.displayName : target.name;
            targetFill.fillAmount = target.Normalized;
        }

        if (boss != null)
        {
            bossFill.fillAmount = boss.Normalized;
            if (boss.IsDead) bossRoot.SetActive(false);
        }

        if (Time.unscaledTime > noticeUntil)
        {
            if (notices.Count > 0) { noticeText.text = notices.Dequeue(); noticeUntil = Time.unscaledTime + 2.5f; }
            else noticeText.text = "";
        }
    }

    void Enqueue(string msg)
    {
        if (string.IsNullOrEmpty(msg)) return;
        if (notices.Count > 4) notices.Dequeue();
        notices.Enqueue(msg);
    }

    public void ShowBoss(Health bossHealth, string title)
    {
        boss = bossHealth;
        bossName.text = title;
        bossRoot.SetActive(bossHealth != null);
    }

    public void ShowDeath(bool show)
    {
        deathRoot.SetActive(show);
        deathText.text = "YOU DIED";
    }

    public void SetVisible(bool visible)
    {
        foreach (Transform child in transform) child.gameObject.SetActive(visible);
    }
}
