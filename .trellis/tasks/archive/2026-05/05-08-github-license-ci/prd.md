# GitHub 正式发布优化：LICENSE + CI + 文档

## Goal

将 Rhythm 项目完善为适合 GitHub 正式发布的开源项目，补充必要的许可证、CI自动化、文档和展示材料。

## What I already know

* 项目：Rhythm - Windows 桌面便签（WPF, .NET 10, xUnit）
* 已有：README（功能完整）、单元测试、基本 gitignore、分层架构
* 缺失项（按优先级）：
  1. LICENSE - GitHub 正式项目必需
  2. 截图/GIF - README 需要展示应用界面
  3. GitHub Actions CI - 构建/测试/发布自动化
  4. CHANGELOG.md - 版本历史追踪
  5. 徽章 - README 增强（构建状态、版本、下载量）
  6. SECURITY.md - 安全政策和漏洞报告
* 构建命令已知：`dotnet build`, `dotnet test`, `dotnet publish`

## Decisions

* **许可证**：MIT（最宽松，适合小型工具项目）
* **CI 触发**：push to master + PR（确保主分支始终可用，PR 有验证）
* **截图来源**：用户提供截图/GIF，我插入 README（预留位置）
* **CHANGELOG 格式**：按日期版本（简洁里程碑风格）
* CI：GitHub Actions 是 Windows .NET 项目标准选择
* 发布目标：win-x64 single-file self-contained exe

## Open Questions

* 许可证选择：MIT vs Apache-2.0 vs 其他？
* CI 触发条件：push to master？PR？tag？
* 截图来源：需要用户提供还是我用工具生成？

## Requirements

* [ ] LICENSE（MIT）
* [ ] CHANGELOG.md（初始版本 v1.0.0）
* [ ] .github/workflows/ci.yml（构建+测试）
* [ ] README 截图预留位置（占位符，待用户提供）
* [ ] README 徽章（CI 状态、版本占位）
* [ ] SECURITY.md（安全政策模板）

## Acceptance Criteria

* [ ] GitHub 项目页面显示 MIT 许可证标识
* [ ] CI workflow 在 push/PR 时自动运行 `dotnet build` + `dotnet test`
* [ ] README 包含截图占位符，用户可替换
* [ ] README 显示构建状态徽章（指向 GitHub Actions）
* [ ] CHANGELOG 有初始版本记录

## Definition of Done

* 所有文件已创建并提交
* CI workflow 已验证（可手动触发测试）
* README 更新后本地预览正常

## Out of Scope

* winget/Chocolatey 发布（后续任务）
* 多语言 README（暂保持中文）
* 国际化代码改动
* Release 自动化（先验证 CI，后续再加）

## Technical Notes

* csproj 无 `<Version>` 标签，版本管理待定
* 测试项目：tests/Rhythm.Tests/（xUnit + coverlet）
* 发布输出：publish/Rhythm.exe