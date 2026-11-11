using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>Run save data, written to disk as JSON at checkpoints.</summary>
[Serializable]
public class RunData
{
    public string scene;
    public string checkpointId;
    public int coins, potions, maxPotions, lockpicks;
    public float healthFraction = 1f;
    public List<string> keys = new List<string>();
    public List<string> blessings = new List<string>();
    public int kills, deaths;
    public float playTime;
    public string savedAt;
}

/// <summary>Save/load for the story run, plus the local high-score table.</summary>
public static class SaveSystem
{
    static string SavePath => Path.Combine(Application.persistentDataPath, "dungeon_escape_save.json");

    /// <summary>The run loaded with Continue (null on a new game).</summary>
    public static RunData CurrentRun { get; set; }

    public static bool HasSave => File.Exists(SavePath);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { CurrentRun = null; }

    public static void SaveCheckpoint(string checkpointId, PlayerStats stats)
    {
        var data = new RunData
        {
            scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,
            checkpointId = checkpointId,
            coins = stats.coins,
            potions = stats.potions,
            maxPotions = stats.maxPotions,
            lockpicks = stats.lockpicks,
            healthFraction = 1f,
            keys = new List<string>(stats.Keys),
            blessings = BlessingSystem.Instance != null ? new List<string>(BlessingSystem.Instance.active) : new List<string>(),
            savedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
        };
        if (RunStats.Instance != null)
        {
            data.kills = RunStats.Instance.kills;
            data.deaths = RunStats.Instance.deaths;
            data.playTime = RunStats.Instance.playTime;
        }
        Write(data);
        CurrentRun = data;
    }

    public static void Write(RunData data)
    {
        try { File.WriteAllText(SavePath, JsonUtility.ToJson(data, true)); }
        catch (Exception e) { Debug.LogWarning("Save failed: " + e.Message); }
    }

    public static RunData Load()
    {
        if (!HasSave) return null;
        try { return JsonUtility.FromJson<RunData>(File.ReadAllText(SavePath)); }
        catch (Exception e) { Debug.LogWarning("Load failed: " + e.Message); return null; }
    }

    public static void DeleteSave()
    {
        if (HasSave) File.Delete(SavePath);
        CurrentRun = null;
    }

    /// <summary>Restore inventory from a save onto the freshly spawned player.</summary>
    public static void ApplyToPlayer(RunData data, PlayerStats stats)
    {
        if (data == null || stats == null) return;
        stats.coins = data.coins;
        stats.maxPotions = Mathf.Max(stats.maxPotions, data.maxPotions);
        stats.potions = data.potions;
        stats.lockpicks = data.lockpicks;
        foreach (var k in data.keys) stats.AddKey(k);
        stats.NotifyChanged();
    }

    // ---------------- High scores ----------------

    [Serializable]
    public class ScoreEntry { public string name; public int score; public int waves; public string date; }
    [Serializable]
    class ScoreTable { public List<ScoreEntry> entries = new List<ScoreEntry>(); }

    static string ScorePath => Path.Combine(Application.persistentDataPath, "dungeon_escape_scores.json");

    public static List<ScoreEntry> LoadScores()
    {
        try
        {
            if (File.Exists(ScorePath))
                return JsonUtility.FromJson<ScoreTable>(File.ReadAllText(ScorePath)).entries;
        }
        catch (Exception e) { Debug.LogWarning("Score load failed: " + e.Message); }
        return new List<ScoreEntry>();
    }

    /// <summary>Adds a score and returns its rank (1-based), or 0 if it didn't make the top 10.</summary>
    public static int AddScore(string name, int score, int waves)
    {
        var list = LoadScores();
        var entry = new ScoreEntry { name = name, score = score, waves = waves, date = DateTime.Now.ToString("yyyy-MM-dd") };
        list.Add(entry);
        list.Sort((a, b) => b.score.CompareTo(a.score));
        if (list.Count > 10) list.RemoveRange(10, list.Count - 10);
        try { File.WriteAllText(ScorePath, JsonUtility.ToJson(new ScoreTable { entries = list }, true)); }
        catch (Exception e) { Debug.LogWarning("Score save failed: " + e.Message); }
        int rank = list.IndexOf(entry);
        return rank >= 0 ? rank + 1 : 0;
    }
}
