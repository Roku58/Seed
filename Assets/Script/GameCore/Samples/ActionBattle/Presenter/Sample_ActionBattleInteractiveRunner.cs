// ============================================================================
// 【サンプルコード】Sample_ActionBattleInteractiveRunner
// ユーザー入力（Input.GetKey / GetKeyDown）で遊ぶ対話型のアクションバトル。
//
// 操作:
//   [1] 斬り上げ→頭   [2] 斬り上げ→翼   [3] 溜め斬り→頭   [4] 鬼人薬
//   [G] 長押しでガード姿勢（モンスターの攻撃をガードで受ける。構え中は攻撃不可）
//   [V] リプレイ検証   [R] 決着後にリスタート
//
// 実演している基盤パターン:
// - プレイヤーの操作も Sample_ActionInput（ID参照のstruct）へ変換して実行するため、
//   「あなたが今遊んだプレイ」がそのまま InputJournal に記録される
// - [V] は記録した入力列＋シードから試合を再計算し、StableHash を突き合わせる
//   （対話プレイでも決定性が保たれていることの実演）
// - 画面表示は RecordPresenterBase（即時Dispatch型）＋OnGUI の最小構成
//
// ※ Input クラス（旧Input Manager）を使うため、Project Settings > Player >
//    Active Input Handling を「Input Manager (Old)」または「Both」にすること。
// ============================================================================

using System.Collections.Generic;
using Seed.Core.Presenter;
using Seed.Core.Samples.ActionBattle;
using UnityEngine;

namespace Seed.Core.Samples
{
    /// <summary>【サンプル】Input.GetKey操作で遊ぶ対話型アクションバトルのランナー。</summary>
    public sealed class Sample_ActionBattleInteractiveRunner : RecordPresenterBase<Sample_Record>
    {
        /// <summary>ロジック乱数の基準シード（リスタートごとに+1して展開を変える）。</summary>
        [SerializeField] private uint _logicSeed = 500;

        /// <summary>モンスターが攻撃してくる間隔（ロジック時間・ミリ秒）。</summary>
        [SerializeField] private int _monsterAttackIntervalMs = 4000;

        /// <summary>現在の狩りの世界（合成ルートは Sample_ActionWorld を再利用）。</summary>
        private Sample_ActionWorld _world;

        /// <summary>この試合の入力記録（リプレイ検証に使う）。</summary>
        private InputJournal<Sample_ActionInput> _journal;

        /// <summary>ID解決用のエンティティ台帳（表示に使う）。</summary>
        private EntityRegistry _registry;

        /// <summary>画面に出す直近のログ行。</summary>
        private readonly List<string> _lines = new List<string>(16);

        /// <summary>ロジック時間の端数繰り越し（float秒→ms の境界変換）。</summary>
        private LogicTimeAccumulator _logicTime;

        /// <summary>次にモンスターが攻撃するロジック時刻（ミリ秒）。</summary>
        private long _nextMonsterAttackMs;

        /// <summary>決着済みか。</summary>
        private bool _isOver;

        /// <summary>リスタート回数（シードへ加算）。</summary>
        private uint _battleIndex;

        /// <summary>起動時に最初の狩りを開始する。</summary>
        private void Start()
        {
            StartHunt();
        }

        /// <summary>毎フレーム、時間を進めてキー入力を処理する（レコード表示は基底のLateUpdateが行う）。</summary>
        private void Update()
        {
            if (_isOver)
            {
                HandleFinishedInput();
                return;
            }

            AdvanceLogicTime();
            HandleMonsterAi();
            HandlePlayerInput();
            CheckBattleEnd();
        }

        // ================================================================
        // 進行
        // ================================================================

        /// <summary>世界とジャーナルを作り直して狩りを開始する。</summary>
        private void StartHunt()
        {
            var seed = _logicSeed + _battleIndex;
            _world = new Sample_ActionWorld(seed, trace: null);
            _journal = new InputJournal<Sample_ActionInput>(512)
            {
                Seed = seed,
                Version = 1,
            };
            _world.Ctx.AddExtension(_journal);
            _registry = _world.Registry;

            _lines.Clear();
            _logicTime.Reset();
            _nextMonsterAttackMs = _monsterAttackIntervalMs;
            _isOver = false;

            Attach(Sample_ActionContext.Log(_world.Ctx), consumeExisting: true);
            PushLine($"――― 狩り開始（シード {seed}）。[1][2][3]攻撃 [4]鬼人薬 [G]ガード ―――");
        }

        /// <summary>フレームの経過時間をロジック時間（ミリ秒）へ変換して進める。</summary>
        private void AdvanceLogicTime()
        {
            var ms = _logicTime.Advance(Time.deltaTime);
            if (ms <= 0)
            {
                return;
            }
            Execute(Sample_ActionInput.AdvanceTime(ms));
        }

