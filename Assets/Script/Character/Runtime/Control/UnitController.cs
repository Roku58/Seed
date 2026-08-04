using System;
using Seed.Hub.Contracts;

namespace Seed.Character
{
    /// <summary>
    /// ユニット単位の制御（Logic と Agent の結線）。
    /// 毎Tick「自分の今」を組み立てて Logic に考えさせ、意図を Agent へ流して駆動する。
    /// 死亡中は Think を飛ばして空の意図を流す（死亡姿勢は維持される）。
    /// </summary>
    public abstract class UnitController
    {
        /// <summary>UnitController を生成する。</summary>
        protected UnitController(CharacterAgent agent, ICharacterLogic logic)
        {
            Agent = agent ?? throw new ArgumentNullException(nameof(agent));
            Logic = logic ?? throw new ArgumentNullException(nameof(logic));
        }

        /// <summary>制御対象のID（Agent の転送）。</summary>
        public CharacterId Id => Agent.Id;

        /// <summary>制御対象のユニット。</summary>
        public CharacterAgent Agent { get; }

        /// <summary>差し込まれた頭脳。</summary>
        public ICharacterLogic Logic { get; }

        /// <summary>1Tick進める（Manager が Add 順に呼ぶ）。</summary>
        public virtual void Tick(float deltaTime)
        {
            var intent = CharacterIntent.None;
            if (Agent.IsAlive)
            {
                var pose = Agent.ActiveActor.Pose;
                var frame = new LogicFrame(Agent.Id, pose.Position, pose.Rotation,
                    Agent.IsAlive, deltaTime);
                intent = Logic.Think(in frame);
            }
            Agent.SetIntent(in intent);
            Agent.Tick(deltaTime);
        }
    }
}
