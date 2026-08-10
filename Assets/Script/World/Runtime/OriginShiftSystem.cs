using System.Collections.Generic;
using Seed.Hub;
using Seed.Hub.Contracts;
using UnityEngine;

namespace Seed.World
{
    /// <summary>原点移動に追従する契約（座標を自前で持っているものが実装する）。</summary>
    public interface IOriginShiftHandler
    {
        /// <summary>世界がずれたので、自分の保持座標に delta を加える。</summary>
        void OnOriginShifted(Vector3 delta);
    }

    /// <summary>
    /// 原点回帰の実行役（判断は <see cref="OriginShifter"/>、適用と告知はここ）。
    ///
    /// [適用の順序] ①登録された根 Transform をまとめて動かす → ②追従契約の実装へ通知
    /// → ③Hub へ通知。①で大半（ステージ・キャラの表示物・エフェクト）が片付き、
    /// ②③は「純C#側に座標を持っているもの」（追跡目標・トリガー位置・カメラの履歴）の担当。
    ///
    /// [親を持つものは登録しない] 根だけを動かせば子は一緒に動く。登録した Transform に
    /// 親子関係があると二重にずれるため、フェーズのルートやプールの親のように
    /// 「最上位のもの」だけを登録する。
    ///
    /// [決定性との関係] ゲームロジックの真実（GameCore の座標・ActorPose）は
    /// この仕組みで書き換わる。原点移動は「見た目の座標系の付け替え」であって
    /// ゲームの内容ではないため、リプレイに載せるなら移動量も入力として記録するか、
    /// 記録は絶対座標（<see cref="OriginShifter.ToAbsolute"/>）で行うのが安全である。
    ///
    /// 判断は Tick（状態）で行う——「艶は Update」の規約に対し、原点は世界の状態だからである。
    /// </summary>
    public sealed class OriginShiftSystem : MonoBehaviour
    {
        /// <summary>まとめて動かす根 Transform。</summary>
        private readonly List<Transform> _roots = new List<Transform>(8);

        /// <summary>追従契約の実装（純C#側に座標を持つもの）。</summary>
        private readonly List<IOriginShiftHandler> _handlers = new List<IOriginShiftHandler>(8);

        /// <summary>通知の発行先（省略可）。</summary>
        private MessageHub _hub;

        /// <summary>原点回帰の基準となる対象（通常はプレイヤー）。</summary>
        private Transform _focus;

        /// <summary>判断役。</summary>
        public OriginShifter Shifter { get; private set; } = new OriginShifter();

        /// <summary>
        /// 依存を差し込む（合成ルートで一度だけ）。
        /// focus を null にすると自動判断は行わず、<see cref="ShiftNow"/> の手動運用になる。
        /// </summary>
        public void Initialize(MessageHub hub, Transform focus,
            float threshold = 2000f, float snapSize = 0f, bool shiftVertical = false)
        {
            _hub = hub;
            _focus = focus;
            Shifter = new OriginShifter(threshold, snapSize, shiftVertical);
        }

        /// <summary>まとめて動かす根を登録する（親を持たない最上位のものだけ）。</summary>
        public OriginShiftSystem AddRoot(Transform root)
        {
            if (root != null && !_roots.Contains(root))
            {
                _roots.Add(root);
            }
            return this;
        }

        /// <summary>根の登録を外す（フェーズ退場時など）。</summary>
        public void RemoveRoot(Transform root)
        {
            _roots.Remove(root);
        }

        /// <summary>追従契約の実装を登録する（純C#側に座標を持つもの）。</summary>
        public OriginShiftSystem AddHandler(IOriginShiftHandler handler)
        {
            if (handler != null && !_handlers.Contains(handler))
            {
                _handlers.Add(handler);
            }
            return this;
        }

        /// <summary>追従契約の登録を外す。</summary>
        public void RemoveHandler(IOriginShiftHandler handler)
        {
            _handlers.Remove(handler);
        }

        /// <summary>
        /// 焦点の位置を見て必要ならずらす（毎フレーム呼ぶ）。戻り値はずらしたか。
        /// </summary>
        public bool Tick()
        {
            if (_focus == null)
            {
                return false;
            }
            if (!Shifter.TryShift(_focus.position, out var delta))
            {
                return false;
            }
            Apply(delta);
            return true;
        }

        /// <summary>
        /// 指定量だけ即座にずらす（テレポート直後など、閾値を待たずに整えたいとき）。
        /// 累積量にも記録されるので絶対座標への変換は保たれる。
        /// </summary>
        public void ShiftNow(Vector3 delta)
        {
            if (delta.sqrMagnitude <= 0f)
            {
                return;
            }
            Shifter.Commit(delta);
            Apply(delta);
        }

        /// <summary>ずらしを適用して告知する。</summary>
        private void Apply(Vector3 delta)
        {
            for (var i = 0; i < _roots.Count; i++)
            {
                if (_roots[i] != null)
                {
                    _roots[i].position += delta;
                }
            }
            // Transform の一括移動を物理へ即時同期する（本プロジェクトは AutoSyncTransforms=0）。
            // これが無いと次の物理ステップまでキャスト・オーバーラップが旧座標＝最大45m
            // ズレた世界を見てしまい、接地・壁判定・足IKレイが1フレーム空振りする
            Physics.SyncTransforms();
            for (var i = 0; i < _handlers.Count; i++)
            {
                _handlers[i].OnOriginShifted(delta);
            }
            var offset = Shifter.TotalOffset;
            _hub?.Publish(new OriginShifted(
                new HubVector3(delta.x, delta.y, delta.z),
                new HubVector3(offset.x, offset.y, offset.z)));
        }
    }
}
