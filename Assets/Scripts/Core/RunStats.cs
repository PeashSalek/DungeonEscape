using UnityEngine;

/// <summary>Statistics for the current run (shown in the ending summary and saved).</summary>
public class RunStats : MonoBehaviour
{
    public static RunStats Instance { get; private set; }

    public int kills;
    public int deaths;
    public int coinsCollected;
    public float playTime;
    public bool timing = true;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void OnEnable() { GameEvents.EnemyKilled += OnKill; }
    void OnDisable() { GameEvents.EnemyKilled -= OnKill; }

    void OnKill(EnemyAI e) => kills++;

    void Update()
    {
        if (timing && Time.timeScale > 0f) playTime += Time.deltaTime;
    }

    public void ResetRun()
    {
        kills = deaths = coinsCollected = 0;
        playTime = 0f;
        timing = true;
    }

    public void LoadFrom(RunData d)
    {
        if (d == null) return;
        kills = d.kills; deaths = d.deaths; playTime = d.playTime;
    }

    public static string FormatTime(float seconds)
    {
        int m = Mathf.FloorToInt(seconds / 60f), s = Mathf.FloorToInt(seconds % 60f);
        return m + ":" + s.ToString("00");
    }
}
