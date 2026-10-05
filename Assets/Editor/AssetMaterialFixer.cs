using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// Material/prefab fixer + verifier. Writes AssetFixResult.txt; exits 0/1 in batch mode.
// Materials are saved as real .mat ASSETS (Assets/Resources/UnityChan/Materials) so the
// prefab references survive serialization and ship in builds.
public static class AssetMaterialFixer
{
    private static readonly Dictionary<string, string> TextureBySlot = new Dictionary<string, string>
    {
        { "body",     "body_01" },
        { "face",     "face_00" },
        { "hair",     "hair_01" },
        { "eyebrow",  "hair_01" },
        { "skin",     "skin_01" },
        { "eye_l1",   "eye_iris_L_00" },
        { "eye_r1",   "eye_iris_R_00" },
        { "eyeline",  "eyeline_00" },
        { "mat_cheek","cheek_00" },
        { "cheek",    "cheek_00" },
    };

    private const string PrefabPath = "Assets/Resources/UnityChan/UnityChanAR.prefab";
    private const string MatsDir = "Assets/Resources/UnityChan/Materials";
    private const string ResultFile = "AssetFixResult.txt";
    private static readonly List<string> Notes = new List<string>();

    public static void RunAndVerify()
    {
        var problems = new List<string>();

        // ---------- 0) Ground truth ----------
        Notes.Add("textures=" + AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/UnityChan" }).Length);
        Notes.Add("models=" + AssetDatabase.FindAssets("t:Model", new[] { "Assets/UnityChan" }).Length);

        // ---------- 1) Cactus materials -> URP ----------
        int cactusUrp = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets/Character_Cactus" }))
        {
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
            if (mat == null) continue;
            if (mat.shader == null || !mat.shader.name.Contains("Universal Render Pipeline"))
            {
                Texture mainTex = mat.HasProperty("_MainTex") ? mat.GetTexture("_MainTex") : null;
                Color color = mat.HasProperty("_Color") ? mat.GetColor("_Color") : Color.white;
                mat.shader = Shader.Find("Universal Render Pipeline/Lit");
                if (mainTex != null) mat.SetTexture("_BaseMap", mainTex);
                mat.SetColor("_BaseColor", color);
                EditorUtility.SetDirty(mat);
            }
            cactusUrp++;
        }
        AssetDatabase.SaveAssets();
        Notes.Add("cactusUrp=" + cactusUrp);

        // ---------- 2) Generic rig for the model ----------
        var importer = AssetImporter.GetAtPath("Assets/UnityChan/Models/unitychan.fbx") as ModelImporter;
        if (importer != null && importer.animationType != ModelImporterAnimationType.Generic)
        {
            Notes.Add("rigWas=" + importer.animationType);
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.SaveAndReimport();
        }

        // ---------- 3) Rebuild prefab (standalone copy) ----------
        File.Delete(PrefabPath);
        if (File.Exists(PrefabPath + ".meta")) File.Delete(PrefabPath + ".meta");
        AssetDatabase.Refresh();
        UnityChanSetupBuilder.BuildIfNeeded();

        // ---------- 4) Create material ASSETS + assign to prefab ----------
        GameObject fbx = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/UnityChan/Models/unitychan.fbx");
        if (fbx == null) problems.Add("model not loadable");
        if (fbx != null)
        {
            if (!Directory.Exists(MatsDir)) Directory.CreateDirectory(MatsDir);
            // clean stale material assets
            foreach (string f in Directory.GetFiles(MatsDir, "*.mat"))
            {
                AssetDatabase.DeleteAsset(f.Replace('\\', '/'));
            }

            GameObject instance = Object.Instantiate(fbx);

            // Avatar: log what the FBX actually contains
            int avatarCount = 0;
            foreach (Object o in AssetDatabase.LoadAllAssetsAtPath("Assets/UnityChan/Models/unitychan.fbx"))
            {
                if (o is Avatar) { avatarCount++; Notes.Add("avatarAssetFound"); }
            }
            Notes.Add("avatarCount=" + avatarCount);
            Animator anim = instance.GetComponent<Animator>();
            if (anim == null) anim = instance.AddComponent<Animator>();
            if (avatarCount > 0)
            {
                foreach (Object o in AssetDatabase.LoadAllAssetsAtPath("Assets/UnityChan/Models/unitychan.fbx"))
                {
                    Avatar av = o as Avatar;
                    if (av != null) { anim.avatar = av; break; }
                }
            }
            anim.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                "Assets/Resources/UnityChan/UnityChanOverride.overrideController");
            anim.applyRootMotion = false;
            instance.transform.localScale = Vector3.one * 0.15f;

            int created = 0;
            foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                var newMats = new List<Material>();
                foreach (Material mat in renderer.sharedMaterials)
                {
                    if (mat == null) { newMats.Add(null); continue; }

                    string slot = mat.name.Trim().ToLowerInvariant();
                    Texture tex = null;
                    foreach (var pair in TextureBySlot)
                    {
                        if (!slot.Contains(pair.Key)) continue;
                        if (!string.IsNullOrEmpty(pair.Value)) tex = LoadTex(pair.Value);
                        break;
                    }

                    string assetName = Sanitize(mat.name) + "_URP.mat";
                    string assetPath = MatsDir + "/" + assetName;
                    Material saved = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
                    if (saved == null)
                    {
                        saved = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                        if (tex != null) { saved.mainTexture = tex; saved.color = Color.white; }
                        else saved.color = new Color(0.85f, 0.85f, 0.85f, 1f);
                        saved.SetFloat("_Smoothness", 0.05f);
                        saved.name = Sanitize(mat.name) + "_URP";
                        AssetDatabase.CreateAsset(saved, assetPath);
                        created++;
                    }
                    newMats.Add(saved);
                }
                renderer.sharedMaterials = newMats.ToArray();
            }
            AssetDatabase.SaveAssets();
            Notes.Add("matAssetsCreated=" + created);

            PrefabUtility.SaveAsPrefabAsset(instance, PrefabPath);
            Object.DestroyImmediate(instance);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        // ---------- 5) Honest verify ----------
        int renderersChecked = 0, matsOk = 0;
        GameObject check = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (check == null) problems.Add("prefab missing after save");
        else
        {
            foreach (Renderer renderer in check.GetComponentsInChildren<Renderer>(true))
            {
                renderersChecked++;
                foreach (Material mat in renderer.sharedMaterials)
                {
                    if (mat == null) { problems.Add("NULL mat on " + renderer.name); continue; }
                    bool urp = mat.shader != null && mat.shader.name.Contains("Universal Render Pipeline");
                    bool textured = mat.mainTexture != null;
                    if (!urp) { problems.Add("not URP: " + mat.name); continue; }
                    if (!textured && !SlotMayBeTexless(mat.name)) { problems.Add("no tex: " + mat.name); continue; }
                    matsOk++;
                }
            }
        }
        Notes.Add("renderers=" + renderersChecked + " matsOk=" + matsOk);

        string report = "ASSETFIX " + string.Join(" ", Notes.ToArray()) +
                        (problems.Count == 0 ? " problems=NONE" : " problems=" + problems.Count + ":" + string.Join(" | ", problems.ToArray()));
        File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), ResultFile), report);
        Debug.Log(report);

        if (Application.isBatchMode)
            EditorApplication.Exit(problems.Count == 0 ? 0 : 1);
    }

    private static bool SlotMayBeTexless(string matName)
    {
        string n = matName.ToLowerInvariant();
        return n.Contains("eyebase") || n.Contains("ground");
    }

    private static Texture LoadTex(string name)
    {
        Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/UnityChan/Textures/" + name + ".tga");
        if (tex == null) tex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/UnityChan/Textures/" + name + ".png");
        return tex;
    }

    private static string Sanitize(string name)
    {
        var sb = new System.Text.StringBuilder();
        foreach (char c in name)
            sb.Append(char.IsLetterOrDigit(c) || c == '_' ? c : '_');
        return sb.ToString();
    }
}
