using System.Collections;
using UnityEngine;

/// <summary>Pull once to open one or more doors or gates.</summary>
public class Lever : MonoBehaviour, IInteractable
{
    public Door[] targets;
    public Transform handle;
    public AudioClip pullSound;
    bool pulled;

    public string Prompt(PlayerInteractor p) => pulled ? "" : "Pull lever";
    public bool CanInteract(PlayerInteractor p) => !pulled;

    public void Interact(PlayerInteractor p)
    {
        if (pulled) return;
        pulled = true;
        if (pullSound != null) AudioSource.PlayClipAtPoint(pullSound, transform.position);
        if (handle != null) StartCoroutine(Pull());
        foreach (var d in targets) if (d != null) d.Open();
        GameEvents.Notify("Somewhere, a gate grinds open...");
    }

    IEnumerator Pull()
    {
        Quaternion a = handle.localRotation, b = a * Quaternion.Euler(-80f, 0f, 0f);
        for (float t = 0; t < 1f; t += Time.deltaTime * 3f)
        {
            handle.localRotation = Quaternion.Slerp(a, b, t);
            yield return null;
        }
        handle.localRotation = b;
    }
}
