using System;
using Seed.Hub.Contracts;

namespace Seed.Character
{
    /// <summary>
    /// Actor 1体ぶんの素材（表現キー＋表示物）。
    /// 表示物（GameObject・プレハブ）の用意はアプリの仕事なので、基盤は IAvatar でだけ受け取る。
    /// </summary>
    public readonly struct ActorBlueprint
    {
        /// <summary>表現形態のキー。</summary>
        public readonly ActorKey Key;

        /// <summary>表示物。</summary>
        public readonly IAvatar Avatar;

        /// <summary>ActorBlueprint を生成する。</summary>
        public ActorBlueprint(ActorKey key, IAvatar avatar)
        {
            Key = key;
            Avatar = avatar;
        }
    }

    /// <summary>
    /// レシピ（CharacterDefinition）から Agent+Actor+Behavior を組み立てる工場。
    ///
    /// 旧構成では合成が全てアプリの手書きで、ユニットを増やすたびに
    /// 同じボイラープレートが複製されていた。ここに一本化することで
    /// 「キャラを増やす＝レシピと表示物を渡すだけ」になる。
    /// プール再利用したい場合は Agent を捨てずに ResetForReuse を呼ぶ。
    /// </summary>
    public static class CharacterFactory
    {
        /// <summary>
        /// ユニットを組み立てる。最初の素材が表示 Actor として立ち上がる。
        /// </summary>
        public static CharacterAgent Create(CharacterId id, CharacterDefinition definition,
            params ActorBlueprint[] actors)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }
            if (actors == null || actors.Length == 0)
            {
                throw new ArgumentException("Actor の素材が1つも無い（表示物なしなら NullAvatar を渡す）",
                    nameof(actors));
            }

            var agent = new CharacterAgent(id);
            for (var i = 0; i < actors.Length; i++)
            {
                var actor = CreateActor(actors[i].Key, actors[i].Avatar, definition);
                agent.AddActor(actor, activate: i == 0);
            }
            return agent;
        }

        /// <summary>Actor 1体をレシピどおりに組み立てる（行動一式を装着済み）。</summary>
        public static CharacterActor CreateActor(ActorKey key, IAvatar avatar,
            CharacterDefinition definition)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            var actor = new CharacterActor(key, avatar);
            if (definition.IncludeStandardBehaviors)
            {
                actor.AddBehavior(new IdleBehavior());
                actor.AddBehavior(new LocomotionBehavior(
                    definition.MoveSpeed, definition.TurnSpeedDegPerSec));
                actor.AddBehavior(new AttackBehavior(definition.AttackSeconds));
                actor.AddBehavior(new GuardBehavior());
                actor.AddBehavior(new HitBehavior(definition.StaggerSeconds));
                actor.AddBehavior(new DeathBehavior());
            }
            for (var i = 0; i < definition.ExtraBehaviors.Count; i++)
            {
                actor.AddBehavior(definition.ExtraBehaviors[i]());
            }
            return actor;
        }

        /// <summary>
        /// プールから再利用する前の初期化（全 Actor の行動状態と姿勢の速度をリセット）。
        /// 位置は呼び出し側がスポーン地点を入れ直す。
        /// </summary>
        public static void ResetForReuse(CharacterAgent agent)
        {
            if (agent == null)
            {
                throw new ArgumentNullException(nameof(agent));
            }
            agent.ResetForReuse();
        }
    }
}
