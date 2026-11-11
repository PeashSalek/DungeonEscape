using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Fades to black between scenes. Lives across scene loads.</summary>
public class ScreenFader : MonoBehaviour
{
    public static ScreenFader Instance { get; private set; }
    Image black;
    bool busy;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Boot()
    {
        if (Instance != null) return;
        var go = new GameObject("ScreenFader");
        DontDestroyOnLoad(go);
        go.AddComponent<ScreenFader>();
    }

    void Awake()
    {
        Instance = this;
        var canvas = UIFactory.CreateCanvas("Fader Canvas", 1000);
        canvas.transform.SetParent(transform, false);
        black = UIFactory.CreateImage(canvas.transform, "Black", Color.black, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        black.raycastTarget = false;
        SceneManager.sceneLoaded += (s, m) => StartCoroutine(Fade(1f, 0f, 0.6f));
    }

    public void FadeAndLoad(string scene)
    {
        if (busy) return;
        StartCoroutine(FadeLoad(scene));
    }

    IEnumerator FadeLoad(string scene)
    {
        busy = true;
        yield return Fade(0f, 1f, 0.5f);
        SceneManager.LoadScene(scene);
        busy = false;
    }

    IEnumerator Fade(float from, float to, float time)
    {
        for (float t = 0; t < 1f; t += Time.unscaledDeltaTime / time)
        {
            black.color = new Color(0, 0, 0, Mathf.Lerp(from, to, t));
            yield return null;
        }
        black.color = new Color(0, 0, 0, to);
    }
}
