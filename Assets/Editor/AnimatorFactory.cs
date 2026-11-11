#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Builds the Animator Controllers for the player and every enemy type from the
/// Quaternius Universal Animation Library clips (CC0). All characters are Humanoid,
/// so the same clips drive the knight, skeletons, mage, brute and bosses.
/// </summary>
public static class AnimatorFactory
{
    const string Dir = "Assets/Animation/Controllers";

    public class ClipSet
    {
        public string idle, walk, run, heavy, dodge, hit, hitHeavy, death, jump, consume;
        public string[] attacks;
        // Direct clips (used for models with their own Generic animations).
        public AnimationClip idleClip, walkClip, runClip, hitClip, deathClip;
        public AnimationClip[] attackClips;
        public float walkSpeed = 1.6f, runSpeed = 4f;
        public float attackSpeed = 1f;
        [Tooltip("Enemies: time from trigger to impact; clip speed is fitted to it.")] public float attackHitTime = 0f;
    }

    static Dictionary<string, AnimationClip> clipCache;

    public static AnimationClip Clip(string shortName)
    {
        if (string.IsNullOrEmpty(shortName)) return null;
        if (clipCache == null)
        {
            clipCache = new Dictionary<string, AnimationClip>();
            foreach (var path in AnimationImport.Libraries)
                foreach (var c in AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>())
                {
                    if (c.name.StartsWith("__preview")) continue;
                    string key = c.name.Contains("|") ? c.name.Substring(c.name.IndexOf('|') + 1) : c.name;
                    clipCache[key] = c;
                }
        }
        clipCache.TryGetValue(shortName, out var clip);
        if (clip == null) Debug.LogWarning("[AnimatorFactory] clip not found: " + shortName);
        return clip;
    }

    [MenuItem("Dungeon Escape/Build Animator Controllers")]
    public static void BuildAll()
    {
        clipCache = null;
        Directory.CreateDirectory(Dir);
        Build("Player", new ClipSet
        {
            idle = "Sword_Idle", walk = "Jog_Fwd_Loop", run = "Sprint_Loop",
            attacks = new[] { "Sword_Regular_A", "Sword_Regular_B", "Sword_Regular_C" },
            heavy = "Sword_Attack", dodge = "Roll", hit = "Hit_Chest", hitHeavy = "Hit_Knockback",
            death = "Death01", jump = "Jump_Loop", consume = "Consume", walkSpeed = 4f, runSpeed = 7f,
        });
        Build("SkeletonWarrior", new ClipSet
        {
            idle = "Sword_Idle", walk = "Walk_Loop", run = "Jog_Fwd_Loop",
            attacks = new[] { "Sword_Regular_A" }, hit = "Hit_Chest", death = "Death01", runSpeed = 3.6f, attackHitTime = 0.55f,
        });
        Build("SkeletonArcher", new ClipSet
        {
            idle = "Idle_Loop", walk = "Walk_Loop", run = "Jog_Fwd_Loop",
            attacks = new[] { "Spell_Simple_Shoot" }, hit = "Hit_Chest", death = "Death01", runSpeed = 3.6f, attackHitTime = 0.8f,
        });
        Build("DarkMage", new ClipSet
        {
            idle = "Spell_Simple_Idle_Loop", walk = "Walk_Formal_Loop", run = "Jog_Fwd_Loop",
            attacks = new[] { "Spell_Simple_Shoot" }, hit = "Hit_Head", death = "Death01", runSpeed = 3f, attackHitTime = 1.0f,
        });
        Build("CursedBrute", new ClipSet
        {
            idle = "Zombie_Idle_Loop", walk = "Zombie_Walk_Fwd_Loop", run = "Zombie_Walk_Fwd_Loop",
            attacks = new[] { "Zombie_Scratch" }, hit = "Hit_Knockback", death = "Death01", walkSpeed = 1.2f, runSpeed = 2.8f, attackHitTime = 0.9f,
        });
        Build("ForgottenKing", new ClipSet
        {
            idle = "Sword_Idle", walk = "Walk_Loop", run = "Jog_Fwd_Loop",
            attacks = new[] { "Sword_Regular_Combo", "Sword_Attack", "Spell_Simple_Shoot", "Shield_Dash_RM" },
            hit = "Hit_Chest", death = "Death01", runSpeed = 3.4f, attackHitTime = 0.7f,
        });
        Build("ChainedHorror", new ClipSet
        {
            idle = "Zombie_Idle_Loop", walk = "Zombie_Walk_Fwd_Loop", run = "Zombie_Walk_Fwd_Loop",
            attacks = new[] { "Zombie_Scratch", "Melee_Hook" }, hit = "Hit_Knockback", death = "Death01", runSpeed = 3f, attackHitTime = 0.8f,
        });
        Build("Overlord", new ClipSet
        {
            idle = "Sword_Idle", walk = "Walk_Loop", run = "Jog_Fwd_Loop",
            attacks = new[] { "Sword_Attack", "Sword_Regular_Combo", "Shield_Dash_RM" },
            hit = "Hit_Chest", death = "Death01", runSpeed = 3.6f, attackHitTime = 0.7f,
        });
        Build("Merchant", new ClipSet { idle = "Idle_Talking_Loop", walk = "Walk_Loop", run = "Walk_Loop", attacks = new string[0], hit = "Hit_Chest", death = "Death01" });
        AssetDatabase.SaveAssets();
        Debug.Log("[AnimatorFactory] Built controllers in " + Dir);
    }

    public static AnimatorController Load(string name) =>
        AssetDatabase.LoadAssetAtPath<AnimatorController>(Dir + "/" + name + ".controller");

    public static AnimatorController BuildWithClips(string name, ClipSet set)
    {
        Directory.CreateDirectory(Dir);
        return Build(name, set);
    }

