[简体中文](README.md) | [English](README-EN.md) | **繁體中文**

<div align="center">

<img src="Plain Craft Launcher 2/Images/icon.ico" alt="Logo" width="80" height="80">

# PCL Community Edition

**SpacJoy 的個人分支** · 基於 [PCL-Community/PCL-CE](https://github.com/PCL-Community/PCL-CE)

[上游儲存庫](https://github.com/PCL-Community/PCL-CE) |
[PCL 原始儲存庫](https://github.com/Meloong-Git/PCL) |
[開發文件](https://docs.pclc.cc/ce/developers/setup)

</div>

> [!IMPORTANT]
> 本儲存庫是 **PCL CE 的個人分支（fork）**，並非官方發行版。
>
> - 不提供發行包、自動更新與技術支援，請自行[建置](#-建置)。
> - 本分支引入的問題請**不要**回報到 [PCL-Community/PCL-CE](https://github.com/PCL-Community/PCL-CE)，該處的回饋管道僅對應官方版本。
> - 由個人按需維護，不保證處理外部 Issue / PR，合併節奏跟隨上游 `dev` 分支。

## 📌 本分支相對上游的改動

基於上游 `v2.15.1`（`upstream/dev`）之後的提交：

| 提交 | 類型 | 說明 |
|---|---|---|
| `2845318` | feat | 移除中國大陸地區功能限制（下載、補全、載入器安裝、實例修復/重設、整合包安裝等不再依區域停用） |
| `8ae19b9` | fix | 修正停用/刪除模組時誤報「檔案被佔用」：容許 `fabric.mod.json` 中的裸換行，內嵌（JiJ）解析失敗不再永久阻塞實例 |
| `096dacb` | fix | 寫入 `options.txt` 等 ini 與啟動腳本時不再輸出 UTF-8 BOM，修正每次經啟動器啟動遊戲設定被重設 |
| `adb5f28` | fix | 修正輸入彈窗無法取消、重新命名時確定按鈕被重名校驗攔下導致的死鎖 |
| `eca1dcc` | fix | 無新日誌時也檢查遊戲行程是否結束，修正關閉遊戲後按鈕不消失、崩潰分析不觸發 |
| `b8c8012` | fix | 修正檢視前置資源時詳情頁內容不重新整理（單例頁面於 `OnNavigatedFrom` 重設重新整理標記） |
| `663452d` | feat | 工具頁新增「遊戲日誌」子頁面：增量讀取 `logs/latest.log`、分級著色、自動偵測編碼、瀏覽歷史日誌與崩潰報告 |

完整差異：`git log --oneline upstream/dev..HEAD` / `git diff upstream/dev..HEAD`。

## 🛠️ 建置

建置方式與上游一致，額外注意密鑰注入與增量產生器兩個坑，詳見 [docs/BUILD.md](docs/BUILD.md)。

最短路徑（Windows，.NET 10 SDK）：

```bash
export PCL_WRITE_SECRET=1
export PCL_MS_CLIENT_ID=<你的 Microsoft OAuth Client ID>
dotnet publish "Plain Craft Launcher 2/Plain Craft Launcher 2.csproj" \
  -p:Configuration=Release -p:Platform=x64 \
  -p:DeleteExistingFiles=true -o ./artifact --no-self-contained -t:Rebuild
```

產物為 `artifact/Plain Craft Launcher 2.exe`。缺少 `PCL_MS_CLIENT_ID` 時啟動器仍可執行，但微軟帳號登入會報 *Microsoft OAuth client id is not configured*。

## 🔄 與上游同步

```bash
git remote add upstream https://github.com/PCL-Community/PCL-CE.git  # 僅首次
git fetch upstream
git merge upstream/dev        # 或 git rebase upstream/dev
```

## 關於 PCL CE

PCL CE 是基於 PCL 開放原始碼二次開發的社群版本，包括了主線暫未製作的功能和改進。社群版的版本號與主線並非嚴格對應關係，也請不要向官方儲存庫回饋社群版問題。

**開發群:** [599620549](https://qm.qq.com/q/PNQA4Md8wc)[✦](https://t.me/+78XW2bUOvKJjMTZl) | **使用者 1 群:** [1028074835](https://qm.qq.com/q/qrMGJo8BQQ) | **使用者 2 群:** [1070029541](https://qm.qq.com/q/lBywbDMWhG)

## 💻 支援平台

| 作業系統 | 支援情況 | 環境要求 |
|---|---|---|
| Windows 10 1809 (17763) 或更高 | ✅ 完整支援 | [.NET 10 Desktop Runtime](https://get.dot.net/10) |
| Windows 8 - Windows 10 1809 (17763) | ⚠️ 理論能跑，酌情提供社群支援 | [.NET 10 Desktop Runtime](https://get.dot.net/10) |
| Windows 7 或更低版本 | ❌ 不支援 | / |
| macOS / Linux / 其他作業系統 | ⚠️ 僅跨平台開發支援（交叉編譯）| [.NET 10 SDK](https://get.dot.net/10) |

**✅ 完整支援**：盡可能提供一切相關支援，但必須確保啟動器為最新版本。

**⚠️ 理論能跑，酌情提供社群支援**：PCL CE 應該可以在這些平台上執行，但不保證功能完全可用。你可能需要升級到完整支援的系統版本以獲得進一步社群技術支援。

**❌ 不支援**：PCL CE 在這些平台的可用性較低，甚至根本打不開。請升級作業系統以使用 PCL CE。

**⚠️ 僅跨平台開發支援（交叉編譯）**：PCL CE 的原始碼可以在 macOS 與 Linux 平台編譯，但無法直接執行。作為開發者，你可以在這些平台上進行開發，然後將編譯產物轉移到 Windows 系統測試。

**注**：
社群僅對最新版本的啟動器提供支援。
取決於部分問題的特殊性（如系統不完整），有時你仍然必須升級作業系統以繼續獲得支援。
PCL CE 始終建議使用最新版本的作業系統以獲得最佳體驗。
你仍然可以嘗試在不受支援的系統上使用 PCL CE，但可能會遇到很多額外問題。

## 🔒 授權條款

- `Plain Craft Launcher 2/` 使用 [自訂授權條款](Plain Craft Launcher 2/LICENCE)（《PCL 分發有限許可》與《PCL 儲存庫合理使用指南》）
- `其餘所有目錄` 使用 [Apache License 2.0](LICENSE)

本分支屬於上述指南中的「重度使用」：由第三方基於 PCL 獨立二次創作，與 PCL 官方及 PCL CE 官方均無隸屬關係。

## 🙏 致謝

- [Meloong-Git/PCL](https://github.com/Meloong-Git/PCL) 與 PCL 全體貢獻者
- [PCL-Community/PCL-CE](https://github.com/PCL-Community/PCL-CE) 與社群版貢獻者
