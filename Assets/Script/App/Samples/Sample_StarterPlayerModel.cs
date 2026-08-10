using Seed.Character;
using Seed.Motion;
using UnityEngine;

namespace Seed.App
{
    /// <summary>
    /// 【サンプル】標準プレイヤー（StarterAssets の Armature・Humanoid）の表示の束。
    ///
    /// **AnimatorController 駆動（AnimatorAvatar）・揺れもの無し**の構成で、
    /// 歩き⇄走りは `Speed` パラメータの1Dブレンドツリーが連続的に混ぜる。
    /// 揺れもののサンプル（UnityChan・Playables 直駆動＝Sample_PlayerModel）とは
    /// 完全に別のサンプル——どちらを使うかはステージのマスターデータが決める。
    ///
    /// [事前セットアップ] メニュー **Seed/Setup/Build StarterAssets Player** を1回実行する
    /// （プレハブ複製＋クリップ複製＋遷移なし Controller の生成）。
    ///
    /// [クリップの出どころ] 移動系（Idle/Walk/Run/Jump/InAir）は StarterAssets、
    /// 戦闘系（Attack/Guard/Hit/Death/Win）は Grruzam Powerful Sword Animation の
    /// 大剣セット（M_Big_Sword）から複製して使う（どちらも Humanoid＝同じ骨格定義で流用できる）。
    /// </summary>
    public sealed class Sample_StarterPlayerModel : Sample_IPlayerModel
    {
        /// <summary>セットアップ済みプレハブの Resources 名。</summary>
        private const string PrefabResourceName = "StarterPlayer";

        /// <summary>遷移なし Controller の Resources 名。</summary>
        private const string ControllerResourceName = "StarterAnimator";

        /// <summary>未セットアップ時のフォールバック（素の FBX。エディタのみ）。</summary>
        private const string RawModelPath =
            "Assets/StarterAssets/ThirdPersonController/Character/Models/Armature.fbx";

        /// <summary>揺れものは持たない（空を返すための共有インスタンス）。</summary>
        private static readonly SpringBoneRig[] NoSprings = new SpringBoneRig[0];

        /// <summary>表示本体（Controller 駆動）。</summary>
        public AnimatorAvatar ControllerAvatar { get; private set; }

        /// <summary>表示本体（IAvatar として）。</summary>
        public IAvatar Avatar => ControllerAvatar;

        /// <summary>Playables の再生器は持たない（Controller 駆動のため）。</summary>
        public AnimationDriver Driver => null;

        /// <summary>常に Controller 駆動。</summary>
        public bool IsControllerDriven => true;

        /// <summary>頭ボーン。</summary>
        public Transform Head { get; private set; }

        /// <summary>首ボーン。</summary>
        public Transform Neck { get; private set; }

        /// <summary>腰ボーン。</summary>
        public Transform Hips { get; private set; }

        /// <summary>左肩。</summary>
        public Transform Shoulder { get; private set; }

        /// <summary>左肘。</summary>
        public Transform Elbow { get; private set; }

        /// <summary>左手。</summary>
        public Transform Hand { get; private set; }

        /// <summary>左脚。</summary>
        public FootIkRig.Leg LeftLeg { get; private set; }

        /// <summary>右脚。</summary>
        public FootIkRig.Leg RightLeg { get; private set; }

        /// <summary>揺れものは無し（このサンプルの仕様）。</summary>
        public System.Collections.Generic.IReadOnlyList<SpringBoneRig> Springs => NoSprings;

        /// <summary>身長（m）。</summary>
        public float Height { get; private set; }

        /// <summary>
        /// モデルから標準プレイヤーを組み上げる。モデル・Controller が見つからない場合は
        /// null（呼び出し側がカプセルへフォールバックする）。
        /// </summary>
        public static Sample_StarterPlayerModel TryCreate(Transform parent)
        {
            var prefab = LoadPrefab();
            if (prefab == null)
            {
                return null;
            }
            var controller = Resources.Load<RuntimeAnimatorController>(ControllerResourceName);
            if (controller == null)
            {
                Debug.LogWarning("[StarterPlayer] StarterAnimator.controller が無いためカプセルで代替"
                    + "（メニュー Seed/Setup/Build StarterAssets Player を実行）");
                return null;
            }

            var instance = Object.Instantiate(prefab, parent);
            instance.name = "Player_StarterAssets";

            var animator = instance.GetComponentInChildren<Animator>();
            if (animator == null || !animator.isHuman)
            {
                Debug.LogWarning("[StarterPlayer] Humanoid の Animator が見つからないためカプセルで代替");
                Object.Destroy(instance);
                return null;
            }

            // Controller 駆動: グラフに遷移は無く、遷移は AnimatorAvatar が CrossFade で命じる
            animator.runtimeAnimatorController = controller;
            var avatar = instance.AddComponent<AnimatorAvatar>();
            avatar.Configure(animator, new AnimatorAvatarBindings()
                .Bind(BehaviorKey.Idle, "Idle")
                .Bind(BehaviorKey.Locomotion, "Locomotion")
                .Bind(BehaviorKey.Attack, "Attack", 0.08f)
                .Bind(BehaviorKey.Guard, "Guard")
                .Bind(BehaviorKey.Hit, "Hit", 0.05f)
                .Bind(BehaviorKey.Death, "Death"));

            var model = new Sample_StarterPlayerModel
            {
                ControllerAvatar = avatar,
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
                Debug.LogWarning("[StarterPlayer] 必須ボーン（Head/Hips）が引けないためカプセルで代替");
                Object.Destroy(instance);
                return null;
            }
            return model;
        }

        /// <summary>プレハブを探す（Resources → エディタ限定で素の FBX）。</summary>
        private static GameObject LoadPrefab()
        {
            var prefab = Resources.Load<GameObject>(PrefabResourceName);
            if (prefab != null)
            {
                return prefab;
            }
#if UNITY_EDITOR
            var raw = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(RawModelPath);
            if (raw != null)
            {
                Debug.LogWarning("[StarterPlayer] 未セットアップのため素の FBX で代替"
                    + "（メニュー Seed/Setup/Build StarterAssets Player の実行を推奨）");
                return raw;
            }
#endif
            return null;
        }

        /// <summary>身長をレンダラーの境界から実測する。</summary>
        private static float MeasureHeight(GameObject instance)
        {
            var renderers = instance.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                return 1.8f;
            }
            var bounds = renderers[0].bounds;
            for (var i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }
            return Mathf.Max(bounds.size.y, 1f);
        }
    }
}
