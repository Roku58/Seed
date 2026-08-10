using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Seed.Tools.Editor
{
    /// <summary>
    /// StarterAssets の Armature を標準プレイヤーとして使うための一括セットアップ。
    ///
    /// メニュー **Seed/Setup/Build StarterAssets Player** を1回実行すると:
    /// 1. 素の FBX モデル（スクリプト非依存）を `Assets/Resources/StarterPlayer.prefab` へ複製
    /// 2. ボディモーション10本を正規名で `Assets/Resources/StarterClips/` へ複製
    ///    （移動系は StarterAssets、戦闘系は Grruzam Powerful Sword Animation の大剣（M_Big_Sword）から。
    ///    ループ設定はここで明示的に整える＝取り込み設定に依存しない）
    /// 3. **遷移なしの AnimatorController** を `Assets/Resources/StarterAnimator.controller` へ生成
    ///    （Idle / Jump / InAir / Attack / Guard / Hit / Death / Win のステート＋
    ///    `Speed` の1Dブレンドツリー Locomotion。遷移はコードが CrossFade で命じる
    ///    ——CODING_STANDARDS §6 の3条件の実例）
    ///
    /// 実行時（Sample_StarterPlayerModel）はこの成果物を読む。再実行すれば作り直せる（冪等）。
    /// </summary>
    public static class StarterAssetsSetup
    {
        /// <summary>元モデル（スクリプトを含まない FBX）。</summary>
        private const string SourceModelPath =
            "Assets/StarterAssets/ThirdPersonController/Character/Models/Armature.fbx";

        /// <summary>移動系アニメーション FBX の置き場（StarterAssets）。</summary>
        private const string StarterAnim =
            "Assets/StarterAssets/ThirdPersonController/Character/Animations/";

        /// <summary>戦闘系アニメーション FBX の置き場（Grruzam・大剣セット）。</summary>
        private const string SwordAnim =
            "Assets/Grruzam Powerful Sword Animation(Great Sword, Katana)/Animation/M_Big_Sword/";

        /// <summary>プレハブの出力先。</summary>
        private const string PrefabPath = "Assets/Resources/StarterPlayer.prefab";

        /// <summary>クリップ複製の出力先。</summary>
        private const string ClipFolder = "Assets/Resources/StarterClips";

        /// <summary>Controller の出力先。</summary>
        private const string ControllerPath = "Assets/Resources/StarterAnimator.controller";

        /// <summary>正規名 → クリップの取り出し元（フルパス）とループ設定。</summary>
        private static readonly (string Key, string Path, bool Loop)[] ClipMap =
        {
            ("Idle", StarterAnim + "Stand--Idle.anim.fbx", true),
            ("Walk", StarterAnim + "Locomotion--Walk_N.anim.fbx", true),
            ("Run", StarterAnim + "Locomotion--Run_N.anim.fbx", true),
            ("Jump", StarterAnim + "Jump--Jump.anim.fbx", false),   // 踏み切り（非ループ）
            ("InAir", StarterAnim + "Jump--InAir.anim.fbx", true),  // 滞空ループ
            ("Attack", SwordAnim + "2_Attacks/5__Upper_Attack/M_Big_Sword@UpperAttack_ZeroHeight.FBX", false),
            ("Guard", SwordAnim + "5_Revenges/Guard_Revenges/M_Big_Sword@Revenge_Guard_Loop.FBX", true),
            ("Hit", SwordAnim + "4_Damages/1__Front/M_Big_Sword@Damage_Front_Small_ver_A.FBX", false),
            ("Death", SwordAnim + "4_Damages/6__Die/M_Big_Sword@Damage_Die.FBX", false),
            ("Win", SwordAnim + "1_Movements/0__Intro/M_Big_Sword@Intro.FBX", false),
        };

        /// <summary>セットアップを実行する。</summary>
        [MenuItem("Seed/Setup/Build StarterAssets Player")]
        public static void Build()
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(SourceModelPath);
            if (source == null)
            {
                Debug.LogError($"[StarterAssetsSetup] モデルが見つからない: {SourceModelPath}");
                return;
            }

            EnsureFolder("Assets/Resources");
            EnsureFolder(ClipFolder);

            // 1. プレハブ複製（素の FBX＝欠落コンポーネントが出ない）
            var temp = Object.Instantiate(source);
            try
            {
                PrefabUtility.SaveAsPrefabAsset(temp, PrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(temp);
            }

            // 2. クリップ複製（正規名＋ループ設定の明示）
            var clips = 0;
            foreach (var (key, path, loop) in ClipMap)
            {
                if (ExportClip(key, path, loop))
                {
                    clips++;
                }
            }

            // 3. 遷移なし Controller（ブレンドツリー入り）
            BuildController();

            AssetDatabase.SaveAssets();
            Debug.Log($"[StarterAssetsSetup] 完了: {PrefabPath}・クリップ {clips}/{ClipMap.Length} 本・"
                + $"{ControllerPath}。ホームから [1]〜[5] で出撃すると標準プレイヤーになります");
        }

        /// <summary>遷移なしの Controller を生成する（ステートとブレンドツリーの置き場）。</summary>
        private static void BuildController()
        {
            AssetDatabase.DeleteAsset(ControllerPath);
            var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            var machine = controller.layers[0].stateMachine;

            // Locomotion: Speed 0=待機 → 0.45=歩き → 1=走り の連続ブレンド
            var tree = new BlendTree
            {
                name = "Locomotion",
                blendParameter = "Speed",
                blendType = BlendTreeType.Simple1D,
                useAutomaticThresholds = false,
                hideFlags = HideFlags.HideInHierarchy,
            };
            AssetDatabase.AddObjectToAsset(tree, controller);
            AddTreeChild(tree, "Idle", 0f);
            AddTreeChild(tree, "Walk", 0.45f);
            AddTreeChild(tree, "Run", 1f);
            var locomotion = machine.AddState("Locomotion");
            locomotion.motion = tree;

            var idle = AddClipState(machine, "Idle");
            AddClipState(machine, "Jump");
            AddClipState(machine, "InAir");
            AddClipState(machine, "Attack");
            AddClipState(machine, "Guard");
            AddClipState(machine, "Hit");
            AddClipState(machine, "Death");
            AddClipState(machine, "Win");
            machine.defaultState = idle;
        }

        /// <summary>FBX 内蔵クリップを正規名・指定ループ設定で Resources へ複製する。</summary>
        private static bool ExportClip(string key, string fbxPath, bool loop)
        {
            AnimationClip clip = null;
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(fbxPath))
            {
                if (asset is AnimationClip candidate && !candidate.name.StartsWith("__"))
                {
                    clip = candidate;
                    break;
                }
            }
            if (clip == null)
            {
                Debug.LogWarning($"[StarterAssetsSetup] クリップが見つからない: {fbxPath}");
                return false;
            }
            var copy = Object.Instantiate(clip);
            copy.name = key;
            var settings = AnimationUtility.GetAnimationClipSettings(copy);
            settings.loopTime = loop;
            AnimationUtility.SetAnimationClipSettings(copy, settings);
            var path = $"{ClipFolder}/{key}.anim";
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(copy, path);
            return true;
        }

        /// <summary>複製済みクリップをブレンドツリーの子へ加える。</summary>
        private static void AddTreeChild(BlendTree tree, string clipName, float threshold)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>($"{ClipFolder}/{clipName}.anim");
            if (clip != null)
            {
                tree.AddChild(clip, threshold);
            }
        }

        /// <summary>複製済みクリップを1ステートとして置く。</summary>
        private static AnimatorState AddClipState(AnimatorStateMachine machine, string name)
        {
            var state = machine.AddState(name);
            state.motion = AssetDatabase.LoadAssetAtPath<AnimationClip>($"{ClipFolder}/{name}.anim");
            return state;
        }

        /// <summary>フォルダを用意する（親から順に）。</summary>
        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }
            var parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            var name = Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            {
                EnsureFolder(parent);
            }
            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
