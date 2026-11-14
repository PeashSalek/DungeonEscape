using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Main menu: Continue, New Game, Trial of the King, High Scores, Settings, Credits, Quit.
/// </summary>
public class MainMenu : MonoBehaviour
{
    [TextArea(10, 40)]
    public string creditsText =
        "DUNGEON ESCAPE: CURSE OF THE FORGOTTEN KING\n\n" +
        "Game design, levels and story: Peash Salek\n" +
        "Programming: Peash Salek\n\n" +
        "Made with Unity 6 (URP)\n" +
        "Characters and animations: Mixamo (Adobe) and Sketchfab artists (CC BY) - full list in CREDITS.md\n" +
        "Textures: Poly Haven (CC0)\n" +
        "Sound effects and music: see CREDITS.md\n\n" +
        "LUT University - Game Development Project";

    GameObject mainPanel, scoresPanel, creditsPanel;
    Text scoresText;
    SettingsMenu settings;

    void Awake()
    {
        Time.timeScale = 1f;
        ThirdPersonCamera.SetCursorLocked(false);
        if (FindAnyObjectByType<BlessingSystem>() == null) new GameObject("BlessingSystem").AddComponent<BlessingSystem>();
        if (FindAnyObjectByType<RunStats>() == null) new GameObject("RunStats").AddComponent<RunStats>();
        AudioListener.volume = PlayerPrefs.GetFloat("master_volume", 0.8f);
        Build();
    }

    void Build()
    {
        var canvas = UIFactory.CreateCanvas("Menu Canvas", 10);
        canvas.transform.SetParent(transform, false);
        var root = canvas.transform;
        var c = new Vector2(0.5f, 0.5f);
        var left = new Vector2(0.25f, 0.5f);

        mainPanel = UIFactory.Stretch("Main", root).gameObject;
        var shade = UIFactory.CreateImage(mainPanel.transform, "Shade", new Color(0, 0, 0, 0.45f), new Vector2(0, 0), new Vector2(0.5f, 1), new Vector2(0, 0.5f), Vector2.zero, Vector2.zero);
        UIFactory.CreateText(mainPanel.transform, "Title", "DUNGEON ESCAPE", 96, TextAnchor.MiddleCenter, UIFactory.Gold, left, left, c, new Vector2(0, 330), new Vector2(1000, 120));
        UIFactory.CreateText(mainPanel.transform, "Subtitle", "Curse of the Forgotten King", 40, TextAnchor.MiddleCenter, UIFactory.Parchment, left, left, c, new Vector2(0, 250), new Vector2(1000, 60));

        float y = 120;
        Button first = null;
        if (SaveSystem.HasSave)
        {
            first = UIFactory.CreateButton(mainPanel.transform, "Continue", "Continue", left, new Vector2(0, y), new Vector2(460, 66), GameFlow.Continue);
            y -= 80;
        }
        var ng = UIFactory.CreateButton(mainPanel.transform, "NewGame", "New Game", left, new Vector2(0, y), new Vector2(460, 66), GameFlow.NewGame); y -= 80;
        if (first == null) first = ng;
        UIFactory.CreateButton(mainPanel.transform, "Trial", "Trial of the King", left, new Vector2(0, y), new Vector2(460, 66), GameFlow.StartTrial); y -= 80;
        UIFactory.CreateButton(mainPanel.transform, "Scores", "High Scores", left, new Vector2(0, y), new Vector2(460, 66), ShowScores); y -= 80;
        UIFactory.CreateButton(mainPanel.transform, "Settings", "Settings", left, new Vector2(0, y), new Vector2(460, 66), OpenSettings); y -= 80;
        UIFactory.CreateButton(mainPanel.transform, "Credits", "Credits", left, new Vector2(0, y), new Vector2(460, 66), () => Show(creditsPanel)); y -= 80;
        UIFactory.CreateButton(mainPanel.transform, "Quit", "Quit", left, new Vector2(0, y), new Vector2(460, 66), Quit);
        first.Select();

        // High scores
        scoresPanel = UIFactory.Stretch("Scores", root).gameObject;
        UIFactory.CreateImage(scoresPanel.transform, "Dim", UIFactory.PanelDark, Vector2.zero, Vector2.one, c, Vector2.zero, Vector2.zero);
        UIFactory.CreateText(scoresPanel.transform, "Title", "TRIAL OF THE KING - HIGH SCORES", 52, TextAnchor.MiddleCenter, UIFactory.Gold, c, c, c, new Vector2(0, 400), new Vector2(1400, 80));
        scoresText = UIFactory.CreateText(scoresPanel.transform, "List", "", 32, TextAnchor.UpperCenter, UIFactory.Parchment, c, c, c, new Vector2(0, -20), new Vector2(1100, 700));
        UIFactory.CreateButton(scoresPanel.transform, "Back", "Back", c, new Vector2(0, -440), new Vector2(320, 64), () => Show(mainPanel));

        // Credits
        creditsPanel = UIFactory.Stretch("Credits", root).gameObject;
        UIFactory.CreateImage(creditsPanel.transform, "Dim", UIFactory.PanelDark, Vector2.zero, Vector2.one, c, Vector2.zero, Vector2.zero);
        UIFactory.CreateText(creditsPanel.transform, "Text", creditsText, 30, TextAnchor.MiddleCenter, UIFactory.Parchment, c, c, c, new Vector2(0, 40), new Vector2(1500, 800));
        UIFactory.CreateButton(creditsPanel.transform, "Back", "Back", c, new Vector2(0, -440), new Vector2(320, 64), () => Show(mainPanel));

        settings = SettingsMenu.Create(transform, 20);
        Show(mainPanel);
    }

    void Show(GameObject panel)
    {
        mainPanel.SetActive(panel == mainPanel);
        scoresPanel.SetActive(panel == scoresPanel);
        creditsPanel.SetActive(panel == creditsPanel);
        var b = panel.GetComponentInChildren<Button>();
        if (b != null) b.Select();
    }

    void ShowScores()
    {
        var list = SaveSystem.LoadScores();
        if (list.Count == 0) scoresText.text = "No scores yet.\nEnter the Trial of the King to set one!";
        else
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < list.Count; i++)
                sb.AppendLine($"{i + 1,2}.   {list[i].name,-12}   {list[i].score,7} pts    wave {list[i].waves}    {list[i].date}");
            scoresText.text = sb.ToString();
        }
        Show(scoresPanel);
    }

    void OpenSettings()
    {
        mainPanel.SetActive(false);
        settings.Open(() => Show(mainPanel));
    }

    void Quit()
    {
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}
