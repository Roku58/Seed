using Seed.Character;

namespace Seed.App
{
    /// <summary>
    /// 【サンプル】一定間隔で攻撃する敵AIの思考ルーチン。
    /// 「いつ・何で攻撃するか」はゲームの方針なのでキャラ基盤に焼かず、Appに置く。
    /// キャラ基盤側は ICharacterLogic の器だけを提供する。
    ///
    /// 実プロジェクトでは、ここが距離・体力・ヘイトを見る本物の思考ルーチンに育つ
    /// （外界は ICharacterQuery をコンストラクタ注入して読む）。
    /// </summary>
    public sealed class Sample_EnemyTimerLogic : ICharacterLogic
    {
        /// <summary>攻撃間隔（秒）。</summary>
        private readonly float _intervalSeconds;

        /// <summary>攻撃に使うモーションのID（GameCore の EntityRegistry の値体系）。</summary>
        private readonly int _moveId;

        /// <summary>経過タイマー（秒）。</summary>
        private float _timer;

        /// <summary>Sample_EnemyTimerLogic を生成する。</summary>
        public Sample_EnemyTimerLogic(float intervalSeconds, int moveId)
        {
            _intervalSeconds = intervalSeconds;
            _moveId = moveId;
        }

        /// <summary>間隔到達フレームでのみ攻撃意図を返す（返したらタイマーを戻す）。</summary>
        public CharacterIntent Think(in LogicFrame frame)
        {
            _timer += frame.DeltaTime;
            if (_timer < _intervalSeconds)
            {
                return CharacterIntent.None;
            }
            _timer = 0f;
            return new CharacterIntent(
                UnityEngine.Vector3.zero, guardHeld: false, BehaviorKey.Attack, _moveId);
        }
    }
}
