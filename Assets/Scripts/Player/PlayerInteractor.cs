using UnityEngine;

/// <summary>Finds the nearest interactable in front of the player and uses it with the Interact key.</summary>
[RequireComponent(typeof(PlayerStats))]
public class PlayerInteractor : MonoBehaviour
{
    public float range = 2.2f;

    public PlayerStats Stats { get; private set; }
    public IInteractable Current { get; private set; }
    public string CurrentPrompt { get; private set; } = "";

    readonly Collider[] buffer = new Collider[24];

    void Awake()
    {
        Stats = GetComponent<PlayerStats>();
        GameInput.Init();
    }

    void Update()
    {
        if (Time.timeScale == 0f || Stats.Health.IsDead) { Current = null; CurrentPrompt = ""; return; }
        FindBest();
        if (Current != null && GameInput.Interact.WasPressedThisFrame() && Current.CanInteract(this))
            Current.Interact(this);
    }

    void FindBest()
    {
        Current = null;
        CurrentPrompt = "";
        Vector3 origin = transform.position + Vector3.up;
        int n = Physics.OverlapSphereNonAlloc(origin, range, buffer, ~0, QueryTriggerInteraction.Collide);
        float best = float.MaxValue;
        for (int i = 0; i < n; i++)
        {
            var it = buffer[i].GetComponentInParent<IInteractable>();
            if (it == null) continue;
            string prompt = it.Prompt(this);
            if (string.IsNullOrEmpty(prompt)) continue;
            Vector3 to = it.transform.position - transform.position; to.y = 0f;
            float score = to.magnitude + Vector3.Angle(transform.forward, to) * 0.01f;
            if (score < best)
            {
                best = score;
                Current = it;
                CurrentPrompt = prompt;
            }
        }
    }
}
