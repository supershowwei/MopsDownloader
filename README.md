# MopsDownloader

公開資訊觀測站電子資料查詢作業 (doc.twse.com.tw) 下載器。C# Console App，.NET Framework 4.8。

## 建置

```bash
dotnet build src/MopsDownloader.sln -c Release
```

或用 Visual Studio 2022 開啟 `src/MopsDownloader.sln`。

## 發佈

```powershell
.\publish.ps1
```

版本號取自 `src/MopsDownloader/MopsDownloader.csproj` 的 `<Version>`，輸出到 `dist\<版本>\`（例如 `dist\1.0.0\`）。
發新版時先修改 `<Version>`；同版本資料夾已存在時會中止，需覆寫請加 `-Force`。

## 用法

```
MopsDownloader fin    <股票代號> <年度> [-o 輸出資料夾] [-f]
MopsDownloader annual <股票代號> <年度> [-o 輸出資料夾] [-f]
```

| 指令 | 說明 | 輸出檔名 |
|---|---|---|
| `fin` | 財務報告書，只下載中文版合併財報 | `代號_年度Q1`、`Q2`、`Q3`、第四季為 `代號_年度Y` |
| `annual` | 股東會年報，F11 (股東會後修訂本) 優先，無則 F04 (尚未適用永續揭露準則) | `代號_年度F11` 或 `代號_年度F04` |

- 年度為民國年。`annual` 的年度是「年報所屬年度」，程式會自動以 `year + 1` 查詢網站。
- 股票代號可用逗號分隔多檔，例如 `2330,8436`。
- `-o` 預設為目前目錄下的 `downloads`；`-f` 表示檔案已存在時覆寫 (預設略過)。
- 網站回應「查詢過量」時會自動等待後重試。
- 結束代碼：`0` 全部成功、`1` 有查無資料或失敗、`2` 參數錯誤。

## 範例

```bash
MopsDownloader fin 8436 115
MopsDownloader annual 1582 113 -o D:\reports
```
