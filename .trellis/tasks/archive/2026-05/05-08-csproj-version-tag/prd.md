# 版本管理：csproj Version 与 tag 同步

## Goal

实现版本号自动化管理：tag push 时 csproj `<Version>` 自动与 tag 版本同步，确保 exe 文件属性显示正确版本，GitHub Release 与构建产物版本一致。

## What I already know

* 项目：.NET 10 WPF，csproj 无 `<Version>` 标签
* 已有 release workflow：构建 exe 后重命名为 `Rhythm-{version}.exe`（版本从 tag 提取）
* 缺失：csproj 无版本号，exe 文件属性不显示版本信息

## Research References

* [`research/dotnet-versioning.md`](research/dotnet-versioning.md) — .NET 版本管理方案对比分析

## Decisions

* **版本管理方案**：MinVer（从 git tag 自动推断版本，zero-config）

## Requirements

* [ ] 添加 `MinVer` NuGet 包到 Rhythm.csproj
* [ ] CI checkout 改为 `fetch-depth: 0`（ci.yml 和 release.yml）
* [ ] exe 文件属性显示版本号（验证 FileVersionInfo）

## Acceptance Criteria

* [ ] `dotnet build` 后 exe 属性显示版本 `1.0.0`（当 tag 为 `v1.0.0`）
* [ ] GitHub Release exe 文件属性版本正确

## Out of Scope

* MinVerMinimumVersion 配置（无 tag 时用默认 `0.0.0`）
* prerelease 版本配置（MinVer 自动支持，无需额外设置）