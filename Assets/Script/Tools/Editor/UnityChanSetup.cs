using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Seed.Tools.Editor
{
    /// <summary>
    /// UnityChan をデモのプレイヤーモデルとして使うための一括セットアップ。
    ///
    /// メニュー **Seed/Setup/Build UnityChan Player Prefab** を1回実行すると:
    /// 1. 素の FBX モデル（スクリプト非依存）から実体を組む
    /// 2. 同梱マテリアルを **URP 用に変換**した複製を作って差し替える
    ///    （同梱トゥーンシェーダーはビルトインRP用のため、URP ではピンクになる）
    /// 3. ボディモーション6本を **正規名（Idle/Locomotion/…）で Resources へ複製**する
    ///    （FBX 内蔵クリップはそのままではビルドから参照できないため）
    /// 4. `Assets/Resources/PlayerModel.prefab` として保存する
    ///
    /// 実行時（Sample_PlayerModel）はこの成果物を読む。再実行すれば作り直せる（冪等）。
    /// </summary>
    public static class UnityChanSetup
    {
        /// <summary>元モデル（スクリプトを含まない FBX を使う＝欠落コンポーネントが出ない）。</summary>
        private const string SourceModelPath = "Assets/UnityChan/Models/unitychan.fbx";

        /// <summary>ボディモーション FBX の置き場。</summary>
        private const string AnimationFolder = "Assets/UnityChan/Animations/";

        /// <summary>URP 変換済みマテリアルの出力先。</summary>
        private const string MaterialFolder = "Assets/UnityChan/URP_Materials";

        /// <summary>プレハブの出力先（Sample_PlayerModel が Resources から読む名前）。</summary>
        private const string PrefabPath = "Assets/Resources/PlayerModel.prefab";

        /// <summary>クリップ複製の出力先。</summary>
        private const string ClipFolder = "Assets/Resources/PlayerClips";

        /// <summary>行動キー正規名 → UnityChan のクリップ（FBXファイル名の unitychan_ 以降）。</summary>
        private static readonly (string Key, string File)[] ClipMap =
        {
            ("Idle", "WAIT00"),        // 待機
            ("Locomotion", "RUN00_F"), // 移動（MoveSpeed 4m/s は走りが合う）
            ("Attack", "SLIDE00"),     // 攻撃の代役=滑り込み（突進攻撃に見える。専用クリップが入ったら差し替え）
            ("Guard", "WAIT01"),       // ガードの代役
            ("Hit", "DAMAGED00"),      // 被弾
            ("Death", "LOSE00"),       // 敗北（非ループ＝最終ポーズで止まる）
            ("Walk", "WALK00_F"),      // 低速時の歩き（走りとの切替は App が速度で選ぶ）
            ("Jump", "JUMP01"),        // ジャンプ滞空
            ("Win", "WIN00"),          // 勝利ポーズ（討伐成功時）
        };

        /// <summary>アルファ抜きが必要なマテリアル名（目のライン・頬など）。</summary>
        private static readonly HashSet<string> CutoutMaterials = new HashSet<string>
        {
            "eyeline", "mat_cheek", "eyelash",
        };

        /// <summary>セットアップを実行する。</summary>
        [MenuItem("Seed/Setup/Build UnityChan Player Prefab")]
        public static void Build()
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(SourceModelPath);
            if (source == null)
            {
                Debug.LogError($"[UnityChanSetup] モデルが見つからない: {SourceModelPath}");
                return;
            }

            EnsureFolder("Assets/Resources");
            EnsureFolder(ClipFolder);
            EnsureFolder(MaterialFolder);

            // 1. 実体化してマテリアルを URP 変換品へ差し替える
            var temp = Object.Instantiate(source);
            try
            {
                var converted = 0;
                foreach (var renderer in temp.GetComponentsInChildren<Renderer>(true))
                {
                    var materials = renderer.sharedMaterials;
                    for (var i = 0; i < materials.Length; i++)
                    {
                        if (materials[i] != null)
                        {
                            materials[i] = GetOrCreateUrpMaterial(materials[i]);
                            converted++;
                        }
                    }
                    renderer.sharedMaterials = materials;
                }

                // 2. クリップを正規名で複製（ビルド同梱＋名前の安定化）
                var clips = 0;
                foreach (var (key, file) in ClipMap)
                {
                    if (ExportClip(key, file))
                    {
                        clips++;
                    }
                }

                // 3. プレハブ保存（再実行時は上書き＝冪等）
                PrefabUtility.SaveAsPrefabAsset(temp, PrefabPath);
                AssetDatabase.SaveAssets();
                Debug.Log($"[UnityChanSetup] 完了: {PrefabPath}（マテリアル差替 {converted} 箇所・"
                    + $"クリップ {clips}/{ClipMap.Length} 本）。Play すればプレイヤーが UnityChan になります");
            }
            finally
            {
                Object.DestroyImmediate(temp);
            }
        }

        /// <summary>URP 変換済みマテリアルを取得する（無ければ作る）。</summary>
        private static Material GetOrCreateUrpMaterial(Material sourceMaterial)
        {
            var path = $"{MaterialFolder}/{sourceMaterial.name}_URP.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                return existing;
            }

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                Debug.LogWarning("[UnityChanSetup] URP/Lit が見つからないため元マテリアルを維持");
                return sourceMaterial;
            }

            var material = new Material(shader) { name = sourceMaterial.name + "_URP" };
            if (sourceMaterial.HasProperty("_MainTex"))
            {
                material.SetTexture("_BaseMap", sourceMaterial.GetTexture("_MainTex"));
            }
            if (sourceMaterial.HasProperty("_Color"))
            {
                material.SetColor("_BaseColor", sourceMaterial.GetColor("_Color"));
            }
            // トゥーン調の質感に寄せる（テカりを消す）
            material.SetFloat("_Smoothness", 0.05f);
            material.SetFloat("_Metallic", 0f);

            if (CutoutMaterials.Contains(sourceMaterial.name))
            {
                material.SetFloat("_AlphaClip", 1f);
                material.SetFloat("_Cutoff", 0.35f);
                material.EnableKeyword("_ALPHATEST_ON");
            }

            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        /// <summary>FBX 内蔵クリップを正規名で Resources へ複製する。</summary>
        private static bool ExportClip(string key, string file)
        {
            var fbxPath = $"{AnimationFolder}unitychan_{file}.fbx";
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
                Debug.LogWarning($"[UnityChanSetup] クリップが見つからない: {fbxPath}");
                return false;
            }
            var copy = Object.Instantiate(clip);
            copy.name = key;
            var path = $"{ClipFolder}/{key}.anim";
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(copy, path);
            return true;
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
