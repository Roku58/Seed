using System;
using Game.Battle.Contracts;
using Seed.Core;
using Seed.Core.Samples.ActionBattle;
using Seed.Hub;
using Seed.Hub.Contracts;

namespace Seed.App
{
    /// <summary>
    /// GameCore（決定的ロジック）とHubの双方向翻訳者。
    ///
    /// GameCore は Hub すら参照しない（純度維持）ため、接続はこのクラスの
    /// 一方向参照（Bridge→Core）だけで行う。構造は共通基盤に委譲し、
    /// このクラスに残るのは「このゲームの意味づけ」だけ:
    /// - Hub→Core: メッセージ → Sample_ActionInput への翻訳（LogicInputFunnel 経由。
    ///   ★セクションは直叩きしない。全入力がジャーナルを通り、統合経路でもリプレイが成立する）
    /// - Core→Hub: レコード種 → 通知メッセージの翻訳表（RecordHubTranslator。
    ///   全種を Map / MapIgnore で明示し、表に無い種は黙って捨てずに警告する）
    /// CharacterId は GameCore の EntityRegistry と同じ値体系なので、翻訳は素通しで済む。
    /// </summary>
    public sealed class CoreHubBridge : IDisposable
    {
        /// <summary>GameCore側の世界（デモでは ActionBattle のサンプル世界を使用）。</summary>
        private readonly Sample_ActionWorld _world;

        /// <summary>発行先のHub。</summary>
        private readonly MessageHub _hub;

        /// <summary>保持中の購読。</summary>
        private readonly SubscriptionBag _subscriptions = new SubscriptionBag(2);

        /// <summary>ロジックへの入力の一本道（記録してから実行）。</summary>
        private readonly LogicInputFunnel<Sample_ActionInput> _funnel;

        /// <summary>レコード→メッセージの翻訳表。</summary>
        private readonly RecordHubTranslator<Sample_Record, Sample_RecordKind> _translator;

        /// <summary>プレイヤーのID値（攻守の意味づけ用）。</summary>
        private readonly int _hunterId;

        /// <summary>モンスターのID値。</summary>
        private readonly int _monsterId;

        /// <summary>CoreHubBridge を生成する。</summary>
        public CoreHubBridge(Sample_ActionWorld world, MessageHub hub)
        {
            _world = world;
            _hub = hub;
            _hunterId = world.Registry.GetId(world.Hunter);
            _monsterId = world.Registry.GetId(world.Monster);

            // 入力の一本道: 記録 → Driver.Execute（このゲームの入力型と実行器を結線）
            Journal = new InputJournal<Sample_ActionInput> { Seed = 0, Version = 1 };
            _funnel = new LogicInputFunnel<Sample_ActionInput>(
                Journal, ExecuteInput, () => world.Ctx.NowMs);

            // 翻訳表: 全レコード種を明示する（Map=翻訳 / MapIgnore=意図的に流さない）
            _translator = new RecordHubTranslator<Sample_Record, Sample_RecordKind>(r => r.Kind)
                .Map(Sample_RecordKind.HitDamage, TranslateDamage)
                .Map(Sample_RecordKind.GuardChip, TranslateDamage) // チップダメージもHPを減らす真実
                .Map(Sample_RecordKind.StatusTriggered, TranslateStatusTriggered)
                .Map(Sample_RecordKind.Defeated, TranslateDefeated)
                .MapIgnore(Sample_RecordKind.ItemUsed)
                .MapIgnore(Sample_RecordKind.ActionBlocked)
                .MapIgnore(Sample_RecordKind.StatusBuildup)
                .MapIgnore(Sample_RecordKind.PartBroken)
                .MapIgnore(Sample_RecordKind.ConditionAdded)
                .MapIgnore(Sample_RecordKind.ConditionExpired);
            _translator.Unmapped += kind =>
                UnityEngine.Debug.LogWarning($"[CoreHubBridge] 翻訳表に無いレコード種: {kind}（Map/MapIgnore を追加すること）");
        }

        /// <summary>
        /// この試合の入力ジャーナル（時間前進を含む全入力が乗る）。
        /// InputJournalCodec で保存すれば、そのままリプレイ再生・検証に使える。
        /// </summary>
        public InputJournal<Sample_ActionInput> Journal { get; }

