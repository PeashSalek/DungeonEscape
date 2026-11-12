using UnityEngine;

/// <summary>
/// Shows a control hint the first time the player walks into this trigger.
/// Write keys as {Interact}, {Dodge} ... and the player's current (rebound) keys are shown.
/// </summary>
[RequireComponent(typeof(BoxCollider))]
public class TutorialHint : MonoBehaviour
{
    [TextArea] public string message = "Press {Interact} to interact.";
    public float duration = 6f;
    bool shown;

    void Awake() { GetComponent<BoxCollider>().isTrigger = true; }

    void OnTriggerEnter(Collider other)
    {
        if (shown || other.GetComponentInParent<PlayerStats>() == null || HUD.Instance == null) return;
        shown = true;
        HUD.Instance.ShowHint(Format(message), duration);
    }

    public static string Format(string text)
    {
        foreach (var name in new[] { "Move", "Sprint", "Jump", "Dodge", "LightAttack", "HeavyAttack", "LockOn", "Interact", "UsePotion", "Pause" })
        {
            string token = "{" + name + "}";
            if (text.Contains(token)) text = text.Replace(token, "[" + GameInput.DisplayName(GameInput.Map.FindAction(name)) + "]");
        }
        return text;
    }
}
