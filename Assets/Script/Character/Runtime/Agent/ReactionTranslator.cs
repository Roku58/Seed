using Seed.Hub.Contracts;

namespace Seed.Character
{
    /// <summary>
    /// ReactionId →（行動キー・force）の機械的な対応表。
    /// 「被弾したらのけぞる**べきか**」（スーパーアーマー等の例外）はアプリの方針層が決め、
    /// ここは命令されたリアクションを行動遷移として実行するだけ。
    ///
    /// リアクションを増やすコスト:
    /// - 標準リアクション（1〜99）… この表に1行足す
    /// - アプリ独自リアクション（100〜）… 標準表に無いIDは
    ///   「同じ値の BehaviorKey への遷移要求」として素通しする規約。
    ///   つまり ReactionId(120) は BehaviorKey(120) の行動を要求する——
    ///   アプリは独自行動を AddBehavior しておくだけで、基盤側の変更ゼロで繋がる。
    /// </summary>
    internal static class ReactionTranslator
    {
        /// <summary>リアクション命令を表示中 Actor の行動遷移へ適用する。</summary>
        public static void Apply(CharacterAgent agent, CharacterActor actor, ReactionId reaction, int payload)
        {
            if (reaction.Equals(ReactionId.Attack))
            {
                // 通常の攻撃開始は Behavior 駆動（Intent 経由）。これは外部命令用の互換経路
                actor.RequestBehavior(BehaviorKey.Attack, payload);
            }
            else if (reaction.Equals(ReactionId.Hit))
            {
                // Death 中は優先度規則により自然に無視される。
                // のけぞり中の再ヒットは HitBehavior.AllowsRefresh が「頭からやり直し」を許す
                actor.RequestBehavior(BehaviorKey.Hit, payload);
            }
            else if (reaction.Equals(ReactionId.GuardOn))
            {
                // 互換経路。継続は意図（GuardHeld）が担うため、意図が無ければ次Tickで解ける
                actor.RequestBehavior(BehaviorKey.Guard, payload);
            }
            else if (reaction.Equals(ReactionId.GuardOff))
            {
                if (actor.CurrentKey.Equals(BehaviorKey.Guard))
                {
                    actor.RequestBehavior(BehaviorKey.Idle, 0, force: true);
                }
            }
            else if (reaction.Equals(ReactionId.Death))
            {
                // 写しを先に落としてから終端行動へ（Enter 時点の IsAlive を正しくする）
                agent.MarkDead();
                actor.SetAlive(false);
                actor.RequestBehavior(BehaviorKey.Death, payload, force: true);
            }
            else if (!reaction.Equals(ReactionId.None))
            {
                // アプリ独自リアクション: 同じ値の行動キーへの遷移要求として素通しする
                actor.RequestBehavior(new BehaviorKey(reaction.Value), payload);
            }
        }
    }
}
