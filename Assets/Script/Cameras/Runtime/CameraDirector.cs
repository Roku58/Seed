using System.Collections.Generic;
using Seed.Hub;
using Seed.Hub.Contracts;
using Unity.Cinemachine;
using UnityEngine;

namespace Seed.Cameras
{
    /// <summary>
    /// カメラの采配役（視点ID → Cinemachine カメラの登録表＋切替命令の唯一の処理者）。
    ///
    /// [役割分担]
    /// - 「どの視点が有効か」の判断 … <see cref="ViewpointStack"/>（純C#）
    /// - 「どう見えるか」（追従・構図・ブレンド曲線） … Cinemachine のカメラとブレイン
    /// - 「いつ切り替えるか」（方針） … App 層が命令を発行する
    ///
    /// 本クラスはその3つを繋ぐだけで、カメラの動き自体は実装しない——
    /// 追従・衝突回避・手ぶれといった「見え方」は Cinemachine の資産をそのまま使い、
    /// Seed 側は「視点IDで指名できる」という薄い契約だけを足している。
    ///
    /// [なぜ Cinemachine を自作で置き換えないか] 追従の減衰・遮蔽回避・ブレンドは
    /// 実装量が大きく、かつ Unity 側で年々改良される領域のため。基盤としての価値は
    /// 「ゲームの他の部分がカメラの実体を知らずに済む」ことにあり、そこだけを担う。
    ///
    /// 命令（<see cref="SetViewpointCommand"/> / <see cref="PushViewpointCommand"/> /
    /// <see cref="PopViewpointCommand"/>）の処理者は本クラス1つ。切替結果は
    /// <see cref="ViewpointChanged"/> 通知で全基盤へ流れる。
    /// </summary>
    public sealed class CameraDirector : MonoBehaviour
    {
        /// <summary>有効な視点に与える優先度（他は0）。</summary>
        private const int ActivePriority = 100;

        /// <summary>視点ID値 → カメラ。</summary>
        private readonly Dictionary<int, CinemachineCamera> _cameras =
            new Dictionary<int, CinemachineCamera>();

        /// <summary>追従対象を差し替えてよいカメラのID値（固定カメラは除外する）。</summary>
        private readonly HashSet<int> _followers = new HashSet<int>();

        /// <summary>どの視点が有効かの判断者。</summary>
        private readonly ViewpointStack _stack = new ViewpointStack();

        /// <summary>購読の束（破棄時に一括解除）。</summary>
        private readonly SubscriptionBag _bag = new SubscriptionBag();

        /// <summary>通知の発行先。</summary>
        private MessageHub _hub;

        /// <summary>ブレンドを司る Cinemachine ブレイン（Camera に付いているもの）。</summary>
        private CinemachineBrain _brain;

        /// <summary>現在の追従対象（原点移動をカメラへ知らせるために覚えておく）。</summary>
        private Transform _trackingTarget;

        /// <summary>今有効な視点。</summary>
        public ViewpointId Active => _stack.Active;

        /// <summary>基本の視点（常用カメラ）。</summary>
        public ViewpointId Base => _stack.Base;

        /// <summary>重ねられている演出視点の枚数。</summary>
        public int OverlayCount => _stack.OverlayCount;

        /// <summary>ブレンド中か（UI の入力ロック判断などに使える）。</summary>
        public bool IsBlending => _brain != null && _brain.IsBlending;

        /// <summary>
        /// 依存を差し込み、視点切替命令の処理者になる（合成ルートで一度だけ）。
        /// brain は省略可（省略時はブレンド秒数の指定が効かないだけで切替は動く）。
        /// </summary>
        public void Initialize(MessageHub hub, CinemachineBrain brain = null)
        {
            _hub = hub;
            _brain = brain;
            _hub.SubscribeCommand<SetViewpointCommand>(HandleSet).AddTo(_bag);
            _hub.SubscribeCommand<PushViewpointCommand>(HandlePush).AddTo(_bag);
            _hub.SubscribeCommand<PopViewpointCommand>(HandlePop).AddTo(_bag);
            _hub.Subscribe<OriginShifted>(OnOriginShifted).AddTo(_bag);
        }

        /// <summary>
        /// 原点回帰に追従する。Cinemachine は追従の減衰のために対象の過去位置を覚えているため、
        /// 世界がずれたことを伝えないと「一瞬すごい速度で移動した」と誤解してカメラが飛ぶ。
        /// </summary>
        private void OnOriginShifted(OriginShifted message)
        {
            if (_trackingTarget == null)
            {
                return;
            }
            var delta = new Vector3(message.Delta.X, message.Delta.Y, message.Delta.Z);
            CinemachineCore.OnTargetObjectWarped(_trackingTarget, delta);
        }

