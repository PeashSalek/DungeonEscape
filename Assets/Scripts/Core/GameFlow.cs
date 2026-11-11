using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Scene names and the order of the story run.</summary>
public static class GameFlow
{
    public const string MainMenu = "MainMenu";
    public const string Intro = "Intro";
    public const string Prison = "Level01_Prison";
    public const string Crypt = "Level02_Crypt";
    public const string KingsChamber = "Level03_KingsChamber";
    public const string Interlude = "Interlude";
    public const string Trial = "Trial";
    public const string Ending = "Ending";

    static readonly string[] storyLevels = { Prison, Crypt, KingsChamber };

    /// <summary>The level the Interlude screen will continue to.</summary>
    public static string NextLevel { get; private set; } = Prison;

    public static void NewGame()
    {
        SaveSystem.DeleteSave();
        if (BlessingSystem.Instance != null) BlessingSystem.Instance.ResetRun();
        if (RunStats.Instance != null) RunStats.Instance.ResetRun();
        Respawner.ClearCheckpoint();
        PlayerCarryOver.Clear();
        Load(Application.CanStreamedLevelBeLoaded(Intro) ? Intro : Prison);
    }

    public static void Continue()
    {
        var data = SaveSystem.Load();
        if (data == null) { NewGame(); return; }
        SaveSystem.CurrentRun = data;
        if (BlessingSystem.Instance != null)
        {
            BlessingSystem.Instance.ResetRun();
            BlessingSystem.Instance.active.AddRange(data.blessings);
        }
        if (RunStats.Instance != null) RunStats.Instance.LoadFrom(data);
        Respawner.ClearCheckpoint();
        Load(data.scene);
    }

    /// <summary>Called when a story level is finished: go to the shrine/merchant interlude.</summary>
    public static void CompleteLevel(PlayerStats stats)
    {
        string current = SceneManager.GetActiveScene().name;
        int i = System.Array.IndexOf(storyLevels, current);
        PlayerCarryOver.Store(stats);
        Respawner.ClearCheckpoint();
        if (i < 0 || i >= storyLevels.Length - 1)
        {
            Load(Ending);
            return;
        }
        NextLevel = storyLevels[i + 1];
        SaveSystem.CurrentRun = null;
        Load(Application.CanStreamedLevelBeLoaded(Interlude) ? Interlude : NextLevel);
    }

    public static void ContinueFromInterlude() => Load(NextLevel);

    public static void StartTrial()
    {
        if (BlessingSystem.Instance != null) BlessingSystem.Instance.ResetRun();
        PlayerCarryOver.Clear();
        Respawner.ClearCheckpoint();
        Load(Trial);
    }

    public static void ToMainMenu() => Load(MainMenu);

    public static void Load(string scene)
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;
        if (ScreenFader.Instance != null) ScreenFader.Instance.FadeAndLoad(scene);
        else SceneManager.LoadScene(scene);
    }
}

/// <summary>Carries the player's inventory from one level to the next.</summary>
public static class PlayerCarryOver
{
    static bool has;
    static int coins, potions, maxPotions, lockpicks;
    static float health, maxHealth;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    public static void Clear() { has = false; }

    public static void Store(PlayerStats s)
    {
        has = true;
        coins = s.coins; potions = s.potions; maxPotions = s.maxPotions; lockpicks = s.lockpicks;
        health = s.Health.CurrentHealth; maxHealth = s.Health.maxHealth;
    }

    /// <summary>Apply to a freshly loaded player. Returns false when there is nothing to restore.</summary>
    public static bool Restore(PlayerStats s)
    {
        if (!has) return false;
        s.coins = coins; s.maxPotions = maxPotions; s.potions = potions; s.lockpicks = lockpicks;
        s.NotifyChanged();
        return true;
    }

    /// <summary>Spend coins between levels (merchant) before the next level loads.</summary>
    public static int Coins { get => coins; set => coins = value; }
    public static int Potions { get => potions; set => potions = value; }
    public static int MaxPotions { get => maxPotions; set => maxPotions = value; }
    public static int Lockpicks { get => lockpicks; set => lockpicks = value; }
    public static bool HasData => has;
}
