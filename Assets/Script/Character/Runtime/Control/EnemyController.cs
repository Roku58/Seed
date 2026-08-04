namespace Seed.Character
{
    /// <summary>
    /// エネミーユニットの制御。頭脳は任意の ICharacterLogic（AI思考ルーチン）。
    /// 思考ルーチンの実装（いつ攻撃するか等）はゲームの方針なのでアプリ側に置く。
    /// 将来「敵だけのヘイト管理・グループ連携」等の置き場もここになる。
    /// </summary>
    public sealed class EnemyController : UnitController
    {
        /// <summary>EnemyController を生成する。</summary>
        public EnemyController(CharacterAgent agent, ICharacterLogic brain)
            : base(agent, brain)
        {
        }
    }
}
