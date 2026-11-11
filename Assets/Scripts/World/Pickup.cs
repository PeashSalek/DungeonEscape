using UnityEngine;

public enum PickupType { Coin, Potion, Key, Lockpick, PowerUpRage, PowerUpWard }

/// <summary>
/// Collectible item. Coins and power-ups are picked up by walking into them;
/// they bob and spin, and coins fly toward the player when close (magnet).
/// </summary>
[RequireComponent(typeof(SphereCollider))]
public class Pickup : MonoBehaviour
{
    public PickupType type = PickupType.Coin;
    public int amount = 1;
    public string keyId = "";
    public string displayName = "";
    public float magnetRange = 3f;
    public AudioClip pickupSound;

    Vector3 basePos;
    float spawnTime;
    Transform player;
    Vector3 velocity;
    bool launched;

    static Pickup coinTemplate, potionTemplate;
    public static void RegisterTemplates(Pickup coin, Pickup potion) { coinTemplate = coin; potionTemplate = potion; }

    void Awake()
    {
        var col = GetComponent<SphereCollider>();
        col.isTrigger = true;
        col.radius = Mathf.Max(col.radius, 0.6f);
        spawnTime = Time.time;
    }

    void Start()
    {
        basePos = transform.position;
        var stats = FindAnyObjectByType<PlayerStats>();
        if (stats != null) player = stats.transform;
    }

    /// <summary>Spill coins in a small burst (enemy deaths, chests, barrels).</summary>
    public static void SpawnCoins(Vector3 position, int total)
    {
        int pieces = Mathf.Clamp(total / 2, 1, 6);
        int each = Mathf.Max(1, total / pieces);
        int left = total;
        for (int i = 0; i < pieces && left > 0; i++)
        {
            int n = i == pieces - 1 ? left : Mathf.Min(each, left);
            left -= n;
            var p = Spawn(PickupType.Coin, position, n);
            if (p != null) p.Launch(new Vector3(Random.Range(-2f, 2f), Random.Range(3f, 5f), Random.Range(-2f, 2f)));
        }
    }

    public static Pickup Spawn(PickupType type, Vector3 position, int amount)
    {
        Pickup template = type == PickupType.Potion ? potionTemplate : coinTemplate;
        Pickup p;
        if (template != null) p = Instantiate(template, position, Quaternion.identity);
        else
        {
            // Fallback primitive so loot always works even without prefabs.
            var go = GameObject.CreatePrimitive(type == PickupType.Potion ? PrimitiveType.Capsule : PrimitiveType.Cylinder);
            go.transform.position = position;
            go.transform.localScale = type == PickupType.Potion ? new Vector3(0.25f, 0.2f, 0.25f) : new Vector3(0.3f, 0.03f, 0.3f);
            Destroy(go.GetComponent<Collider>());
            var r = go.GetComponent<Renderer>();
            r.material.color = type == PickupType.Potion ? new Color(0.8f, 0.1f, 0.15f) : new Color(1f, 0.8f, 0.2f);
            go.AddComponent<SphereCollider>();
            p = go.AddComponent<Pickup>();
        }
        p.type = type;
        p.amount = amount;
        p.gameObject.SetActive(true);
        return p;
    }

    public void Launch(Vector3 v)
    {
        velocity = v;
        launched = true;
    }

    void Update()
    {
        if (launched)
        {
            velocity += Physics.gravity * Time.deltaTime;
            Vector3 next = transform.position + velocity * Time.deltaTime;
            if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, 0.3f + Mathf.Max(0f, -velocity.y * Time.deltaTime), ~0, QueryTriggerInteraction.Ignore) && velocity.y < 0f)
            {
                next.y = hit.point.y + 0.3f;
                launched = false;
                basePos = next;
            }
            transform.position = next;
            if (Time.time - spawnTime > 3f) { launched = false; basePos = transform.position; }
            return;
        }

        transform.Rotate(0f, 120f * Time.deltaTime, 0f, Space.World);

        bool magnet = (type == PickupType.Coin) && player != null && Time.time - spawnTime > 0.6f &&
                      Vector3.Distance(player.position, transform.position) < magnetRange;
        if (magnet)
            transform.position = Vector3.MoveTowards(transform.position, player.position + Vector3.up, 10f * Time.deltaTime);
        else
            transform.position = basePos + Vector3.up * (Mathf.Sin((Time.time + basePos.x) * 2.5f) * 0.08f);
    }

    void OnTriggerEnter(Collider other)
    {
        var stats = other.GetComponentInParent<PlayerStats>();
        if (stats == null || stats.Health.IsDead) return;
        if (Time.time - spawnTime < 0.3f && type == PickupType.Coin) return;
        if (!Apply(stats)) return;
        if (pickupSound != null) AudioSource.PlayClipAtPoint(pickupSound, transform.position, 0.7f);
        else AudioManager.Play(type == PickupType.Coin ? "coin" : type == PickupType.Potion ? "potion" : type == PickupType.Key ? "key" : "magic", transform.position, 0.7f);
        Destroy(gameObject);
    }

    bool Apply(PlayerStats stats)
    {
        switch (type)
        {
            case PickupType.Coin:
                stats.AddCoins(amount);
                return true;
            case PickupType.Potion:
                if (!stats.AddPotion(amount)) { GameEvents.Notify("Potion bag full"); return false; }
                GameEvents.Notify("+1 Healing Potion");
                return true;
            case PickupType.Key:
                stats.AddKey(keyId);
                GameEvents.Notify("Found: " + (string.IsNullOrEmpty(displayName) ? "Key" : displayName));
                return true;
            case PickupType.Lockpick:
                stats.AddLockpicks(amount);
                GameEvents.Notify("+" + amount + " Lockpick" + (amount > 1 ? "s" : ""));
                return true;
            case PickupType.PowerUpRage:
            case PickupType.PowerUpWard:
                var pu = stats.GetComponent<PowerUps>();
                if (pu == null) pu = stats.gameObject.AddComponent<PowerUps>();
                pu.Activate(type);
                return true;
        }
        return false;
    }
}
