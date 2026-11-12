using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Makes hits feel good: sparks where a blow lands, floating damage numbers,
/// small health bars over recently-hit enemies, and a red screen flash when the
/// player is hurt. Lives across scenes and listens to Health.AnyDamaged.
/// </summary>
public class CombatFeedback : MonoBehaviour
{
    public static CombatFeedback Instance { get; private set; }

    class Number { public RectTransform rt; public Text text; public Vector3 world; public float born; public bool active; }
    class Bar { public RectTransform root; public Image fill; public Health target; }

    RectTransform canvasRect;
    Image hurtFlash;
    readonly List<Number> numbers = new List<Number>();
    readonly Dictionary<Health, float> recentlyHit = new Dictionary<Health, float>();
    readonly List<Bar> bars = new List<Bar>();
    Material sparkMaterial;
    float flashAlpha;

    const float NumberLife = 0.9f;
    const float BarShowTime = 4f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Boot()
    {
        if (Instance != null) return;
        var go = new GameObject("CombatFeedback");
        DontDestroyOnLoad(go);
        go.AddComponent<CombatFeedback>();
    }

    void Awake()
    {
        Instance = this;
        var canvas = UIFactory.CreateCanvas("Feedback Canvas", 5);
        canvas.transform.SetParent(transform, false);
        canvasRect = (RectTransform)canvas.transform;
        hurtFlash = UIFactory.CreateImage(canvas.transform, "HurtFlash", new Color(0.7f, 0f, 0f, 0f), Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        hurtFlash.raycastTarget = false;
        sparkMaterial = Resources.Load<Material>("FX/SparkMat");
        Health.AnyDamaged += OnAnyDamaged;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += (s, m) => { recentlyHit.Clear(); foreach (var n in numbers) Hide(n); };
    }

    void OnDestroy() { Health.AnyDamaged -= OnAnyDamaged; }

    void OnAnyDamaged(Health h, DamageInfo info)
    {
        Vector3 at = info.point != Vector3.zero ? info.point : h.transform.position + Vector3.up * 1.2f;
        if (h.Team == Team.Player)
        {
            flashAlpha = Mathf.Max(flashAlpha, info.heavy ? 0.45f : 0.3f);
            Spark(at, new Color(1f, 0.15f, 0.1f), 10);
            return;
        }
        bool fromPlayer = info.source != null && info.source.GetComponent<PlayerStats>() != null;
        Spark(at, fromPlayer ? new Color(1f, 0.75f, 0.35f) : new Color(0.9f, 0.9f, 1f), info.heavy ? 22 : 12);
        ShowNumber(at + Vector3.up * 0.3f, info.amount, info.heavy);
        recentlyHit[h] = Time.time;
        if (fromPlayer && info.heavy && ThirdPersonCamera.Instance != null) ThirdPersonCamera.Instance.Shake(0.1f, 0.12f);
    }

    // ------------------------------------------------------------ sparks

    public void Spark(Vector3 pos, Color color, int count)
    {
        if (sparkMaterial == null) return;
        var go = new GameObject("Spark");
        go.transform.position = pos;
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.duration = 0.3f;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.15f, 0.4f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(2.5f, 6f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.08f);
        main.startColor = color;
        main.gravityModifier = 1.2f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 64;
        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.08f;
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.material = sparkMaterial;
        r.renderMode = ParticleSystemRenderMode.Stretch;
        r.velocityScale = 0.06f;
        r.lengthScale = 1f;
        ps.Play();
        Destroy(go, 1f);
    }

    // ------------------------------------------------------------ numbers

    void ShowNumber(Vector3 world, float amount, bool heavy)
    {
        var n = numbers.Find(x => !x.active);
        if (n == null)
        {
            var t = UIFactory.CreateText(canvasRect, "Dmg", "", 30, TextAnchor.MiddleCenter, Color.white,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(200, 50));
            n = new Number { rt = t.rectTransform, text = t };
            numbers.Add(n);
        }
        n.active = true;
        n.world = world + Random.insideUnitSphere * 0.25f;
        n.born = Time.unscaledTime;
        n.text.text = Mathf.RoundToInt(amount).ToString();
        n.text.fontSize = heavy ? 42 : 30;
        n.text.color = heavy ? new Color(1f, 0.7f, 0.2f) : Color.white;
        n.rt.gameObject.SetActive(true);
    }

    void Hide(Number n) { n.active = false; if (n.rt != null) n.rt.gameObject.SetActive(false); }

    bool ToCanvas(Camera cam, Vector3 world, out Vector2 local)
    {
        local = Vector2.zero;
        Vector3 sp = cam.WorldToScreenPoint(world);
        if (sp.z < 0.1f) return false;
        return RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, sp, null, out local);
    }

    // ------------------------------------------------------------ update

    void LateUpdate()
    {
        var cam = Camera.main;
        flashAlpha = Mathf.MoveTowards(flashAlpha, 0f, 1.2f * Time.unscaledDeltaTime);
        hurtFlash.color = new Color(0.7f, 0f, 0f, flashAlpha);
        if (cam == null) return;

        foreach (var n in numbers)
        {
            if (!n.active) continue;
            float age = Time.unscaledTime - n.born;
            if (age > NumberLife) { Hide(n); continue; }
            if (!ToCanvas(cam, n.world + Vector3.up * age * 1.2f, out var p)) { n.rt.gameObject.SetActive(false); continue; }
            n.rt.gameObject.SetActive(true);
            n.rt.anchoredPosition = p;
            var c = n.text.color; c.a = 1f - Mathf.Clamp01((age - NumberLife * 0.5f) / (NumberLife * 0.5f)); n.text.color = c;
        }

        // Enemy health bars for recently hit enemies (bosses use the big HUD bar instead).
        int used = 0;
        var stale = new List<Health>();
        foreach (var kv in recentlyHit)
        {
            var h = kv.Key;
            if (h == null || h.IsDead || Time.time - kv.Value > BarShowTime || h.GetComponent<BossAI>() != null) { stale.Add(h); continue; }
            var col = h.GetComponent<CapsuleCollider>();
            float height = col != null ? col.height * h.transform.lossyScale.y : 2f;
            if (!ToCanvas(cam, h.transform.position + Vector3.up * (height + 0.35f), out var p)) continue;
            var bar = GetBar(used++);
            bar.root.gameObject.SetActive(true);
            bar.root.anchoredPosition = p;
            bar.fill.fillAmount = h.Normalized;
        }
        foreach (var s in stale) recentlyHit.Remove(s);
        for (int i = used; i < bars.Count; i++) bars[i].root.gameObject.SetActive(false);
    }

    Bar GetBar(int i)
    {
        while (bars.Count <= i)
        {
            var fill = UIFactory.Bar(canvasRect, "EnemyBar", new Color(0.8f, 0.15f, 0.1f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(110, 12));
            var root = (RectTransform)fill.transform.parent;
            bars.Add(new Bar { root = root, fill = fill });
        }
        return bars[i];
    }
}
