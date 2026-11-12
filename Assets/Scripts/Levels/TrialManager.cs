using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;

/// <summary>
/// Trial of the King: endless waves of enemies in the arena. Waves grow and mix
/// enemy types; every 3rd wave a curse shrine offers a blessing; every 10th wave
/// the Overlord appears. Score = kills, waves and coins. On death the player enters
/// a name for the local top-10 high-score table.
/// </summary>
public class TrialManager : MonoBehaviour
{
    public GameObject[] enemyPrefabs;          // warrior, archer, mage, brute
    public GameObject overlordPrefab;
    public Transform[] spawnPoints;
    public CurseShrine shrinePrefab;
    public Transform shrinePoint;
    public float timeBetweenWaves = 4f;

    public int Wave { get; private set; }
    public int Score => kills * 100 + Mathf.Max(0, Wave - 1) * 500 + (stats != null ? stats.coins : 0);

    int kills;
    readonly List<Health> alive = new List<Health>();
    PlayerStats stats;
    Text waveText, scoreText;
    GameObject gameOverRoot;
    InputField nameField;
    Text resultText;
    bool over;

    void Start()
    {
        stats = FindAnyObjectByType<PlayerStats>();
        GameEvents.EnemyKilled += OnKill;
        stats.Health.Died += OnPlayerDied;
        var respawner = FindAnyObjectByType<Respawner>();
        if (respawner != null) Destroy(respawner); // no respawns in the Trial
        BuildUI();
        StartCoroutine(Run());
    }

    void OnDestroy() { GameEvents.EnemyKilled -= OnKill; }

    void OnKill(EnemyAI e) { if (!over) kills++; }

    IEnumerator Run()
    {
        yield return new WaitForSeconds(2f);
        while (!over)
        {
            Wave++;
            waveText.text = "WAVE " + Wave;
            GameEvents.Notify(Wave % 10 == 0 ? "Wave " + Wave + " - THE OVERLORD APPROACHES" : "Wave " + Wave);
            AudioManager.PlayUI("king_roar", 0.4f);
            SpawnWave();
            while (!over && alive.Exists(h => h != null && !h.IsDead)) yield return new WaitForSeconds(0.5f);
            if (over) yield break;
            alive.Clear();
            stats.Health.Heal(stats.Health.maxHealth * 0.25f);
            if (Wave % 3 == 0 && shrinePrefab != null && shrinePoint != null)
            {
                Instantiate(shrinePrefab, shrinePoint.position, shrinePoint.rotation);
                GameEvents.Notify("A curse shrine rises... ");
            }
            if (Wave % 10 == 0) AudioManager.PlayMusic("explore");
            yield return new WaitForSeconds(timeBetweenWaves);
        }
    }

    void SpawnWave()
    {
        if (Wave % 10 == 0 && overlordPrefab != null)
        {
            var boss = Spawn(overlordPrefab, spawnPoints[0].position);
            var ai = boss.GetComponent<BossAI>();
            if (ai != null)
            {
                var h = boss.GetComponent<Health>();
                h.SetMaxHealth(h.maxHealth * (1f + 0.5f * (Wave / 10 - 1)), true);
                ai.Activate();
            }
            return;
        }
        int count = Mathf.RoundToInt(2 + Wave * 0.75f);
        for (int i = 0; i < count; i++)
        {
            int maxType = Mathf.Min(enemyPrefabs.Length, 1 + Wave / 2);
            var prefab = enemyPrefabs[Random.Range(0, maxType)];
            var p = spawnPoints[Random.Range(0, spawnPoints.Length)].position + Random.insideUnitSphere.WithY(0) * 2f;
            var go = Spawn(prefab, p);
            var h = go.GetComponent<Health>();
            h.SetMaxHealth(h.maxHealth * (1f + 0.08f * Wave), true);
            var ai = go.GetComponent<EnemyAI>();
            if (ai != null) { ai.attackDamage *= 1f + 0.05f * Wave; ai.sightRange = 60f; ai.hearingRange = 60f; }
        }
    }

    GameObject Spawn(GameObject prefab, Vector3 pos)
    {
        if (NavMesh.SamplePosition(pos, out NavMeshHit hit, 4f, NavMesh.AllAreas)) pos = hit.position;
        var go = Instantiate(prefab, pos, Quaternion.LookRotation((stats.transform.position - pos).WithY(0)));
        go.SetActive(true);
        alive.Add(go.GetComponent<Health>());
        return go;
    }

