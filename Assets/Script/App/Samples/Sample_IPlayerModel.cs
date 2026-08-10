using System.Collections.Generic;
using Seed.Character;
using Seed.Motion;
using UnityEngine;

namespace Seed.App
{
    /// <summary>
    /// 【サンプル】プレイヤーモデル束の共通契約。
    ///
    /// 標準プレイヤー（StarterAssets・Controller 駆動＝Sample_StarterPlayerModel）と
    /// 揺れものデモ（UnityChan・Playables 直駆動＝Sample_PlayerModel）は
    /// **完全に別のサンプル**として分かれている。この契約は、戦闘フェーズが
    /// どちらのモデルでも同じ配線（体・リグ・カメラ・拾う）を組めるようにするための面で、
    /// どちらを使うかはステージのマスターデータ（PlayerModelKind）が決める。
    /// </summary>
    public interface Sample_IPlayerModel
    {
        /// <summary>表示本体（IAvatar 実装）。</summary>
        IAvatar Avatar { get; }

        /// <summary>Playables 直駆動の再生器（Controller 駆動のモデルでは null）。</summary>
        AnimationDriver Driver { get; }

        /// <summary>Controller 駆動の Avatar（Playables 駆動のモデルでは null）。</summary>
        AnimatorAvatar ControllerAvatar { get; }

        /// <summary>Controller 駆動で組まれているか（クリップ制御の分岐に使う）。</summary>
        bool IsControllerDriven { get; }

        /// <summary>頭ボーン（注視リグ・FPS カメラの追従先）。</summary>
        Transform Head { get; }

        /// <summary>首ボーン（注視の配分先。null 許容）。</summary>
        Transform Neck { get; }

        /// <summary>腰ボーン（足IKの沈み込み対象）。</summary>
        Transform Hips { get; }

        /// <summary>左肩（腕IKの根本）。</summary>
        Transform Shoulder { get; }

        /// <summary>左肘（腕IKの中間）。</summary>
        Transform Elbow { get; }

        /// <summary>左手（腕IKの先端・拾ったアイテムの吸着先）。</summary>
        Transform Hand { get; }

        /// <summary>左脚（足IK用）。</summary>
        FootIkRig.Leg LeftLeg { get; }

        /// <summary>右脚（足IK用）。</summary>
        FootIkRig.Leg RightLeg { get; }

        /// <summary>揺れもの（無いモデルでは空。装着と[4]トグルの対象）。</summary>
        IReadOnlyList<SpringBoneRig> Springs { get; }

        /// <summary>身長（m。コライダーとカメラ位置の算出に使う）。</summary>
        float Height { get; }
    }
}