        /// <summary>Hubへ購読を張り、翻訳表をレコードログへ接続する。</summary>
        public void Initialize()
        {
            _hub.Subscribe<AttackRequested>(OnAttackRequested).AddTo(_subscriptions);
            _translator.Attach(Sample_ActionContext.Log(_world.Ctx));
        }

        /// <summary>
        /// ロジック時間を進める（TickPipeline の LogicTime フェーズから呼ぶ）。
        /// 時間前進も「入力」としてジャーナルに乗る——リアルタイム制の決定的リプレイの要。
        /// </summary>
        public void AdvanceTime(int deltaMs)
        {
            if (deltaMs <= 0)
            {
                return;
            }
            _funnel.Submit(Sample_ActionInput.AdvanceTime(deltaMs));
        }

        /// <summary>
        /// 鬼人薬を使う（宝箱イベント等から）。攻撃バフもロジックへの「入力」として
        /// ジャーナルに乗る——フィールドイベントまでリプレイ互換になる。
        /// </summary>
        public void UseDemonDrug(int attackBonus, int durationMs)
        {
            _funnel.Submit(Sample_ActionInput.UseDemonDrug(attackBonus, durationMs));
        }

        /// <summary>新着レコードをHubの通知メッセージへ翻訳する（Drain フェーズから呼ぶ）。</summary>
        public void Drain()
        {
            _translator.Drain();
        }

        /// <summary>攻撃要求を、このゲームの入力型へ翻訳して一本道に流す。</summary>
        private void OnAttackRequested(AttackRequested message)
        {
            if (_world.Hunter.IsDead || _world.Monster.IsDead)
            {
                return;
            }

            if (message.Attacker.Value == _hunterId)
            {
                // 部位指定なし（0）は既定ターゲット（頭）に解決する
                var partId = message.PartId != 0
                    ? message.PartId
                    : _world.Registry.GetId(_world.Head);
                _funnel.Submit(Sample_ActionInput.HunterAttack(message.MoveId, partId));
            }
            else if (message.Attacker.Value == _monsterId)
            {
                // ガードの真実はメッセージに載って届く（入力状態の写しは持たない）
                _funnel.Submit(Sample_ActionInput.MonsterAttack(message.MoveId, message.TargetGuarding));
            }
            else
            {
                // 未知の攻撃者はゲーム構成の不整合（旧実装は黙ってモンスター扱いしていた）
                UnityEngine.Debug.LogWarning($"[CoreHubBridge] 未知の攻撃者: {message.Attacker}");
            }
        }

        /// <summary>入力1件の実行本体（Funnel から呼ばれる。リプレイ再生でも同じ経路が使われる）。</summary>
        private void ExecuteInput(in Sample_ActionInput input)
        {
            _world.Driver.Execute(in input);
        }

        /// <summary>ダメージ系レコード（HitDamage / GuardChip）を通知へ翻訳する。</summary>
        private void TranslateDamage(in Sample_Record record)
        {
            if (record.Value <= 0)
            {
                return; // ガード性能でチップ0に抑えた等、HPが動いていない事象は流さない
            }
            _hub.Publish(new CharacterDamaged(new CharacterId(record.TargetId),
                record.Value, record.Value2, MaxHpOf(record.TargetId)));
        }

        /// <summary>状態異常の発動のうち、ダメージを伴うもの（爆破）を通知へ翻訳する。</summary>
        private void TranslateStatusTriggered(in Sample_Record record)
        {
            if (record.Status == Sample_StatusKind.Blast && record.Value > 0)
            {
                _hub.Publish(new CharacterDamaged(new CharacterId(record.TargetId),
                    record.Value, record.Value2, MaxHpOf(record.TargetId)));
            }
        }

        /// <summary>戦闘不能レコードを通知へ翻訳する（ロジックが明示発行するのでHP推論はしない）。</summary>
        private void TranslateDefeated(in Sample_Record record)
        {
            _hub.Publish(new CharacterDied(new CharacterId(record.TargetId)));
        }

        /// <summary>ユニットIDから最大HPを引く（HPバー表示用）。</summary>
        private int MaxHpOf(int unitId)
        {
            var unit = _world.Registry.GetEntity<Sample_Unit>(unitId);
            return unit != null ? unit.MaxHp : 0;
        }

        /// <summary>購読と翻訳表の接続を片付ける。</summary>
        public void Dispose()
        {
            _subscriptions.Dispose();
            _translator.Detach();
        }
    }
}
