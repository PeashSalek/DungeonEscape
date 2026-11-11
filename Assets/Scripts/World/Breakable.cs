using UnityEngine;

/// <summary>Barrels and crates: break into physics debris and may drop coins.</summary>
public class Breakable : MonoBehaviour, IDamageable
{
    public int hitsToBreak = 1;
    public Vector2Int coins = new Vector2Int(0, 4);
    [Range(0f, 1f)] public float dropChance = 0.6f;
    public int debrisPieces = 6;
    public AudioClip breakSound;

    public Team Team => Team.Neutral;
    public bool IsDead { get; private set; }
    int hits;

    public void TakeDamage(DamageInfo info)
    {
        if (IsDead) return;
        if (++hits < hitsToBreak) return;
        IsDead = true;
        Break(info.direction);
    }

    void Break(Vector3 dir)
    {
        if (breakSound != null) AudioSource.PlayClipAtPoint(breakSound, transform.position);
        var rend = GetComponentInChildren<Renderer>();
        Material mat = rend != null ? rend.sharedMaterial : null;
        Vector3 size = rend != null ? rend.bounds.size : Vector3.one;

        for (int i = 0; i < debrisPieces; i++)
        {
            var piece = GameObject.CreatePrimitive(PrimitiveType.Cube);
            piece.name = "Debris";
            piece.transform.position = transform.position + Vector3.up * size.y * 0.5f + Random.insideUnitSphere * 0.3f;
            piece.transform.rotation = Random.rotation;
            piece.transform.localScale = new Vector3(Random.Range(0.08f, 0.25f), Random.Range(0.05f, 0.12f), Random.Range(0.2f, 0.5f)) * Mathf.Max(0.5f, size.x);
            if (mat != null) piece.GetComponent<Renderer>().sharedMaterial = mat;
            var rb = piece.AddComponent<Rigidbody>();
            rb.mass = 0.3f;
            rb.AddForce((dir + Random.insideUnitSphere + Vector3.up) * Random.Range(2f, 4f), ForceMode.Impulse);
            Destroy(piece, 4f + Random.value * 2f);
        }

        if (Random.value < dropChance)
        {
            int n = Random.Range(coins.x, coins.y + 1);
            if (n > 0) Pickup.SpawnCoins(transform.position + Vector3.up * 0.5f, n);
        }
        Destroy(gameObject);
    }
}
