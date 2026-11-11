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
        else AudioManager.Play("door", transform.position);
        StartCoroutine(Animate());
    }

    /// <summary>Close an open door again (boss arenas seal behind the player).</summary>
    public void Close()
    {
        if (!IsOpen) return;
        IsOpen = false;
        StopAllCoroutines();
        StartCoroutine(Animate(true));
        var obstacle = GetComponentInChildren<UnityEngine.AI.NavMeshObstacle>();
        if (obstacle != null) obstacle.enabled = true;
        AudioManager.Play("door", transform.position);
    }

    public bool startOpen = false;
    Vector3 closedPos; Quaternion closedRot; bool cached;

    void Awake()
    {
        Transform part = movingPart != null ? movingPart : transform;
        closedPos = part.localPosition; closedRot = part.localRotation; cached = true;
        if (startOpen)
        {
            IsOpen = true;
            if (slideDown) part.localPosition = closedPos + Vector3.down * slideDistance;
            else part.localRotation = closedRot * Quaternion.Euler(0f, openAngle, 0f);
            var obstacle = GetComponentInChildren<UnityEngine.AI.NavMeshObstacle>();
            if (obstacle != null) obstacle.enabled = false;
        }
    }

    IEnumerator Animate() => Animate(false);

    IEnumerator Animate(bool closing)
    {
        Transform part = movingPart != null ? movingPart : transform;
        if (!cached) { closedPos = part.localPosition; closedRot = part.localRotation; cached = true; }
        Vector3 openPos = closedPos + Vector3.down * slideDistance;
        Quaternion openRot = closedRot * Quaternion.Euler(0f, openAngle, 0f);
        Vector3 startPos = part.localPosition;
        Quaternion startRot = part.localRotation;
        Vector3 endPos = closing ? closedPos : openPos;
        Quaternion endRot = closing ? closedRot : openRot;
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
        if (!closing)
        {
            var obstacle = GetComponentInChildren<UnityEngine.AI.NavMeshObstacle>();
            if (obstacle != null) obstacle.enabled = false;
        }
    }
}

