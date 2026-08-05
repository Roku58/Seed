# 15. Seed.Pooling — オブジェクトプール（生成/破棄のコストを抑える）

[← 前: 14_Cameras](14_Cameras.md) | [索引](README.md) | [次: 16_World →](16_World.md)

## この章で分かること

- 弾・エフェクト・タイルのような「短命で大量」の実体を使い回す仕組み
- 標準の `UnityEngine.Pool.ObjectPool` があるのに自作した3つの理由
- 統計（ピーク同時使用数・破棄数）の読み方＝プール量の決め方と返却漏れの見つけ方
- 返却先を持ち回らずに済む台帳（`PoolRegistry`）の仕組み
- 統合デモでヒットエフェクトがどう使い回されているか

## 前提

- [08_Character.md](08_Character.md) — 既存の「再利用」思想（`CharacterAgent.ResetForReuse` など）と同じ流儀です
- C# の基本（`Stack<T>`・`HashSet<T>`・`IDisposable` の考え方）

この章が初出の主な用語: プール / 貸出と返却 / 二重返却 / 保持上限 / 事前生成（Prewarm）/ `IPoolable` / 統計（PoolStats）/ 目印コンポーネント。

## 1. これは何か

`Seed.Pooling` は「使い終わった実体を捨てずに待機させ、次の要求で再利用する」基盤です。

> 📖 **用語 — プール**: 使い回すインスタンスの待機列。`Rent`（借りる）で取り出し、
> `Return`（返す）で戻します。生成コストと破棄コストを初回だけに寄せるのが目的です。

無いと困ることは、フレームレートの落ち込みとして現れます。

1. **生成コストの山**。`Instantiate` はプレハブの複製・コンポーネントの初期化・
   `Awake`/`OnEnable` の実行を伴います。弾を毎フレーム撃つ、被弾ごとにエフェクトを出す、
   といった場面で毎回作ると、その瞬間だけフレームが落ちます
2. **GC の山**。`Destroy` した分は後でガベージコレクションの対象になり、
   まとめて回収されるタイミングで処理が止まります（カクつきの典型的な原因）
3. **リセット漏れによる怪奇現象**。使い回しを自前で書くと、前回の状態（残り寿命・親・
   スケール・パーティクルの再生位置）が残ったまま再利用されて、原因の分かりにくい不具合になります

> 📖 **用語 — GC（ガベージコレクション）**: 使われなくなったメモリを自動回収する仕組み。
> 回収は「まとめて」行われるため、確保・破棄を繰り返すと不定期にフレームが止まります。
> プールは確保そのものを減らすことでこれを避けます。

### 標準の `UnityEngine.Pool.ObjectPool` があるのに自作した理由

3点あり、いずれも「事故を発生点で止める」ためです。

1. **二重返却を即例外にする**。同じインスタンスが2箇所で使われる事故は、
   症状（表示が飛ぶ・当たり判定が二重に出る）が原因から離れた場所で出ます。
   貸出中の集合を持ち、返却時に照合して `PoolException` を投げます
2. **統計を持つ**。ピーク同時使用数と破棄数が見えないと、保持上限を何個にすべきか決められません
3. **貸出・返却を型に載せる**（`IPoolable`）。リセットを書く場所が型として決まっていれば、
   書き忘れをレビューで見つけられます

先行実装として GameCore に `EventPool`（`Assets/Script/GameCore/Runtime/Events/EventPool.cs`）があり、
「二重返却は即例外」「保持上限を超えた分は捨てる」という方針はそこから引き継いでいます。

## 2. 全体像

### 部品表

| 部品 | 場所 | 役割 |
|---|---|---|
| `ObjectPool<T>` | `Assets/Script/Pooling/Runtime/ObjectPool.cs` | 純C#の汎用プール。貸し借りの規約と統計を持つ |
| `IPoolable` | 同上 | 貸出・返却の通知を受け取る任意契約（リセットの置き場） |
| `PoolStats` | 同上 | 統計（Created / Rented / Idle / PeakRented / Discarded） |
| `PoolException` | 同上 | 規約違反（二重返却・他所からの返却・工場が null） |
| `GameObjectPool` | `Assets/Script/Pooling/Runtime/GameObjectPool.cs` | プレハブ専用プール。非表示化・親の付け戻しを担当 |
| `PooledInstance` | 同上 | 貸し出した実体に付く目印（貸し主を覚える） |
| `PoolRegistry` | `Assets/Script/Pooling/Runtime/PoolRegistry.cs` | プレハブごとのプールをまとめる台帳 |