    void Update()
    {
        if (scoreText != null) scoreText.text = "Score  " + Score + "     Kills  " + kills;
    }

    void BuildUI()
    {
        var canvas = UIFactory.CreateCanvas("Trial Canvas", 15);
        canvas.transform.SetParent(transform, false);
        var tc = new Vector2(0.5f, 1);
        var c = new Vector2(0.5f, 0.5f);
        waveText = UIFactory.CreateText(canvas.transform, "Wave", "", 44, TextAnchor.MiddleCenter, UIFactory.Gold, tc, tc, tc, new Vector2(0, -30), new Vector2(600, 60));
        scoreText = UIFactory.CreateText(canvas.transform, "Score", "", 28, TextAnchor.MiddleCenter, UIFactory.Parchment, tc, tc, tc, new Vector2(0, -80), new Vector2(800, 40));

        gameOverRoot = UIFactory.Stretch("GameOver", canvas.transform).gameObject;
        UIFactory.CreateImage(gameOverRoot.transform, "Dim", new Color(0, 0, 0, 0.85f), Vector2.zero, Vector2.one, c, Vector2.zero, Vector2.zero);
        UIFactory.CreateText(gameOverRoot.transform, "Title", "THE TRIAL IS OVER", 72, TextAnchor.MiddleCenter, new Color(0.8f, 0.1f, 0.1f), c, c, c, new Vector2(0, 300), new Vector2(1400, 100));
        resultText = UIFactory.CreateText(gameOverRoot.transform, "Result", "", 36, TextAnchor.MiddleCenter, UIFactory.Parchment, c, c, c, new Vector2(0, 170), new Vector2(1400, 140));
        UIFactory.CreateText(gameOverRoot.transform, "NameLabel", "Enter your name:", 30, TextAnchor.MiddleCenter, UIFactory.Gold, c, c, c, new Vector2(0, 50), new Vector2(800, 50));
        var fieldBg = UIFactory.CreateImage(gameOverRoot.transform, "Name", new Color(0.15f, 0.12f, 0.1f), c, c, c, new Vector2(0, -10), new Vector2(500, 64));
        fieldBg.raycastTarget = true;
        var text = UIFactory.CreateText(fieldBg.transform, "Text", "", 34, TextAnchor.MiddleCenter, UIFactory.Parchment, Vector2.zero, Vector2.one, c, Vector2.zero, Vector2.zero);
        text.raycastTarget = false;
        nameField = fieldBg.gameObject.AddComponent<InputField>();
        nameField.textComponent = text;
        nameField.characterLimit = 12;
        nameField.text = PlayerPrefs.GetString("last_name", "Knight");
        UIFactory.CreateButton(gameOverRoot.transform, "Save", "Save score", c, new Vector2(-180, -120), new Vector2(320, 66), SaveScore);
        UIFactory.CreateButton(gameOverRoot.transform, "Menu", "Main menu", c, new Vector2(180, -120), new Vector2(320, 66), GameFlow.ToMainMenu);
        gameOverRoot.SetActive(false);
    }

    void OnPlayerDied()
    {
        if (over) return;
        over = true;
        StartCoroutine(ShowGameOver());
    }

    IEnumerator ShowGameOver()
    {
        yield return new WaitForSeconds(2.5f);
        if (HUD.Instance != null) HUD.Instance.ShowDeath(false);
        ThirdPersonCamera.SetCursorLocked(false);
        resultText.text = "You survived to wave " + Wave + "\nScore: " + Score + "    Kills: " + kills;
        gameOverRoot.SetActive(true);
        nameField.Select();
    }

    void SaveScore()
    {
        string name = string.IsNullOrWhiteSpace(nameField.text) ? "Knight" : nameField.text.Trim();
        PlayerPrefs.SetString("last_name", name);
        int rank = SaveSystem.AddScore(name, Score, Wave);
        resultText.text = rank > 0 ? "New high score! Rank #" + rank : "Score saved (not in the top 10 this time)";
        AudioManager.PlayUI("ui_confirm");
        nameField.interactable = false;
    }
}