        /// <summary>
        /// 視点IDにカメラを登録する（流れるように書ける糖衣）。
        /// followsTarget を false にすると <see cref="SetTarget"/> の対象外になる
        /// （俯瞰カメラのように追従しない固定カメラ向け）。
        /// </summary>
        public CameraDirector Register(ViewpointId viewpoint, CinemachineCamera camera,
            bool followsTarget = true)
        {
            if (camera == null)
            {
                throw new HubException($"{viewpoint} に null のカメラを登録できない");
            }
            if (_cameras.ContainsKey(viewpoint.Value))
            {
                throw new HubException($"{viewpoint} は登録済み（視点IDの重複）");
            }
            _cameras[viewpoint.Value] = camera;
            if (followsTarget)
            {
                _followers.Add(viewpoint.Value);
            }
            camera.Priority = 0;
            return this;
        }

        /// <summary>
        /// 追従対象を一括で差し替える（プレイヤーの乗り換え・ステージ再入で使う）。
        /// lookAt を省略すると tracking を注視対象にも使う。
        /// </summary>
        public void SetTarget(Transform tracking, Transform lookAt = null)
        {
            _trackingTarget = tracking;
            foreach (var pair in _cameras)
            {
                if (!_followers.Contains(pair.Key))
                {
                    continue;
                }
                var target = pair.Value.Target;
                target.TrackingTarget = tracking;
                target.LookAtTarget = lookAt;
                target.CustomLookAtTarget = lookAt != null;
                pair.Value.Target = target;
            }
        }

        /// <summary>
        /// 基本の視点を切り替える（命令を使わない直接呼び出し。フェーズの初期化用）。
        /// 登録されていない視点は即例外（配線漏れを実行時に隠さない規約）。
        /// </summary>
        public void SetBase(ViewpointId viewpoint, float blendSeconds = -1f)
        {
            RequireRegistered(viewpoint);
            var previous = _stack.Active;
            var changed = _stack.SetBase(viewpoint);
            Commit(changed, previous, blendSeconds);
        }

        /// <summary>重ねを全部外して基本へ戻す（フェーズ退場・強制解除）。</summary>
        public void ClearOverlays(float blendSeconds = -1f)
        {
            var previous = _stack.Active;
            var changed = _stack.ClearOverlays();
            Commit(changed, previous, blendSeconds);
        }

        /// <summary>基本の視点を切り替える命令の処理。</summary>
        private void HandleSet(SetViewpointCommand command)
        {
            SetBase(command.Viewpoint, command.BlendSeconds);
        }

        /// <summary>演出視点を重ねる命令の処理。</summary>
        private void HandlePush(PushViewpointCommand command)
        {
            RequireRegistered(command.Viewpoint);
            var previous = _stack.Active;
            var changed = _stack.Push(command.Viewpoint);
            Commit(changed, previous, command.BlendSeconds);
        }

        /// <summary>演出視点を取り下げる命令の処理。</summary>
        private void HandlePop(PopViewpointCommand command)
        {
            var previous = _stack.Active;
            var changed = _stack.Pop(command.Viewpoint);
            Commit(changed, previous, command.BlendSeconds);
        }

        /// <summary>登録済みか検査する（未登録は配線漏れとして即例外）。</summary>
        private void RequireRegistered(ViewpointId viewpoint)
        {
            if (!_cameras.ContainsKey(viewpoint.Value))
            {
                throw new HubException(
                    $"{viewpoint} は未登録の視点（Register 漏れかIDの打ち間違い）");
            }
        }

        /// <summary>切替を Cinemachine へ反映し、変化したら通知する。</summary>
        private void Commit(bool changed, ViewpointId previous, float blendSeconds)
        {
            if (!changed)
            {
                return; // 見え方が変わらない操作でブレンドを起こさない
            }
            ApplyBlend(blendSeconds);
            ApplyPriorities();
            _hub?.Publish(new ViewpointChanged(previous, _stack.Active));
        }

        /// <summary>
        /// ブレンド秒数を反映する（負値なら既定のまま・0 ならカット）。
        /// 指定は次に指定されるまで維持される——Cinemachine のブレンドは遷移開始時に
        /// 確定するため、直後に元へ戻すと進行中のブレンドが崩れるからである。
        /// </summary>
        private void ApplyBlend(float blendSeconds)
        {
            if (_brain == null || blendSeconds < 0f)
            {
                return;
            }
            _brain.DefaultBlend = blendSeconds <= 0f
                ? new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.Cut, 0f)
                : new CinemachineBlendDefinition(
                    CinemachineBlendDefinition.Styles.EaseInOut, blendSeconds);
        }

        /// <summary>有効な視点のカメラだけ優先度を上げる（合成はブレインが行う）。</summary>
        private void ApplyPriorities()
        {
            var active = _stack.Active.Value;
            foreach (var pair in _cameras)
            {
                pair.Value.Priority = pair.Key == active ? ActivePriority : 0;
            }
        }

        /// <summary>購読の解除漏れ防止。</summary>
        private void OnDestroy()
        {
            _bag.Dispose();
        }
    }
}
