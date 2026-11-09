**简体中文** | [English](README-EN.md) | [繁體中文](README-ZH_TW.md)

<div align="center">

<img src="Plain Craft Launcher 2/Images/icon.ico" alt="Logo" width="80" height="80">

# PCL Community Edition

**SpacJoy 的个人分支** · 基于 [PCL-Community/PCL-CE](https://github.com/PCL-Community/PCL-CE)

[上游仓库](https://github.com/PCL-Community/PCL-CE) |
[PCL 原始仓库](https://github.com/Meloong-Git/PCL) |
[开发文档](https://docs.pclc.cc/ce/developers/setup)

</div>

> [!IMPORTANT]
> 本仓库是 **PCL CE 的个人分支（fork）**，不是官方发行版。
>
> - 不提供发行包、自动更新与技术支持，请自行[构建](#-构建)。
> - 本分支引入的问题请**不要**提交到 [PCL-Community/PCL-CE](https://github.com/PCL-Community/PCL-CE)，那边的反馈渠道只对应官方版本。
> - 由个人按需维护，不保证处理外部 Issue / PR，合并节奏跟随上游 `dev` 分支。

## 📌 本分支相对上游的改动

基于上游 `v2.15.1`（`upstream/dev`）之后的提交：

| 提交 | 类型 | 说明 |
|---|---|---|
| `2845318` | feat | 移除中国大陆地区功能限制（下载、补全、加载器安装、实例修复/重置、整合包安装等不再按区域禁用） |
| `8ae19b9` | fix | 修复禁用/删除模组时误报「文件被占用」：容忍 `fabric.mod.json` 中的裸换行，内嵌（JiJ）解析失败不再永久阻塞实例 |
| `096dacb` | fix | 写 `options.txt` 等 ini 与启动脚本时不再输出 UTF-8 BOM，修复每次经启动器启动游戏设置被重置 |
| `adb5f28` | fix | 修复输入弹窗无法取消、重命名时确定按钮被重名校验拦下导致死锁 |
| `eca1dcc` | fix | 无新日志时也检查游戏进程是否退出，修复关闭游戏后按钮不消失、崩溃分析不触发 |
| `b8c8012` | fix | 修复查看前置资源时详情页内容不刷新（单例页面在 `OnNavigatedFrom` 复位刷新标记） |
| `663452d` | feat | 工具页新增「游戏日志」子页面：增量读取 `logs/latest.log`、分级着色、自动探测编码、浏览历史日志与崩溃报告 |

完整差异：`git log --oneline upstream/dev..HEAD` / `git diff upstream/dev..HEAD`。

## 🛠️ 构建

本分支的构建方式与上游一致，额外注意密钥注入与增量生成器两个坑，详见 [docs/BUILD.md](docs/BUILD.md)。

最短路径（Windows，.NET 10 SDK）：

```bash
export PCL_WRITE_SECRET=1
export PCL_MS_CLIENT_ID=<你的 Microsoft OAuth Client ID>
dotnet publish "Plain Craft Launcher 2/Plain Craft Launcher 2.csproj" \
  -p:Configuration=Release -p:Platform=x64 \
  -p:DeleteExistingFiles=true -o ./artifact --no-self-contained -t:Rebuild
```

产物为 `artifact/Plain Craft Launcher 2.exe`。缺少 `PCL_MS_CLIENT_ID` 时启动器仍可运行，但微软账号登录会报 *Microsoft OAuth client id is not configured*。

## 🔄 与上游同步

```bash
git remote add upstream https://github.com/PCL-Community/PCL-CE.git  # 仅首次
git fetch upstream
git merge upstream/dev        # 或 git rebase upstream/dev
```

## 关于 PCL CE

PCL CE 是基于 PCL 开源代码二次开发的社区版本，包括了主线暂未制作的功能和改进。社区版的版本号与主线并非严格对应关系，也请不要向官方仓库反馈社区版问题。

**开发群:** [599620549](https://qm.qq.com/q/PNQA4Md8wc)[✦](https://t.me/+78XW2bUOvKJjMTZl) | **用户 1 群:** [1028074835](https://qm.qq.com/q/qrMGJo8BQQ) | **用户 2 群:** [1070029541](https://qm.qq.com/q/lBywbDMWhG)

## 💻 支持平台

| 操作系统 | 支持情况 | 环境要求 |
|---|---|---|
| Windows 10 1809 (17763) 或更高 | ✅ 完整支持 | [.NET 10 Desktop Runtime](https://get.dot.net/10) |
| Windows 8 - Windows 10 1809 (17763) | ⚠️ 理论能跑，酌情提供社区支持 | [.NET 10 Desktop Runtime](https://get.dot.net/10) |
| Windows 7 或更低版本 | ❌ 不支持 | / |
| macOS / Linux / 其他操作系统 | ⚠️ 仅跨平台开发支持（交叉编译） | [.NET 10 SDK](https://get.dot.net/10) |

**✅ 完整支持**：尽可能提供一切相关支持，但必须确保启动器为最新版本。

**⚠️ 理论能跑，酌情提供社区支持**：PCL CE 应该可以在这些平台上运行，但不保证功能完全可用。你可能需要升级到完整支持的系统版本以获得进一步社区技术支持。

**❌ 不支持**：PCL CE 在这些平台的可用性较低，甚至压根打不开。请升级操作系统以使用 PCL CE。

**⚠️ 仅跨平台开发支持（交叉编译）**：PCL CE 的源代码可以在 macOS 与 Linux 平台编译，但无法直接运行。作为开发者，你可以在这些平台上进行开发，然后将编译产物转移到 Windows 系统测试。

**注**：
社区仅对最新版本的启动器提供支持。
取决于部分问题的特殊性（如系统不完整），有时你仍然必须升级操作系统以继续获得支持。
PCL CE 始终建议使用最新版本的操作系统以获得最佳体验。
你仍然可以尝试在不受支持的系统上使用 PCL CE，但可能会遇到很多额外问题。

## 🔒 许可证

- `Plain Craft Launcher 2/` 使用 [自定义许可证](Plain Craft Launcher 2/LICENCE)（《PCL 分发有限许可》与《PCL 存储库合理使用指南》）
- `其余所有目录` 使用 [Apache License 2.0](LICENSE)

本分支属于上述指南中的「重度使用」：由第三方基于 PCL 独立二次创作，与 PCL 官方及 PCL CE 官方均无隶属关系。

## 🙏 致谢

- [Meloong-Git/PCL](https://github.com/Meloong-Git/PCL) 与 PCL 全体贡献者
- [PCL-Community/PCL-CE](https://github.com/PCL-Community/PCL-CE) 与社区版贡献者