`Seed.Pooling` の asmdef は**参照ゼロ**です（`UnityEngine` のみ）。どの基盤からでも使えます。

### データの流れ

```
呼び出し側（App の方針: いつ出すか・いつ返すか）
   │ registry.Rent(prefab, position, rotation)
   ▼
PoolRegistry ──(プレハブごと)──▶ GameObjectPool
                                   │ 待機列にあれば取り出す / 無ければ Instantiate（初回だけ）
                                   │ 親を付け替え・位置と向きを設定・SetActive(true)
                                   └─▶ IPoolable.OnRent() を通知
   …使用中…
   │ registry.Return(instance)   ← 目印から貸し主を辿るので返却先を持ち回らない
   ▼
GameObjectPool
   ├─ IPoolable.OnReturn() で状態をリセット
   ├─ SetActive(false) → 親を待機用の親へ戻す
   └─ 待機列へ（保持上限を超えていたら破棄して Discarded に数える）
```

## 3. 動かして試す

統合デモの戦闘フェーズがヒットエフェクトで実演しています。

1. 空の GameObject に `Sample_GameFlowRunner` を付けて **Play** → **[1]** で草原へ出撃
2. Hierarchy で `Stage_Grassland > Pools` を開く。`HitEffect`（非表示のテンプレート）と、
   **事前生成された6個**の複製が並んでいます（`Prewarm` の結果）
3. **[1]** で攻撃して敵に当てる → 黄色い球が一瞬出ます。このとき Hierarchy を見ると、
   **待機していた実体の1つが有効化されるだけ**で、新しい行は増えません
4. 連続で攻撃して同時に複数出しても、増えるのは同時使用数の上限までです
5. 0.35 秒後に自動で非表示へ戻ります（`Pools` の下に戻っている）

期待される結果:

- 攻撃を繰り返しても Hierarchy の行数が増え続けない（= Instantiate されていない）
- Profiler で見ると被弾時の GC 確保が出ない（`Destroy` していないため）

エディタで統計を見たい場合は、`PoolRegistry.Snapshot()` の結果をログに出します。

```csharp
foreach (var (prefab, stats) in _pools.Snapshot())
{
    Debug.Log($"{prefab}: {stats}");   // 例: HitEffect: 生成6 貸出2 待機4 ピーク3 破棄0
}
```

## 4. コードで使う

### 最小例 — 純C#のプール

```csharp
using Seed.Pooling;

// 工場（新規生成の方法）と保持上限を渡す
var pool = new ObjectPool<Bullet>(() => new Bullet(), maxRetained: 64);

var bullet = pool.Rent();   // 待機があれば再利用、無ければ生成
// …使う…
pool.Return(bullet);        // 返す（IPoolable なら OnReturn が呼ばれる）

var stats = pool.Stats;     // 生成/貸出/待機/ピーク/破棄
```

リセットを型に載せる場合は `IPoolable` を実装します。

```csharp
/// <summary>使い回される弾（状態のリセットを型として持つ）。</summary>
public sealed class Bullet : IPoolable
{
    public float Life;
    public Vector3 Velocity;

    /// <summary>貸出時の初期化。</summary>
    public void OnRent() { Life = 3f; }

    /// <summary>返却時のリセット（前回の状態を残さない）。</summary>
    public void OnReturn() { Life = 0f; Velocity = Vector3.zero; }
}
```

### 実戦例 — GameObject を台帳経由で使い回す（デモの構成）

