// ============================================================================
// Hub経由の統合テスト（純C#部分のみ: Hub + Bridge + 方針 + GameCore + キャラ基盤）。
// MonoBehaviour（Avatar/Screen）を使わずに、メッセージの往復が正しいことを検証する。
// 命令（〜Command）の観測は SubscribeCommand（処理者1基盤の規約に従う）で行う。
// ============================================================================

using System.Collections.Generic;
using Game.Battle.Contracts;
using NUnit.Framework;
using Seed.Character;
using Seed.Core.Samples.ActionBattle;
using Seed.Hub;
using Seed.Hub.Contracts;
using UnityEngine;

namespace Seed.App.Tests
{
    /// <summary>CoreHubBridge と Sample_ReactionPolicy とキャラ基盤の統合テスト。</summary>
    public sealed class HubIntegrationTests
    {
        /// <summary>行動遷移の記録だけを行う偽Avatar（MonoBehaviourを使わないための道具）。</summary>
        private sealed class ProbeAvatar : IAvatar
        {
            /// <summary>遷移の記録（to のみ）。</summary>
            public readonly List<BehaviorKey> Transitions = new List<BehaviorKey>();

            /// <summary>何もしない。</summary>
            public void SetActive(bool active)
            {
            }

            /// <summary>何もしない。</summary>
            public void ApplyPose(Vector3 position, Quaternion rotation)
            {
            }

            /// <summary>遷移先を記録する。</summary>
            public void OnBehaviorChanged(BehaviorKey previous, BehaviorKey next)
            {
                Transitions.Add(next);
            }

            /// <summary>何もしない。</summary>
            public void SetLocomotionSpeed(float normalizedSpeed)
            {
            }

            /// <summary>何もしない。</summary>
            public void BindEventSink(IAvatarEventSink sink)
            {
            }

            /// <summary>何もしない。</summary>
            public void Release()
            {
            }
        }

        /// <summary>攻撃要求がジャーナル経由でGameCoreに解決され、ダメージ通知として返ってくる。</summary>
        [Test]
        public void AttackRequested_FlowsThroughGameCore_AndPublishesCharacterDamaged()
        {
            var hub = new MessageHub();
            var world = new Sample_ActionWorld(seed: 700, trace: null);
            var bridge = new CoreHubBridge(world, hub);
            bridge.Initialize();

            var damaged = new List<CharacterDamaged>();
            hub.Subscribe<CharacterDamaged>(m => damaged.Add(m));

            var playerId = new CharacterId(world.Registry.GetId(world.Hunter));
            var enemyId = new CharacterId(world.Registry.GetId(world.Monster));
            hub.Publish(new AttackRequested(playerId, enemyId, world.Registry.GetId(world.SlashUp)));
            bridge.Drain();

            Assert.AreEqual(1, damaged.Count, "頭への攻撃1回でダメージ通知が1件届く");
            Assert.IsTrue(damaged[0].Target.Equals(enemyId));
            Assert.Greater(damaged[0].Damage, 0);
            Assert.AreEqual(world.Monster.Hp, damaged[0].RemainingHp, "残りHPはスナップショットと一致");
            Assert.AreEqual(1, bridge.Journal.Count, "攻撃入力がジャーナルに記録されている");
            bridge.Dispose();
        }

