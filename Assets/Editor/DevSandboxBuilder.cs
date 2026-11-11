#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;

/// <summary>
/// Builds the Dev_Sandbox test scene with placeholder geometry so every core
/// system (combat, AI, doors, keys, chests, lockpicking, hazards, braziers,
/// shrines) can be play-tested before the real art arrives.
/// Menu: Dungeon Escape > Build Dev Sandbox. Also runnable in batch mode.
/// </summary>
public static class DevSandboxBuilder
{
    const string ScenePath = "Assets/Scenes/Dev_Sandbox.unity";
    const string MatDir = "Assets/Materials/Dev";
    const string PrefabDir = "Assets/Prefabs";

    static readonly Dictionary<string, Material> mats = new Dictionary<string, Material>();

    [MenuItem("Dungeon Escape/Build Dev Sandbox")]
    public static void Build()
    {
        Directory.CreateDirectory(MatDir);
        Directory.CreateDirectory(PrefabDir);
        mats.Clear();

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // --- Lighting: dark dungeon with warm torches ---
        RenderSettings.skybox = null;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.16f, 0.17f, 0.22f);
        RenderSettings.fog = true;
        RenderSettings.fogColor = new Color(0.05f, 0.05f, 0.07f);
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogDensity = 0.025f;
        var sun = new GameObject("Moonlight").AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.color = new Color(0.55f, 0.6f, 0.8f);
        sun.intensity = 0.35f;
        sun.shadows = LightShadows.Soft;
        sun.transform.rotation = Quaternion.Euler(55f, -30f, 0f);

        var level = new GameObject("Level");
        var surface = level.AddComponent<NavMeshSurface>();
        surface.collectObjects = CollectObjects.Children;
        surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;

        // --- Floor and walls (a 40 x 40 m test hall) ---
        Box(level.transform, "Floor", new Vector3(0, -0.25f, 0), new Vector3(40, 0.5f, 40), Mat("Stone", new Color(0.32f, 0.3f, 0.29f)));
        var wallMat = Mat("Wall", new Color(0.4f, 0.37f, 0.34f));
        Box(level.transform, "Wall_N", new Vector3(0, 2, 20), new Vector3(40, 4, 1), wallMat);
        Box(level.transform, "Wall_S", new Vector3(0, 2, -20), new Vector3(40, 4, 1), wallMat);
        Box(level.transform, "Wall_E", new Vector3(20, 2, 0), new Vector3(1, 4, 40), wallMat);
        Box(level.transform, "Wall_W", new Vector3(-20, 2, 0), new Vector3(1, 4, 40), wallMat);
        // Inner wall with a door gap splitting the hall in two (north part is locked).
        Box(level.transform, "Inner_L", new Vector3(-11.5f, 2, 6), new Vector3(17, 4, 1), wallMat);
        Box(level.transform, "Inner_R", new Vector3(11.5f, 2, 6), new Vector3(17, 4, 1), wallMat);
        Box(level.transform, "Pillar1", new Vector3(-6, 2, -6), new Vector3(1.2f, 4, 1.2f), wallMat);
        Box(level.transform, "Pillar2", new Vector3(6, 2, -6), new Vector3(1.2f, 4, 1.2f), wallMat);

        // --- Player ---
        var player = BuildPlayer(new Vector3(0, 0.05f, -16));

