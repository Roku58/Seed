using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Seed.Input
{
    /// <summary>
    /// Unity Input System Package（<see cref="InputActionAsset"/>）から入力を読む <see cref="IInputReader"/> 実装。
    ///
    /// このプロジェクトは ProjectSettings の activeInputHandler が「Input System Package 専用」なので、
    /// 旧 UnityEngine.Input を直叩きすると実行時に例外になる。デバイス読み取りを本クラス1点に集約し、
    /// 他のコードは <see cref="IInputReader"/> / <see cref="InputRouter"/> しか触らない形にすることで、
    /// 「どのAPIで読むか」の判断をアプリ全体から追い出している。
    ///
    /// 設計上の約束:
    /// - 対応表は「<see cref="ActionId"/> ↔ アクション名」だけ。どのアクションが攻撃かはアプリが決める
    /// - アクション名が見つからなくても例外にせず「押されていない」として扱い、初回だけ警告する
    ///   （入力1つの綴り違いでゲームが起動不能になる方が実害が大きい）
    /// - Enable/Disable のライフサイクルを持つ（<see cref="IDisposable"/>）。有効化は
    ///   解決できたアクションが属する <see cref="InputActionMap"/> 単位で行う——
    ///   既定アセットのように Player と UI へアクションが散っている構成をそのまま扱えるようにするため
    /// </summary>
    public sealed class InputSystemReader : IInputReader, IDisposable
    {
        /// <summary>移動アクションの既定名（Unity既定 InputSystem_Actions.inputactions の Player マップ）。</summary>
        public const string DefaultMoveActionName = "Player/Move";

        /// <summary>視点アクションの既定名（同アセットの Player マップ）。</summary>
        public const string DefaultLookActionName = "Player/Look";

        /// <summary>
        /// ボタンの既定対応表。Unity既定 InputSystem_Actions.inputactions に実在するアクション名だけを並べる
        /// （Player: Move/Look/Attack/Interact/Crouch/Jump/Previous/Next/Sprint、UI: Submit/Cancel ほか）。
        /// マップ名で修飾するのは、同名アクションが複数マップにある場合の取り違えを防ぐため。
        /// <see cref="ActionId.Guard"/> は既定アセットに対応アクションが無いので意図的に載せていない
        /// （アプリが <see cref="Bind"/> で割り当てる）。
        /// </summary>
        public static readonly IReadOnlyList<KeyValuePair<ActionId, string>> DefaultButtonBindings =
            new[]
            {
                new KeyValuePair<ActionId, string>(ActionId.Attack, "Player/Attack"),
                new KeyValuePair<ActionId, string>(ActionId.Jump, "Player/Jump"),
                new KeyValuePair<ActionId, string>(ActionId.Interact, "Player/Interact"),
                new KeyValuePair<ActionId, string>(ActionId.Sprint, "Player/Sprint"),
                new KeyValuePair<ActionId, string>(ActionId.Crouch, "Player/Crouch"),
                new KeyValuePair<ActionId, string>(ActionId.Previous, "Player/Previous"),
                new KeyValuePair<ActionId, string>(ActionId.Next, "Player/Next"),
                new KeyValuePair<ActionId, string>(ActionId.Submit, "UI/Submit"),
                new KeyValuePair<ActionId, string>(ActionId.Cancel, "UI/Cancel"),
            };

        /// <summary>読み取り元のアクションアセット。</summary>
        private readonly InputActionAsset _asset;

        /// <summary>解決できたボタン（ID順は問わない。毎フレーム全走査するので配列相当の軽さで足りる）。</summary>
        private readonly List<KeyValuePair<ActionId, InputAction>> _buttons =
            new List<KeyValuePair<ActionId, InputAction>>(16);

        /// <summary>有効化対象のマップ（解決できたアクションの所属マップを重複なく集めたもの）。</summary>
        private readonly List<InputActionMap> _maps = new List<InputActionMap>(2);

        /// <summary>警告済みのアクション名（同じ綴り違いを毎フレーム叫ばないための記録）。</summary>
        private readonly HashSet<string> _warnedNames = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>移動アクション（解決できなければ null＝ゼロ扱い）。</summary>
        private InputAction _move;

        /// <summary>視点アクション（解決できなければ null＝ゼロ扱い）。</summary>
        private InputAction _look;

        /// <summary>Enable 済みか（二重 Enable/Disable を無害にする）。</summary>
        private bool _enabled;

        /// <summary>Dispose 済みか（以後の Read は空の写しを返す）。</summary>
        private bool _disposed;

        /// <summary>
        /// InputSystemReader を生成する。
        /// buttonBindings を省略すると <see cref="DefaultButtonBindings"/> を使う。
        /// 解決はここで一度だけ行い、以後の Read は解決済み参照を読むだけにする（毎フレームの検索を避ける）。
        /// </summary>
        public InputSystemReader(
            InputActionAsset asset,
            IEnumerable<KeyValuePair<ActionId, string>> buttonBindings = null,
            string moveActionName = DefaultMoveActionName,
            string lookActionName = DefaultLookActionName)
        {
            _asset = asset ?? throw new ArgumentNullException(nameof(asset));

            _move = Resolve(moveActionName);
            _look = Resolve(lookActionName);

            foreach (var binding in buttonBindings ?? DefaultButtonBindings)
            {
                Bind(binding.Key, binding.Value);
            }
        }

        /// <summary>
        /// アクションIDにアクション名を割り当てる（既存の割り当ては上書き）。
        /// 名前が見つからない・IDがビットマスクに載らない場合は警告して黙って無視する
        /// （＝そのアクションは常に「押されていない」）。
        /// </summary>
        public void Bind(ActionId action, string actionName)
        {
            if (InputSnapshot.MaskOf(action) == 0UL)
            {
                Debug.LogWarning(
                    $"[Seed.Input] {action} はボタンとして扱えません（1〜{InputSnapshot.MaxButtonActionValue} のIDのみ）。'{actionName}' の割り当てを無視します。");
                return;
            }

            var resolved = Resolve(actionName);
            if (resolved == null)
            {
                return;
            }

            for (var i = 0; i < _buttons.Count; i++)
            {
                if (_buttons[i].Key.Equals(action))
                {
                    _buttons[i] = new KeyValuePair<ActionId, InputAction>(action, resolved);
                    return;
                }
            }
            _buttons.Add(new KeyValuePair<ActionId, InputAction>(action, resolved));
        }

        /// <summary>解決できたアクションの所属マップをまとめて有効化する（二重呼び出しは無害）。</summary>
        public void Enable()
        {
            if (_enabled || _disposed)
            {
                return;
            }
            for (var i = 0; i < _maps.Count; i++)
            {
                _maps[i].Enable();
            }
            _enabled = true;
        }

        /// <summary>有効化したマップを無効化する（二重呼び出しは無害）。</summary>
        public void Disable()
        {
            if (!_enabled)
            {
                return;
            }
            for (var i = 0; i < _maps.Count; i++)
            {
                _maps[i].Disable();
            }
            _enabled = false;
        }

        /// <summary>
        /// 現在の入力状態を読む。
        /// Move/Look は Vector2 の値読み、ボタンは <see cref="InputAction.IsPressed"/>。
        /// 「押した瞬間」の判定は意図的にここでやらない（<see cref="InputRouter"/> がフレーム差分で出す）。
        /// </summary>
        public InputSnapshot Read()
        {
            if (_disposed)
            {
                return InputSnapshot.Empty;
            }

            var move = _move != null ? _move.ReadValue<Vector2>() : Vector2.zero;
            var look = _look != null ? _look.ReadValue<Vector2>() : Vector2.zero;

            var pressed = 0UL;
            for (var i = 0; i < _buttons.Count; i++)
            {
                if (_buttons[i].Value.IsPressed())
                {
                    pressed = InputSnapshot.SetPressed(pressed, _buttons[i].Key, true);
                }
            }
            return new InputSnapshot(move, look, pressed);
        }

        /// <summary>無効化して参照を捨てる（合成ルートの OnDestroy から呼ぶ）。二重 Dispose は無害。</summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }
            Disable();
            _buttons.Clear();
            _maps.Clear();
            _move = null;
            _look = null;
            _disposed = true;
        }

        /// <summary>
        /// アクション名を解決し、所属マップを有効化対象に登録する。
        /// 見つからなければ初回だけ警告して null を返す（例外にしない理由はクラスのdoc参照）。
        /// </summary>
        private InputAction Resolve(string actionName)
        {
            if (string.IsNullOrEmpty(actionName))
            {
                return null;
            }

            var action = _asset.FindAction(actionName);
            if (action == null)
            {
                if (_warnedNames.Add(actionName))
                {
                    Debug.LogWarning(
                        $"[Seed.Input] アクション '{actionName}' が {_asset.name} に見つかりません。この入力は常に無反応として扱います。");
                }
                return null;
            }

            var map = action.actionMap;
            if (map != null && !_maps.Contains(map))
            {
                _maps.Add(map);
                if (_enabled)
                {
                    map.Enable();
                }
            }
            return action;
        }
    }
}
