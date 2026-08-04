using System;
using System.Collections.Generic;
using Seed.Hub;
using Seed.Hub.Contracts;

namespace Seed.Character
{
    /// <summary>
    /// 陣営ごとの管理の基底。ユニット（UnitController）を Add 順という決定的順序で Tick する。
    /// 名簿（CharacterRegistry）への登録・抹消もここが所有し、
    /// 「Manager に居る = 名簿に居る」を常に一致させる。
    /// 退場時は Agent.ReleaseAll() まで一気通貫で行い、表示物のリークを防ぐ。
    /// Tick 中の Add/Remove は順序の決定性を壊すため例外とする（次フレームで行うこと）。
    /// </summary>
    public abstract class UnitManager
    {
        /// <summary>ユニット（Add 順 = Tick 順）。</summary>
        private readonly List<UnitController> _controllers = new List<UnitController>(4);

        /// <summary>ID → ユニットの索引。</summary>
        private readonly Dictionary<CharacterId, UnitController> _byId =
            new Dictionary<CharacterId, UnitController>();

        /// <summary>共有の在籍名簿。</summary>
        private readonly CharacterRegistry _registry;

        /// <summary>Tick 実行中か（Tick 中の増減ガード）。</summary>
        private bool _isTicking;

        /// <summary>UnitManager を生成する。</summary>
        protected UnitManager(CharacterRegistry registry, FactionId faction)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            Faction = faction;
        }

        /// <summary>この Manager が管理する陣営。</summary>
        public FactionId Faction { get; }

        /// <summary>所属ユニット数。</summary>
        public int Count => _controllers.Count;

        /// <summary>ユニットを追加し、名簿へも陣営付きで登録する（重複IDは名簿側が例外）。</summary>
        public void Add(UnitController controller)
        {
            if (controller == null)
            {
                throw new ArgumentNullException(nameof(controller));
            }
            GuardMutationDuringTick();
            _registry.Register(controller.Agent, Faction);
            _controllers.Add(controller);
            _byId.Add(controller.Id, controller);
        }

        /// <summary>
        /// ユニットを外す。名簿からの抹消と表示物の解放（Avatar.Release）まで一気通貫で行う。
        /// </summary>
        public bool Remove(CharacterId id)
        {
            GuardMutationDuringTick();
            if (!_byId.TryGetValue(id, out var controller))
            {
                return false;
            }
            _byId.Remove(id);
            _controllers.Remove(controller);
            _registry.Unregister(id);
            controller.Agent.ReleaseAll();
            return true;
        }

        /// <summary>ユニットの取得を試みる。</summary>
        public bool TryGet(CharacterId id, out UnitController controller)
        {
            return _byId.TryGetValue(id, out controller);
        }

        /// <summary>全ユニットを Add 順に1Tick進める。</summary>
        public void Tick(float deltaTime)
        {
            _isTicking = true;
            try
            {
                for (var i = 0; i < _controllers.Count; i++)
                {
                    _controllers[i].Tick(deltaTime);
                }
            }
            finally
            {
                _isTicking = false;
            }
        }

        /// <summary>全ユニットを外し、名簿抹消と表示物の解放まで行う（シーン終了時）。</summary>
        public void Clear()
        {
            GuardMutationDuringTick();
            for (var i = 0; i < _controllers.Count; i++)
            {
                _registry.Unregister(_controllers[i].Id);
                _controllers[i].Agent.ReleaseAll();
            }
            _controllers.Clear();
            _byId.Clear();
        }

        /// <summary>指定番目のユニットを返す（Add 順。デバッグ・列挙用）。</summary>
        protected UnitController ControllerAt(int index)
        {
            return _controllers[index];
        }

        /// <summary>Tick 中の構成変更を拒否する。</summary>
        private void GuardMutationDuringTick()
        {
            if (_isTicking)
            {
                throw new HubException("Tick 中のユニット増減は禁止（順序の決定性が壊れる。次フレームで行う）");
            }
        }
    }
}
