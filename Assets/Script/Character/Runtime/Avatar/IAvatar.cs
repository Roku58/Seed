using UnityEngine;

namespace Seed.Character
{
    /// <summary>
    /// 表示物（3Dモデル・2D立ち絵など）の意味レベル契約。
    ///
    /// 旧基盤の PlayAttack/PlayHit… の固定APIを「行動遷移の通知1本」に一般化し、
    /// 行動（Behavior）を増やしても本契約が変わらないようにした。
    /// 実装は遷移先キーに応じてアニメーション・スプライト差し替え等を行う。
    ///
    /// 規約「状態は Tick、艶は Update」——位置・向き・行動はすべて純C#側（Actor/Behavior）が
    /// 決めて ApplyPose / OnBehaviorChanged で通知され、Avatar 側の Update に許されるのは
    /// 色フェードやバウンスなど真実に影響しない純装飾のみ。
    ///
    /// 逆方向（Avatar→ロジック）の唯一の経路は <see cref="IAvatarEventSink"/>。
    /// アニメーションイベント（攻撃の当たり判定フレーム・コンボ受付窓・足音）を
    /// 行動側へ戻すために、生成後に <see cref="BindEventSink"/> で受け口が差し込まれる。
    /// </summary>
    public interface IAvatar
    {
        /// <summary>表示を立てる/降ろす（Actor切替時に呼ばれる）。</summary>
        void SetActive(bool active);

        /// <summary>姿勢を反映する（Actor の Tick 末尾で毎回呼ばれる）。</summary>
        void ApplyPose(Vector3 position, Quaternion rotation);

        /// <summary>行動が遷移した（演出のトリガー）。</summary>
        void OnBehaviorChanged(BehaviorKey previous, BehaviorKey next);

        /// <summary>移動の正規化速度（0=停止〜1=全力）。歩行ブレンド用。</summary>
        void SetLocomotionSpeed(float normalizedSpeed);

        /// <summary>
        /// アニメーションイベントの戻し先を差し込む（Actor が自分を渡す）。
        /// null を渡すと以後は通知しない（Actor 破棄時の切断）。
        /// </summary>
        void BindEventSink(IAvatarEventSink sink);

        /// <summary>
        /// 表示物を解放する（GameObject の破棄・プールへの返却）。
        /// ユニットの退場時に UnitManager から一気通貫で呼ばれる
        /// ——旧構成では破棄の契約が無く GameObject がリークしていた。
        /// </summary>
        void Release();
    }

    /// <summary>
    /// Avatar から行動側へイベントを戻す受け口（実装は CharacterActor）。
    /// 「演出の時間軸（アニメクリップ）」と「論理の時間軸（Behavior）」を
    /// 同期させるための唯一の逆流経路。
    /// </summary>
    public interface IAvatarEventSink
    {
        /// <summary>アニメーションイベントを現在の行動へ届ける。</summary>
        void PostAvatarEvent(int eventId);
    }
}