    static AnimatorController Build(string name, ClipSet set)
    {
        var idleC = set.idleClip ?? Clip(set.idle);
        var walkC = set.walkClip ?? Clip(set.walk) ?? idleC;
        var runC = set.runClip ?? Clip(set.run) ?? walkC;
        var hitC = set.hitClip ?? Clip(set.hit);
        var deathC = set.deathClip ?? Clip(set.death);
        var attackC = set.attackClips ?? (set.attacks ?? new string[0]).Select(Clip).ToArray();
        string path = Dir + "/" + name + ".controller";
        AssetDatabase.DeleteAsset(path);
        var ac = AnimatorController.CreateAnimatorControllerAtPath(path);
        ac.AddParameter("Speed", AnimatorControllerParameterType.Float);
        ac.AddParameter("Grounded", AnimatorControllerParameterType.Bool);
        ac.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
        ac.AddParameter("Combo", AnimatorControllerParameterType.Int);
        ac.AddParameter("HeavyAttack", AnimatorControllerParameterType.Trigger);
        ac.AddParameter("Dodge", AnimatorControllerParameterType.Trigger);
        ac.AddParameter("Jump", AnimatorControllerParameterType.Trigger);
        ac.AddParameter("Hit", AnimatorControllerParameterType.Trigger);
        ac.AddParameter("Die", AnimatorControllerParameterType.Trigger);
        ac.AddParameter("Consume", AnimatorControllerParameterType.Trigger);
        ac.parameters = ac.parameters.Select(p => { if (p.name == "Grounded") p.defaultBool = true; return p; }).ToArray();

        var sm = ac.layers[0].stateMachine;
        sm.entryPosition = new Vector3(0, 0);
        sm.anyStatePosition = new Vector3(0, 200);

        // Locomotion blend tree driven by Speed (m/s).
        var loco = ac.CreateBlendTreeInController("Locomotion", out BlendTree tree, 0);
        tree.blendType = BlendTreeType.Simple1D;
        tree.blendParameter = "Speed";
        tree.useAutomaticThresholds = false;
        tree.AddChild(idleC, 0f);
        tree.AddChild(walkC, set.walkSpeed);
        tree.AddChild(runC, set.runSpeed);
        sm.defaultState = loco;

        // Attacks (Combo int picks which one).
        for (int i = 0; i < attackC.Length; i++)
        {
            var clip = attackC[i];
            float speed = set.attackSpeed;
            if (clip != null && set.attackHitTime > 0f) speed = clip.length / (set.attackHitTime + 0.25f);
            else if (clip != null && clip.length > 1.2f) speed = clip.length / 1.1f; // long finishers play faster
            var st = OneShot(sm, "Attack" + i, clip, loco, speed, new Vector3(400, -150 + i * 60));
            var t = sm.AddAnyStateTransition(st);
            t.AddCondition(AnimatorConditionMode.If, 0, "Attack");
            if (attackC.Length > 1) t.AddCondition(AnimatorConditionMode.Equals, i, "Combo");
            t.duration = 0.08f;
            t.canTransitionToSelf = true;
        }

        if (set.heavy != null)
        {
            var heavy = OneShot(sm, "Heavy", Clip(set.heavy), loco, 1.15f, new Vector3(400, 120));
            var t = sm.AddAnyStateTransition(heavy);
            t.AddCondition(AnimatorConditionMode.If, 0, "HeavyAttack");
            t.duration = 0.1f;
        }
        if (set.dodge != null)
        {
            var dodge = OneShot(sm, "Dodge", Clip(set.dodge), loco, 2.4f, new Vector3(400, 180));
            var t = sm.AddAnyStateTransition(dodge);
            t.AddCondition(AnimatorConditionMode.If, 0, "Dodge");
            t.duration = 0.05f;
        }
        if (set.consume != null)
        {
            var drink = OneShot(sm, "Consume", Clip(set.consume), loco, 1.4f, new Vector3(400, 240));
            var t = sm.AddAnyStateTransition(drink);
            t.AddCondition(AnimatorConditionMode.If, 0, "Consume");
            t.duration = 0.1f;
        }
        if (set.jump != null)
        {
            var jump = sm.AddState("Jump", new Vector3(650, 0));
            jump.motion = Clip(set.jump);
            var t = sm.AddAnyStateTransition(jump);
            t.AddCondition(AnimatorConditionMode.If, 0, "Jump");
            t.duration = 0.1f;
            var back = jump.AddTransition(loco);
            back.AddCondition(AnimatorConditionMode.If, 0, "Grounded");
            back.hasExitTime = true;
            back.exitTime = 0.35f;
            back.duration = 0.15f;
        }

        var hit = OneShot(sm, "Hit", hitC, loco, 1.2f, new Vector3(650, 120));
        var th = sm.AddAnyStateTransition(hit);
        th.AddCondition(AnimatorConditionMode.If, 0, "Hit");
        th.duration = 0.05f;

        var death = sm.AddState("Death", new Vector3(650, 240));
        death.motion = deathC;
        var td = sm.AddAnyStateTransition(death);
        td.AddCondition(AnimatorConditionMode.If, 0, "Die");
        td.duration = 0.1f;
        td.canTransitionToSelf = false;

        EditorUtility.SetDirty(ac);
        return ac;
    }

    static AnimatorState OneShot(AnimatorStateMachine sm, string name, AnimationClip clip, AnimatorState back, float speed, Vector3 pos)
    {
        var st = sm.AddState(name, pos);
        st.motion = clip;
        st.speed = speed;
        var t = st.AddTransition(back);
        t.hasExitTime = true;
        t.exitTime = 0.9f;
        t.duration = 0.12f;
        return st;
    }
}
#endif
