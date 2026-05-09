# 优化 README

## Goal

提升 `README.md` 的首屏可读性和实用性，让新用户能快速理解 Rhythm 是什么、如何运行、数据存在哪里、如何开发/发布，同时让当前仓库状态与文档描述保持一致。

## Requirements

* 保持 README 以中文为主，语气简洁、面向最终用户和开发者。
* 改善开头摘要、功能说明、运行方式、开发命令、数据/卸载说明的结构。
* 明确当前是 Windows WPF 桌面常驻 app，运行目标是 Windows 10/11 x64。
* 保留 CI、License、截图占位、架构说明、已知限制等有价值信息。
* 不声称仓库里已经存在真实截图；若引用截图，应与 `docs/screenshot-placeholder.md` 当前状态一致。
* 不修改应用代码、测试代码、项目配置或发布产物。

## Acceptance Criteria

* [ ] `README.md` 结构清晰，包含项目简介、核心功能、运行、数据位置、开发、发布、架构、限制/安全说明。
* [ ] README 中的命令与当前仓库的 `.NET 10`、`Rhythm.slnx`、`src/Rhythm/Rhythm.csproj` 相匹配。
* [ ] README 不引用不存在的 `docs/screenshot.png` 作为真实截图。
* [ ] README 不引入未经验证的新功能承诺。
* [ ] 文档改动完成后执行一个轻量验证命令，确认工作树只包含预期文档/任务文件变更。

## Definition of Done

* README 已优化并保存。
* 检查 diff，确认没有无关代码改动。
* 不需要运行完整测试套件，因为本任务仅修改文档；如运行，应记录结果。

## Technical Approach

直接重写 README 的信息架构，使用当前仓库文件推导准确命令和功能边界。保留原有技术事实，补充更明确的开发/发布命令和安全/隐私说明。

## Decision (ADR-lite)

**Context**: 原 README 已有核心信息，但首屏定位、截图占位状态、运行/开发路径和用户说明可以更清晰。

**Decision**: 做文档层面的整理，不改代码、不补资产。将截图说明降级为占位提示，避免 README 指向不存在的真实 PNG。

**Consequences**: README 更准确但仍缺真实视觉截图；未来补充 `docs/screenshot.png` 后可把占位提示换成图片。

## Out of Scope

* 生成或添加真实应用截图。
* 修改应用行为、UI、测试或构建配置。
* 新增安装器、release 自动化或下载链接。

## Technical Notes

* Existing README: `README.md`
* Screenshot placeholder: `docs/screenshot-placeholder.md`
* Solution: `Rhythm.slnx`
* App project: `src/Rhythm/Rhythm.csproj`
* Test project: `tests/Rhythm.Tests/Rhythm.Tests.csproj`
* Frontend/WPF conventions: `.trellis/spec/frontend/wpf-desktop-app.md`