```csharp
using Seed.Pooling;
using UnityEngine;

// 1. 台帳を作る（待機中の実体は poolRoot の下にまとまる＝Hierarchy が散らからない）
var poolRoot = new GameObject("Pools").transform;
poolRoot.SetParent(_stageRoot.transform, false);
_pools = new PoolRegistry(poolRoot, defaultMaxRetained: 32);

// 2. 複製元を用意して事前生成（読み込み時にコストを払う）
_pools.Prewarm(_hitEffectPrefab, 6);

// 3. 借りる（プールが無ければ自動で作られる）
var effect = _pools.Rent(_hitEffectPrefab, position, Quaternion.identity);

// 4. 返す（目印から貸し主を辿るので、どのプールから来たか覚えておく必要がない）
_pools.Return(effect);

// プール由来でない可能性があるものは、まとめてこちらで扱える
_pools.ReturnOrDestroy(maybePooled);
```

寿命の管理は**呼び出し側の方針**です。デモは残り時間つきのリストで持ち、
`TickPhase.Drain` で減算して 0 になったら返しています。

```csharp
/// <summary>エフェクトの寿命を進め、切れたものをプールへ返す。</summary>
private void UpdateEffects(float deltaTime)
{
    for (var i = _liveEffects.Count - 1; i >= 0; i--)   // 後ろから走査（途中で削除するため）
    {
        var live = _liveEffects[i];
        var remain = live.Remain - deltaTime;
        if (remain <= 0f)
        {
            _pools.Return(live.Instance);
            _liveEffects.RemoveAt(i);
            continue;
        }
        _liveEffects[i] = (live.Instance, remain);
    }
}
```

## 5. 仕組み

> 📖 **用語 — 事前生成（Prewarm）**: 実行前に必要数を作っておくこと。読み込み画面のような
> フレーム落ちが許される場面でコストを払い、戦闘中の生成をゼロにします。
> 目安は統計の `PeakRented`（同時使用数の最大）です。

### 統計の読み方（プール量の決め方）

| 項目 | 意味 | 読み方 |
|---|---|---|
| `Created` | 累計の新規生成数 | 実行中に増え続けるなら待機が足りていない |
| `Rented` | 現在の貸出数 | 何もしていないのに 0 に戻らないなら**返却漏れ** |
| `Idle` | 待機中の数 | 常に大きいなら上限を下げてよい |
| `PeakRented` | 同時貸出数の最大 | **保持上限はこの値を目安に決める** |
| `Discarded` | 上限超過で捨てた数 | 増え続けるなら上限が小さすぎる |

「`PeakRented` を上限にし、`Discarded` が 0 になるまで上げる」が基本の調整手順です。

> 📖 **用語 — Profiler**: Unity 付属の性能計測ウィンドウ（`Window > Analysis > Profiler`）。
> GC Alloc の行を見ると、そのフレームで確保されたメモリ量が分かります。
> プールが効いていれば、弾やエフェクトを出しても確保が出ません。

### 保持上限を超えた返却は「捨てる」

上限を超えた返却は待機列へ入れず破棄します（`Discarded` に数える）。
これは「一時的に大量に使った直後、その分のメモリを永久に抱え続ける」のを防ぐためです。
先行実装の `EventPool` と同じ方針です。

### GameObject 版が返却時にすること

順序は「通知 → 非表示 → 親の付け戻し」です。

- **通知**（`IPoolable.OnReturn`）: 実体側が自分の状態を捨てる機会。非表示より先に呼ぶので、
  まだ有効な状態でリセットできます
- **非表示**（`SetActive(false)`）: 更新も描画も止まります
- **親の付け戻し**: 待機用の親へ戻すことで Hierarchy が整理され、
  フェーズ退場時に親ごと破棄できます

**位置と向きは触りません**。貸出時に必ず上書きするので、返却時に整える必要がないからです。
非表示のまま待機するので「前回の姿勢が一瞬見える」ことも起きません。

> 📖 **用語 — SetActive**: GameObject の有効/無効の切り替え。無効にすると `Update` も描画も
> 止まるため、破棄せずに「居ないことにする」ことができます。プールの待機状態はこれで表現します。

### 目印から貸し主を辿る

貸し出した実体には `PooledInstance` が付き、貸し主のプールを覚えています。
返却先を呼び出し側が持ち回る設計だと、持ち回りを間違えて別のプールへ混ざる事故が起きます。
実体自身に書いておけば `PoolRegistry.Return(instance)` だけで正しい場所へ帰ります。

### 三大規約との関係

