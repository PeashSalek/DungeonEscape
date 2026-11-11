using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Cinematic for the Intro and Ending scenes: the camera glides between waypoints
/// while narration lines fade in as subtitles. Any key skips.
/// Ending mode also shows the run summary and scrolling credits.
/// </summary>
public class StorySequence : MonoBehaviour
{
    public enum Mode { Intro, Ending }
    public Mode mode = Mode.Intro;
    public Transform[] cameraPath;
    public float secondsPerShot = 6f;
    [TextArea(2, 4)] public string[] lines;
    [TextArea(10, 40)] public string credits;

    Camera cam;
    Text subtitle, summary, creditsText, skipHint;
    CanvasGroup subtitleGroup;
    bool skipping;

    void Start()
    {
        cam = Camera.main;
        ThirdPersonCamera.SetCursorLocked(true);
        BuildUI();
        if (RunStats.Instance != null && mode == Mode.Ending) RunStats.Instance.timing = false;
        StartCoroutine(mode == Mode.Intro ? PlayIntro() : PlayEnding());
    }

    void BuildUI()
    {
        var canvas = UIFactory.CreateCanvas("Story Canvas", 20);
        canvas.transform.SetParent(transform, false);
        var bc = new Vector2(0.5f, 0); var c = new Vector2(0.5f, 0.5f);
        // Letterbox bars
        UIFactory.CreateImage(canvas.transform, "BarTop", Color.black, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(0, 110));
        UIFactory.CreateImage(canvas.transform, "BarBottom", Color.black, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), Vector2.zero, new Vector2(0, 160));
        subtitle = UIFactory.CreateText(canvas.transform, "Subtitle", "", 36, TextAnchor.MiddleCenter, UIFactory.Parchment, bc, bc, bc, new Vector2(0, 60), new Vector2(1600, 120));
        subtitleGroup = subtitle.gameObject.AddComponent<CanvasGroup>();
        summary = UIFactory.CreateText(canvas.transform, "Summary", "", 40, TextAnchor.MiddleCenter, UIFactory.Gold, c, c, c, new Vector2(0, 0), new Vector2(1400, 600));
        creditsText = UIFactory.CreateText(canvas.transform, "Credits", "", 32, TextAnchor.UpperCenter, UIFactory.Parchment, c, c, new Vector2(0.5f, 1), new Vector2(0, -600), new Vector2(1500, 2400));
        skipHint = UIFactory.CreateText(canvas.transform, "Skip", "Press any key to skip", 22, TextAnchor.LowerRight, new Color(1, 1, 1, 0.5f), new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-30, 20), new Vector2(500, 40));
    }

    void Update()
    {
        if (!skipping && UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.anyKey.wasPressedThisFrame && Time.timeSinceLevelLoad > 1f)
        {
            skipping = true;
            Finish();
        }
    }

    IEnumerator PlayIntro()
    {
        for (int i = 0; i < lines.Length; i++)
        {
            int shot = cameraPath.Length > 0 ? Mathf.Min(i, cameraPath.Length - 2) : -1;
            StartCoroutine(Fly(shot, secondsPerShot));
            yield return ShowLine(lines[i], secondsPerShot);
        }
        Finish();
    }

    IEnumerator PlayEnding()
    {
        for (int i = 0; i < lines.Length; i++)
        {
            int shot = cameraPath.Length > 1 ? Mathf.Min(i, cameraPath.Length - 2) : -1;
            StartCoroutine(Fly(shot, secondsPerShot));
            yield return ShowLine(lines[i], secondsPerShot);
        }
        var rs = RunStats.Instance;
        summary.text = "THE CURSE IS BROKEN\n\n" +
            (rs != null ? "Time  " + RunStats.FormatTime(rs.playTime) + "\nEnemies defeated  " + rs.kills + "\nDeaths  " + rs.deaths + "\n" : "") +
            "Blessings  " + (BlessingSystem.Instance != null && BlessingSystem.Instance.active.Count > 0 ? BlessingSystem.Instance.ActiveSummary : "none");
        yield return new WaitForSeconds(6f);
        summary.text = "";
        creditsText.text = credits;
        var rt = creditsText.rectTransform;
        float y = -600;
        while (y < 2000)
        {
            y += 70f * Time.deltaTime;
            rt.anchoredPosition = new Vector2(0, y);
            yield return null;
        }
        Finish();
    }

    IEnumerator ShowLine(string line, float duration)
    {
        subtitle.text = line;
        for (float t = 0; t < 1f; t += Time.deltaTime / 0.8f) { subtitleGroup.alpha = t; yield return null; }
        subtitleGroup.alpha = 1f;
        yield return new WaitForSeconds(duration - 1.6f);
        for (float t = 1; t > 0f; t -= Time.deltaTime / 0.8f) { subtitleGroup.alpha = t; yield return null; }
        subtitleGroup.alpha = 0f;
    }

    IEnumerator Fly(int shot, float duration)
    {
        if (cam == null || shot < 0 || cameraPath.Length < 2) yield break;
        Transform a = cameraPath[shot], b = cameraPath[shot + 1];
        for (float t = 0; t < 1f; t += Time.deltaTime / duration)
        {
            float k = Mathf.SmoothStep(0, 1, t);
            cam.transform.SetPositionAndRotation(Vector3.Lerp(a.position, b.position, k), Quaternion.Slerp(a.rotation, b.rotation, k));
            yield return null;
        }
    }

    void Finish()
    {
        StopAllCoroutines();
        if (mode == Mode.Intro) GameFlow.Load(GameFlow.Prison);
        else
        {
            SaveSystem.DeleteSave();
            GameFlow.ToMainMenu();
        }
    }
}
