using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// One-time (idempotent) generator for the Unity-chan AR integration:
//  - builds Assets/Resources/UnityChan/UnityChanOverride.overrideController
//    (the Cactus animator with Idle->FUC Idle, Attack->FUC Jab overrides)
//  - builds Assets/Resources/UnityChan/UnityChanAR.prefab
//    (Unity-chan model + Animator + avatar + the override controller)
// Run via menu "Tools/Unity-chan/Build AR Prefab", or automatically in
// batch mode through UnityChanSetupBuilder.BuildFromMenu.
public static class UnityChanSetupBuilder
{
    private const string ModelsPath = "Assets/UnityChan/Models/unitychan.fbx";
    private const string IdleFbx = "Assets/UnityChan/Animations/FUCM05_0000_Idle.fbx";
    private const string JabFbx = "Assets/UnityChan/Animations/FUCM05_0001_M_CMN_LJAB.fbx";
    private const string CactusController = "Assets/CactusAnimator.controller";
    private const string OutputDir = "Assets/Resources/UnityChan";
    private const string OverridePath = OutputDir + "/UnityChanOverride.overrideController";
    private const string PrefabPath = OutputDir + "/UnityChanAR.prefab";

    [MenuItem("Tools/Unity-chan/Build AR Prefab")]
    public static void BuildFromMenu()
    {
        BuildAll(force: true);
        if (Application.isBatchMode) EditorApplication.Exit(0);
    }

    public static void BuildIfNeeded()
    {
        BuildAll(force: false);
    }

    private static void BuildAll(bool force)
    {
        if (!force && File.Exists(PrefabPath) && File.Exists(OverridePath))
        {
            Debug.Log("[UnityChanSetupBuilder] Outputs already exist, skipping.");
            return;
        }

        if (!File.Exists(ModelsPath) || !File.Exists(IdleFbx) || !File.Exists(JabFbx) || !File.Exists(CactusController))
        {
            Debug.LogError("[UnityChanSetupBuilder] Missing source assets; check imports.");
            if (Application.isBatchMode) EditorApplication.Exit(1);
            return;
        }

        Directory.CreateDirectory(OutputDir);

        // 1) Resolve which clip the cactus states actually use, by walking the
        //    controller states (both cactus clips are named "Take 001", so
        //    name-based matching is impossible).
        AnimationClip cactusIdleClip = null, cactusAttackClip = null;
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(CactusController);
        if (controller == null)
        {
            Debug.LogError("[UnityChanSetupBuilder] Could not load " + CactusController);
            if (Application.isBatchMode) EditorApplication.Exit(1);
            return;
        }
        foreach (AnimatorControllerLayer layer in controller.layers)
        {
            WalkStateMachine(layer.stateMachine, ref cactusIdleClip, ref cactusAttackClip);
        }
        if (cactusIdleClip == null || cactusAttackClip == null)
        {
            Debug.LogError("[UnityChanSetupBuilder] Cactus states Idle/Attack not found. idle=" +
                (cactusIdleClip != null) + " attack=" + (cactusAttackClip != null));
            if (Application.isBatchMode) EditorApplication.Exit(1);
            return;
        }

        AnimationClip ucIdle = FindClip(IdleFbx, "idle");
        AnimationClip ucJab = FindClip(JabFbx, "jab");
        if (ucIdle == null || ucJab == null)
        {
            Debug.LogError("[UnityChanSetupBuilder] Could not load FUC clips (Idle/Jab).");
            if (Application.isBatchMode) EditorApplication.Exit(1);
            return;
        }

        // 2) Override controller: inherit the cactus animator, swap its clips.
        var overrides = new AnimatorOverrideController(controller);
        var pairs = new List<KeyValuePair<AnimationClip, AnimationClip>>();
        overrides.GetOverrides(pairs);

        bool idleMapped = false, attackMapped = false;
        for (int i = 0; i < pairs.Count; i++)
        {
            if (pairs[i].Key == cactusIdleClip)
            {
                pairs[i] = new KeyValuePair<AnimationClip, AnimationClip>(pairs[i].Key, ucIdle);
                idleMapped = true;
            }
            else if (pairs[i].Key == cactusAttackClip)
            {
                pairs[i] = new KeyValuePair<AnimationClip, AnimationClip>(pairs[i].Key, ucJab);
                attackMapped = true;
            }
        }
        overrides.ApplyOverrides(pairs);
        AssetDatabase.CreateAsset(overrides, OverridePath);
        Debug.Log("[UnityChanSetupBuilder] Override controller created. idleMapped=" + idleMapped +
                  " attackMapped=" + attackMapped);

        // 3) Prefab: model root + Animator (avatar from FBX) + override controller.
        var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(ModelsPath);
        if (fbx == null)
        {
            Debug.LogError("[UnityChanSetupBuilder] unitychan.fbx failed to load.");
            if (Application.isBatchMode) EditorApplication.Exit(1);
            return;
        }

        Avatar avatar = null;
        foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(ModelsPath))
        {
            avatar = asset as Avatar;
            if (avatar != null) break;
        }

        GameObject instance = Object.Instantiate(fbx); // standalone copy -> materials get embedded


        Animator animator = instance.GetComponent<Animator>();
        if (animator == null) animator = instance.AddComponent<Animator>();
        if (avatar != null) animator.avatar = avatar;
        animator.runtimeAnimatorController =
            AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(OverridePath);
        animator.applyRootMotion = false;

        // Scale Unity-chan down so she matches the cactus scale in the AR scene.
        instance.transform.localScale = Vector3.one * 0.4f;

        PrefabUtility.SaveAsPrefabAsset(instance, PrefabPath);
        Object.DestroyImmediate(instance);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[UnityChanSetupBuilder] Created " + PrefabPath + " (avatar=" + (avatar != null) + ")");
    }

    private static void WalkStateMachine(AnimatorStateMachine stateMachine,
        ref AnimationClip idleClip, ref AnimationClip attackClip)
    {
        if (stateMachine == null) return;
        foreach (ChildAnimatorState child in stateMachine.states)
        {
            AnimatorState state = child.state;
            if (state == null) continue;
            AnimationClip clip = state.motion as AnimationClip;
            if (clip == null) continue;
            if (state.name == "Idle") idleClip = clip;
            else if (state.name == "Attack") attackClip = clip;
        }
        foreach (ChildAnimatorStateMachine child in stateMachine.stateMachines)
        {
            WalkStateMachine(child.stateMachine, ref idleClip, ref attackClip);
        }
    }

    private static AnimationClip FindClip(string fbxPath, string namePart)
    {
        foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(fbxPath))
        {
            AnimationClip clip = asset as AnimationClip;
            if (clip != null && clip.name.ToLowerInvariant().Contains(namePart))
                return clip;
        }
        return null;
    }
}
