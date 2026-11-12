#if UNITY_EDITOR
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Sets up the downloaded Sketchfab characters in Assets/Characters/&lt;Type&gt;/:
/// Humanoid rig when possible (so the shared Quaternius animations work), URP materials
/// built from the downloaded textures, and a Generic controller from the model's own
/// clips if the rig can't be Humanoid.
/// </summary>
public static class CharacterImport
{
    public static readonly string[] Types = { "SkeletonWarrior", "SkeletonArcher", "DarkMage", "CursedBrute", "ForgottenKing", "ChainedHorror", "Overlord", "Knight" };

    [MenuItem("Dungeon Escape/Import Characters")]
    public static void ImportAll()
    {
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        var report = new StringBuilder("[CharacterImport] REPORT");
        foreach (var type in Types)
        {
            string dir = "Assets/Characters/" + type;
            if (!AssetDatabase.IsValidFolder(dir)) { report.Append("\n  " + type + ": no model (placeholder)"); continue; }

            // Some downloads (e.g. Mixamo) embed their textures in the FBX: pull them out once.
            var firstModel = AssetDatabase.FindAssets("t:Model", new[] { dir }).FirstOrDefault();
            if (firstModel != null && AssetDatabase.FindAssets("t:Texture2D", new[] { dir }).Length == 0)
            {
                var mi = (ModelImporter)AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(firstModel));
                if (mi.ExtractTextures(dir)) AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            }

            foreach (var tg in AssetDatabase.FindAssets("t:Texture2D", new[] { dir }))
            {
                string tp = AssetDatabase.GUIDToAssetPath(tg);
                var ti = (TextureImporter)AssetImporter.GetAtPath(tp);
                bool normal = IsNormal(Path.GetFileNameWithoutExtension(tp));
                bool changed = false;
                if (normal && ti.textureType != TextureImporterType.NormalMap) { ti.textureType = TextureImporterType.NormalMap; changed = true; }
                if (ti.maxTextureSize > 2048) { ti.maxTextureSize = 2048; changed = true; }
                if (changed) ti.SaveAndReimport();
            }

            var modelGuid = AssetDatabase.FindAssets("t:Model", new[] { dir }).FirstOrDefault();
            if (modelGuid == null) { report.Append("\n  " + type + ": folder but no model"); continue; }
            string mp = AssetDatabase.GUIDToAssetPath(modelGuid);
            var imp = (ModelImporter)AssetImporter.GetAtPath(mp);
            imp.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            imp.importCameras = false;   // some downloads contain their own camera / lights
            imp.importLights = false;
            imp.animationType = ModelImporterAnimationType.Human;
            imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            imp.importAnimation = true;
            imp.SaveAndReimport();

            var avatar = AssetDatabase.LoadAllAssetsAtPath(mp).OfType<Avatar>().FirstOrDefault();
            bool human = avatar != null && avatar.isValid && avatar.isHuman;
            if (!human)
            {
                imp.animationType = ModelImporterAnimationType.Generic;
                imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                imp.SaveAndReimport();
            }

            // Materials: one URP Lit material per source material, textures matched by name.
            var srcMats = AssetDatabase.LoadAllAssetsAtPath(mp).OfType<Material>().Select(m => m.name).Distinct().ToList();
            srcMats = srcMats.Select(n => n.EndsWith("_URP") ? n.Substring(0, n.Length - 4) : n).Distinct().ToList();
            if (srcMats.Count == 0)
            {
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(mp);
                srcMats = go.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials).Where(m => m != null)
                    .Select(m => m.name.EndsWith("_URP") ? m.name.Substring(0, m.name.Length - 4) : m.name).Distinct().ToList();
            }
            var texPaths = AssetDatabase.FindAssets("t:Texture2D", new[] { dir }).Select(AssetDatabase.GUIDToAssetPath).ToList();
            foreach (var matName in srcMats)
            {
                string matPath = $"{dir}/{Sanitize(matName)}_URP.mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                if (mat == null) { mat = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(mat, matPath); }
                var baseTex = PickTexture(texPaths, matName, srcMats.Count, false);
                var norTex = PickTexture(texPaths, matName, srcMats.Count, true);
                if (baseTex != null) mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(baseTex));
                if (norTex != null) { mat.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(norTex)); mat.EnableKeyword("_NORMALMAP"); }
                mat.SetColor("_BaseColor", Color.white);
                mat.SetFloat("_Smoothness", 0.3f);
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", Color.black);
                EditorUtility.SetDirty(mat);
                imp.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), matName), mat);
                report.Append($"\n    material '{matName}' -> base={Path.GetFileName(baseTex)} normal={Path.GetFileName(norTex)}");
            }
            imp.SaveAndReimport();

            var clips = AssetDatabase.LoadAllAssetsAtPath(mp).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview")).ToList();
            if (!human) BuildGenericController(type, clips);
            report.Append($"\n  {type}: {Path.GetFileName(mp)} humanoid={human} clips=[{string.Join(", ", clips.Select(c => c.name))}]");
        }
        AssetDatabase.SaveAssets();
        Debug.Log(report.ToString());
    }

    static bool IsNormal(string n)
    {
        n = n.ToLowerInvariant();
        return n.Contains("normal") || n.EndsWith("_n") || n.Contains("_n_") || n.EndsWith("nor");
    }

    static string Sanitize(string s) => string.Concat(s.Select(ch => char.IsLetterOrDigit(ch) ? ch : '_'));

    static string PickTexture(System.Collections.Generic.List<string> paths, string matName, int matCount, bool normal)
    {
        var candidates = paths.Where(p => IsNormal(Path.GetFileNameWithoutExtension(p)) == normal).ToList();
        if (!normal)
        {
            var colour = candidates.Where(p => { var n = Path.GetFileNameWithoutExtension(p).ToLowerInvariant();
                return !(n.Contains("specular") || n.Contains("metal") || n.Contains("rough") || n.Contains("_ao") || n.Contains("occlusion") || n.Contains("gloss")); }).ToList();
            if (colour.Count > 0) candidates = colour;
        }
        if (candidates.Count == 0) return null;
        string key = Sanitize(matName).ToLowerInvariant();
        // Exact-ish match on the material name.
        var hit = candidates.FirstOrDefault(p => Sanitize(Path.GetFileNameWithoutExtension(p)).ToLowerInvariant().Contains(key));
        if (hit != null) return hit;
        // Best match by shared keywords (bow, arrow, pants, silver, 01, 02 ...).
        var tokens = key.Split('_').Where(t => t.Length >= 2 && t != "inst" && t != "mat").ToArray();
        int best = 0;
        foreach (var p in candidates)
        {
            var fileTokens = Sanitize(Path.GetFileNameWithoutExtension(p)).ToLowerInvariant().Split('_');
            int score = tokens.Count(t => fileTokens.Contains(t));
            if (score > best) { best = score; hit = p; }
        }
        if (hit != null) return hit;
        // Single-material models or a body texture fallback.
        hit = candidates.FirstOrDefault(p => { var n = Path.GetFileNameWithoutExtension(p).ToLowerInvariant(); return n.Contains("body") || n.Contains("skeleton") || n.Contains("color"); });
        return hit ?? candidates[0];
    }

    /// <summary>For non-humanoid rigs: a controller using the model's own clips.</summary>
    static void BuildGenericController(string type, System.Collections.Generic.List<AnimationClip> clips)
    {
        AnimationClip Find(params string[] keys) =>
            clips.FirstOrDefault(c => keys.Any(k => c.name.ToLowerInvariant().Contains(k)));
        var set = new AnimatorFactory.ClipSet
        {
            idleClip = Find("idle", "stand", "wait") ?? clips.FirstOrDefault(),
            walkClip = Find("walk"),
            runClip = Find("run", "charge", "walk"),
            attackClips = clips.Where(c => c.name.ToLowerInvariant().Contains("attack") || c.name.ToLowerInvariant().Contains("skill")).Take(3).ToArray(),
            hitClip = Find("hit", "damage", "hurt", "stun"),
            deathClip = Find("death", "die", "dead"),
            walkSpeed = 1.5f, runSpeed = 3.2f, attackHitTime = 0.8f,
        };
        if (set.attackClips.Length == 0) set.attackClips = new[] { set.idleClip };
        AnimatorFactory.BuildWithClips(type + "_Generic", set);
    }

    public static bool IsHumanoid(GameObject modelAsset)
    {
        if (modelAsset == null) return false;
        var path = AssetDatabase.GetAssetPath(modelAsset);
        var av = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Avatar>().FirstOrDefault();
        return av != null && av.isValid && av.isHuman;
    }
}
#endif
