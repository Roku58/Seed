namespace Seed.Core
{
    /// <summary>
    /// 事象レコードの「構造化ハッシュ」対応。
    ///
    /// [目的] リプレイ検証・ゴールデンログを表示文字列に依存させない。
    /// Presenter の文言を1文字直しただけで検証NGになる事故を防ぐため、
    /// レコード自身が数値・IDフィールドを StableHash へ流し込む。
    ///
    /// 実装規約: すべてのフィールドを固定順で Add すること（順序もハッシュの一部）。
    /// </summary>
    public interface IHashableRecord
    {
        void AddTo(ref StableHash hash);
    }
}
