using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Esc pause menu: Resume, Settings, Quit to main menu, Quit game.</summary>
public class PauseMenu : MonoBehaviour
{
    public static bool IsPaused { get; private set; }

    GameObject root;
    SettingsMenu settings;

    void Awake()
    {
        var canvas = UIFactory.CreateCanvas("Pause Canvas", 50);
        canvas.transform.SetParent(transform, false);
        root = canvas.gameObject;
        var c = new Vector2(0.5f, 0.5f);
        UIFactory.CreateImage(root.transform, "Dim", new Color(0, 0, 0, 0.7f), Vector2.zero, Vector2.one, c, Vector2.zero, Vector2.zero);
        UIFactory.CreateText(root.transform, "Title", "PAUSED", 72, TextAnchor.MiddleCenter, UIFactory.Gold, c, c, c, new Vector2(0, 240), new Vector2(800, 100));
        UIFactory.CreateButton(root.transform, "Resume", "Resume", c, new Vector2(0, 100), new Vector2(420, 70), Resume).Select();
        UIFactory.CreateButton(root.transform, "Settings", "Settings", c, new Vector2(0, 15), new Vector2(420, 70), OpenSettings);
        UIFactory.CreateButton(root.transform, "Menu", "Quit to main menu", c, new Vector2(0, -70), new Vector2(420, 70), QuitToMenu);
        UIFactory.CreateButton(root.transform, "Quit", "Quit game", c, new Vector2(0, -155), new Vector2(420, 70), QuitGame);
        root.SetActive(false);
        settings = SettingsMenu.Create(transform, 60);
    }

    void Update()
    {
        if (!GameInput.Pause.WasPressedThisFrame()) return;
        if (settings.IsOpen || SettingsMenu.ClosedFrame == Time.frameCount) return;
        if (LockpickMinigame.Instance != null && LockpickMinigame.Instance.IsActive) return;
        if (!IsPaused && Time.timeScale == 0f) return; // another modal (shrine, shop) is open
        if (IsPaused) Resume(); else Pause();
    }

    void Pause()
    {
        IsPaused = true;
        root.SetActive(true);
        Time.timeScale = 0f;
        AudioListener.pause = true;
        ThirdPersonCamera.SetCursorLocked(false);
    }

    public void Resume()
    {
        IsPaused = false;
        root.SetActive(false);
        Time.timeScale = 1f;
        AudioListener.pause = false;
        ThirdPersonCamera.SetCursorLocked(true);
    }

    void OpenSettings()
    {
        root.SetActive(false);
        settings.Open(() => root.SetActive(true));
    }

    void QuitToMenu()
    {
        Resume();
        ThirdPersonCamera.SetCursorLocked(false);
        if (Application.CanStreamedLevelBeLoaded("MainMenu")) SceneManager.LoadScene("MainMenu");
    }

    void QuitGame()
    {
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    void OnDestroy()
    {
        if (IsPaused) { Time.timeScale = 1f; AudioListener.pause = false; IsPaused = false; }
    }
}
