using System;
using Game.Battle.Contracts;
using Seed.Hub;
using Seed.Hub.Contracts;
using Seed.UI;
using UnityEngine;

namespace Seed.App
{
    /// <summary>
    /// 【サンプル】バトル中のHUD画面。
    /// 「データを映す画面は自分でHubを購読する（OnShowで購読・OnHideで解除）」規約の実例。
    /// Hub は UIScreen.Attach（UISystem.Register 経由）で配られるため、
    /// アプリが差し込むのはゲーム固有の知識（ID・表示名）だけでよい。
    /// 表示はOnGUIの最小構成（実プロジェクトではuGUI/UI Toolkitに置き換える）。
    /// </summary>
    public sealed class Sample_HudScreen : UIScreen
    {
        /// <summary>この画面のID。</summary>
        public override ScreenId Id => Sample_ScreenIds.BattleHud;

        /// <summary>表示名の解決に使う（Runnerが差し込む）。</summary>
        private Func<CharacterId, string> _nameOf;

        /// <summary>ダメージ購読のトークン。</summary>
        private IDisposable _subscription;

        /// <summary>キャラごとのHP表示テキスト。</summary>
        private string _playerLine = "";

        /// <summary>敵側のHP表示テキスト。</summary>
        private string _enemyLine = "";

        /// <summary>プレイヤーのID（表示振り分け用）。</summary>
        private CharacterId _playerId;

        /// <summary>ゲーム固有の知識を差し込む（生成直後に一度だけ呼ぶ）。</summary>
        public void Construct(CharacterId playerId, Func<CharacterId, string> nameOf)
        {
            _playerId = playerId;
            _nameOf = nameOf;
        }

        /// <summary>初期表示テキストを設定する。</summary>
        public void SetInitialLine(CharacterId id, int hp, int maxHp)
        {
            ApplyLine(id, hp, maxHp);
        }

        /// <summary>表示中だけダメージ通知を購読する。</summary>
        protected override void OnShow()
        {
            _subscription = Hub.Subscribe<CharacterDamaged>(OnDamaged);
        }

        /// <summary>非表示になったら購読を解除する。</summary>
        protected override void OnHide()
        {
            _subscription?.Dispose();
            _subscription = null;
        }

        /// <summary>ダメージ通知でHP表示を更新する（値はスナップショットなので自己完結）。</summary>
        private void OnDamaged(CharacterDamaged message)
        {
            ApplyLine(message.Target, message.RemainingHp, message.MaxHp);
        }

        /// <summary>該当キャラの行を更新する。</summary>
        private void ApplyLine(CharacterId id, int hp, int maxHp)
        {
            var line = $"{_nameOf(id)}  HP {hp}/{maxHp}";
            if (id.Equals(_playerId))
            {
                _playerLine = line;
            }
            else
            {
                _enemyLine = line;
            }
        }

        /// <summary>HPと操作説明を描画する。</summary>
        private void OnGUI()
        {
            GUI.Label(new Rect(16f, 8f, 900f, 24f), $"{_playerLine}    {_enemyLine}");
            GUI.Label(new Rect(16f, 30f, 900f, 22f), "[1] 攻撃  [G] ガード  WASD 移動  [T] 3D/2D  [O] 自動操縦  [P] ポーズ  [B] ホームへ");
        }
    }
}