        /// <summary>死亡時に CharacterDied が1回だけ発行され、方針がリザルト画面命令へ変換する。</summary>
        [Test]
        public void Death_PublishesCharacterDiedOnce_AndPolicyShowsResultScreen()
        {
            var hub = new MessageHub();
            var world = new Sample_ActionWorld(seed: 700, trace: null);
            var bridge = new CoreHubBridge(world, hub);
            bridge.Initialize();
            var policy = new Sample_ReactionPolicy();
            policy.Initialize(hub);

            var died = new List<CharacterDied>();
            var screens = new List<ShowScreenCommand>();
            var reactions = new List<PlayReactionCommand>();
            hub.Subscribe<CharacterDied>(m => died.Add(m));
            hub.SubscribeCommand<ShowScreenCommand>(m => screens.Add(m));
            hub.SubscribeCommand<PlayReactionCommand>(m => reactions.Add(m));

            var playerId = new CharacterId(world.Registry.GetId(world.Hunter));
            var enemyId = new CharacterId(world.Registry.GetId(world.Monster));
            var moveId = world.Registry.GetId(world.ChargedSlash);

            // 討伐まで攻撃を繰り返す（スタミナ回復のための時間前進もジャーナル経由）
            for (var i = 0; i < 200 && !world.Monster.IsDead; i++)
            {
                bridge.AdvanceTime(2000);
                hub.Publish(new AttackRequested(playerId, enemyId, moveId));
                bridge.Drain();
            }

            Assert.IsTrue(world.Monster.IsDead, "討伐が完了する");
            Assert.AreEqual(1, died.Count, "死亡通知は1回だけ（Defeatedレコード由来。HP推論ではない）");
            Assert.AreEqual(1, screens.Count, "方針がリザルト画面命令を発行する");
            Assert.AreEqual(Sample_ScreenIds.Result.Value, screens[0].Screen.Value);
            Assert.IsTrue(reactions.Exists(r => r.Reaction.Equals(ReactionId.Death)), "倒れリアクション命令も出る");

            policy.Dispose();
            bridge.Dispose();
        }

        /// <summary>
        /// 対象のガード状態（Behaviorの真実）がメッセージに載って届き、敵の攻撃がガードとして解決される。
        /// Bridge は入力状態の写しを持たない——「のけぞり中もガード成立」のような実状態との乖離が構造的に消える。
        /// </summary>
        [Test]
        public void TargetGuarding_MakesEnemyAttackGuarded()
        {
            var hub = new MessageHub();
            var world = new Sample_ActionWorld(seed: 700, trace: null);
            var bridge = new CoreHubBridge(world, hub);
            bridge.Initialize();

            var damaged = new List<CharacterDamaged>();
            hub.Subscribe<CharacterDamaged>(m => damaged.Add(m));

            var playerId = new CharacterId(world.Registry.GetId(world.Hunter));
            var enemyId = new CharacterId(world.Registry.GetId(world.Monster));
            hub.Publish(new AttackRequested(enemyId, playerId,
                world.Registry.GetId(world.TailSwipe), partId: 0, targetGuarding: true));
            bridge.Drain();

            Assert.AreEqual(0, damaged.Count, "ガード性能Lv2のチップダメージ0はダメージ通知にならない");
            Assert.AreEqual(world.Hunter.MaxHp, world.Hunter.Hp, "HPは減っていない");
            bridge.Dispose();
        }

        /// <summary>
        /// 統合経路のプレイがそのままリプレイ可能なことの検証（A1改修の本丸）。
        /// Hub経由で戦った試合のジャーナルを新しい世界へ流し直すと、同じ結果になる。
        /// </summary>
        [Test]
        public void Replay_JournalFromHubPlay_ReproducesSameResult()
        {
            // 1戦目: Hub経由で攻撃と時間前進を行う（全部ジャーナルに乗る）
            var hub = new MessageHub();
            var world = new Sample_ActionWorld(seed: 700, trace: null);
            var bridge = new CoreHubBridge(world, hub);
            bridge.Initialize();

            var playerId = new CharacterId(world.Registry.GetId(world.Hunter));
            var enemyId = new CharacterId(world.Registry.GetId(world.Monster));
            for (var i = 0; i < 5; i++)
            {
                bridge.AdvanceTime(1500);
                hub.Publish(new AttackRequested(playerId, enemyId, world.Registry.GetId(world.SlashUp)));
                hub.Publish(new AttackRequested(enemyId, playerId, world.Registry.GetId(world.TailSwipe)));
            }
            bridge.Drain();

            // 2戦目: 同じシードの新しい世界へ、記録された入力列を先頭から流し直す
            var replayWorld = new Sample_ActionWorld(seed: 700, trace: null);
            for (var i = 0; i < bridge.Journal.Count; i++)
            {
                var entry = bridge.Journal[i];
                replayWorld.Driver.Execute(entry.Input);
            }

            Assert.AreEqual(world.Monster.Hp, replayWorld.Monster.Hp, "敵HPが一致（決定的リプレイ成立）");
            Assert.AreEqual(world.Hunter.Hp, replayWorld.Hunter.Hp, "自HPも一致");
            Assert.AreEqual(Sample_ActionContext.Log(world.Ctx).Count,
                Sample_ActionContext.Log(replayWorld.Ctx).Count, "レコード件数も一致");
            bridge.Dispose();
        }

