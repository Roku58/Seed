using System.Collections.Generic;
using Seed.Character;
using Seed.Motion;
using UnityEngine;

namespace Seed.App
{
    /// <summary>
    /// 【サンプル】揺れものデモ用プレイヤー（UnityChan・Humanoid・Playables 直駆動）の表示の束。
    ///
    /// 標準プレイヤー（StarterAssets・Controller 駆動＝Sample_StarterPlayerModel）とは
    /// **完全に別のサンプル**——どちらを使うかはステージのマスターデータが決める
    /// （ホーム [6] の揺れものデモ出撃がこちら）。
    ///
    /// [役割] プレハブの発見 → RiggedAvatar の構成（Playables 直駆動＋クリップ台帳）→
    /// リグ装着に必要な Humanoid ボーンの抽出 → **揺れものチェーンの収集**、までを1か所に集める。
    /// モデルが見つからない環境（アセット未取得・CI）では null を返し、
    /// 呼び出し側（Sample_BattlePhase）が従来のカプセル表示へフォールバックする。
    ///
    /// [事前セットアップ] メニュー **Seed/Setup/Build UnityChan Player Prefab** を1回実行する
    /// （URP 用マテリアル変換＋クリップの正規名複製＋Resources/PlayerModel.prefab の生成）。
    /// 未実行でもエディタでは素の FBX で動くが、URP ではマテリアルがピンクになる。
    ///
    /// [揺れものについて] UnityChan の髪（HairTail/HairSide/HairFront）・リボン・スカート・袖は
    /// Humanoid クリップに焼かれていない揺れもの専用ボーン——Seed.Motion の SpringBoneRig が
    /// そのまま駆動できる（揺れは基盤の SpringBoneRig が担う）。
    /// </summary>
    public sealed class Sample_PlayerModel : Sample_IPlayerModel
    {
        /// <summary>セットアップ済みプレハブの Resources 名。</summary>
        private const string PrefabResourceName = "PlayerModel";

        /// <summary>クリップ複製の Resources フォルダ。</summary>
        private const string ClipResourceFolder = "PlayerClips/";

        /// <summary>未セットアップ時のフォールバック（素の FBX。エディタのみ）。</summary>
        private const string RawModelPath = "Assets/UnityChan/Models/unitychan.fbx";

        /// <summary>表示本体（IAvatar 実装。Behavior 遷移→クロスフェードの翻訳役）。</summary>
        public RiggedAvatar Avatar { get; private set; }

        /// <summary>共通契約: 表示本体。</summary>
        IAvatar Sample_IPlayerModel.Avatar => Avatar;

        /// <summary>共通契約: Playables の再生器。</summary>
        AnimationDriver Sample_IPlayerModel.Driver => Avatar != null ? Avatar.Driver : null;

        /// <summary>共通契約: Controller 駆動ではない。</summary>
        AnimatorAvatar Sample_IPlayerModel.ControllerAvatar => null;

        /// <summary>共通契約: Playables 直駆動。</summary>
        bool Sample_IPlayerModel.IsControllerDriven => false;

        /// <summary>共通契約: 揺れもの一覧（このサンプルは常時有効）。</summary>
        System.Collections.Generic.IReadOnlyList<SpringBoneRig> Sample_IPlayerModel.Springs => Springs;

        /// <summary>頭ボーン（注視リグ・FPS カメラの追従先）。</summary>
        public Transform Head { get; private set; }

        /// <summary>首ボーン（注視の配分先。null 許容）。</summary>
        public Transform Neck { get; private set; }

        /// <summary>腰ボーン（足IKの沈み込み対象・スカートのコライダー中心）。</summary>
        public Transform Hips { get; private set; }

        /// <summary>左肩（腕IKの根本）。</summary>
        public Transform Shoulder { get; private set; }

        /// <summary>左肘（腕IKの中間）。</summary>
        public Transform Elbow { get; private set; }

        /// <summary>左手（腕IKの先端）。</summary>
        public Transform Hand { get; private set; }

        /// <summary>左脚（足IK用）。</summary>
        public FootIkRig.Leg LeftLeg { get; private set; }

        /// <summary>右脚（足IK用）。</summary>
        public FootIkRig.Leg RightLeg { get; private set; }

        /// <summary>揺れもの（髪・リボン・スカート・袖。MotionRig へ装着する）。</summary>
        public List<SpringBoneRig> Springs { get; } = new List<SpringBoneRig>();

        /// <summary>身長（m。コライダーとカメラ位置の算出に使う）。</summary>
        public float Height { get; private set; }