        // --- Camera ---
        var camGo = new GameObject("Main Camera");
        camGo.tag = "MainCamera";
        var cam = camGo.AddComponent<Camera>();
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = 200f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.02f, 0.02f, 0.03f);
        camGo.AddComponent<AudioListener>();
        var tpc = camGo.AddComponent<ThirdPersonCamera>();
        tpc.target = player.transform;
        camGo.transform.position = player.transform.position + new Vector3(0, 3, -6);

        // --- Systems ---
        var systems = new GameObject("Systems");
        systems.AddComponent<HUD>();
        systems.AddComponent<LockpickMinigame>();
        systems.AddComponent<BlessingSystem>();
        systems.AddComponent<RunStats>();
        systems.AddComponent<Respawner>();
        systems.AddComponent<PauseMenu>();

        // --- Prefabs for projectiles ---
        var arrow = ProjectilePrefab("Arrow", new Vector3(0.05f, 0.05f, 0.7f), Mat("ArrowWood", new Color(0.45f, 0.33f, 0.2f)), 18f, 0f, PrimitiveType.Cube);
        var orb = ProjectilePrefab("MageOrb", Vector3.one * 0.35f, Mat("OrbPurple", new Color(0.55f, 0.2f, 0.9f), new Color(0.6f, 0.2f, 1f) * 3f), 7f, 70f, PrimitiveType.Sphere);

        // --- Enemies (placeholder capsules; real models swap in later) ---
        Enemy("Skeleton Warrior", new Vector3(-4, 0, -4), EnemyAI.AttackStyle.Melee, new Color(0.85f, 0.82f, 0.7f), 60f, 1f, null,
            patrol: new[] { new Vector3(-4, 0, -4), new Vector3(4, 0, -2) });
        Enemy("Skeleton Archer", new Vector3(8, 0, 2), EnemyAI.AttackStyle.Ranged, new Color(0.7f, 0.7f, 0.55f), 40f, 1f, arrow);
        Enemy("Dark Mage", new Vector3(-6, 0, 14), EnemyAI.AttackStyle.Caster, new Color(0.25f, 0.1f, 0.35f), 45f, 1f, orb);
        var brute = Enemy("Cursed Brute", new Vector3(6, 0, 14), EnemyAI.AttackStyle.Melee, new Color(0.35f, 0.4f, 0.25f), 160f, 1.35f, null);
        brute.attackDamage = 25f; brute.attackWindup = 0.9f; brute.heavyAttack = true; brute.chaseSpeed = 2.8f; brute.poiseHits = 5; brute.attackRadius = 1.6f; brute.attackRange = 2.4f;

        // --- Key door (north part) ---
        var keyDoor = DoorAt(level.transform, "KeyDoor", new Vector3(0, 0, 6), Door.OpenMode.Key, "sandbox_key", "Rusty Key", false);
        PickupAt("Rusty Key", new Vector3(-14, 0.6f, -14), PickupType.Key, 1, "sandbox_key");

        // --- Lever gate in the east ---
        Box(level.transform, "GateWall", new Vector3(15, 2, 12), new Vector3(1, 4, 15), wallMat);
        var gate = DoorAt(level.transform, "Gate", new Vector3(15, 0, 3), Door.OpenMode.LeverOnly, "", "", true);
        var lever = new GameObject("Lever");
        lever.transform.position = new Vector3(-17, 0, 10);
        Box(lever.transform, "Base", lever.transform.position + new Vector3(0, 0.5f, 0), new Vector3(0.6f, 1, 0.4f), Mat("Iron", new Color(0.25f, 0.25f, 0.28f)));
        var handle = new GameObject("Handle").transform;
        handle.SetParent(lever.transform);
        handle.position = lever.transform.position + new Vector3(0, 1f, 0);
        Box(handle, "Stick", handle.position + new Vector3(0, 0.4f, 0), new Vector3(0.1f, 0.8f, 0.1f), Mat("Iron", Color.gray), false);
        var leverC = lever.AddComponent<Lever>();
        leverC.targets = new[] { gate };
        leverC.handle = handle;
        lever.AddComponent<BoxCollider>().size = new Vector3(1, 2, 1);

        // --- Chests ---
        ChestAt("Chest", new Vector3(-10, 0, -10), false, 0, "");
        ChestAt("LockedChest", new Vector3(17, 0, 17), true, 2, "");

        // --- Barrels ---
        for (int i = 0; i < 5; i++) Barrel(new Vector3(12 + (i % 3) * 1.1f, 0, -14 + (i / 3) * 1.1f));

        // --- Hazards ---
        HazardZone("Spikes", new Vector3(0, 0, -8), new Vector3(4, 1, 2), Hazard.Kind.Spikes);
        HazardZone("PoisonWater", new Vector3(-12, 0, 0), new Vector3(6, 0.6f, 5), Hazard.Kind.PoisonWater);

        // --- Brazier, shrine, items ---
        BrazierAt(new Vector3(3, 0, -17));
        ShrineAt(new Vector3(0, 0, 17));
        PickupAt("Potion", new Vector3(-3, 0.6f, -15), PickupType.Potion, 1, "");
        PickupAt("Rage", new Vector3(10, 0.6f, -10), PickupType.PowerUpRage, 1, "");
        PickupAt("Ward", new Vector3(-8, 0.6f, -16), PickupType.PowerUpWard, 1, "");
        PickupAt("Lockpicks", new Vector3(14, 0.6f, 10), PickupType.Lockpick, 2, "");
        Torch(new Vector3(-19.3f, 2.5f, -10)); Torch(new Vector3(19.3f, 2.5f, -10));
        Torch(new Vector3(-19.3f, 2.5f, 12)); Torch(new Vector3(19.3f, 2.5f, 12)); Torch(new Vector3(0, 2.5f, -19.3f));

        surface.BuildNavMesh();

        EditorSceneManager.SaveScene(scene, ScenePath);
        AddToBuild(ScenePath);
        AssetDatabase.SaveAssets();
        Debug.Log("[DevSandboxBuilder] Built " + ScenePath);
    }

    // ------------------------------------------------------------------ helpers

    static Material Mat(string name, Color color, Color? emission = null)
    {
        if (mats.TryGetValue(name, out var m)) return m;
        string path = MatDir + "/" + name + ".mat";
        m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(m, path);
        }
        m.SetColor("_BaseColor", color);
        m.SetFloat("_Smoothness", 0.15f);
        m.EnableKeyword("_EMISSION");
        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        m.SetColor("_EmissionColor", emission ?? Color.black);
        EditorUtility.SetDirty(m);
        mats[name] = m;
        return m;
    }

    static GameObject Box(Transform parent, string name, Vector3 pos, Vector3 size, Material mat, bool collider = true)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, true);
        go.transform.position = pos;
        go.transform.localScale = size;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
        return go;
    }

    static GameObject BuildPlayer(Vector3 pos)
    {
        var root = new GameObject("Player");
        root.transform.position = pos;
        var cc = root.AddComponent<CharacterController>();
        cc.center = new Vector3(0, 0.9f, 0);
        cc.height = 1.8f;
        cc.radius = 0.35f;
        var health = root.AddComponent<Health>();
        health.maxHealth = 100f;
        root.AddComponent<PlayerStats>();
        root.AddComponent<PlayerMovement>();
        var combat = root.AddComponent<PlayerCombat>();
        root.AddComponent<PlayerInteractor>();
        root.AddComponent<PowerUps>();

        var knightAsset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Player/Knight.fbx");
        if (knightAsset != null)
        {
            var knight = (GameObject)PrefabUtility.InstantiatePrefab(knightAsset);
            knight.name = "Knight";
            knight.transform.SetParent(root.transform, false);
            knight.transform.localPosition = Vector3.zero;
            var anim = knight.GetComponent<Animator>();
            if (anim != null) anim.applyRootMotion = false;
        }
        else
        {
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.transform.SetParent(root.transform, false);
            body.transform.localPosition = new Vector3(0, 0.9f, 0);
            Object.DestroyImmediate(body.GetComponent<Collider>());
        }

        // Placeholder sword swung by code until attack animations are added.
        var pivot = new GameObject("WeaponPivot").transform;
        pivot.SetParent(root.transform, false);
        pivot.localPosition = new Vector3(0.35f, 1.15f, 0.25f);
        var blade = GameObject.CreatePrimitive(PrimitiveType.Cube);
        blade.name = "PlaceholderSword";
        blade.transform.SetParent(pivot, false);
        blade.transform.localPosition = new Vector3(0, 0, 0.6f);
        blade.transform.localScale = new Vector3(0.06f, 0.12f, 1.1f);
        blade.GetComponent<Renderer>().sharedMaterial = Mat("Steel", new Color(0.75f, 0.77f, 0.8f));
        Object.DestroyImmediate(blade.GetComponent<Collider>());
        combat.weaponPivot = pivot;
        return root;
    }

    static Projectile ProjectilePrefab(string name, Vector3 scale, Material mat, float speed, float homing, PrimitiveType shape)
    {
        string path = PrefabDir + "/" + name + ".prefab";
        var go = new GameObject(name);
        var vis = GameObject.CreatePrimitive(shape);
        vis.transform.SetParent(go.transform, false);
        vis.transform.localScale = scale;
        vis.GetComponent<Renderer>().sharedMaterial = mat;
        Object.DestroyImmediate(vis.GetComponent<Collider>());
        if (shape == PrimitiveType.Sphere)
        {
            var l = go.AddComponent<Light>();
            l.color = new Color(0.6f, 0.3f, 1f);
            l.range = 4f;
            l.intensity = 2f;
        }
        var p = go.AddComponent<Projectile>();
        p.speed = speed;
        p.homingStrength = homing;
        var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
        Object.DestroyImmediate(go);
        return prefab.GetComponent<Projectile>();
    }

    static EnemyAI Enemy(string name, Vector3 pos, EnemyAI.AttackStyle style, Color color, float hp, float scale, Projectile projectile,
        Vector3[] patrol = null)
    {
        var root = new GameObject(name);
        root.transform.position = pos;
        root.transform.localScale = Vector3.one * scale;
        var col = root.AddComponent<CapsuleCollider>();
        col.center = new Vector3(0, 0.9f, 0);
        col.height = 1.8f;
        col.radius = 0.4f;
        var agent = root.AddComponent<NavMeshAgent>();
        agent.radius = 0.4f;
        agent.height = 1.8f;
        agent.angularSpeed = 540f;
        agent.acceleration = 12f;
        agent.stoppingDistance = style == EnemyAI.AttackStyle.Melee ? 1.3f : 0.5f;
        var health = root.AddComponent<Health>();
        health.maxHealth = hp;

        var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        body.name = "Body (placeholder)";
        body.transform.SetParent(root.transform, false);
        body.transform.localPosition = new Vector3(0, 0.9f, 0);
        Object.DestroyImmediate(body.GetComponent<Collider>());
        body.GetComponent<Renderer>().sharedMaterial = Mat("Enemy_" + name.Replace(" ", ""), color);
        var eyes = GameObject.CreatePrimitive(PrimitiveType.Cube);
        eyes.transform.SetParent(body.transform, false);
        eyes.transform.localPosition = new Vector3(0, 0.55f, 0.42f);
        eyes.transform.localScale = new Vector3(0.6f, 0.08f, 0.1f);
        eyes.GetComponent<Renderer>().sharedMaterial = Mat("EnemyEyes", Color.black, new Color(1f, 0.3f, 0.1f) * 4f);
        Object.DestroyImmediate(eyes.GetComponent<Collider>());

        var ai = root.AddComponent<EnemyAI>();
        ai.displayName = name;
        ai.style = style;
        ai.projectilePrefab = projectile;
        ai.flashRenderers = new[] { body.GetComponent<Renderer>() };
        if (style == EnemyAI.AttackStyle.Ranged) { ai.attackRange = 11f; ai.preferredDistance = 8f; ai.attackDamage = 10f; ai.attackWindup = 0.8f; ai.attackCooldown = 1.8f; }
        if (style == EnemyAI.AttackStyle.Caster) { ai.attackRange = 12f; ai.preferredDistance = 9f; ai.attackDamage = 14f; ai.attackWindup = 1.0f; ai.attackCooldown = 2.4f; ai.chaseSpeed = 3f; }

        if (patrol != null)
        {
            var pts = new List<Transform>();
            for (int i = 0; i < patrol.Length; i++)
            {
                var pt = new GameObject(name + "_Patrol" + i).transform;
                pt.position = patrol[i];
                pts.Add(pt);
            }
            ai.patrolPoints = pts.ToArray();
        }
        return ai;
    }

    static Door DoorAt(Transform level, string name, Vector3 pos, Door.OpenMode mode, string keyId, string keyName, bool gate)
    {
        var root = new GameObject(name);
        root.transform.position = pos;
        var frameMat = Mat("Iron", new Color(0.25f, 0.25f, 0.28f));
        var woodMat = Mat("DoorWood", new Color(0.35f, 0.22f, 0.12f));

        var hinge = new GameObject("Hinge").transform;
        hinge.SetParent(root.transform, false);
        hinge.localPosition = gate ? Vector3.zero : new Vector3(-1.5f, 0, 0);
        var panel = Box(hinge, gate ? "Bars" : "Panel", root.transform.position + new Vector3(0, 1.75f, 0),
            gate ? new Vector3(1.0f, 3.5f, 5f) : new Vector3(3f, 3.5f, 0.25f), gate ? frameMat : woodMat);
        if (gate) panel.transform.localScale = new Vector3(0.6f, 3.5f, 5f);
        var obstacle = panel.AddComponent<NavMeshObstacle>();
        obstacle.carving = true;

        var door = root.AddComponent<Door>();
        door.mode = mode;
        door.keyId = keyId;
        door.keyDisplayName = keyName;
        door.movingPart = hinge;
        door.slideDown = gate;
        var trigger = root.AddComponent<BoxCollider>();
        trigger.isTrigger = true;
        trigger.center = new Vector3(0, 1.5f, 0);
        trigger.size = gate ? new Vector3(3f, 3f, 5f) : new Vector3(3f, 3f, 2.5f);
        return door;
    }

    static void PickupAt(string name, Vector3 pos, PickupType type, int amount, string keyId)
    {
        var shape = type == PickupType.Key ? PrimitiveType.Cube : type == PickupType.Potion ? PrimitiveType.Capsule : PrimitiveType.Sphere;
        var go = GameObject.CreatePrimitive(shape);
        go.name = name;
        go.transform.position = pos;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        Color c = type == PickupType.Key ? new Color(0.9f, 0.7f, 0.2f) : type == PickupType.Potion ? new Color(0.85f, 0.1f, 0.15f) :
                  type == PickupType.PowerUpRage ? new Color(1f, 0.3f, 0.1f) : type == PickupType.PowerUpWard ? new Color(0.3f, 0.6f, 1f) : Color.white;
        go.transform.localScale = type == PickupType.Key ? new Vector3(0.15f, 0.4f, 0.08f) : Vector3.one * 0.35f;
        go.GetComponent<Renderer>().sharedMaterial = Mat("Pickup_" + type, c, c * 1.5f);
        go.AddComponent<SphereCollider>();
        var p = go.AddComponent<Pickup>();
        p.type = type;
        p.amount = amount;
        p.keyId = keyId;
        p.displayName = name;
    }

    static void ChestAt(string name, Vector3 pos, bool locked, int difficulty, string keyId)
    {
        var root = new GameObject(name);
        root.transform.position = pos;
        root.transform.rotation = Quaternion.Euler(0, 180, 0);
        var wood = Mat("ChestWood", new Color(0.4f, 0.25f, 0.12f));
        Box(root.transform, "Base", pos + new Vector3(0, 0.35f, 0), new Vector3(1.2f, 0.7f, 0.8f), wood);
        var lid = new GameObject("LidHinge").transform;
        lid.SetParent(root.transform, false);
        lid.localPosition = new Vector3(0, 0.7f, -0.4f);
        Box(lid, "Lid", lid.position + root.transform.rotation * new Vector3(0, 0.12f, 0.4f), new Vector3(1.2f, 0.25f, 0.8f), locked ? Mat("ChestIron", new Color(0.3f, 0.3f, 0.32f)) : wood, false);
        var chest = root.AddComponent<Chest>();
        chest.locked = locked;
        chest.lockDifficulty = Mathf.Max(1, difficulty);
        chest.lid = lid;
        chest.guaranteedKeyId = keyId;
    }

    static void Barrel(Vector3 pos)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = "Barrel";
        go.transform.position = pos + new Vector3(0, 0.55f, 0);
        go.transform.localScale = new Vector3(0.8f, 0.55f, 0.8f);
        go.GetComponent<Renderer>().sharedMaterial = Mat("BarrelWood", new Color(0.45f, 0.3f, 0.15f));
        go.AddComponent<Breakable>();
    }

    static void HazardZone(string name, Vector3 pos, Vector3 size, Hazard.Kind kind)
    {
        var root = new GameObject(name);
        root.transform.position = pos;
        var col = root.AddComponent<BoxCollider>();
        col.isTrigger = true;
        col.center = new Vector3(0, size.y * 0.5f, 0);
        col.size = size;
        var hz = root.AddComponent<Hazard>();
        hz.kind = kind;
        if (kind == Hazard.Kind.Spikes)
        {
            var spikes = new GameObject("Spikes").transform;
            spikes.SetParent(root.transform, false);
            var steel = Mat("Steel", new Color(0.75f, 0.77f, 0.8f));
            for (float x = -size.x / 2 + 0.3f; x < size.x / 2; x += 0.5f)
                for (float z = -size.z / 2 + 0.3f; z < size.z / 2; z += 0.5f)
                {
                    var s = Box(spikes, "Spike", pos + new Vector3(x, 0.3f, z), new Vector3(0.08f, 0.6f, 0.08f), steel, false);
                    s.transform.SetParent(spikes, true);
                }
            hz.spikesVisual = spikes;
            Box(root.transform, "Plate", pos + new Vector3(0, 0.01f, 0), new Vector3(size.x, 0.02f, size.z), Mat("DarkIron", new Color(0.15f, 0.15f, 0.16f)), false);
        }
        else if (kind == Hazard.Kind.PoisonWater)
        {
            hz.damage = 4f;
            hz.damageInterval = 0.8f;
            Box(root.transform, "Water", pos + new Vector3(0, 0.02f, 0), new Vector3(size.x, 0.04f, size.z),
                Mat("Poison", new Color(0.2f, 0.45f, 0.15f), new Color(0.1f, 0.4f, 0.05f)), false);
        }
    }

    static void BrazierAt(Vector3 pos)
    {
        var root = new GameObject("Brazier");
        root.transform.position = pos;
        Box(root.transform, "Bowl", pos + new Vector3(0, 0.6f, 0), new Vector3(0.8f, 1.2f, 0.8f), Mat("Iron", Color.gray));
        var light = new GameObject("Fire").AddComponent<Light>();
        light.transform.SetParent(root.transform, false);
        light.transform.localPosition = new Vector3(0, 1.6f, 0);
        light.color = new Color(1f, 0.55f, 0.2f);
        light.range = 8f;
        light.intensity = 3f;
        var b = root.AddComponent<Brazier>();
        b.checkpointId = "sandbox_brazier";
        b.fireLight = light;
    }

    static void ShrineAt(Vector3 pos)
    {
        var root = new GameObject("CurseShrine");
        root.transform.position = pos;
        root.transform.rotation = Quaternion.Euler(0, 180, 0);
        Box(root.transform, "Altar", pos + new Vector3(0, 0.5f, 0), new Vector3(1.6f, 1f, 0.8f), Mat("Wall", Color.gray));
        Box(root.transform, "Obelisk", pos + new Vector3(0, 1.8f, 0.2f), new Vector3(0.5f, 1.8f, 0.3f), Mat("Obsidian", new Color(0.08f, 0.05f, 0.1f), new Color(0.5f, 0.1f, 0.8f) * 0.8f));
        var light = new GameObject("CurseLight").AddComponent<Light>();
        light.transform.SetParent(root.transform, false);
        light.transform.localPosition = new Vector3(0, 2.2f, -0.6f);
        light.color = new Color(0.6f, 0.2f, 1f);
        light.range = 7f;
        light.intensity = 2.5f;
        root.AddComponent<CurseShrine>().shrineLight = light;
    }

    static void Torch(Vector3 pos)
    {
        var l = new GameObject("Torch").AddComponent<Light>();
        l.transform.position = pos;
        l.color = new Color(1f, 0.6f, 0.25f);
        l.range = 12f;
        l.intensity = 2.2f;
        l.shadows = LightShadows.None;
        l.gameObject.AddComponent<TorchFlicker>();
    }

    static void AddToBuild(string path)
    {
        var list = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        if (list.Exists(s => s.path == path)) return;
        list.Add(new EditorBuildSettingsScene(path, true));
        EditorBuildSettings.scenes = list.ToArray();
    }
}
#endif