- **方針は App**: 「いつ出すか・いつ返すか・何秒で消えるか」はゲームの手触りなので App が決めます。
  基盤は貸し借りの仕組みと事故検出だけを持ちます
- **状態は Tick**: デモの寿命管理は `TickPhase.Drain` で進みます（艶ではなく状態として扱う）
- **命令の処理者は1基盤**: プールは Hub を使いません（座標や実体を毎フレーム流す用途ではないため）。
  必要なら App が窓口を `ServiceRegistry` へ登録します

> 📖 **用語 — 貸出中の集合**: プールが「今どれを貸しているか」を覚えている集合（`HashSet<T>`）。
> 返却時にここから取り除けなければ「二重返却」か「他所のもの」と判定できます。
> この照合があるおかげで、事故を発生点で止められます。

## 6. よくあるつまずき

- **症状: `PoolException`「二重返却、またはこのプールが貸したものではない」**
  → 原因: 同じ実体を2回返している、または別のプールへ返している
  → 対処: 返却を1箇所に集める（デモのように寿命リストで管理する）。
  この例外は事故を発生点で止めるためのもので、握り潰してはいけません

- **症状: `Rented` が減らない / `Created` が増え続ける**
  → 原因: 返却漏れ。イベント購読やコルーチンの途中で参照が失われている
  → 対処: `Snapshot()` をログに出して、どのプレハブで漏れているか特定する

- **症状: 返した実体が画面に残る**
  → 原因: プール由来でない実体を `Return` に渡している（`false` が返って何も起きない）
  → 対処: `ReturnOrDestroy` を使うか、戻り値を確認する

- **症状: 再利用したエフェクトが「途中から」再生される**
  → 原因: パーティクルなどの内部状態がリセットされていない
  → 対処: 実体側のコンポーネントに `IPoolable` を実装し、`OnRent` で
  `ParticleSystem.Clear()` → `Play()` のように作り直す

- **症状: EditMode テストで `Destroy` が効かない**
  → 原因: EditMode では `Object.Destroy` が遅延する
  → 対処: テストでは `Object.DestroyImmediate` を使う（`PoolingTests` の TearDown がこの形）

- **症状: プレハブが `null` でプールを作ろうとして例外**
  → 原因: 参照切れ（シーン破棄・アセット削除）
  → 対処: `GetOrCreate` の前に存在を確認する。例外文言は「プレハブが null のプールは作れない」

## 7. 増やす・拡張する

| やりたいこと | 手順 |
|---|---|
| 新しい種類を使い回す | `PoolRegistry.Rent(prefab, …)` を呼ぶだけ（プールは自動で作られる） |
| リセットを型に載せる | 実体のコンポーネントに `IPoolable` を実装（`OnRent` / `OnReturn`） |
| 読み込み時にコストを払う | `Prewarm(prefab, count)`。目安は `PeakRented` |
| 純C#のオブジェクトを使い回す | `ObjectPool<T>` を直接使う（Unity 非依存） |
| プール量を調整する | `Snapshot()` の `PeakRented` と `Discarded` を見て `maxRetained` を決める |
| 生成ステージのタイルを使い回す | `StageBuilder` の工場をプール経由の実体化に差し替える（→ [11_StageGen.md](11_StageGen.md)） |

## 8. 関連ファイルとテスト

- `Assets/Script/Pooling/Runtime/ObjectPool.cs`（純C#の本体・`IPoolable`・`PoolStats`・`PoolException`）
- `Assets/Script/Pooling/Runtime/GameObjectPool.cs`（プレハブ版・`PooledInstance`）
- `Assets/Script/Pooling/Runtime/PoolRegistry.cs`（台帳・統計の一覧）
- `Assets/Script/Pooling/Tests/Editor/PoolingTests.cs`（15件。再利用・二重返却の例外・
  上限超過の破棄・事前生成・ピーク記録・台帳の振る舞いを仕様として読める）
- `Assets/Script/GameCore/Runtime/Events/EventPool.cs`（先行実装。方針の出所）
- `Assets/Script/App/Samples/Sample_BattlePhase.cs` の `BuildPools` / `SpawnHitEffect` / `UpdateEffects`

[← 前: 14_Cameras](14_Cameras.md) | [索引](README.md) | [次: 16_World →](16_World.md)