        /// <summary>一定間隔でモンスターが尻尾回転を仕掛けてくる。[G]長押し中はガードで受ける。</summary>
        private void HandleMonsterAi()
        {
            if (_world.Ctx.NowMs < _nextMonsterAttackMs)
            {
                return;
            }
            _nextMonsterAttackMs += _monsterAttackIntervalMs;

            var guarded = Input.GetKey(KeyCode.G);
            Execute(Sample_ActionInput.MonsterAttack(_registry.GetId(_world.TailSwipe), guarded));
        }

        /// <summary>プレイヤーのキー入力を Sample_ActionInput へ変換して実行する。</summary>
        private void HandlePlayerInput()
        {
            if (Input.GetKeyDown(KeyCode.V))
            {
                VerifyReplay();
                return;
            }

            // ガード姿勢中は攻撃・アイテムを出せない（構えの表現）
            if (Input.GetKey(KeyCode.G))
            {
                return;
            }

            if (Input.GetKeyDown(KeyCode.Alpha1))
            {
                Execute(Sample_ActionInput.HunterAttack(_registry.GetId(_world.SlashUp), _registry.GetId(_world.Head)));
            }
            if (Input.GetKeyDown(KeyCode.Alpha2))
            {
                Execute(Sample_ActionInput.HunterAttack(_registry.GetId(_world.SlashUp), _registry.GetId(_world.Wing)));
            }
            if (Input.GetKeyDown(KeyCode.Alpha3))
            {
                Execute(Sample_ActionInput.HunterAttack(_registry.GetId(_world.ChargedSlash), _registry.GetId(_world.Head)));
            }
            if (Input.GetKeyDown(KeyCode.Alpha4))
            {
                Execute(Sample_ActionInput.UseDemonDrug(15, 20000));
            }
        }

        /// <summary>決着後のキー入力（[R]リスタート / [V]リプレイ検証）。</summary>
        private void HandleFinishedInput()
        {
            if (Input.GetKeyDown(KeyCode.R))
            {
                _battleIndex++;
                StartHunt();
            }
            if (Input.GetKeyDown(KeyCode.V))
            {
                VerifyReplay();
            }
        }

        /// <summary>入力を「記録してから実行」する（★パターン: InputJournal＝対話プレイの丸ごと記録）。</summary>
        private void Execute(Sample_ActionInput input)
        {
            _journal.Record(_world.Ctx.NowMs, in input);
            _world.Driver.Execute(in input);
        }

        /// <summary>どちらかが倒れたら決着にする。</summary>
        private void CheckBattleEnd()
        {
            if (!_world.Hunter.IsDead && !_world.Monster.IsDead)
            {
                return;
            }
            _isOver = true;
            var result = _world.Monster.IsDead ? "討伐成功！" : "力尽きた…";
            PushLine($"――― {result} [R]リスタート / [V]リプレイ検証 ―――");
        }

        /// <summary>
        /// ★パターン: 対話プレイのリプレイ検証。
        /// いま記録されている入力列＋シードで試合を再計算し、レコード列のハッシュを比較する。
        /// </summary>
        private void VerifyReplay()
        {
            var match = Sample_ActionBattleDemo.VerifyReplay(_world.Ctx, out var original, out var replay);
            PushLine(match
                ? $"リプレイ検証 OK: hash={original:X8}（あなたのプレイは入力列だけで完全再現できる）"
                : $"リプレイ検証 NG: original={original:X8} replay={replay:X8}");
        }

        // ================================================================
        // 表示
        // ================================================================

        /// <summary>レコード→表示の対応（基底のDrainから呼ばれる）。</summary>
        protected override void Dispatch(in Sample_Record record)
        {
            PushLine(Sample_ActionBattlePresenter.Format(record, _registry));
        }

        /// <summary>ログ行を追加する（画面には直近14行、Consoleには全行）。</summary>
        private void PushLine(string line)
        {
            _lines.Add(line);
            if (_lines.Count > 14)
            {
                _lines.RemoveAt(0);
            }
            Debug.Log(line);
        }

        /// <summary>HP・スタミナ・操作説明・ログを最小構成で描画する。</summary>
        private void OnGUI()
        {
            var hunter = _world.Hunter;
            var monster = _world.Monster;
            GUI.Label(new Rect(16f, 8f, 900f, 24f),
                $"{hunter.Name} HP {hunter.Hp}/{hunter.MaxHp}  スタミナ {hunter.Stamina}/{hunter.MaxStamina}" +
                $"    {monster.Name} HP {monster.Hp}/{monster.MaxHp}" +
                (Input.GetKey(KeyCode.G) ? "    [ガード中]" : string.Empty));

            var y = 36f;
            for (var i = 0; i < _lines.Count; i++)
            {
                GUI.Label(new Rect(16f, y, 1200f, 22f), _lines[i]);
                y += 20f;
            }
        }
    }
}
