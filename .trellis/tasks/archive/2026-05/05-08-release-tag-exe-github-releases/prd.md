# Release 自动化：tag 触发发布 exe 到 GitHub Releases

## Goal

实现 GitHub Actions 自动发布流程：当推送 tag（如 `v1.0.0`）时，自动构建 single-file exe 并上传到 GitHub Releases，用户可直接下载。

## What I already know

* 项目：WPF .NET 10，win-x64 single-file self-contained
* 已有 CI workflow：`.github/workflows/ci.yml`（构建+测试）
* 发布命令已知：
  ```bash
  dotnet publish src/Rhythm/Rhythm.csproj -c Release -r win-x64 \
    -p:PublishSingleFile=true -p:SelfContained=true \
    -p:IncludeNativeLibrariesForSelfExtract=true -o publish
  ```
* csproj 无 `<Version>` 标签，版本需从 tag 提取

## Decisions

* **Release notes**：自动生成（`generate_release_notes: true`）
* **exe 文件名**：带版本号（如 `Rhythm-1.0.0.exe`）

## Open Questions

* tag 格式是否强制 `v` 前缀？
* Release notes 来源：自动生成还是手动编辑？
* 是否需要同时更新 csproj Version？

## Requirements

* [ ] `.github/workflows/release.yml` — tag push (`v*`) 触发
* [ ] 构建 win-x64 single-file self-contained exe
* [ ] 重命名为 `Rhythm-{version}.exe`
* [ ] 创建 GitHub Release + 自动生成 notes + 上传 exe

## Acceptance Criteria

* [ ] 推送 `v1.0.0` tag 后，Releases 页面出现 v1.0.0
* [ ] Release 包含 `Rhythm-1.0.0.exe` 可下载
* [ ] Release body 自动生成 commit 变更列表

## Definition of Done

* workflow 文件已创建
* yaml 语法正确
* 与现有 CI workflow 不冲突

## Out of Scope

* exe 签名/代码签名证书
* winget/Chocolatey 自动发布
* 多平台构建（仅 win-x64）

## Technical Notes

* 参考：GitHub Actions `softprops/action-gh-release`
* 版本提取：`${GITHUB_REF#refs/tags/v}`