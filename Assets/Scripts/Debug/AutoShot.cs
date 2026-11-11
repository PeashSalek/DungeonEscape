using System.Collections;
using System.IO;
using UnityEngine;

/// <summary>
/// Development helper: when the game is started with "-autoshot <folder>",
/// it saves a few screenshots and quits, so builds can be checked automatically.
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

    IEnumerator Start()
    {
        Directory.CreateDirectory(folder);
        yield return new WaitForSecondsRealtime(3f);
        ScreenCapture.CaptureScreenshot(Path.Combine(folder, "shot_1.png"));
        yield return new WaitForSecondsRealtime(1f);
        // Second view: orbit the camera to the side if there is one.
        var cam = Camera.main;
        var player = FindAnyObjectByType<PlayerStats>();
        if (cam != null && player != null)
        {
            var tpc = cam.GetComponent<ThirdPersonCamera>();
            if (tpc != null) tpc.enabled = false;
            cam.transform.position = player.transform.position + new Vector3(0, 9, 8);
            cam.transform.LookAt(player.transform.position + new Vector3(0, 0, 12));
        }
        yield return new WaitForSecondsRealtime(1f);
        ScreenCapture.CaptureScreenshot(Path.Combine(folder, "shot_2.png"));
        yield return new WaitForSecondsRealtime(1.5f);
        Application.Quit();
    }
}
