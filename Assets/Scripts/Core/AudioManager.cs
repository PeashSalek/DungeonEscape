using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Plays sound effects and music. Clips live in Resources/Audio (all CC0, see CREDITS.md).
/// Music cross-fades between tracks: ambience (menus), explore (levels), boss (fights).
/// </summary>
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    readonly Dictionary<string, AudioClip> cache = new Dictionary<string, AudioClip>();
    AudioSource musicA, musicB, ui;
    bool aActive = true;
    string currentTrack;

    public float musicVolume = 0.45f;
    public float sfxVolume = 1f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Boot()
    {
        if (Instance != null) return;
        var go = new GameObject("AudioManager");
        DontDestroyOnLoad(go);
        go.AddComponent<AudioManager>();
    }

    void Awake()
    {
        Instance = this;
        musicA = gameObject.AddComponent<AudioSource>();
        musicB = gameObject.AddComponent<AudioSource>();
        ui = gameObject.AddComponent<AudioSource>();
        foreach (var m in new[] { musicA, musicB }) { m.loop = true; m.playOnAwake = false; m.volume = 0f; }
        AudioListener.volume = PlayerPrefs.GetFloat("master_volume", 0.8f);
        SceneManager.sceneLoaded += (s, mode) => OnScene(s.name);

        GameEvents.EnemyKilled += e => Play(e.deathSound, e.transform.position);
        for (int i = 1; i <= 4; i++) cache["step" + i] = MakeStep(i);
        GameEvents.PlayerDied += () => PlayUI("brute_die", 0.6f);
    }

    void OnScene(string scene)
    {
        if (scene == GameFlow.MainMenu || scene == GameFlow.Interlude || scene == GameFlow.Intro || scene == GameFlow.Ending) PlayMusic("ambience");
        else PlayMusic("explore");
    }

    /// <summary>Procedural footstep on stone: a short filtered noise thump (no audio file needed).</summary>
    static AudioClip MakeStep(int seed)
    {
        int rate = 44100, len = (int)(rate * 0.11f);
        var data = new float[len];
        var rng = new System.Random(seed * 7919);
        float lp = 0f, lp2 = 0f;
        for (int i = 0; i < len; i++)
        {
            float t = (float)i / rate;
            float noise = (float)(rng.NextDouble() * 2 - 1);
            lp += (noise - lp) * 0.18f;          // low-pass: scuff
            lp2 += (lp - lp2) * 0.35f;
            float thump = Mathf.Sin(2 * Mathf.PI * (70f + seed * 6f) * t) * Mathf.Exp(-t * 55f);
            data[i] = (lp2 * 1.6f * Mathf.Exp(-t * 38f) + thump * 0.7f) * 0.8f;
        }
        var clip = AudioClip.Create("step" + seed, len, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    public AudioClip Get(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        if (cache.TryGetValue(name, out var c)) return c;
        c = Resources.Load<AudioClip>("Audio/SFX/" + name) ?? Resources.Load<AudioClip>("Audio/Music/" + name);
        cache[name] = c;
        return c;
    }

    /// <summary>Play a 3D sound at a position. Name may be a prefix with variants, e.g. "swing" → swing1..3.</summary>
    public static void Play(string name, Vector3 pos, float volume = 1f, float pitchJitter = 0.08f)
    {
        if (Instance == null || string.IsNullOrEmpty(name)) return;
        var clip = Instance.Variant(name);
        if (clip == null) return;
        var go = new GameObject("SFX_" + name);
        go.transform.position = pos;
        var src = go.AddComponent<AudioSource>();
        src.clip = clip;
        src.volume = volume * Instance.sfxVolume;
        src.pitch = 1f + Random.Range(-pitchJitter, pitchJitter);
        src.spatialBlend = 0.85f;
        src.minDistance = 2f;
        src.maxDistance = 30f;
        src.rolloffMode = AudioRolloffMode.Linear;
        src.Play();
        Destroy(go, clip.length / Mathf.Max(0.5f, src.pitch) + 0.1f);
    }

    public static void PlayUI(string name, float volume = 0.7f)
    {
        if (Instance == null) return;
        var clip = Instance.Variant(name);
        if (clip != null) Instance.ui.PlayOneShot(clip, volume * Instance.sfxVolume);
    }

    AudioClip Variant(string name)
    {
        var exact = Get(name);
        if (exact != null) return exact;
        // Look for numbered variants: name1, name2, ...
        var list = new List<AudioClip>();
        for (int i = 1; i <= 8; i++) { var v = Get(name + i); if (v != null) list.Add(v); }
        return list.Count > 0 ? list[Random.Range(0, list.Count)] : null;
    }

    public static void PlayMusic(string track, float fade = 1.5f)
    {
        if (Instance == null || Instance.currentTrack == track) return;
        Instance.StartCoroutine(Instance.CrossFade(track, fade));
    }

    IEnumerator CrossFade(string track, float fade)
    {
        currentTrack = track;
        var clip = Get(track);
        var from = aActive ? musicA : musicB;
        var to = aActive ? musicB : musicA;
        aActive = !aActive;
        to.clip = clip;
        to.volume = 0f;
        if (clip != null) to.Play();
        float startFrom = from.volume;
        for (float t = 0; t < 1f; t += Time.unscaledDeltaTime / fade)
        {
            from.volume = Mathf.Lerp(startFrom, 0f, t);
            to.volume = Mathf.Lerp(0f, musicVolume, t);
            yield return null;
        }
        from.Stop();
        to.volume = musicVolume;
    }
}