        /// <summary>
        /// 全周E2E: 手動入力 → Logic → Behavior遷移 → BehaviorStarted → AttackRequested →
        /// Bridge → GameCore解決 → Drain → CharacterDamaged → 方針 → PlayReactionCommand →
        /// CharacterSystem → 行動割り込み（敵がのけぞる）まで、MonoBehaviourなしで一周する。
        /// </summary>
        [Test]
        public void FullLoop_ManualAttack_MakesEnemyFlinch()
        {
            // 合成: 仲介基盤 + GameCore + Bridge + 方針
            var hub = new MessageHub();
            var services = new ServiceRegistry();
            var world = new Sample_ActionWorld(seed: 700, trace: null);
            var bridge = new CoreHubBridge(world, hub);
            bridge.Initialize();
            var policy = new Sample_ReactionPolicy();
            policy.Initialize(hub);

            // 合成: キャラ基盤（全体管理＋陣営管理＋Hub玄関）
            var registry = new CharacterRegistry();
            var characters = new CharactersManager(registry);
            var players = new PlayersManager(registry);
            var enemies = new EnemiesManager(registry);
            characters.AddManager(players);
            characters.AddManager(enemies);
            var system = new CharacterSystem();
            system.Initialize(hub, services, characters);

            var playerId = new CharacterId(world.Registry.GetId(world.Hunter));
            var enemyId = new CharacterId(world.Registry.GetId(world.Monster));

            // ユニットは Factory で組む（合成の一本道の検証も兼ねる）
            var playerAvatar = new ProbeAvatar();
            var manual = new ManualLogic();
            var playerAgent = BuildUnit(playerId, enemyId, playerAvatar, hub);
            players.Add(new PlayerController(playerAgent, manual));

            var enemyAvatar = new ProbeAvatar();
            var enemyAgent = BuildUnit(enemyId, playerId, enemyAvatar, hub);
            enemies.Add(new EnemyController(enemyAgent,
                new Sample_EnemyTimerLogic(999f, world.Registry.GetId(world.TailSwipe))));

            // 実行: 攻撃入力 → 1Tick → Drain
            manual.RequestAction(BehaviorKey.Attack, world.Registry.GetId(world.SlashUp));
            characters.Tick(0.016f);
            bridge.Drain();

            // 検証: 自分は攻撃遷移、敵はダメージ通知経由でのけぞり遷移
            Assert.IsTrue(playerAvatar.Transitions.Contains(BehaviorKey.Attack), "攻撃の振りが始まっている");
            Assert.AreEqual(BehaviorKey.Hit.Value, enemyAgent.ActiveActor.CurrentKey.Value,
                "被弾通知が方針→リアクション命令→行動割り込みまで一周して敵がのけぞる");
            Assert.Less(world.Monster.Hp, world.Monster.MaxHp, "真実のHPも減っている");

            // 連打拒否: 攻撃中の再要求では AttackRequested が増えない
            var attackCount = 0;
            hub.Subscribe<AttackRequested>(m => attackCount++);
            manual.RequestAction(BehaviorKey.Attack, world.Registry.GetId(world.SlashUp));
            characters.Tick(0.016f); // まだ振りの途中（0.4秒未満）
            Assert.AreEqual(0, attackCount, "振り中の連打は Actor が拒否し、Hub へも流れない");

            system.Dispose();
            policy.Dispose();
            bridge.Dispose();
        }

        /// <summary>標準行動一式を備えた1ユニットを Factory で組み立てる（E2E用の共通手順）。</summary>
        private static CharacterAgent BuildUnit(CharacterId id, CharacterId target,
            IAvatar avatar, MessageHub hub)
        {
            var agent = CharacterFactory.Create(id, new CharacterDefinition(),
                new ActorBlueprint(new ActorKey(1), avatar));
            agent.BehaviorStarted += ev =>
            {
                if (ev.Behavior.Equals(BehaviorKey.Attack))
                {
                    hub.Publish(new AttackRequested(ev.Id, target, ev.Payload));
                }
            };
            return agent;
        }
    }
}
