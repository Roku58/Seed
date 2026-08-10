# 15. Seed.Pooling — オブジェクトプール（生成/破棄のコストと GC を抑える)

[← 前: 14_Cameras](14_Cameras.md) | [索引](00_Roadmap.md) | [次: 16_World →](16_World.md)

## この章で分かること

- 弾・エフェクトなど「短命で大量」の実体を Instantiate/Destroy せず使い回す方法
- 純C#プール（`ObjectPool<T>`）と GameObject プール・台帳（`PoolRegistry`）の使い分け
- 統計（ピーク・破棄数）からプール量を決め、返却漏れを見つける方法
- 二重返却が**即例外**になる理由

## 前提

- [01_Demo.md](01_Demo.md) — 戦闘フェーズの起動（デモの被弾エフェクトがプール経由）

この章が初出の主な用語: GC（ガベージコレクション）/ Prewarm / 保持上限。

## 1. これは何か

`Instantiate` は生成コスト、`Destroy` は GC の弾込めです。毎フレームどちらかが走ると
スパイク（カクつき）の原因になります。プールは使い終わった実体を**非表示にして待機列へ
戻し**、次の要求で再利用します。

> 📖 **用語 — GC（ガベージコレクション）**: 使われなくなったメモリの自動回収。回収の瞬間に
> 処理が止まるため、ゲームでは「そもそもゴミを出さない」（使い回す）のが定石。

標準の `UnityEngine.Pool.ObjectPool` ではなく自作しているのは、次の3点を型で守るためです:

1. **二重返却を即例外にする**——同じ実体が2箇所で使われる事故は、症状が発生点から
   離れた場所で出る。発生の瞬間に `PoolException` で落とすのが一番安い
2. **統計を持つ**——ピーク同時使用数と破棄数が見えないと、プール量を根拠を持って決められない
3. **`IPoolable` 契約**——貸出/返却の通知を型に載せ、リセット漏れをレビューで見つけやすくする

GameCore の `EventPool`（先行実装）と同じ「保持上限超過は捨てる」方針を引き継いでいます。

## 2. 全体像

| 部品 | 役割 |
|---|---|
| `ObjectPool<T>` | 純C#の貸し借り本体（工場・上限・統計・二重返却検査） |
| `IPoolable` | 貸出（OnRent）/返却（OnReturn）の通知を受けたい実体が実装 |
| `PoolStats` | 統計（Created / Rented / Idle / PeakRented / Discarded） |
| `GameObjectPool` | プレハブ1種のプール（返却時: 通知→非表示→親の付け戻し） |
| `PooledInstance` | 実体に付く「貸し主」の目印 |
| `PoolRegistry` | プレハブ→プールの台帳。返却先を持ち回らなくて済む |

## 3. 動かして試す

1. 統合デモの戦闘で攻撃を当てる/受ける——**黄色い球のヒットエフェクト**が出て 0.35 秒で消える
2. Hierarchy の `Stage_Grassland/Pools` を開くと、消えたエフェクトが**非表示で待機**しているのが見える
   （Destroy されていない＝次の被弾で同じ実体が再利用される）
3. 何度攻撃しても `HitEffect` の実体数が増えなくなる点を確認（ピークで頭打ち）

## 4. コードで使う

### 最小例 — 台帳から借りて返す

```csharp
var pools = new PoolRegistry(poolRoot, defaultMaxRetained: 32);
pools.Prewarm(effectPrefab, 6);   // 読み込み時に確保して実行中の生成を避ける

// 借りる（プールが無ければ作られる）
var effect = pools.Rent(effectPrefab, position, Quaternion.identity);

// 返す（実体の目印から貸し主を辿る＝どのプールから来たか覚えなくてよい）
pools.Return(effect);
// プール由来か不明なものは pools.ReturnOrDestroy(instance);
```

### 実戦例 — 寿命は App の方針（デモの被弾エフェクト）

```csharp
// 借りるとき残り時間を添えて記録し（Sample_BattlePhase.SpawnHitEffect）
_liveEffects.Add((effect, 0.35f));

// TickPipeline の Drain で寿命を進め、切れたら返す（UpdateEffects）
for (var i = _liveEffects.Count - 1; i >= 0; i--) { /* 残り時間 <= 0 で pools.Return */ }
```

「いつ返すか」はゲームの方針なので基盤は持ちません——時間・アニメ終了・画面外など、
判断は呼び出し側が書きます。

### 純C#の使い回し（GameObject 以外）

```csharp
var pool = new ObjectPool<PathRequest>(() => new PathRequest(), maxRetained: 64);
var req = pool.Rent();     // IPoolable 実装なら OnRent が呼ばれる
pool.Return(req);          // OnReturn で自分の状態を捨てる（リセット漏れ対策）
```

## 5. 仕組み

- **返却時にすること**は「`IPoolable` 通知 → 非表示 → 親を待機ルートへ」の3つだけ。
  **位置・向きは触らない**——貸出時に必ず上書きされるため、非表示のまま待機させれば
  前回の姿勢が一瞬見える事故が起きない
- 保持上限（`MaxRetained`）を超えた返却は**捨てる**（`Discarded` に計上）。際限なく
  溜め込むとピーク後もメモリを占有し続けるため
- `PoolRegistry.Return` は実体の `PooledInstance.Owner` から貸し主を辿る。プール外の
  実体は `false` を返し、呼び出し側が Destroy を選べる
- 三大規約との関係: プールは「艶」の実体管理であり、ゲームの真実（Tick 側の状態）には
  関与しない。デモで寿命を Drain フェーズに置いているのは「表示物の後始末」だから

## 6. よくあるつまずき

- **症状**: `PoolException: 不正な返却` → **原因**: 二重返却、または別プールの実体を返した →
  **対処**: 返却箇所を1つに絞る（寿命管理の一元化）。これは事故検出であり無効化しない
- **症状**: `PeakRented` が増え続ける → **原因**: 返却漏れ（リーク） →
  **対処**: `PoolRegistry.Snapshot()` をデバッグ表示し、増え続けるプレハブ名から辿る
- **症状**: `Discarded` が多い → **原因**: 保持上限が実際のピークより小さい →
  **対処**: `PeakRented` を目安に `maxRetained` を上げる
- **症状**: 再利用された実体に前回の状態が残る → **原因**: リセット漏れ →
  **対処**: `IPoolable.OnReturn` に状態のリセットを書く（置き場を型で固定するのが本基盤の流儀）

## 7. 増やす・拡張する

- 弾・足跡・ダメージ数字など種類を増やす = プレハブを増やして `Rent` するだけ
  （台帳がプレハブごとにプールを自動作成）
- フェーズ退場時の片付け = `pools.Clear()`（待機中のみ破棄。貸出中は所有者の責任）
- 統計のゲーム内表示 = `Snapshot()` をデバッグメニュー（18章）のページに載せる

## 8. 関連ファイルとテスト

- `Assets/Script/Pooling/Runtime/`（ObjectPool / GameObjectPool / PoolRegistry）
- デモ統合: `Sample_BattlePhase.cs` の `BuildPools` / `SpawnHitEffect` / `UpdateEffects`
- テスト: `Assets/Script/Pooling/Tests/Editor/PoolingTests.cs`（15件——再利用・二重返却・上限・統計・台帳）

[← 前: 14_Cameras](14_Cameras.md) | [索引](00_Roadmap.md) | [次: 16_World →](16_World.md)
