#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>
/// Builds every scene of the game from the text maps in LevelMaps.
/// Menu: Dungeon Escape > Build All Scenes (also runnable in batch mode).
/// </summary>
public static class DungeonBuilder
{
    public const float Cell = 4f;
    const string SceneDir = "Assets/Scenes";

    public class Theme
    {
        public string wallTex = "castle_brick_07", floorTex = "cobblestone_floor_08";
        public Color wallTint = new Color(0.75f, 0.72f, 0.7f), floorTint = new Color(0.7f, 0.68f, 0.66f);
        public Color ambient = new Color(0.3f, 0.28f, 0.32f), fog = new Color(0.03f, 0.03f, 0.05f);
        public float fogDensity = 0.022f, wallHeight = 5f;
        public Color torchColor = new Color(1f, 0.58f, 0.25f);
        public bool ceiling = true;
    }

    public class Level
    {
        public string scene, title, subtitle, keyId = "key", keyName = "Rusty Key";
        public string[] map;
        public Theme theme = new Theme();
        public bool player = true, systems = true;
    }

    // Collected while building a level
    static Transform root, geo, entities;
    static Level current;
    static readonly List<Door> gates = new List<Door>();
    static readonly List<Transform> spawnPoints = new List<Transform>();
    static Transform shrinePoint;
    static Vector3 playerStart;
    static Quaternion playerRot;
    static GameObject playerObj;
    static Door arenaGate;
    static Transform bossPoint;
    static int torchCounter;
    static bool profileRebuilt;

    [MenuItem("Dungeon Escape/Build All Scenes")]
    public static void BuildAll()
    {
        AnimatorFactory.BuildAll();
        CharacterImport.ImportAll();
        GameAssets.BuildAllPrefabs();
        PreserveOldPrototype();

        BuildMenu();
        BuildIntro();
        BuildPrison();
        BuildInterlude();
        BuildCrypt();
        BuildKingsChamber();
        BuildTrial();
        BuildEnding();

        EditorBuildSettings.scenes = new[]
        {
            GameFlow.MainMenu, GameFlow.Intro, GameFlow.Prison, GameFlow.Interlude, GameFlow.Crypt,
            GameFlow.KingsChamber, GameFlow.Trial, GameFlow.Ending,
        }.Select(n => new EditorBuildSettingsScene($"{SceneDir}/{n}.unity", true)).ToArray();
        AssetDatabase.SaveAssets();
        Debug.Log("[DungeonBuilder] All scenes built");
    }

