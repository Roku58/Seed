# 09. Seed.Persistence — セーブ/ロード

「byte[] を安全にディスクへ置く」層。契約は `ISaveStore`（TrySave/TryLoad/Exists/Delete、
すべて bool・例外を投げない）。何を byte[] にするか（直列化）はゲーム側の責務。
純C#アセンブリ（UnityEngine 非依存）。

## 実装2種

| 実装 | 用途 | 特徴 |
|---|---|---|
| `FileSaveStore(directory, version)` | 本番 | 封筒形式（magic "SEDS"+版数+FNV-1aハッシュ）＋一時ファイル→`File.Replace` の原子的書き込み。拡張子 `.sav` |
| `MemorySaveStore()` | テスト・体験版 | メモリのみ。`Count` プロパティあり |

## 試す（デモには配線されていない→自作1本で）

デモにセーブキーは無い。空 GameObject に以下を付けて Play:

```csharp
using Seed.App;
using Seed.Input;
using Seed.Persistence;
using UnityEngine;

/// <summary>[3]で保存・[2]で読込する最小デモ。</summary>
public sealed class SaveDemo : MonoBehaviour
{
    private InputRouter _input;
    private FileSaveStore _store;

    private void Start()
    {
        _input = new InputRouter(new Sample_KeyboardReader());
        _store = new FileSaveStore(Application.persistentDataPath, version: 1);
    }

    private void Update()
    {
        _input.Tick();
        if (_input.WasPressedThisFrame(ActionId.Submit))   // [3]
            Debug.Log("保存: " + _store.TrySave("slot1", new byte[] { 42 }));
        if (_input.WasPressedThisFrame(ActionId.Interact)  // [2]
            && _store.TryLoad("slot1", out var data))
            Debug.Log("読込: " + data[0]);
    }
}
```

- 保存先: 渡したディレクトリ直下の `slot1.sav`
  （macOS の persistentDataPath は `~/Library/Application Support/<会社名>/<製品名>/`）
- `.sav` を1バイト書き換えて再読込すると TryLoad が false（ハッシュ不一致で破損拒否）
- テストで見る場合: Test Runner の EditMode で `SaveStoreTests`（6件）

## ハマりどころ

- key は英数字と `- _ .` のみ（パス注入防止。`a/b` は黙って false）
- 版数不一致の TryLoad は false（=破損と同じ扱い）。マイグレーションは上位層の判断
- 全メソッドが例外を投げず false を返す契約——**戻り値を無視すると保存失敗に気付けない**
- persistentDataPath 等の Unity API は呼び出し側で解決して文字列で渡す（基盤は純C#）

## 増やすとき

- クラウド保存など = `ISaveStore`（`Assets/Script/Hub/Contracts/ISaveStore.cs`）を実装して差し替え
- リプレイの保存 = GameCore の `InputJournalCodec.ToBytes` で byte[] 化 → ISaveStore へ（→ [03_GameCore.md](03_GameCore.md)）

主要ファイル: `Assets/Script/Persistence/Runtime/FileSaveStore.cs` / `SaveEnvelope.cs`。
テスト: `Assets/Script/Persistence/Tests/Editor/SaveStoreTests.cs`
