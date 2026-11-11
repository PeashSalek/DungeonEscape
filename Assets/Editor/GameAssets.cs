#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Creates the shared materials (Poly Haven textures) and prefabs (player, enemies,
/// bosses, projectiles, shrine). Character models are picked from Assets/Characters/&lt;Type&gt;/
/// (the Sketchfab downloads) and fall back to the Quaternius mannequin until those exist.
/// </summary>
public static class GameAssets
{
    public const string MatDir = "Assets/Materials/Game";
    public const string PrefabDir = "Assets/Prefabs";
    const string PolyHaven = "Assets/ThirdParty/PolyHaven";

    static readonly Dictionary<string, Material> mats = new Dictionary<string, Material>();

    // ------------------------------------------------------------ materials

    public static Material Textured(string name, string texture, Vector2 tiling, Color tint, float smoothness = 0.2f)
    {
        string key = name;
        if (mats.TryGetValue(key, out var cached)) return cached;
        Directory.CreateDirectory(MatDir);
        string path = MatDir + "/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
        var diff = AssetDatabase.LoadAssetAtPath<Texture2D>($"{PolyHaven}/{texture}/{texture}_diff.jpg");
        string norPath = $"{PolyHaven}/{texture}/{texture}_nor_gl.jpg";
        var imp = AssetImporter.GetAtPath(norPath) as TextureImporter;
        if (imp != null && imp.textureType != TextureImporterType.NormalMap) { imp.textureType = TextureImporterType.NormalMap; imp.SaveAndReimport(); }
        var nor = AssetDatabase.LoadAssetAtPath<Texture2D>(norPath);
        m.SetTexture("_BaseMap", diff);
        m.SetTextureScale("_BaseMap", tiling);
        m.SetColor("_BaseColor", tint);
        if (nor != null) { m.SetTexture("_BumpMap", nor); m.SetFloat("_BumpScale", 1f); m.EnableKeyword("_NORMALMAP"); }
        m.SetFloat("_Smoothness", smoothness);
        m.EnableKeyword("_EMISSION");
        m.SetColor("_EmissionColor", Color.black);
        EditorUtility.SetDirty(m);
        mats[key] = m;
        return m;
    }

    public static Material Solid(string name, Color color, Color? emission = null, float smoothness = 0.2f, float metallic = 0f)
    {
        if (mats.TryGetValue(name, out var cached)) return cached;
        Directory.CreateDirectory(MatDir);
        string path = MatDir + "/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
        m.SetColor("_BaseColor", color);
        m.SetFloat("_Smoothness", smoothness);
        m.SetFloat("_Metallic", metallic);
        m.EnableKeyword("_EMISSION");
        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        m.SetColor("_EmissionColor", emission ?? Color.black);
        EditorUtility.SetDirty(m);
        mats[name] = m;
        return m;
    }

    public static void ClearCache() => mats.Clear();

    // ------------------------------------------------------------ characters

    /// <summary>Model asset for a character type: Sketchfab download if present, otherwise the mannequin.</summary>
    public static GameObject ModelFor(string type, out bool isPlaceholder)
    {
        string dir = "Assets/Characters/" + type;
        if (AssetDatabase.IsValidFolder(dir))
        {
            var guid = AssetDatabase.FindAssets("t:Model", new[] { dir }).FirstOrDefault();
            if (guid != null)
            {
                isPlaceholder = false;
                return AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
            }
        }
        isPlaceholder = true;
        return Mannequin();
    }

