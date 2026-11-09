# 如何为本分支做贡献

> **本仓库是 PCL CE 的个人分支（fork），不是官方仓库。** 官方贡献流程、Issue 模板与社区群组见 [PCL-Community/PCL-CE](https://github.com/PCL-Community/PCL-CE)。

## 适用范围

| 你想做的事 | 去哪里 |
|---|---|
| 报告官方 PCL CE 的 Bug / 提建议 | [PCL-Community/PCL-CE/issues](https://github.com/PCL-Community/PCL-CE/issues/new/choose) |
| 反馈 PCL CE 帮助文档 | [PCL-Community/docs.pclc.cc](https://github.com/PCL-Community/docs.pclc.cc/issues) |
| 反馈本分支（SpacJoy/PCL-CE）的问题 | 本仓库 Issues；也可直接提 PR |
| 讨论 / 提问 | 本仓库 Discussions（如已开启） |

本分支按需维护，不保证响应时间，也不保证接受外部 PR。改动以「跟随上游 + 少量个人定制」为原则。

## 开发文档

上游开发文档对本分支同样适用：

- [开发环境配置](https://docs.pclc.cc/ce/developers/setup)
- [技术规范](https://docs.pclc.cc/ce/developers/guidelines)
- [生成式人工智能使用方针](https://docs.pclc.cc/ce/developers/ai-policy)
- 本分支的构建说明（含密钥注入坑）：[docs/BUILD.md](docs/BUILD.md)

提交 Issue 或 PR 前请先阅读上述文档。使用生成式 AI 工具时，必须按方针披露使用情况，并对提交内容的正确性承担全部责任。

## 分支与提交

- 目标分支：`dev`
- 从 `dev` 切出特性分支：`feat/your-feat-name`、`fix/issue-number-desc`
- 提交信息遵循 Angular 规范，与上游一致：

```commit message
<type>(scope?): <subject>

<body>

<footer>
```

可用 `type`：`build` `chore` `ci` `docs` `feat` `fix` `pref` `refactor` `style` `test`。页眉必填，正文说明动机与行为变化，页脚用 `Closes #123` 关联 Issue。

## 提交前检查

1. `dotnet build "Plain Craft Launcher 2/Plain Craft Launcher 2.csproj"` 通过，0 错误。
2. 涉及纯逻辑的改动补上 `PCL.Core.Test` 单元测试，`dotnet test PCL.Core.Test/PCL.Core.Test.csproj` 通过。
3. 不提交构建产物（`artifact/`、`bin/`、`obj/` 已忽略）、临时文件、无关改动。
4. PR 描述写清动机、实现方式、影响范围与验证方式。

## 向上游提交

若要把本分支的改动回馈给 [PCL-Community/PCL-CE](https://github.com/PCL-Community/PCL-CE)，请以独立分支向**上游**仓库提 PR，并按上游的 AI 使用方针披露；不要直接把本分支的合并提交推过去。原始贡献者不是本人时，用 `Co-authored-by` 注明。