        /// <summary>歩きクリップ（アプリ独自帯。速度が遅いとき Locomotion の代わりに流す）。</summary>
        public static readonly MotionClipId WalkClipId = new MotionClipId(101);

        /// <summary>ジャンプ滞空クリップ（接地が切れている間に流す）。</summary>
        public static readonly MotionClipId JumpClipId = new MotionClipId(102);

        /// <summary>勝利ポーズ（討伐成功時に1回流す。非ループ＝決めポーズで止まる）。</summary>
        public static readonly MotionClipId WinClipId = new MotionClipId(103);

        /// <summary>
        /// モデルからプレイヤー表示を組み上げる。見つからない・Humanoid でない場合は null
        /// （呼び出し側がカプセルへフォールバックする）。
        /// </summary>
        public static Sample_PlayerModel TryCreate(Transform parent)
        {
            var prefab = LoadPrefab();
            if (prefab == null)
            {
                return null;
            }
            var instance = Object.Instantiate(prefab, parent);
            instance.name = "Player_UnityChan";

            var animator = instance.GetComponentInChildren<Animator>();
            if (animator == null || !animator.isHuman)
            {
                Debug.LogWarning("[PlayerModel] Humanoid の Animator が見つからないためカプセルで代替");
                Object.Destroy(instance);
                return null;
            }

            // Playables 直駆動へ寄せる:
            // - Controller はアセットの状態機械なので使わない（遷移の真実は Behavior 側）
            // - ルートモーションは切る（位置の真実は ActorPose。09章の規約）
            animator.runtimeAnimatorController = null;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            var avatar = instance.AddComponent<RiggedAvatar>();
            // 移動の入場は歩きから（走りへの昇格は App が速度ヒステリシスで判断する）
            avatar.Configure(animator, BuildMotionSet(),
                new MotionBindings().Bind(BehaviorKey.Locomotion, WalkClipId));

            var model = new Sample_PlayerModel
            {
                Avatar = avatar,
                Head = animator.GetBoneTransform(HumanBodyBones.Head),
                Neck = animator.GetBoneTransform(HumanBodyBones.Neck),
                Hips = animator.GetBoneTransform(HumanBodyBones.Hips),
                Shoulder = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm),
                Elbow = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm),
                Hand = animator.GetBoneTransform(HumanBodyBones.LeftHand),
                LeftLeg = new FootIkRig.Leg(
                    animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg),
                    animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg),
                    animator.GetBoneTransform(HumanBodyBones.LeftFoot),
                    animator.GetBoneTransform(HumanBodyBones.LeftToes)),
                RightLeg = new FootIkRig.Leg(
                    animator.GetBoneTransform(HumanBodyBones.RightUpperLeg),
                    animator.GetBoneTransform(HumanBodyBones.RightLowerLeg),
                    animator.GetBoneTransform(HumanBodyBones.RightFoot),
                    animator.GetBoneTransform(HumanBodyBones.RightToes)),
                Height = MeasureHeight(instance),
            };

            if (model.Head == null || model.Hips == null)
            {
                Debug.LogWarning("[PlayerModel] 必須ボーン（Head/Hips）が引けないためカプセルで代替");
                Object.Destroy(instance);
                return null;
            }

            model.CollectSprings(animator);
            return model;
        }

        /// <summary>
        /// クリップ台帳を組む（モーション追加＝ここに1行）。
        /// 行動キーと同値のモーション番号へ割り当てるので対応表は不要（同値素通し）。
        /// </summary>
        private static MotionSet BuildMotionSet()
        {
            var set = new MotionSet();
            Add(set, MotionClipId.Idle, "Idle", loop: true);
            Add(set, MotionClipId.Locomotion, "Locomotion", loop: true);
            Add(set, MotionClipId.Attack, "Attack", loop: false, fadeSeconds: 0.08f);
            Add(set, MotionClipId.Guard, "Guard", loop: true);
            Add(set, MotionClipId.Hit, "Hit", loop: false, fadeSeconds: 0.05f);
            Add(set, MotionClipId.Death, "Death", loop: false); // 非ループ＝最終ポーズで止まる
            Add(set, WalkClipId, "Walk", loop: true);            // 低速時の差し替え（App が選ぶ）
            Add(set, JumpClipId, "Jump", loop: false, fadeSeconds: 0.05f);
            Add(set, WinClipId, "Win", loop: false, fadeSeconds: 0.2f);
            return set;
        }

        /// <summary>クリップを1本登録する（見つからなければ登録しない＝素立ちで動く）。</summary>
        private static void Add(MotionSet set, MotionClipId id, string clipName,
            bool loop, float fadeSeconds = 0.15f)
        {
            var clip = Resources.Load<AnimationClip>(ClipResourceFolder + clipName);
            if (clip != null)
            {
                set.Add(id, clip, fadeSeconds, speed: 1f, loop: loop);
            }
        }

        /// <summary>
        /// 揺れものチェーンを名前規約から収集する。
        /// UnityChan の揺れボーンは `J_L_HairTail_00 → _01 → …` のような連番チェーン。
        /// 「キーワードを含み、親はそのキーワードを含まない」ものをチェーンの根とみなす。
        /// </summary>
        private void CollectSprings(Animator animator)
        {
            // 髪・リボンは頭へ、スカート・袖は体へ、めり込み防止の球を置く
            var hairColliders = new[] { (Head, 0.11f) };
            var skirtColliders = new[]
            {
                (Hips, 0.12f),
                (animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg), 0.08f),
                (animator.GetBoneTransform(HumanBodyBones.RightUpperLeg), 0.08f),
            };

            // 髪: 柔らかく大きく揺れる / スカート・袖: 硬めで重力強め（足が透けない）
            var hairParams = new SpringBoneParams
            {
                Stiffness = 20f,               // 柔らかく＝走りの揺れがはっきり見える
                Drag = 0.18f,
                Gravity = new Vector3(0f, -3f, 0f),
                JointRadius = 0.02f,
            };
            var clothParams = new SpringBoneParams
            {
                Stiffness = 60f,
                Drag = 0.28f,
                Gravity = new Vector3(0f, -6f, 0f),
                JointRadius = 0.025f,
            };

            AddChains(animator.transform, "HairTail", hairParams, hairColliders);
            AddChains(animator.transform, "HairSide", hairParams, hairColliders);
            AddChains(animator.transform, "HairFront", hairParams, hairColliders);
            AddChains(animator.transform, "HeadRibbon", hairParams, hairColliders);
            AddChains(animator.transform, "Skirt", clothParams, skirtColliders);
            AddChains(animator.transform, "Sode", clothParams, null);
        }

        /// <summary>キーワードに合致する全チェーンへ SpringBoneRig を組む。</summary>
        private void AddChains(Transform root, string keyword, SpringBoneParams parameters,
            (Transform Center, float Radius)[] colliders)
        {
            foreach (var start in FindChainRoots(root, keyword))
            {
                var chain = new List<Transform> { start };
                var current = start;
                while (true)
                {
                    Transform next = null;
                    for (var i = 0; i < current.childCount; i++)
                    {
                        var child = current.GetChild(i);
                        if (child.name.Contains(keyword))
                        {
                            next = child;
                            break;
                        }
                    }
                    if (next == null)
                    {
                        break;
                    }
                    chain.Add(next);
                    current = next;
                }
                if (chain.Count >= 2)
                {
                    Springs.Add(new SpringBoneRig(chain.ToArray(), parameters, colliders));
                }
            }
        }

        /// <summary>チェーンの根（キーワードを含み、親は含まない Transform）を列挙する。</summary>
        private static List<Transform> FindChainRoots(Transform root, string keyword)
        {
            var results = new List<Transform>();
            foreach (var transform in root.GetComponentsInChildren<Transform>(true))
            {
                if (transform.name.Contains(keyword)
                    && (transform.parent == null || !transform.parent.name.Contains(keyword)))
                {
                    results.Add(transform);
                }
            }
            return results;
        }

        /// <summary>
        /// プレハブを探す。正規はセットアップ成果物（Resources/PlayerModel）。
        /// 未セットアップのエディタでは素の FBX で代替する（URP ではピンク表示になるため、
        /// メニュー Seed/Setup/Build UnityChan Player Prefab の実行を促す）。
        /// </summary>
        private static GameObject LoadPrefab()
        {
            var built = Resources.Load<GameObject>(PrefabResourceName);
            if (built != null)
            {
                return built;
            }
#if UNITY_EDITOR
            var raw = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(RawModelPath);
            if (raw != null)
            {
                Debug.LogWarning("[PlayerModel] セットアップ未実行のため素の FBX で表示します"
                    + "（URP では色が正しく出ません）。メニュー "
                    + "Seed/Setup/Build UnityChan Player Prefab を1回実行してください");
            }
            return raw;
#else
            return null;
#endif
        }

        /// <summary>身長を実測する（レンダラー境界から。コライダー・カメラ位置の根拠）。</summary>
        private static float MeasureHeight(GameObject instance)
        {
            var renderers = instance.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                return 1.5f;
            }
            var bounds = renderers[0].bounds;
            for (var i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }
            var height = bounds.max.y - instance.transform.position.y;
            return height > 0.5f ? height : 1.5f;
        }
    }
}