    public static GameObject Mannequin()
    {
        foreach (var p in new[] { "Assets/ThirdParty/Quaternius/AnimationLibrary_Unity_Standard.fbx", "Assets/ThirdParty/Quaternius/Mannequin_F.fbx" })
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(p);
            if (go != null && go.GetComponentInChildren<SkinnedMeshRenderer>(true) != null) return go;
        }
        return null;
    }

    /// <summary>Instantiates a character model under parent with the given controller, height and tint.</summary>
    public static GameObject AddModel(Transform parent, string type, string controller, float height, Color tint, string tintName)
        => AddModel(parent, type, controller, height, tint, tintName, out _);

    public static GameObject AddModel(Transform parent, string type, string controller, float height, Color tint, string tintName, out bool placeholder)
    {
        var asset = ModelFor(type, out placeholder);
        GameObject model;
        if (asset != null)
        {
            model = (GameObject)PrefabUtility.InstantiatePrefab(asset);
            model.name = "Model";
            model.transform.SetParent(parent, false);
            // Normalise height so any downloaded model fits the gameplay capsule.
            var bounds = CalcBounds(model);
            if (bounds.size.y > 0.01f) model.transform.localScale *= height / bounds.size.y;
            bounds = CalcBounds(model);
            model.transform.position += Vector3.up * (parent.position.y - bounds.min.y);
            if (placeholder)
            {
                var mat = Solid("Char_" + tintName, tint, null, 0.35f);
                foreach (var r in model.GetComponentsInChildren<Renderer>(true))
                    r.sharedMaterials = Enumerable.Repeat(mat, r.sharedMaterials.Length).ToArray();
            }
            else
            {
                foreach (var r in model.GetComponentsInChildren<Renderer>(true))
                    foreach (var m in r.sharedMaterials) if (m != null) { m.EnableKeyword("_EMISSION"); }
            }
        }
        else
        {
            model = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            model.name = "Model";
            model.transform.SetParent(parent, false);
            model.transform.localPosition = new Vector3(0, height / 2f, 0);
            model.transform.localScale = new Vector3(0.6f, height / 2f, 0.6f);
            Object.DestroyImmediate(model.GetComponent<Collider>());
            model.GetComponent<Renderer>().sharedMaterial = Solid("Char_" + tintName, tint);
        }
        var anim = model.GetComponent<Animator>() ?? model.GetComponentInChildren<Animator>();
        if (anim == null) anim = model.AddComponent<Animator>();
        anim.applyRootMotion = false;
        anim.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
        if (!placeholder && asset != null && !CharacterImport.IsHumanoid(asset) && AnimatorFactory.Load(type + "_Generic") != null)
            controller = type + "_Generic";
        var ctrl = AnimatorFactory.Load(controller);
        if (ctrl != null) anim.runtimeAnimatorController = ctrl;
        foreach (var c in model.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
        foreach (var c in model.GetComponentsInChildren<Camera>(true)) Object.DestroyImmediate(c.gameObject);
        foreach (var l in model.GetComponentsInChildren<Light>(true)) Object.DestroyImmediate(l.gameObject);
        return model;
    }

    static Bounds CalcBounds(GameObject go)
    {
        var rs = go.GetComponentsInChildren<Renderer>(true);
        if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
        var b = rs[0].bounds;
        foreach (var r in rs) b.Encapsulate(r.bounds);
        return b;
    }

    /// <summary>Simple sword mesh attached to the right hand bone.</summary>
    public static Transform AttachSword(GameObject model, float length, Material blade, Material hilt)
    {
        var anim = model.GetComponentInChildren<Animator>();
        Transform hand = anim != null && anim.isHuman ? anim.GetBoneTransform(HumanBodyBones.RightHand) : null;
        if (hand == null) return null; // non-humanoid rigs keep their own weapons
        var sword = new GameObject("Sword").transform;
        sword.SetParent(hand != null ? hand : model.transform, false);
        // Counter the model scale so the sword keeps real-world size.
        float s = 1f / Mathf.Max(0.0001f, sword.lossyScale.x);
        sword.localScale = Vector3.one * s;
        sword.localPosition = hand != null ? new Vector3(0, 0.08f, 0.02f) * s : new Vector3(0.4f, 1f, 0.3f);
        sword.localRotation = Quaternion.Euler(0, 0, 0);
        Part(sword, "Blade", new Vector3(0, length * 0.5f + 0.12f, 0), new Vector3(0.06f, length, 0.015f), blade);
        Part(sword, "Guard", new Vector3(0, 0.1f, 0), new Vector3(0.28f, 0.04f, 0.05f), hilt);
        Part(sword, "Grip", new Vector3(0, -0.02f, 0), new Vector3(0.035f, 0.2f, 0.035f), hilt);
        return sword;
    }

    static void Part(Transform parent, string name, Vector3 pos, Vector3 size, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = size;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        Object.DestroyImmediate(go.GetComponent<Collider>());
    }

    // ------------------------------------------------------------ prefabs

    public static Material Steel => Solid("Steel", new Color(0.72f, 0.74f, 0.78f), null, 0.75f, 0.9f);
    public static Material Gold => Solid("Gold", new Color(0.8f, 0.6f, 0.2f), null, 0.6f, 1f);
    public static Material DarkIron => Solid("DarkIron", new Color(0.18f, 0.18f, 0.2f), null, 0.4f, 0.8f);

    public static GameObject BuildPlayer(Vector3 pos, Quaternion rot)
    {
        var root = new GameObject("Player");
        root.transform.SetPositionAndRotation(pos, rot);
        var cc = root.AddComponent<CharacterController>();
        cc.center = new Vector3(0, 0.9f, 0); cc.height = 1.8f; cc.radius = 0.35f;
        root.AddComponent<Health>().maxHealth = 100f;
        root.AddComponent<PlayerStats>();
        root.AddComponent<PlayerMovement>();
        var combat = root.AddComponent<PlayerCombat>();
        root.AddComponent<PlayerInteractor>();
        root.AddComponent<PowerUps>();

        var knightAsset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Player/Knight.fbx");
        GameObject model;
        if (knightAsset != null)
        {
            model = (GameObject)PrefabUtility.InstantiatePrefab(knightAsset);
            model.name = "Knight";
            model.transform.SetParent(root.transform, false);
            var anim = model.GetComponent<Animator>() ?? model.AddComponent<Animator>();
            anim.applyRootMotion = false;
            anim.runtimeAnimatorController = AnimatorFactory.Load("Player");
        }
        else model = AddModel(root.transform, "Knight", "Player", 1.8f, Color.gray, "Knight");
        AttachSword(model, 0.9f, Steel, Gold);
        // Soft light that follows the knight so he stays readable in dark rooms.
        var lamp = new GameObject("PlayerLight").AddComponent<Light>();
        lamp.transform.SetParent(root.transform, false);
        lamp.transform.localPosition = new Vector3(0, 2.6f, -0.8f);
        lamp.color = new Color(1f, 0.85f, 0.7f); lamp.range = 8f; lamp.intensity = 1.6f; lamp.shadows = LightShadows.None;
        var a = model.GetComponentInChildren<Animator>();
        root.GetComponent<PlayerMovement>().animator = a;
        combat.animator = a;
        return root;
    }

    public class EnemySpec
    {
        public string name, type, controller, growl = "skeleton_growl", death = "skeleton_die";
        public EnemyAI.AttackStyle style;
        public float hp = 60, height = 1.8f, radius = 0.4f;
        public Color tint;
        public Projectile projectile;
        public bool sword = true;
        public System.Action<EnemyAI> tune;
    }

    public static GameObject EnemyPrefab(EnemySpec s)
    {
        Directory.CreateDirectory(PrefabDir + "/Enemies");
        var root = new GameObject(s.name);
        var col = root.AddComponent<CapsuleCollider>();
        col.center = new Vector3(0, s.height / 2f, 0); col.height = s.height; col.radius = s.radius;
        var agent = root.AddComponent<NavMeshAgent>();
        agent.radius = s.radius; agent.height = s.height; agent.angularSpeed = 540f; agent.acceleration = 14f;
        agent.stoppingDistance = s.style == EnemyAI.AttackStyle.Melee ? 1.3f : 0.5f;
        root.AddComponent<Health>().maxHealth = s.hp;
        var model = AddModel(root.transform, s.type, s.controller, s.height, s.tint, s.type, out bool placeholder);
        if (s.sword && (placeholder || s.type == "SkeletonWarrior")) AttachSword(model, s.height > 2.2f ? 1.4f : 0.85f, s.height > 2.2f ? DarkIron : Steel, DarkIron);
        var ai = root.AddComponent<EnemyAI>();
        ai.displayName = s.name;
        ai.style = s.style;
        ai.projectilePrefab = s.projectile;
        ai.animator = model.GetComponentInChildren<Animator>();
        ai.flashRenderers = model.GetComponentsInChildren<Renderer>();
        ai.growlSound = s.growl;
        ai.deathSound = s.death;
        s.tune?.Invoke(ai);
        var prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabDir}/Enemies/{s.type}.prefab");
        Object.DestroyImmediate(root);
        return prefab;
    }

    public static GameObject BossPrefab(string name, string title, string type, string controller, float hp, float height, Color tint,
        Projectile orb, GameObject[] summons, float speed)
    {
        Directory.CreateDirectory(PrefabDir + "/Enemies");
        var root = new GameObject(name);
        var col = root.AddComponent<CapsuleCollider>();
        col.center = new Vector3(0, height / 2f, 0); col.height = height; col.radius = 0.7f;
        var agent = root.AddComponent<NavMeshAgent>();
        agent.radius = 0.7f; agent.height = height; agent.angularSpeed = 300f; agent.acceleration = 10f; agent.stoppingDistance = 2f;
        root.AddComponent<Health>().maxHealth = hp;
        var model = AddModel(root.transform, type, controller, height, tint, type, out bool placeholder);
        if (placeholder) AttachSword(model, 1.6f, Gold, DarkIron);
        var boss = root.AddComponent<BossAI>();
        boss.bossTitle = title;
        boss.orbPrefab = orb;
        boss.summonOnPhase2 = summons ?? new GameObject[0];
        boss.moveSpeed = speed;
        boss.animator = model.GetComponentInChildren<Animator>();
        boss.flashRenderers = model.GetComponentsInChildren<Renderer>();
        var prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabDir}/Enemies/{type}.prefab");
        Object.DestroyImmediate(root);
        return prefab;
    }

    public static Projectile ProjectilePrefab(string name, Vector3 scale, Material mat, float speed, float homing, PrimitiveType shape, Color? light)
    {
        Directory.CreateDirectory(PrefabDir);
        var go = new GameObject(name);
        var vis = GameObject.CreatePrimitive(shape);
        vis.transform.SetParent(go.transform, false);
        vis.transform.localScale = scale;
        vis.GetComponent<Renderer>().sharedMaterial = mat;
        Object.DestroyImmediate(vis.GetComponent<Collider>());
        if (light.HasValue)
        {
            var l = go.AddComponent<Light>();
            l.color = light.Value; l.range = 4f; l.intensity = 2.5f;
        }
        var p = go.AddComponent<Projectile>();
        p.speed = speed; p.homingStrength = homing;
        var prefab = PrefabUtility.SaveAsPrefabAsset(go, $"{PrefabDir}/{name}.prefab");
        Object.DestroyImmediate(go);
        return prefab.GetComponent<Projectile>();
    }

    /// <summary>Builds every character prefab. Run again after adding Sketchfab models.</summary>
    [MenuItem("Dungeon Escape/Build Character Prefabs")]
    public static void BuildAllPrefabs()
    {
        ClearCache();
        var arrow = ProjectilePrefab("Arrow", new Vector3(0.04f, 0.04f, 0.8f), Solid("ArrowWood", new Color(0.45f, 0.33f, 0.2f)), 20f, 0f, PrimitiveType.Cube, null);
        var orb = ProjectilePrefab("MageOrb", Vector3.one * 0.35f, Solid("OrbPurple", new Color(0.5f, 0.15f, 0.9f), new Color(0.7f, 0.25f, 1f) * 4f), 7f, 70f, PrimitiveType.Sphere, new Color(0.6f, 0.3f, 1f));
        var kingOrb = ProjectilePrefab("CurseOrb", Vector3.one * 0.5f, Solid("OrbCurse", new Color(0.8f, 0.1f, 0.3f), new Color(1f, 0.1f, 0.3f) * 4f), 9f, 40f, PrimitiveType.Sphere, new Color(1f, 0.2f, 0.3f));

        var bone = new Color(0.82f, 0.78f, 0.66f);
        EnemyPrefab(new EnemySpec { name = "Skeleton Warrior", type = "SkeletonWarrior", controller = "SkeletonWarrior", style = EnemyAI.AttackStyle.Melee, hp = 60, tint = bone,
            tune = ai => { ai.attackDamage = 12; ai.attackWindup = 0.55f; ai.attackCooldown = 1.3f; } });
        EnemyPrefab(new EnemySpec { name = "Skeleton Archer", type = "SkeletonArcher", controller = "SkeletonArcher", style = EnemyAI.AttackStyle.Ranged, hp = 40, tint = new Color(0.7f, 0.68f, 0.55f), projectile = arrow, sword = false,
            tune = ai => { ai.attackRange = 13; ai.preferredDistance = 9; ai.attackDamage = 10; ai.attackWindup = 0.8f; ai.attackCooldown = 1.8f; } });
        EnemyPrefab(new EnemySpec { name = "Dark Mage", type = "DarkMage", controller = "DarkMage", style = EnemyAI.AttackStyle.Caster, hp = 45, tint = new Color(0.22f, 0.1f, 0.3f), projectile = orb, sword = false, growl = "mage_cast", death = "skeleton_die",
            tune = ai => { ai.attackRange = 14; ai.preferredDistance = 10; ai.attackDamage = 14; ai.attackWindup = 1f; ai.attackCooldown = 2.4f; ai.chaseSpeed = 3f; ai.coinDrop = new Vector2Int(6, 12); } });
        EnemyPrefab(new EnemySpec { name = "Cursed Brute", type = "CursedBrute", controller = "CursedBrute", style = EnemyAI.AttackStyle.Melee, hp = 170, height = 2.4f, radius = 0.6f, tint = new Color(0.35f, 0.38f, 0.25f), sword = false, growl = "brute_growl", death = "brute_die",
            tune = ai => { ai.attackDamage = 26; ai.attackWindup = 0.9f; ai.heavyAttack = true; ai.chaseSpeed = 2.8f; ai.poiseHits = 5; ai.attackRadius = 1.6f; ai.attackRange = 2.5f; ai.coinDrop = new Vector2Int(15, 25); ai.potionDropChance = 0.3f; } });
        var horror = EnemyPrefab(new EnemySpec { name = "The Chained Horror", type = "ChainedHorror", controller = "ChainedHorror", style = EnemyAI.AttackStyle.Melee, hp = 220, height = 2.6f, radius = 0.6f, tint = new Color(0.4f, 0.18f, 0.18f), sword = false, growl = "horror_growl", death = "horror_die",
            tune = ai => { ai.attackDamage = 24; ai.attackWindup = 0.8f; ai.heavyAttack = true; ai.chaseSpeed = 3.4f; ai.poiseHits = 6; ai.attackRadius = 1.7f; ai.attackRange = 2.6f; ai.sightRange = 40; ai.hearingRange = 40; } });
        var warrior = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "/Enemies/SkeletonWarrior.prefab");

        BossPrefab("The Forgotten King", "Malgrath, the Forgotten King", "ForgottenKing", "ForgottenKing", 650, 2.9f, new Color(0.45f, 0.38f, 0.3f), kingOrb,
            new[] { horror, warrior, warrior }, 3.2f);
        BossPrefab("The Overlord", "The Overlord, Champion of the Trial", "Overlord", "Overlord", 500, 2.7f, new Color(0.15f, 0.13f, 0.13f), kingOrb,
            new[] { warrior, warrior }, 3.5f);

        // Shrine prefab for the Trial
        var shrine = BuildShrine(Vector3.zero, Quaternion.identity);
        PrefabUtility.SaveAsPrefabAsset(shrine, PrefabDir + "/CurseShrine.prefab");
        Object.DestroyImmediate(shrine);
        AssetDatabase.SaveAssets();
        Debug.Log("[GameAssets] Prefabs built");
    }

    public static GameObject BuildShrine(Vector3 pos, Quaternion rot)
    {
        var root = new GameObject("CurseShrine");
        root.transform.SetPositionAndRotation(pos, rot);
        var stone = Textured("ShrineStone", "castle_wall_slates", new Vector2(1, 1), new Color(0.5f, 0.5f, 0.55f));
        Child(root.transform, PrimitiveType.Cube, "Altar", new Vector3(0, 0.5f, 0), new Vector3(1.8f, 1f, 1f), stone, true);
        Child(root.transform, PrimitiveType.Cube, "Obelisk", new Vector3(0, 2f, 0.25f), new Vector3(0.6f, 2.2f, 0.4f),
            Solid("Obsidian", new Color(0.06f, 0.03f, 0.08f), new Color(0.55f, 0.1f, 0.9f) * 1.2f, 0.8f), true);
        var l = new GameObject("CurseLight").AddComponent<Light>();
        l.transform.SetParent(root.transform, false);
        l.transform.localPosition = new Vector3(0, 2.4f, -0.8f);
        l.color = new Color(0.6f, 0.2f, 1f); l.range = 8f; l.intensity = 3f;
        l.gameObject.AddComponent<TorchFlicker>().amount = 0.15f;
        root.AddComponent<CurseShrine>().shrineLight = l;
        return root;
    }

    public static GameObject Child(Transform parent, PrimitiveType type, string name, Vector3 localPos, Vector3 scale, Material mat, bool collider)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
        return go;
    }
}
#endif
