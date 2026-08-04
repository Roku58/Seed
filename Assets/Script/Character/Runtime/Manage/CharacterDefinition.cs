using System;
using System.Collections.Generic;

namespace Seed.Character
{
    /// <summary>
    /// キャラクター1種の組み立てレシピ（純データ）。
    ///
    /// 「新しいキャラを増やす＝このレシピを1つ書く」で済むことを狙った型で、
    /// 数値はマスターデータ（Seed.Data の定義）から流し込むことを想定している。
    /// 行動の追加は StandardSet のフラグ切替か ExtraBehaviors への生成器追加だけで、
    /// 基盤コードには手を入れない。
    /// </summary>
    public sealed class CharacterDefinition
    {
        /// <summary>移動速度（m/s）。</summary>
        public float MoveSpeed = 4f;

        /// <summary>旋回速度（度/秒）。</summary>
        public float TurnSpeedDegPerSec = 720f;

        /// <summary>攻撃の拘束秒（アニメイベント MotionFinished でも解ける）。</summary>
        public float AttackSeconds = 0.4f;

        /// <summary>のけぞりの拘束秒。</summary>
        public float StaggerSeconds = 0.25f;

        /// <summary>標準行動一式（Idle/Locomotion/Attack/Guard/Hit/Death）を組み込むか。</summary>
        public bool IncludeStandardBehaviors = true;

        /// <summary>
        /// 独自行動の生成器（回避・詠唱・必殺技など）。
        /// Behavior は Actor ごとに専用インスタンスが必要なため、実体ではなく生成器で持つ。
        /// </summary>
        public readonly List<Func<ICharacterBehavior>> ExtraBehaviors =
            new List<Func<ICharacterBehavior>>();

        /// <summary>独自行動の生成器を追加する（流れるように書ける糖衣）。</summary>
        public CharacterDefinition WithBehavior(Func<ICharacterBehavior> factory)
        {
            if (factory == null)
            {
                throw new ArgumentNullException(nameof(factory));
            }
            ExtraBehaviors.Add(factory);
            return this;
        }
    }
}
