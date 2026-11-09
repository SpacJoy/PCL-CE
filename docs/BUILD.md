# 构建说明

本文件描述 **SpacJoy/PCL-CE 分支** 的本地构建方式。上游构建流程见 [docs.pclc.cc](https://docs.pclc.cc/ce/developers/setup)，这里只记录本分支实际会用到的命令与已经踩过的坑。

## 环境要求

| 项目 | 要求 |
|---|---|
| .NET SDK | 10.0.x（`dotnet --version` 应输出 10.x） |
| 操作系统 | Windows 10 1809+（运行产物）；macOS / Linux 可交叉编译但无法运行 |
| 解决方案 | `Plain Craft Launcher 2.slnx`（需要支持 slnx 的 SDK / IDE） |

目标框架为 `net10.0-windows`，WPF 项目不能在不带 Windows Desktop 运行时的环境里直接运行。

## 快速构建

在仓库根目录执行：

```bash
export PCL_WRITE_SECRET=1
export PCL_MS_CLIENT_ID=<Microsoft OAuth Client ID>

dotnet publish "Plain Craft Launcher 2/Plain Craft Launcher 2.csproj" \
  -p:Configuration=Release -p:Platform=x64 \
  -p:DeleteExistingFiles=true -o ./artifact --no-self-contained -t:Rebuild
```

- 产物：`artifact/Plain Craft Launcher 2.exe`（`artifact/` 已在 `.gitignore` 中，不会进入版本库）
- 架构可选 `x64` / `ARM64`；配置可选 `Debug` / `CI` / `Beta` / `Release`
- 只想验证能否编译：`dotnet build "Plain Craft Launcher 2/Plain Craft Launcher 2.csproj" -c Debug`

## 密钥注入（重要）

启动器的部分功能依赖**编译期烧入**的密钥，而不是运行时读取环境变量：

- `PCL.Core/App/Secrets.cs` 通过 `EnvironmentInterop.GetSecret(...)` 取值，Release 构建下 `readEnvDebugOnly: true` **不读环境变量**。
- `PCL.Core.SourceGenerators/EnvironmentInteropGenerator.cs` 仅在编译进程存在 `PCL_WRITE_SECRET` 时，把所有 `PCL_*` 环境变量生成进 `EnvironmentInterop.g.cs` 的 `SecretDictionary`。

因此：

1. **必须**先 `export PCL_WRITE_SECRET=1`，否则 exe 里一个密钥都没有。
2. **必须**带 `-t:Rebuild`：source generator 是增量生成器，`obj/` 未失效时不会重跑，环境变量烧不进去。
3. 缺少 `PCL_MS_CLIENT_ID` 时启动器仍能运行，但微软账号登录会抛 `Microsoft OAuth client id is not configured`；已缓存的 AccessToken 未过期时可临时绕过。

验证密钥是否真的进了 exe（用 UTF-16 搜索，PowerShell 示例）：

```powershell
$bytes = [IO.File]::ReadAllBytes("artifact/Plain Craft Launcher 2.exe")
$text = [Text.Encoding]::Unicode.GetString($bytes)
$text.Contains("<你的 client id>")
```

能命中 1 次即注入成功。

## 已知构建坑

| 现象 | 原因与处理 |
|---|---|
| `Value cannot be null. (Parameter 'path1')`（NuGet 报错） | 由自造的精简环境变量启动构建进程导致，改用正常终端（bash / PowerShell）执行即可 |
| exe 里搜不到 client id | 没带 `-t:Rebuild`，或没设 `PCL_WRITE_SECRET` |
| 修改后行为没变化 | 增量生成器 / MSBuild 增量缓存；加 `-t:Rebuild` 或清理 `obj`、`bin` |
| 测试项目编译失败 | `PCL.Core.Test` 在解决方案中被标记为不随解决方案构建（`Build Solution="*|*" Project="false"`），需单独构建 |

## 测试

```bash
dotnet test PCL.Core.Test/PCL.Core.Test.csproj
```

测试目标框架同为 `net10.0-windows`。改动涉及校验器、JSON 兼容层、编码探测等纯逻辑时，优先补对应单元测试。

## CI

- `.github/workflows/build-test.yml`：推送 `dev` 或提 PR 时构建 CI 配置的 x64 / ARM64 产物，密钥来自仓库 Secrets。
- `.github/workflows/reusable-build.yml`：实际构建步骤，`dotnet publish ... --no-self-contained`。
- `.github/workflows/release-publish.yml`、`mirrorchyan_*.yml`：官方发行流程，**个人分支不使用**（依赖 PCL-Community 的密钥与 MirrorChyan 账号）。

个人分支如需自动化构建，建议复制 `reusable-build.yml` 并只保留自己需要的密钥项。