    static void PreserveOldPrototype()
    {
        string old = SceneDir + "/Level01_Prison.unity";
        string dest = SceneDir + "/Prototype/Level01_Prison_Prototype.unity";
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(dest) == null && AssetDatabase.LoadAssetAtPath<SceneAsset>(old) != null)
        {
            if (!AssetDatabase.IsValidFolder(SceneDir + "/Prototype")) AssetDatabase.CreateFolder(SceneDir, "Prototype");
            AssetDatabase.MoveAsset(old, dest);
        }
    }

    // ================================================================ levels

    public static void BuildPrison()
    {
        var l = new Level { scene = GameFlow.Prison, title = "THE PRISON", subtitle = "Find a way out of the cells", map = LevelMaps.Prison, keyId = "prison_key", keyName = "Jailer's Key" };
        Begin(l);
        Finish();
    }

    public static void BuildCrypt()
    {
        var l = new Level
        {
            scene = GameFlow.Crypt, title = "THE CRYPT", subtitle = "Beware the poisoned water", map = LevelMaps.Crypt, keyId = "crypt_key", keyName = "Bone Key",
            theme = new Theme { wallTex = "castle_wall_slates", floorTex = "brick_floor_02", wallTint = new Color(0.6f, 0.65f, 0.7f), floorTint = new Color(0.6f, 0.62f, 0.66f),
                ambient = new Color(0.22f, 0.29f, 0.31f), fog = new Color(0.02f, 0.05f, 0.05f), fogDensity = 0.028f, torchColor = new Color(0.45f, 0.9f, 0.7f) },
        };
        Begin(l);
        Finish();
    }

    public static void BuildKingsChamber()
    {
        var l = new Level
        {
            scene = GameFlow.KingsChamber, title = "THE KING'S CHAMBER", subtitle = "The throne awaits", map = LevelMaps.KingsChamber,
            theme = new Theme { wallTex = "castle_brick_broken_06", floorTex = "marble_01", wallTint = new Color(0.55f, 0.5f, 0.55f), floorTint = new Color(0.4f, 0.38f, 0.42f),
                ambient = new Color(0.3f, 0.22f, 0.33f), fog = new Color(0.06f, 0.02f, 0.06f), fogDensity = 0.018f, wallHeight = 8f, torchColor = new Color(0.9f, 0.35f, 1f) },
        };
        Begin(l);

        // Boss arena: trigger just past the gate, the King in the middle.
        var bossPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(GameAssets.PrefabDir + "/Enemies/ForgottenKing.prefab");
        var boss = Place(bossPrefab, bossPoint.position, Quaternion.Euler(0, -90, 0));
        var bossAI = boss.GetComponent<BossAI>();
        bossAI.summonPoints = spawnPoints.ToArray();
        var throne = GameAssets.Child(entities, PrimitiveType.Cube, "Throne", bossPoint.position + new Vector3(7, 1.5f, 0), new Vector3(2, 3, 2.5f),
            GameAssets.Solid("ThroneGold", new Color(0.4f, 0.3f, 0.12f), null, 0.6f, 0.9f), true);
        var arena = new GameObject("BossArena").AddComponent<BoxCollider>();
        arena.transform.SetParent(entities);
        arena.transform.position = arenaGate.transform.position + new Vector3(Cell * 1.5f, 2, 0);
        arena.size = new Vector3(Cell, 4, Cell * 2);
        var ba = arena.gameObject.AddComponent<BossArena>();
        ba.boss = bossAI;
        ba.sealDoors = new[] { arenaGate };
        ba.completeLevelOnVictory = true;
        ba.victoryDelay = 6f;
        Finish();
    }

    public static void BuildTrial()
    {
        var l = new Level
        {
            scene = GameFlow.Trial, title = "TRIAL OF THE KING", subtitle = "Survive as long as you can", map = LevelMaps.Trial,
            theme = new Theme { wallTex = "castle_brick_02_white", floorTex = "cobblestone_large_01", wallTint = new Color(0.55f, 0.5f, 0.5f), floorTint = new Color(0.6f, 0.55f, 0.5f),
                ambient = new Color(0.34f, 0.27f, 0.27f), fog = new Color(0.05f, 0.02f, 0.02f), fogDensity = 0.012f, wallHeight = 7f, torchColor = new Color(1f, 0.4f, 0.2f), ceiling = false },
        };
        l.theme.wallTex = "castle_brick_07";
        Begin(l);
        var tm = new GameObject("TrialManager").AddComponent<TrialManager>();
        tm.transform.SetParent(root);
        string e = GameAssets.PrefabDir + "/Enemies/";
        tm.enemyPrefabs = new[] { "SkeletonWarrior", "SkeletonArcher", "DarkMage", "CursedBrute" }
            .Select(n => AssetDatabase.LoadAssetAtPath<GameObject>(e + n + ".prefab")).ToArray();
        tm.overlordPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(e + "Overlord.prefab");
        tm.spawnPoints = spawnPoints.ToArray();
        tm.shrinePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(GameAssets.PrefabDir + "/CurseShrine.prefab").GetComponent<CurseShrine>();
        tm.shrinePoint = shrinePoint;
        Finish();
    }

    public static void BuildMenu()
    {
        var l = new Level { scene = GameFlow.MainMenu, map = LevelMaps.MenuRoom, player = false, systems = false,
            theme = new Theme { ambient = new Color(0.18f, 0.16f, 0.17f), fogDensity = 0.02f } };
        Begin(l);
        // Knight standing by the brazier, camera framing him on the right side of the screen.
        var knight = GameAssets.BuildPlayer(playerStart, Quaternion.Euler(0, 200, 0));
        StripGameplay(knight);
        knight.transform.SetParent(entities);
        var cam = MakeCamera(false);
        cam.transform.position = playerStart + new Vector3(-2.2f, 1.7f, -4.2f);
        cam.transform.LookAt(playerStart + new Vector3(-1.4f, 1.2f, 0));
        cam.gameObject.AddComponent<MenuCameraDrift>();
        new GameObject("MainMenu").AddComponent<MainMenu>().transform.SetParent(root);
        Finish(false);
    }

    public static void BuildInterlude()
    {
        var l = new Level { scene = GameFlow.Interlude, map = LevelMaps.MenuRoom, player = false, systems = false,
            theme = new Theme { wallTex = "castle_wall_slates", ambient = new Color(0.06f, 0.1f, 0.14f), torchColor = new Color(0.4f, 0.8f, 1f) } };
        Begin(l);
        var merchant = new GameObject("Ghost Merchant");
        merchant.transform.SetParent(entities);
        merchant.transform.position = playerStart + new Vector3(0, 0, 2);
        merchant.transform.rotation = Quaternion.Euler(0, 180, 0);
        GameAssets.AddModel(merchant.transform, "Merchant", "Merchant", 1.85f, new Color(0.3f, 0.7f, 0.9f), "GhostMerchant");
        foreach (var r in merchant.GetComponentsInChildren<Renderer>())
            r.sharedMaterial = GameAssets.Solid("GhostGlow", new Color(0.2f, 0.45f, 0.6f), new Color(0.2f, 0.6f, 0.9f) * 1.3f, 0.9f);
        var glow = new GameObject("GhostLight").AddComponent<Light>();
        glow.transform.SetParent(merchant.transform, false);
        glow.transform.localPosition = new Vector3(0, 1.5f, 1f);
        glow.color = new Color(0.4f, 0.8f, 1f); glow.range = 6; glow.intensity = 2.5f;
        var cam = MakeCamera(false);
        cam.transform.position = merchant.transform.position + new Vector3(1.2f, 1.6f, -4.5f);
        cam.transform.LookAt(merchant.transform.position + new Vector3(1.6f, 1.2f, 0));
        new GameObject("Interlude").AddComponent<InterludeScreen>().transform.SetParent(root);
        Finish(false);
    }

    public static void BuildIntro()
    {
        var l = new Level { scene = GameFlow.Intro, map = LevelMaps.IntroHall, player = false, systems = false };
        Begin(l);
        var king = Place(AssetDatabase.LoadAssetAtPath<GameObject>(GameAssets.PrefabDir + "/Enemies/ForgottenKing.prefab"), bossPoint.position, Quaternion.Euler(0, 180, 0));
        Object.DestroyImmediate(king.GetComponent<BossAI>());
        Object.DestroyImmediate(king.GetComponent<NavMeshAgent>());
        Object.DestroyImmediate(king.GetComponent<Health>());
        var cam = MakeCamera(false);
        Vector3 c0 = CellPos(2, 1), c1 = CellPos(2, 13), k = bossPoint.position;
        var path = new[]
        {
            Waypoint("Shot0", c0 + new Vector3(0, 3.5f, 0), c1),
            Waypoint("Shot1", CellPos(2, 7) + new Vector3(0, 2.2f, 0), CellPos(3, 7)),
            Waypoint("Shot2", CellPos(3, 7) + new Vector3(0, 1.6f, 0), k + Vector3.up * 2),
            Waypoint("Shot3", k + new Vector3(0, 2.6f, -5f), k + Vector3.up * 2.2f),
            Waypoint("Shot4", k + new Vector3(0, 2.4f, -2.6f), k + Vector3.up * 2.4f),
        };
        cam.transform.SetPositionAndRotation(path[0].position, path[0].rotation);
        var seq = new GameObject("Intro").AddComponent<StorySequence>();
        seq.transform.SetParent(root);
        seq.mode = StorySequence.Mode.Intro;
        seq.cameraPath = path;
        seq.lines = new[]
        {
            "Long ago, King Malgrath traded the lives of his people for a crown that could never be taken.",
            "The curse that followed buried him, his castle and every soul inside it beneath the earth.",
            "Now every trapped soul feeds the Forgotten King... and yours is next.",
            "You wake in his prison. Break the curse - or become part of it.",
        };
        Finish(false);
    }

    public static void BuildEnding()
    {
        var l = new Level { scene = GameFlow.Ending, map = LevelMaps.EndingHall, player = false, systems = false,
            theme = new Theme { ambient = new Color(0.3f, 0.28f, 0.25f), fog = new Color(0.7f, 0.65f, 0.55f), fogDensity = 0.03f } };
        Begin(l);
        var knight = GameAssets.BuildPlayer(playerStart, Quaternion.Euler(0, 180, 0));
        StripGameplay(knight);
        var sun = new GameObject("Daylight").AddComponent<Light>();
        sun.type = LightType.Spot; sun.spotAngle = 70; sun.range = 30; sun.intensity = 8; sun.color = new Color(1f, 0.92f, 0.75f);
        sun.transform.position = CellPos(5, 4) + new Vector3(0, 3, -2);
        sun.transform.LookAt(playerStart);
        var cam = MakeCamera(false);
        var path = new[]
        {
            Waypoint("Shot0", playerStart + new Vector3(0, 1.8f, -3f), playerStart + Vector3.up * 1.4f),
            Waypoint("Shot1", playerStart + new Vector3(2.5f, 1.5f, -2f), playerStart + Vector3.up * 1.4f),
            Waypoint("Shot2", playerStart + new Vector3(0, 2.5f, 4f), CellPos(5, 4) + Vector3.up * 2),
            Waypoint("Shot3", CellPos(5, 4) + new Vector3(0, 2f, 3f), CellPos(5, 4) + new Vector3(0, 2, -5)),
        };
        cam.transform.SetPositionAndRotation(path[0].position, path[0].rotation);
        var seq = new GameObject("Ending").AddComponent<StorySequence>();
        seq.transform.SetParent(root);
        seq.mode = StorySequence.Mode.Ending;
        seq.cameraPath = path;
        seq.lines = new[]
        {
            "With a final cry, Malgrath fell - and the curse he had fed for centuries died with him.",
            "The stones began to fall. Every trapped soul rose toward the light.",
            "And for the first time in a thousand years, someone walked out of the King's dungeon alive.",
        };
        seq.credits = CreditsText;
        Finish(false);
    }

    public const string CreditsText =
        "DUNGEON ESCAPE\nCurse of the Forgotten King\n\n\n" +
        "GAME DESIGN, LEVELS & STORY\nPeash Salek\n\n" +
        "PROGRAMMING\nPeash Salek\nwith AI assistance from Claude (Anthropic)\n\n" +
        "ENGINE\nUnity 6 (Universal Render Pipeline)\n\n" +
        "CHARACTERS\nKnight: Mixamo (Adobe)\nEnemy and boss models: Sketchfab artists (CC BY) - see CREDITS.md\n\n" +
        "ANIMATIONS\nUniversal Animation Library 1 & 2 by Quaternius (CC0)\n\n" +
        "TEXTURES\nPoly Haven (CC0)\n\n" +
        "SOUND EFFECTS\nRPG Sound Pack by artisticdude (CC0)\nSword sounds by StarNinjas (CC0)\n\n" +
        "MUSIC\nDungeon Ambience by yd (CC0)\nDark Cavern Ambient by Paul Wortmann (CC0)\nHeavy Dungeon by MintoDog (CC0)\n\n\n" +
        "Made for the LUT University Game Development Project\n\n\nThank you for playing!";

    // ================================================================ core

    static void Begin(Level level)
    {
        current = level;
        gates.Clear(); spawnPoints.Clear(); shrinePoint = null; arenaGate = null; bossPoint = null; playerObj = null; torchCounter = 0;
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        root = new GameObject("Level").transform;
        geo = new GameObject("Geometry").transform; geo.SetParent(root);
        entities = new GameObject("Entities").transform; entities.SetParent(root);

        var t = level.theme;
        RenderSettings.skybox = null;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = t.ambient;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = t.fog;
        RenderSettings.fogDensity = t.fogDensity;

        var map = level.map;
        int h = map.Length, w = map[0].Length;
        var wallMat = GameAssets.Textured("Wall_" + t.wallTex, t.wallTex, new Vector2(2, 2.5f), t.wallTint);
        var floorMat = GameAssets.Textured("Floor_" + t.floorTex, t.floorTex, new Vector2(2, 2), t.floorTint);
        var ceilMat = GameAssets.Textured("Ceil_" + t.wallTex, t.wallTex, new Vector2(2, 2), t.wallTint * 0.45f);

        for (int r = 0; r < h; r++)
            for (int c = 0; c < w; c++)
            {
                char ch = map[r][c];
                Vector3 p = CellPos(r, c);
                if (ch == '#')
                {
                    if (TouchesFloor(map, r, c))
                    {
                        var wall = Box(geo, "Wall", p + Vector3.up * t.wallHeight / 2f, new Vector3(Cell, t.wallHeight, Cell), wallMat);
                        TryTorch(map, r, c, p, t);
                    }
                    continue;
                }
                Box(geo, "Floor", p + Vector3.down * 0.15f, new Vector3(Cell, 0.3f, Cell), floorMat);
                if (t.ceiling) Box(geo, "Ceiling", p + Vector3.up * (t.wallHeight + 0.15f), new Vector3(Cell, 0.3f, Cell), ceilMat);
                PlaceEntity(ch, r, c, p);
            }

        // Lever opens every lever gate in the level.
        foreach (var lever in entities.GetComponentsInChildren<Lever>()) lever.targets = gates.ToArray();

        if (level.player)
        {
            playerObj = GameAssets.BuildPlayer(playerStart, playerRot);
            var cam = MakeCamera(true);
            cam.GetComponent<ThirdPersonCamera>().target = playerObj.transform;
            cam.transform.position = playerStart + playerRot * new Vector3(0, 3, -6);
        }
        if (level.systems)
        {
            var sys = new GameObject("Systems");
            sys.AddComponent<HUD>();
            sys.AddComponent<LockpickMinigame>();
            sys.AddComponent<PauseMenu>();
            sys.AddComponent<Respawner>();
            var ls = sys.AddComponent<LevelStart>();
            ls.levelTitle = level.title;
            ls.levelSubtitle = level.subtitle;
        }
        MakePostProcessing();
    }

    static void Finish(bool bakeNav = true)
    {
        foreach (Transform t in geo) GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic | StaticEditorFlags.NavigationStatic);
        if (bakeNav)
        {
            // Only the static geometry is baked; doors carve at runtime, props are avoided by agents.
            var surface = geo.gameObject.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.layerMask = ~0;
            surface.BuildNavMesh();
        }
        string path = SceneDir + "/" + current.scene + ".unity";
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), path);
        Debug.Log("[DungeonBuilder] Saved " + path);
    }

    /// <summary>Removes gameplay scripts (in dependency-safe order) so a character can be scenery.</summary>
    static void StripGameplay(GameObject go)
    {
        var comps = go.GetComponents<MonoBehaviour>();
        for (int i = comps.Length - 1; i >= 0; i--) Object.DestroyImmediate(comps[i]);
        var cc = go.GetComponent<CharacterController>();
        if (cc != null) Object.DestroyImmediate(cc);
    }

    static Vector3 CellPos(int r, int c) => new Vector3(c * Cell, 0, -r * Cell);

    static bool IsFloor(string[] m, int r, int c) => r >= 0 && r < m.Length && c >= 0 && c < m[0].Length && m[r][c] != '#';

    static bool TouchesFloor(string[] m, int r, int c)
    {
        for (int dr = -1; dr <= 1; dr++) for (int dc = -1; dc <= 1; dc++) if (IsFloor(m, r + dr, c + dc)) return true;
        return false;
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

    static GameObject Place(GameObject prefab, Vector3 pos, Quaternion rot)
    {
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        go.transform.SetParent(entities, true);
        go.transform.SetPositionAndRotation(pos, rot);
        return go;
    }

    static Transform Waypoint(string name, Vector3 pos, Vector3 lookAt)
    {
        var t = new GameObject(name).transform;
        t.SetParent(root);
        t.position = pos;
        t.LookAt(lookAt);
        return t;
    }

    static Camera MakeCamera(bool gameplay)
    {
        var go = new GameObject("Main Camera");
        go.tag = "MainCamera";
        var cam = go.AddComponent<Camera>();
        cam.nearClipPlane = 0.1f; cam.farClipPlane = 250f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.black;
        go.AddComponent<AudioListener>();
        var data = go.GetComponent<UniversalAdditionalCameraData>() ?? go.AddComponent<UniversalAdditionalCameraData>();
        data.renderPostProcessing = true;
        data.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
        if (gameplay) go.AddComponent<ThirdPersonCamera>();
        return cam;
    }

    static void MakePostProcessing()
    {
        string path = "Assets/Settings/GamePostProcess.asset";
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
        if (profile != null && !profileRebuilt) { AssetDatabase.DeleteAsset(path); profile = null; }
        profileRebuilt = true;
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, path);
            var bloom = profile.Add<Bloom>(true); bloom.intensity.Override(0.8f); bloom.threshold.Override(1.0f);
            var vig = profile.Add<Vignette>(true); vig.intensity.Override(0.38f); vig.smoothness.Override(0.45f);
            var tone = profile.Add<Tonemapping>(true); tone.mode.Override(TonemappingMode.ACES);
            var ca = profile.Add<ColorAdjustments>(true); ca.postExposure.Override(0.7f); ca.contrast.Override(12f); ca.saturation.Override(-8f);
            foreach (var c in profile.components) AssetDatabase.AddObjectToAsset(c, profile);
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
        }
        var vol = new GameObject("Global Volume").AddComponent<Volume>();
        vol.isGlobal = true;
        vol.sharedProfile = profile;
    }

    static void TryTorch(string[] m, int r, int c, Vector3 p, Theme t)
    {
        // Put a torch on a wall that faces an open floor cell, spaced out.
        (int dr, int dc)[] dirs = { (1, 0), (-1, 0), (0, 1), (0, -1) };
        foreach (var d in dirs)
        {
            if (!IsFloor(m, r + d.dr, c + d.dc)) continue;
            if ((r * 7 + c * 3 + torchCounter) % 3 != 0) return;
            torchCounter++;
            Vector3 dir = new Vector3(d.dc, 0, -d.dr);
            Vector3 pos = p + dir * (Cell / 2f + 0.15f) + Vector3.up * 2.8f;
            var torch = new GameObject("Torch").transform;
            torch.SetParent(entities);
            torch.position = pos;
            GameAssets.Child(torch, PrimitiveType.Cube, "Bracket", -dir * 0.1f, new Vector3(0.12f, 0.5f, 0.12f), GameAssets.DarkIron, false);
            GameAssets.Child(torch, PrimitiveType.Sphere, "Flame", Vector3.up * 0.32f, new Vector3(0.18f, 0.28f, 0.18f),
                GameAssets.Solid("Flame_" + ColorUtility.ToHtmlStringRGB(t.torchColor), t.torchColor, t.torchColor * 6f), false);
            var light = new GameObject("Light").AddComponent<Light>();
            light.transform.SetParent(torch, false);
            light.transform.localPosition = dir * 0.6f + Vector3.up * 0.3f;
            light.color = t.torchColor; light.range = 14f; light.intensity = 3.4f; light.shadows = LightShadows.None;
            light.gameObject.AddComponent<TorchFlicker>();
            return;
        }
    }

    static Quaternion FacingOpen(string[] m, int r, int c)
    {
        if (IsFloor(m, r + 1, c)) return Quaternion.Euler(0, 180, 0);
        if (IsFloor(m, r, c + 1)) return Quaternion.Euler(0, 90, 0);
        if (IsFloor(m, r, c - 1)) return Quaternion.Euler(0, -90, 0);
        return Quaternion.identity;
    }

    // ================================================================ entities

    static void PlaceEntity(char ch, int r, int c, Vector3 p)
    {
        var m = current.map;
        string e = GameAssets.PrefabDir + "/Enemies/";
        switch (ch)
        {
            case '@': playerStart = p; playerRot = FacingOpen(m, r, c); break;
            case 'D': MakeDoor(r, c, p, Door.OpenMode.Interact, false, false); break;
            case 'K': MakeDoor(r, c, p, Door.OpenMode.Key, false, false); break;
            case 'G': gates.Add(MakeDoor(r, c, p, Door.OpenMode.LeverOnly, true, false)); break;
            case 'Y': arenaGate = MakeDoor(r, c, p, Door.OpenMode.LeverOnly, true, true); break;
            case 'L': MakeLever(p); break;
            case 'k': MakePickup(p, PickupType.Key, 1, current.keyId, current.keyName); break;
            case 'l': MakePickup(p, PickupType.Lockpick, 2, "", "Lockpicks"); break;
            case 'p': MakePickup(p, PickupType.Potion, 1, "", "Potion"); break;
            case 'r': MakePickup(p, PickupType.PowerUpRage, 1, "", "Rage"); break;
            case 'w': MakePickup(p, PickupType.PowerUpWard, 1, "", "Ward"); break;
            case 'C': MakeChest(p, false, FacingOpen(m, r, c)); break;
            case 'X': MakeChest(p, true, FacingOpen(m, r, c)); break;
            case 'B': for (int i = 0; i < 3; i++) MakeBarrel(p + new Vector3((i - 1) * 1.1f, 0, (i % 2) * 1.1f - 0.5f)); break;
            case 'F': MakeBrazier(p, r, c); break;
            case 'H': GameAssets.BuildShrine(p, FacingOpen(m, r, c)).transform.SetParent(entities); break;
            case 'E': MakeExit(p); break;
            case 'S': MakeHazard(p, Hazard.Kind.Spikes); break;
            case 'P': MakeHazard(p, Hazard.Kind.PoisonWater); break;
            case 'Q': MakeHazard(p, Hazard.Kind.Blade); break;
            case 'W': Enemy(e + "SkeletonWarrior.prefab", r, c, p, true); break;
            case 'A': Enemy(e + "SkeletonArcher.prefab", r, c, p, false); break;
            case 'M': Enemy(e + "DarkMage.prefab", r, c, p, false); break;
            case 'R': Enemy(e + "CursedBrute.prefab", r, c, p, false); break;
            case 'O': bossPoint = new GameObject("BossPoint").transform; bossPoint.SetParent(entities); bossPoint.position = p; break;
            case 'U': var sp = new GameObject("SpawnPoint").transform; sp.SetParent(entities); sp.position = p; spawnPoints.Add(sp); break;
            case 'h': shrinePoint = new GameObject("ShrinePoint").transform; shrinePoint.SetParent(entities); shrinePoint.position = p; break;
        }
    }

    static void Enemy(string prefabPath, int r, int c, Vector3 p, bool patrol)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null) { Debug.LogWarning("Missing prefab " + prefabPath); return; }
        var go = Place(prefab, p, Quaternion.Euler(0, Random.Range(0, 4) * 90f, 0));
        if (!patrol) return;
        // Patrol to a floor cell up to 2 cells away.
        var m = current.map;
        var pts = new List<Transform>();
        foreach (var (dr, dc) in new[] { (0, 2), (0, -2), (2, 0), (-2, 0), (0, 1), (1, 0) })
            if (IsFloor(m, r + dr, c + dc) && IsFloor(m, r + dr / 2, c + dc / 2) && "DKGY".IndexOf(m[r + dr][c + dc]) < 0)
            {
                var t = new GameObject(go.name + "_Patrol").transform;
                t.SetParent(entities);
                t.position = CellPos(r + dr, c + dc);
                pts.Add(t);
                if (pts.Count == 1) { var home = new GameObject(go.name + "_Home").transform; home.SetParent(entities); home.position = p; pts.Add(home); }
                break;
            }
        if (pts.Count > 0) go.GetComponent<EnemyAI>().patrolPoints = pts.ToArray();
    }

    static Door MakeDoor(int r, int c, Vector3 p, Door.OpenMode mode, bool gate, bool startOpen)
    {
        var m = current.map;
        bool wallsNS = !IsFloor(m, r - 1, c) && !IsFloor(m, r + 1, c); // passage runs east-west
        var rootT = new GameObject(gate ? "Gate" : mode == Door.OpenMode.Key ? "KeyDoor" : "Door").transform;
        rootT.SetParent(entities);
        rootT.position = p;
        rootT.rotation = Quaternion.Euler(0, wallsNS ? 90 : 0, 0); // local X spans the opening
        var wallMat = GameAssets.Textured("Wall_" + current.theme.wallTex, current.theme.wallTex, new Vector2(2, 2.5f), current.theme.wallTint);
        float wh = current.theme.wallHeight;
        // Lintel above the 4 m opening
        var lintel = Box(rootT, "Lintel", p + Vector3.up * (4f + (wh - 4f) / 2f), new Vector3(Cell, wh - 4f, Cell * 0.5f), wallMat);
        lintel.transform.rotation = rootT.rotation;
        foreach (float side in new[] { -1f, 1f })
        {
            var post = Box(rootT, "Post", Vector3.zero, new Vector3(0.4f, 4f, 0.6f), GameAssets.DarkIron);
            post.transform.localPosition = new Vector3(side * (Cell / 2f - 0.2f), 2f, 0);
            post.transform.localRotation = Quaternion.identity;
        }
        var hinge = new GameObject("Hinge").transform;
        hinge.SetParent(rootT, false);
        hinge.localPosition = gate ? Vector3.zero : new Vector3(-Cell / 2f + 0.4f, 0, 0);
        GameObject panel;
        if (gate)
        {
            panel = new GameObject("Bars");
            panel.transform.SetParent(hinge, false);
            for (int i = 0; i < 8; i++)
                GameAssets.Child(panel.transform, PrimitiveType.Cube, "Bar", new Vector3(-1.4f + i * 0.4f, 2f, 0), new Vector3(0.09f, 4f, 0.09f), GameAssets.DarkIron, false);
            GameAssets.Child(panel.transform, PrimitiveType.Cube, "Rail", new Vector3(0, 3.6f, 0), new Vector3(3.2f, 0.15f, 0.12f), GameAssets.DarkIron, false);
            GameAssets.Child(panel.transform, PrimitiveType.Cube, "Rail", new Vector3(0, 0.4f, 0), new Vector3(3.2f, 0.15f, 0.12f), GameAssets.DarkIron, false);
            var col = panel.AddComponent<BoxCollider>();
            col.center = new Vector3(0, 2f, 0); col.size = new Vector3(3.2f, 4f, 0.3f);
        }
        else
        {
            var wood = GameAssets.Textured("DoorWood", "dark_wooden_planks", new Vector2(1, 1.3f), mode == Door.OpenMode.Key ? new Color(0.75f, 0.6f, 0.5f) : Color.white);
            panel = GameAssets.Child(hinge, PrimitiveType.Cube, "Panel", new Vector3(Cell / 2f - 0.4f, 1.95f, 0), new Vector3(3.2f, 3.9f, 0.2f), wood, true);
            if (mode == Door.OpenMode.Key)
                for (int i = 0; i < 3; i++)
                    GameAssets.Child(panel.transform, PrimitiveType.Cube, "Band", new Vector3(0, -0.35f + i * 0.35f, -0.6f), new Vector3(1.02f, 0.05f, 0.3f), GameAssets.DarkIron, false);
        }
        var obstacle = panel.AddComponent<NavMeshObstacle>();
        obstacle.carving = true;
        obstacle.shape = NavMeshObstacleShape.Box;
        obstacle.size = new Vector3(3.2f, 4f, 0.4f);
        obstacle.center = gate ? new Vector3(0, 2f, 0) : Vector3.zero;
        if (!gate) obstacle.size = new Vector3(1f, 1f, 2f);

        var door = rootT.gameObject.AddComponent<Door>();
        door.mode = mode;
        door.keyId = current.keyId;
        door.keyDisplayName = current.keyName;
        door.movingPart = hinge;
        door.slideDown = gate;
        door.slideDistance = 3.9f;
        door.openAngle = 100f;
        door.startOpen = startOpen;
        var trig = rootT.gameObject.AddComponent<BoxCollider>();
        trig.isTrigger = true;
        trig.center = new Vector3(0, 1.5f, 0);
        trig.size = new Vector3(Cell, 3f, Cell);
        return door;
    }

    static void MakeLever(Vector3 p)
    {
        var lever = new GameObject("Lever").transform;
        lever.SetParent(entities);
        lever.position = p + new Vector3(0, 0, 1.2f);
        GameAssets.Child(lever, PrimitiveType.Cube, "Base", new Vector3(0, 0.5f, 0), new Vector3(0.7f, 1f, 0.5f), GameAssets.DarkIron, true);
        var handle = new GameObject("Handle").transform;
        handle.SetParent(lever, false);
        handle.localPosition = new Vector3(0, 1f, 0);
        handle.localRotation = Quaternion.Euler(40, 0, 0);
        GameAssets.Child(handle, PrimitiveType.Cube, "Stick", new Vector3(0, 0.4f, 0), new Vector3(0.1f, 0.8f, 0.1f), GameAssets.Steel, false);
        GameAssets.Child(handle, PrimitiveType.Sphere, "Knob", new Vector3(0, 0.82f, 0), Vector3.one * 0.18f, GameAssets.Solid("LeverRed", new Color(0.6f, 0.1f, 0.08f)), false);
        var l = lever.gameObject.AddComponent<Lever>();
        l.handle = handle;
        var col = lever.gameObject.AddComponent<BoxCollider>();
        col.isTrigger = true; col.center = new Vector3(0, 1, 0); col.size = new Vector3(1.4f, 2, 1.4f);
    }

    static void MakePickup(Vector3 p, PickupType type, int amount, string keyId, string display)
    {
        var shape = type == PickupType.Key ? PrimitiveType.Cube : type == PickupType.Potion ? PrimitiveType.Capsule : PrimitiveType.Sphere;
        var go = GameObject.CreatePrimitive(shape);
        go.name = display;
        go.transform.SetParent(entities);
        go.transform.position = p + Vector3.up * 0.8f;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        Color c = type == PickupType.Key ? new Color(0.95f, 0.7f, 0.2f) : type == PickupType.Potion ? new Color(0.85f, 0.1f, 0.15f) :
                  type == PickupType.PowerUpRage ? new Color(1f, 0.3f, 0.1f) : type == PickupType.PowerUpWard ? new Color(0.3f, 0.6f, 1f) : new Color(0.8f, 0.8f, 0.85f);
        go.transform.localScale = type == PickupType.Key ? new Vector3(0.12f, 0.45f, 0.06f) : type == PickupType.Potion ? new Vector3(0.25f, 0.2f, 0.25f) : Vector3.one * 0.3f;
        go.GetComponent<Renderer>().sharedMaterial = GameAssets.Solid("Pickup_" + type, c, c * 2f, 0.8f);
        var l = new GameObject("Glow").AddComponent<Light>();
        l.transform.SetParent(go.transform, false);
        l.color = c; l.range = 3f; l.intensity = 1.5f;
        go.AddComponent<SphereCollider>();
        var pu = go.AddComponent<Pickup>();
        pu.type = type; pu.amount = amount; pu.keyId = keyId; pu.displayName = display;
    }

    static void MakeChest(Vector3 p, bool locked, Quaternion facing)
    {
        var rootT = new GameObject(locked ? "LockedChest" : "Chest").transform;
        rootT.SetParent(entities);
        rootT.SetPositionAndRotation(p, facing);
        var wood = GameAssets.Textured("ChestWood", "dark_wooden_planks", new Vector2(1, 1), new Color(0.9f, 0.75f, 0.6f));
        GameAssets.Child(rootT, PrimitiveType.Cube, "Base", new Vector3(0, 0.35f, 0), new Vector3(1.2f, 0.7f, 0.8f), wood, true);
        GameAssets.Child(rootT, PrimitiveType.Cube, "Band", new Vector3(0, 0.35f, 0), new Vector3(1.24f, 0.08f, 0.84f), locked ? GameAssets.Gold : GameAssets.DarkIron, false);
        var lid = new GameObject("LidHinge").transform;
        lid.SetParent(rootT, false);
        lid.localPosition = new Vector3(0, 0.7f, -0.4f);
        GameAssets.Child(lid, PrimitiveType.Cube, "Lid", new Vector3(0, 0.12f, 0.4f), new Vector3(1.2f, 0.25f, 0.8f), wood, false);
        if (locked) GameAssets.Child(lid, PrimitiveType.Cube, "Lock", new Vector3(0, 0f, 0.82f), new Vector3(0.18f, 0.22f, 0.06f), GameAssets.Gold, false);
        var chest = rootT.gameObject.AddComponent<Chest>();
        chest.locked = locked;
        chest.lockDifficulty = locked ? 3 : 1;
        chest.lid = lid;
        if (locked) { chest.coins = new Vector2Int(40, 70); chest.powerUpChance = 0.6f; chest.potionChance = 0.6f; }
    }

    static void MakeBarrel(Vector3 p)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = "Barrel";
        go.transform.SetParent(entities);
        go.transform.position = p + new Vector3(0, 0.55f, 0);
        go.transform.localScale = new Vector3(0.8f, 0.55f, 0.8f);
        go.GetComponent<Renderer>().sharedMaterial = GameAssets.Textured("BarrelWood", "dark_wooden_planks", new Vector2(2, 1), new Color(1f, 0.85f, 0.7f));
        go.AddComponent<Breakable>();
    }

    static void MakeBrazier(Vector3 p, int r, int c)
    {
        var rootT = new GameObject("Brazier").transform;
        rootT.SetParent(entities);
        rootT.SetPositionAndRotation(p, FacingOpen(current.map, r, c));
        GameAssets.Child(rootT, PrimitiveType.Cylinder, "Stand", new Vector3(0, 0.5f, 0), new Vector3(0.3f, 0.5f, 0.3f), GameAssets.DarkIron, true);
        GameAssets.Child(rootT, PrimitiveType.Cylinder, "Bowl", new Vector3(0, 1.05f, 0), new Vector3(1f, 0.12f, 1f), GameAssets.DarkIron, false);
        var fire = GameAssets.Child(rootT, PrimitiveType.Sphere, "Fire", new Vector3(0, 1.3f, 0), new Vector3(0.6f, 0.5f, 0.6f),
            GameAssets.Solid("BrazierFire", new Color(1f, 0.5f, 0.1f), new Color(1f, 0.45f, 0.1f) * 8f), false);
        var light = new GameObject("FireLight").AddComponent<Light>();
        light.transform.SetParent(rootT, false);
        light.transform.localPosition = new Vector3(0, 1.8f, 0);
        light.color = new Color(1f, 0.55f, 0.2f); light.range = 10f; light.intensity = 3.5f;
        light.gameObject.AddComponent<TorchFlicker>();
        var spawn = new GameObject("Respawn").transform;
        spawn.SetParent(rootT, false);
        spawn.localPosition = new Vector3(0, 0, 0.5f);
        var b = rootT.gameObject.AddComponent<Brazier>();
        b.checkpointId = current.scene + "_" + r + "_" + c;
        b.fireLight = light;
        b.fireEffect = fire;
        b.respawnPoint = spawn;
        var col = rootT.gameObject.AddComponent<BoxCollider>();
        col.isTrigger = true; col.center = new Vector3(0, 1, 0); col.size = new Vector3(2, 2, 2);
    }

    static void MakeExit(Vector3 p)
    {
        var rootT = new GameObject("Exit").transform;
        rootT.SetParent(entities);
        rootT.position = p;
        var stone = GameAssets.Textured("ExitStone", "castle_wall_slates", new Vector2(1, 2), Color.white);
        GameAssets.Child(rootT, PrimitiveType.Cube, "ArchL", new Vector3(-1.5f, 1.75f, 0), new Vector3(0.6f, 3.5f, 0.6f), stone, true);
        GameAssets.Child(rootT, PrimitiveType.Cube, "ArchR", new Vector3(1.5f, 1.75f, 0), new Vector3(0.6f, 3.5f, 0.6f), stone, true);
        GameAssets.Child(rootT, PrimitiveType.Cube, "ArchTop", new Vector3(0, 3.7f, 0), new Vector3(3.6f, 0.5f, 0.6f), stone, false);
        GameAssets.Child(rootT, PrimitiveType.Cube, "Portal", new Vector3(0, 1.75f, 0), new Vector3(2.4f, 3.4f, 0.05f),
            GameAssets.Solid("PortalLight", new Color(0.9f, 0.8f, 0.5f), new Color(1f, 0.85f, 0.5f) * 3f), false);
        var l = new GameObject("ExitLight").AddComponent<Light>();
        l.transform.SetParent(rootT, false);
        l.transform.localPosition = new Vector3(0, 2, -1);
        l.color = new Color(1f, 0.85f, 0.55f); l.range = 10; l.intensity = 3;
        var col = rootT.gameObject.AddComponent<BoxCollider>();
        col.center = new Vector3(0, 1.5f, 0); col.size = new Vector3(2.6f, 3f, 1.5f);
        rootT.gameObject.AddComponent<LevelExit>();
    }

    static void MakeHazard(Vector3 p, Hazard.Kind kind)
    {
        var rootT = new GameObject(kind.ToString()).transform;
        rootT.SetParent(entities);
        rootT.position = p;
        var col = rootT.gameObject.AddComponent<BoxCollider>();
        col.isTrigger = true;
        var hz = rootT.gameObject.AddComponent<Hazard>();
        hz.kind = kind;
        if (kind == Hazard.Kind.Spikes)
        {
            col.center = new Vector3(0, 0.5f, 0); col.size = new Vector3(Cell - 0.4f, 1f, Cell - 0.4f);
            GameAssets.Child(rootT, PrimitiveType.Cube, "Plate", new Vector3(0, 0.02f, 0), new Vector3(Cell - 0.2f, 0.04f, Cell - 0.2f), GameAssets.DarkIron, false);
            var spikes = new GameObject("Spikes").transform;
            spikes.SetParent(rootT, false);
            for (float x = -1.6f; x <= 1.6f; x += 0.4f)
                for (float z = -1.6f; z <= 1.6f; z += 0.4f)
                    GameAssets.Child(spikes, PrimitiveType.Cube, "Spike", new Vector3(x, 0.3f, z), new Vector3(0.06f, 0.6f, 0.06f), GameAssets.Steel, false)
                        .transform.localRotation = Quaternion.Euler(0, 45, 0);
            hz.spikesVisual = spikes;
            hz.damage = 18f;
        }
        else if (kind == Hazard.Kind.PoisonWater)
        {
            col.center = new Vector3(0, 0.3f, 0); col.size = new Vector3(Cell, 0.6f, Cell);
            GameAssets.Child(rootT, PrimitiveType.Cube, "Water", new Vector3(0, 0.03f, 0), new Vector3(Cell, 0.05f, Cell),
                GameAssets.Solid("PoisonWater", new Color(0.12f, 0.3f, 0.1f), new Color(0.15f, 0.55f, 0.1f) * 0.6f, 0.95f), false);
            hz.damage = 4f; hz.damageInterval = 0.7f; hz.slowMultiplier = 0.5f;
        }
        else
        {
            col.center = new Vector3(0, 1f, 0); col.size = new Vector3(Cell, 2f, 1.2f);
            var pivot = new GameObject("BladePivot").transform;
            pivot.SetParent(rootT, false);
            pivot.localPosition = new Vector3(0, 4.6f, 0);
            GameAssets.Child(pivot, PrimitiveType.Cube, "Arm", new Vector3(0, -1.8f, 0), new Vector3(0.1f, 3.6f, 0.1f), GameAssets.DarkIron, false);
            GameAssets.Child(pivot, PrimitiveType.Cube, "Blade", new Vector3(0, -3.8f, 0), new Vector3(1.6f, 0.5f, 0.06f), GameAssets.Steel, false);
            hz.bladePivot = pivot;
            hz.damage = 22f; hz.knockback = 2f; hz.damageInterval = 0.8f;
        }
    }
}

#endif
