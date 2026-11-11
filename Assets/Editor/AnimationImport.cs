#if UNITY_EDITOR
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>Sets up the Quaternius animation libraries as Humanoid and lists their clips.</summary>
public static class AnimationImport
{
    public static readonly string[] Libraries =
    {
        "Assets/ThirdParty/Quaternius/AnimationLibrary_Unity_Standard.fbx",
        "Assets/ThirdParty/Quaternius/UAL2_Standard.fbx",
        "Assets/ThirdParty/Quaternius/Mannequin_F.fbx",
    };

    [MenuItem("Dungeon Escape/Setup Animation Libraries")]
    public static void Setup()
    {
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        foreach (var path in Libraries) AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        foreach (var path in Libraries)
        {
            var imp = AssetImporter.GetAtPath(path) as ModelImporter;
            if (imp == null) { Debug.LogWarning("[AnimationImport] missing " + path); continue; }
            imp.animationType = ModelImporterAnimationType.Human;
            imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            imp.importAnimation = true;
            imp.materialImportMode = ModelImporterMaterialImportMode.None;
            var clips = imp.defaultClipAnimations;
            foreach (var c in clips)
            {
                string n = c.name.ToLowerInvariant();
                bool loop = n.Contains("idle") || n.Contains("walk") || n.Contains("run") || n.Contains("jog") ||
                            n.Contains("sprint") || n.Contains("loop") || n.Contains("strafe") || n.Contains("crouch_fwd");
                c.loopTime = loop;
                c.lockRootRotation = true;
                c.lockRootHeightY = true;
                c.lockRootPositionXZ = true;
                c.keepOriginalOrientation = true;
                c.keepOriginalPositionY = true;
                c.keepOriginalPositionXZ = true;
            }
            imp.clipAnimations = clips;
            imp.SaveAndReimport();
            Debug.Log("[AnimationImport] " + Path.GetFileName(path) + " avatarValid=" +
                      (AssetDatabase.LoadAllAssetsAtPath(path).OfType<Avatar>().FirstOrDefault()?.isHuman ?? false));
        }
        var sb = new System.Text.StringBuilder("[AnimationImport] CLIPS:");
        foreach (var path in Libraries)
            foreach (var clip in AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview")))
                sb.Append("\n  " + Path.GetFileNameWithoutExtension(path) + " :: " + clip.name + " (" + clip.length.ToString("0.00") + "s)");
        Debug.Log(sb.ToString());
    }
}
#endif
