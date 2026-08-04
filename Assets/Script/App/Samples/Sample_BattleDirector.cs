using Seed.AI;
using Seed.Hub.Contracts;

namespace Seed.App
{
    /// <summary>
    /// 【サンプル】この戦闘のメタAI（采配ルール＝方針なのでApp側）。
    ///
    /// - 攻撃権の采配: 敵の攻撃は間隔制。攻撃が始まったらタイマーを戻す
    /// - 手心（動的難易度）: プレイヤーのHPが減るほど攻撃間隔を延ばし、攻撃性も下げる
    ///   （満タン: 基準間隔×1 / 瀕死: 基準間隔×2）——「あと少しで勝てたのに」を作る定番の采配
    /// - 注目対象: 敵の狙いをプレイヤーに固定（多人数戦ならヘイト計算がここに入る）
    ///
    /// 敵が複数いる編成では AttackTokenPool を併用して「同時に殴りかかるのはN体まで」を
    /// ここで采配する（本デモは1体なので使っていない）。
    /// </summary>
    public sealed class Sample_BattleDirector : AiDirector
    {
        /// <summary>采配対象の敵。</summary>
        private readonly CharacterId _enemyId;

        /// <summary>注目対象（プレイヤー）。</summary>
        private readonly CharacterId _playerId;

        /// <summary>基準の攻撃間隔（秒。ステージ仕様から）。</summary>
        private readonly float _baseInterval;

        /// <summary>攻撃タイマー（秒）。</summary>
        private float _timer;

        /// <summary>プレイヤーの体力率（0〜1。手心の入力）。</summary>
        private float _playerHpRatio = 1f;

        /// <summary>Sample_BattleDirector を生成する。</summary>
        public Sample_BattleDirector(CharacterId enemyId, CharacterId playerId, float baseInterval)
        {
            _enemyId = enemyId;
            _playerId = playerId;
            _baseInterval = baseInterval;
        }

        /// <summary>今の攻撃間隔（HPが減るほど延びる＝手心）。</summary>
        public float CurrentInterval => _baseInterval * (2f - _playerHpRatio);

        /// <summary>プレイヤーの被弾を知らせる（BattlePhase が CharacterDamaged から中継する）。</summary>
        public void NotifyPlayerHp(int remainingHp, int maxHp)
        {
            _playerHpRatio = maxHp > 0 ? (float)remainingHp / maxHp : 0f;
        }

        /// <summary>攻撃が始まったことを知らせる（攻撃権の消費＝タイマーを戻す）。</summary>
        public void NotifyAttackStarted(CharacterId attacker)
        {
            if (attacker.Equals(_enemyId))
            {
                _timer = 0f;
            }
        }

        /// <summary>采配を進め、敵への指示書を更新する。</summary>
        public override void Tick(float deltaTime)
        {
            _timer += deltaTime;
            var aggression = 400 + (int)(600 * _playerHpRatio); // 瀕死の相手は追い方も緩める
            SetOrders(_enemyId, new AiOrders(
                attackPermitted: _timer >= CurrentInterval,
                aggressionPermille: aggression,
                focusTarget: _playerId));
        }
    }
}
