using System;

namespace Seed.Core.Samples.CommandBattle
{
    /// <summary>
    /// 【サンプル】ダメージ付与セクション：攻撃力決定 → 防御力決定 → ダメージ算出（整数演算）→ HP減少。
    /// 講演スライドの階層（ダメージ付与 ⊃ ダメージ計算 ⊃ …）を1セクションに畳んで表現。
    /// セクション粒度は「再利用の単位」で決める（細かすぎる階層は畳んでよい）。
    /// </summary>
    public sealed class Sample_DamageApplySection : Section<Sample_MoveInput, int>
    {
        /// <summary>世界に1つだけの共有インスタンス（セクションはステートレス）。</summary>
        public static readonly Sample_DamageApplySection Instance = new Sample_DamageApplySection();
        /// <summary>表示・デバッグ用の名前。</summary>
        public override string Name => "ダメージ付与";
        /// <summary>Sample_DamageApplySection を生成する。</summary>
        private Sample_DamageApplySection()
        {
        }

        /// <summary>セクション本体の処理（LogicContext.RunSection から呼ばれる）。</summary>
        protected override int Execute(LogicContext ctx, in Sample_MoveInput input)
        {
            var attack = ctx.RunSection(Sample_AttackDetermineSection.Instance, in input);
            var defense = ctx.RunSection(Sample_DefenseDetermineSection.Instance, in input);

            // ダメージ算出（すべて整数。float禁止）
            var typePermille = Sample_TypeChart.GetPermille(input.Move.Element, input.Target.Types);
            var value = input.Move.Power * attack / Math.Max(1, defense);
            if (input.User.HasType(input.Move.Element)) value = Permille.Apply(value, 1500); // タイプ一致
            value = Permille.Apply(value, typePermille);
            value = Permille.Apply(value, 350); // 全体係数（デモ用）
            value = Math.Max(1, value);

            ((Sample_IActorWriter)input.Target).ApplyDamage(value);
            Sample_CommandContext.AddRecord(ctx, new Sample_Record(
                Sample_CommandContext.Field(ctx).Turn, Sample_RecordKind.Hit,
                actor: input.User, target: input.Target, move: input.Move,
                value: value, value2: input.Target.Hp, typePermille: typePermille));
            return value;
        }
    }
}
