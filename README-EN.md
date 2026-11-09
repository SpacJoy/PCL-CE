[简体中文](README.md) | **English** | [繁體中文](README-ZH_TW.md)

<div align="center">

<img src="Plain Craft Launcher 2/Images/icon.ico" alt="Logo" width="80" height="80">

# PCL Community Edition

**A personal fork by SpacJoy** · based on [PCL-Community/PCL-CE](https://github.com/PCL-Community/PCL-CE)

[Upstream CE repository](https://github.com/PCL-Community/PCL-CE) |
[Original PCL repository](https://github.com/Meloong-Git/PCL) |
[Developer docs](https://docs.pclc.cc/en/ce/developers/setup)

</div>

> [!IMPORTANT]
> This repository is a **personal fork of PCL CE**, not an official distribution.
>
> - No release builds, auto-update, or support are provided. Build it yourself — see [Building](#-building).
> - Please **do not** report problems caused by this fork to [PCL-Community/PCL-CE](https://github.com/PCL-Community/PCL-CE); its feedback channels cover the official build only.
> - Maintained on a best-effort, personal-use basis. External issues and pull requests are not guaranteed to be handled; upstream merges follow the `dev` branch.

## 📌 Changes compared to upstream

Commits on top of upstream `v2.15.1` (`upstream/dev`):

| Commit | Type | Description |
|---|---|---|
| `2845318` | feat | Remove the mainland-China regional feature restrictions (downloads, client completion, loader installation, instance repair/reset, modpack installation are no longer disabled by region) |
| `8ae19b9` | fix | Fix a false "file in use" error when disabling or deleting mods: tolerate raw newlines in `fabric.mod.json` so a failed embedded (JiJ) parse no longer blocks the instance permanently |
| `096dacb` | fix | Stop emitting a UTF-8 BOM when writing `options.txt` and other ini files or the launch script, fixing game settings being reset on every launch through the launcher |
| `adb5f28` | fix | Fix input dialogs that could not be cancelled and the rename deadlock caused by the duplicate-name check blocking the OK button |
| `eca1dcc` | fix | Check whether the game process exited even when no new log lines arrive, fixing the stuck "close game" button and skipped crash analysis |
| `b8c8012` | fix | Fix the dependency detail page not refreshing (reset the refresh flag in `OnNavigatedFrom` for singleton pages) |
| `663452d` | feat | New "Game Log" tool page: incremental `logs/latest.log` reading, per-level coloring, encoding auto-detection, and browsing of previous logs and crash reports |

Full diff: `git log --oneline upstream/dev..HEAD` / `git diff upstream/dev..HEAD`.

## 🛠️ Building

Building is the same as upstream, with two extra pitfalls around secret injection and the incremental source generator — see [docs/BUILD.md](docs/BUILD.md).

Shortest path (Windows, .NET 10 SDK):

```bash
export PCL_WRITE_SECRET=1
export PCL_MS_CLIENT_ID=<your Microsoft OAuth client ID>
dotnet publish "Plain Craft Launcher 2/Plain Craft Launcher 2.csproj" \
  -p:Configuration=Release -p:Platform=x64 \
  -p:DeleteExistingFiles=true -o ./artifact --no-self-contained -t:Rebuild
```

The output is `artifact/Plain Craft Launcher 2.exe`. Without `PCL_MS_CLIENT_ID` the launcher still runs, but Microsoft sign-in fails with *Microsoft OAuth client id is not configured*.

## 🔄 Syncing with upstream

```bash
git remote add upstream https://github.com/PCL-Community/PCL-CE.git  # first time only
git fetch upstream
git merge upstream/dev        # or: git rebase upstream/dev
```

## About PCL CE

PCL CE is a community edition developed as a secondary project based on PCL's open-source code. It includes features and improvements that have not yet been implemented in the main branch. Version numbers of the community edition do not strictly correspond to the main branch, and community edition issues should not be reported to the official repository.

**Dev group:** [599620549](https://qm.qq.com/q/PNQA4Md8wc)[✦](https://t.me/+78XW2bUOvKJjMTZl) | **User group 1:** [1028074835](https://qm.qq.com/q/qrMGJo8BQQ) | **User group 2:** [1070029541](https://qm.qq.com/q/lBywbDMWhG)

## 💻 Supported platforms

| Operating System | Support Status | Requirements |
|---|---|---|
| Windows 10 1809 (17763) or later | ✅ Fully supported | [.NET 10 Desktop Runtime](http://get.dot.net/10) |
| Windows 8 to Windows 10 1809 (17763) | ⚠️ Expected to run; community support offered at discretion | [.NET 10 Desktop Runtime](http://get.dot.net/10) |
| Windows 7 or earlier | ❌ Not supported | N/A |
| macOS / Linux / Other OS | ⚠️ Cross-platform development only (cross-compilation) | [.NET 10 SDK](http://get.dot.net/10) |

**✅ Fully supported**: We will provide as much support as possible, but you must ensure you are using the latest version of the launcher.

**⚠️ Expected to run; community support offered at discretion**: PCL CE is expected to run on these platforms, but full functionality is not guaranteed. You may need to upgrade to a fully supported OS version to receive further community technical support.

**❌ Not supported**: PCL CE may not be usable on these platforms and might not even launch at all. Please upgrade your OS to use PCL CE.

**⚠️ Cross-platform development only (cross-compilation)**: PCL CE's source code can be built on macOS and Linux, but it cannot run directly. As a developer, you can develop on these platforms and then move the build artifacts to Windows for testing.

**Note**:
The community only supports the latest version of the launcher.
Depending on the nature of certain issues (e.g., an incomplete system environment), you may still need to upgrade your OS to continue receiving support.
PCL CE always recommends using the latest version of your OS for the best experience.
You can still try using the latest launcher version on unsupported platforms, but you may encounter many additional problems.

## 🔒 License

- `Plain Craft Launcher 2/` uses the [Custom License](Plain Craft Launcher 2/LICENCE) (the PCL Distribution Limited License and the PCL Repository Fair Use Guidelines)
- `All other directories` use [Apache License 2.0](LICENSE)

Under those guidelines this fork counts as "heavy use": an independent third-party derivative work, not affiliated with PCL or PCL CE.

## 🙏 Credits

- [Meloong-Git/PCL](https://github.com/Meloong-Git/PCL) and all PCL contributors
- [PCL-Community/PCL-CE](https://github.com/PCL-Community/PCL-CE) and all community edition contributors
