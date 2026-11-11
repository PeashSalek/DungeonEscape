using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Development helper: start the game with "-autoshot &lt;folder&gt;" and it tours every
/// scene, saving screenshots (gameplay view, close-ups, overview), then quits.
/// Does nothing in normal play.
/// </summary>
public class AutoShot : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        var args = System.Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
            if (args[i] == "-autoshot")
            {
                var go = new GameObject("AutoShot");
                DontDestroyOnLoad(go);
                go.AddComponent<AutoShot>().folder = args[i + 1];
            }
    }

    string folder;
    static readonly string[] scenes = { "MainMenu", "Intro", "Level01_Prison", "Interlude", "Level02_Crypt", "Level03_KingsChamber", "Trial", "Ending" };

    IEnumerator Start()
    {
        Directory.CreateDirectory(folder);
        foreach (var s in scenes)
        {
            SceneManager.LoadScene(s);
            yield return new WaitForSecondsRealtime(3f);
            yield return Shot(s + "_1_view");

            var player = FindAnyObjectByType<PlayerStats>();
            var cam = Camera.main;
            if (player != null && cam != null)
            {
                var tpc = cam.GetComponent<ThirdPersonCamera>();
                if (tpc != null) tpc.enabled = false;

                // Knight close-up (checks model, sword and animation).
                var p = player.transform;
                cam.transform.position = p.position + p.forward * 3f + Vector3.up * 1.6f;
                cam.transform.LookAt(p.position + Vector3.up * 1.1f);
                yield return new WaitForSecondsRealtime(0.5f);
                yield return Shot(s + "_2_knight");

                // Enemy or boss close-up.
                var enemy = FindAnyObjectByType<EnemyAI>();
                var boss = FindAnyObjectByType<BossAI>();
                Transform e = boss != null ? boss.transform : enemy != null ? enemy.transform : null;
                if (e != null)
                {
                    float h = boss != null ? 3f : 1.8f;
                    cam.transform.position = e.position + e.forward * (h + 1.5f) + Vector3.up * h * 0.9f;
                    cam.transform.LookAt(e.position + Vector3.up * h * 0.55f);
                    yield return new WaitForSecondsRealtime(0.5f);
                    yield return Shot(s + "_3_enemy");
                }

                // Overview from above with the ceiling hidden.
                var geo = GameObject.Find("Geometry");
                if (geo != null && geo.transform.childCount > 0)
                {
                    var b = new Bounds(geo.transform.GetChild(0).position, Vector3.zero);
                    foreach (Transform t in geo.transform) b.Encapsulate(t.position);
                    foreach (var r in geo.GetComponentsInChildren<Renderer>()) if (r.name == "Ceiling") r.enabled = false;
                    cam.transform.position = b.center + new Vector3(0, Mathf.Max(b.size.x, b.size.z) * 0.9f, -b.size.z * 0.25f);
                    cam.transform.LookAt(b.center);
                    cam.farClipPlane = 400f;
                    RenderSettings.fog = false;
                    RenderSettings.ambientLight = new Color(0.45f, 0.45f, 0.5f);
                    yield return new WaitForSecondsRealtime(0.5f);
                    yield return Shot(s + "_4_overview");
                }
            }
        }
        Application.Quit();
    }

    IEnumerator Shot(string name)
    {
        yield return new WaitForEndOfFrame();
        ScreenCapture.CaptureScreenshot(Path.Combine(folder, name + ".png"));
        yield return new WaitForSecondsRealtime(0.3f);
    }
}
