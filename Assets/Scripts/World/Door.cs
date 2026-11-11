using System.Collections;
using UnityEngine;

/// <summary>A door that opens with a key, a lever, or just by interacting.</summary>
public class Door : MonoBehaviour, IInteractable
{
    public enum OpenMode { Interact, Key, LeverOnly }
    public OpenMode mode = OpenMode.Interact;
    public string keyId = "";
    public string keyDisplayName = "Rusty Key";
    [Tooltip("The moving part. Rotates around its pivot (hinge) or slides down for gates.")]
    public Transform movingPart;
    public bool slideDown = false;
    public float openAngle = 100f;
    public float slideDistance = 3.2f;
    public float openTime = 1.0f;
    public AudioClip openSound;

    public bool IsOpen { get; private set; }

    public string Prompt(PlayerInteractor p)
    {
        if (IsOpen) return "";
        switch (mode)
        {
            case OpenMode.Key: return p.Stats.HasKey(keyId) ? "Unlock door" : "Locked - needs " + keyDisplayName;
            case OpenMode.LeverOnly: return "The gate is shut. Find a lever.";
            default: return "Open door";
        }
    }

    public bool CanInteract(PlayerInteractor p)
    {
        if (IsOpen) return false;
        if (mode == OpenMode.LeverOnly) return false;
        if (mode == OpenMode.Key) return p.Stats.HasKey(keyId);
        return true;
    }

    public void Interact(PlayerInteractor p)
    {
        if (!CanInteract(p)) return;
        if (mode == OpenMode.Key)
        {
            p.Stats.UseKey(keyId);
            GameEvents.Notify("Unlocked with " + keyDisplayName);
        }
        Open();
    }

    public void Open()
    {
        if (IsOpen) return;
        IsOpen = true;
        if (openSound != null) AudioSource.PlayClipAtPoint(openSound, transform.position);
        StartCoroutine(Animate());
    }

    IEnumerator Animate()
    {
        Transform part = movingPart != null ? movingPart : transform;
        Vector3 startPos = part.localPosition;
        Quaternion startRot = part.localRotation;
        Vector3 endPos = startPos + Vector3.down * slideDistance;
        Quaternion endRot = startRot * Quaternion.Euler(0f, openAngle, 0f);
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / openTime;
            float k = Mathf.SmoothStep(0f, 1f, t);
            if (slideDown) part.localPosition = Vector3.Lerp(startPos, endPos, k);
            else part.localRotation = Quaternion.Slerp(startRot, endRot, k);
            yield return null;
        }
        // Opened gates should not block the NavMesh path either.
        var obstacle = GetComponentInChildren<UnityEngine.AI.NavMeshObstacle>();
        if (obstacle != null) obstacle.enabled = false;
    }
}

