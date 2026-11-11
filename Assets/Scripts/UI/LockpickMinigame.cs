using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Lock-picking minigame: a marker sweeps around the lock and the player presses
/// Interact while it is inside the golden sweet spot. Hit it 3 times in a row
/// to open the lock; a miss breaks one lockpick. Higher difficulty = smaller
/// sweet spot and faster marker.
/// </summary>
public class LockpickMinigame : MonoBehaviour
{
    public static LockpickMinigame Instance { get; private set; }

    public int pinsNeeded = 3;

    GameObject root;
    RectTransform marker, zone;
    Image zoneImage;
    Text infoText, pinsText;
    Action<bool> onDone;
    PlayerStats stats;
    bool active;
    int pins;
    float angle, speed, zoneCenter, zoneWidth;
    float feedbackUntil;
    int difficulty;

    public bool IsActive => active;

    void Awake()
    {
        Instance = this;
        Build();
    }

    void Build()
    {
        var canvas = UIFactory.CreateCanvas("Lockpick Canvas", 30);
        canvas.transform.SetParent(transform, false);
        root = canvas.gameObject;
        var c = new Vector2(0.5f, 0.5f);

        UIFactory.CreateImage(root.transform, "Dim", new Color(0, 0, 0, 0.6f), Vector2.zero, Vector2.one, c, Vector2.zero, Vector2.zero);
        var ring = UIFactory.CreateImage(root.transform, "Lock", new Color(0.15f, 0.13f, 0.12f, 0.95f), c, c, c, Vector2.zero, new Vector2(420, 420));
        ring.sprite = MakeCircle(128, 0.0f);
        var inner = UIFactory.CreateImage(ring.transform, "Inner", new Color(0.05f, 0.05f, 0.05f, 1f), c, c, c, Vector2.zero, new Vector2(300, 300));
        inner.sprite = MakeCircle(128, 0f);

        zoneImage = UIFactory.CreateImage(ring.transform, "SweetSpot", UIFactory.Gold, c, c, c, Vector2.zero, new Vector2(420, 420));
        zoneImage.sprite = MakeCircle(128, 0.72f);
        zoneImage.type = Image.Type.Filled;
        zoneImage.fillMethod = Image.FillMethod.Radial360;
        zoneImage.fillOrigin = (int)Image.Origin360.Top;
        zoneImage.fillClockwise = true;
        zone = zoneImage.rectTransform;

        var markerPivot = UIFactory.Rect("MarkerPivot", ring.transform, c, c, c, Vector2.zero, new Vector2(10, 10));
        marker = markerPivot;
        UIFactory.CreateImage(markerPivot, "Pick", new Color(0.85f, 0.85f, 0.9f), c, c, new Vector2(0.5f, 0f), new Vector2(0, 140), new Vector2(10, 80));

        infoText = UIFactory.CreateText(root.transform, "Info", "", 30, TextAnchor.MiddleCenter, UIFactory.Parchment, c, c, c, new Vector2(0, -290), new Vector2(1000, 50));
        pinsText = UIFactory.CreateText(root.transform, "Pins", "", 40, TextAnchor.MiddleCenter, UIFactory.Gold, c, c, c, new Vector2(0, 280), new Vector2(800, 60));
        root.SetActive(false);
    }

    static Sprite MakeCircle(int size, float innerRadius)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float r = size * 0.5f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r)) / r;
                float a = Mathf.Clamp01((1f - d) * r) * Mathf.Clamp01((d - innerRadius) * r);
                tex.SetPixel(x, y, new Color(1, 1, 1, a));
            }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    public void Begin(int lockDifficulty, PlayerStats playerStats, Action<bool> done)
    {
        if (active) return;
        difficulty = Mathf.Clamp(lockDifficulty, 1, 5);
        stats = playerStats;
        onDone = done;
        pins = 0;
        active = true;
        root.SetActive(true);
        Time.timeScale = 0f;
        feedbackUntil = Time.unscaledTime + 0.2f; // ignore the key press that opened the lock
        NewPin();
    }

    void NewPin()
    {
        zoneWidth = (Mathf.Lerp(70f, 26f, (difficulty - 1) / 4f) - pins * 4f) * (BlessingSystem.lockpickBonus ? 1.5f : 1f);
        speed = Mathf.Lerp(160f, 320f, (difficulty - 1) / 4f) + pins * 25f;
        zoneCenter = UnityEngine.Random.Range(0f, 360f);
        zoneImage.fillAmount = zoneWidth / 360f;
        // Filled radial starts at top and goes clockwise; rotate so the arc is centred on zoneCenter.
        zone.localRotation = Quaternion.Euler(0, 0, -(zoneCenter - zoneWidth * 0.5f));
        UpdateTexts("");
    }

    void UpdateTexts(string feedback)
    {
        string key = GameInput.DisplayName(GameInput.Interact);
        pinsText.text = "Pins  " + pins + " / " + pinsNeeded;
        infoText.text = (feedback.Length > 0 ? feedback + "\n" : "") +
                        "Press [" + key + "] in the gold zone   -   Lockpicks: " + stats.lockpicks + "   -   [Esc] give up";
    }

    void Update()
    {
        if (!active) return;
        angle = Mathf.Repeat(angle + speed * Time.unscaledDeltaTime, 360f);
        marker.localRotation = Quaternion.Euler(0, 0, -angle);

        if (GameInput.Pause.WasPressedThisFrame()) { Finish(false); return; }
        if (!GameInput.Interact.WasPressedThisFrame() || Time.unscaledTime < feedbackUntil) return;

        float diff = Mathf.Abs(Mathf.DeltaAngle(angle, zoneCenter));
        if (diff <= zoneWidth * 0.5f)
        {
            pins++;
            if (pins >= pinsNeeded) { Finish(true); return; }
            NewPin();
            UpdateTexts("Click!");
        }
        else
        {
            stats.UseLockpick();
            feedbackUntil = Time.unscaledTime + 0.4f;
            if (stats.lockpicks <= 0) { GameEvents.Notify("Your last lockpick snapped"); Finish(false); return; }
            pins = 0;
            NewPin();
            UpdateTexts("Snap! The pick broke.");
        }
    }

    void Finish(bool success)
    {
        active = false;
        root.SetActive(false);
        Time.timeScale = 1f;
        if (success) GameEvents.Notify("Lock opened!");
        var cb = onDone;
        onDone = null;
        cb?.Invoke(success);
    }
}
