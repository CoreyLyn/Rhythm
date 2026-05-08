# Research: .NET Version Management Approaches

- **Query**: Research .NET version management approaches for syncing csproj Version with git tags
- **Scope**: External (library documentation and best practices)
- **Date**: 2026-05-08

## Findings

### Current Project State

| File Path | Description |
|---|---|
| `src/Rhythm/Rhythm.csproj` | Main WPF project, no Version property defined |
| `tests/Rhythm.Tests/Rhythm.Tests.csproj` | Test project |
| `.github/workflows/*` | Existing release workflow (not examined in this research) |

**Current Setup**:
- .NET 10 WPF application
- OutputType: WinExe
- No `<Version>` tag in csproj
- GitHub release workflow already extracts version from tags for artifact naming

---

## Approach 1: MinVer

### How It Works

MinVer is a minimalist, Git tag-based versioning tool. It:
1. Inspects git history to find the most recent tag reachable from the current commit
2. Derives version from that tag (e.g., `v1.2.3` → version `1.2.3`)
3. Calculates version height from tag to determine build metadata
4. Handles prerelease versions (e.g., `v1.0.0-alpha.1`)

**Version Derivation**:
- **Source**: Git tags only (no config file required)
- **Format**: Tag must be in semver format (e.g., `v1.2.3`, `1.2.3`)
- **Height calculation**: Commits after tag → build metadata or patch increment

### Setup Complexity

**NuGet Package**:
```xml
<PackageReference Include="MinVer" Version="6.0.0" />
```

**Configuration** (optional, in csproj or Directory.Build.props):
```xml
<PropertyGroup>
  <MinVerTagPrefix>v</MinVerTagPrefix>  <!-- optional: if tags use 'v' prefix -->
  <MinVerMinimumMajorMinor>1.0</MinVerMinimumMajorMinor>  <!-- optional -->
</PropertyGroup>
```

**CI Integration**:
- No special CI setup required
- Just ensure git tags are fetched during build: `git fetch --tags` or `git fetch --unshallow`
- GitHub Actions: checkout action with `fetch-depth: 0` (full history)

**Example GitHub Actions workflow**:
```yaml
- uses: actions/checkout@v4
  with:
    fetch-depth: 0  # Fetch all history for tags
```

### Pros

- **Zero configuration**: Works out of the box with just the NuGet package
- **Tag-driven**: No separate version file to maintain - single source of truth (git tags)
- **Simple CI**: No complex CI pipeline changes, just fetch tags
- **Transparent**: Easy to understand, version is literally the git tag
- **Fast**: Minimal overhead during build
- **No build-time dependencies**: Only needed at build time, not at runtime

### Cons

- **Tag requirement**: Must create tags for every release - can't build without a tag
- **Local builds**: Developers need tags locally to get proper versions
- **Limited flexibility**: Harder to customize version calculation beyond tag format

### Suitability for Small Single-File Exe

**Rating: Excellent (9/10)**

MinVer is ideal for this use case because:
- Single source of truth (git tags)
- Zero maintenance - just add package and create tags
- No config file to manage
- Works perfectly with existing release workflow
- Version appears in exe file properties automatically

---

## Approach 2: GitVersion

### How It Works

GitVersion is a comprehensive versioning tool that:
1. Analyzes git history, branches, and tags
2. Uses configurable rules to determine version based on branch type
3. Supports multiple branching strategies (GitFlow, GitHubFlow, Mainline)
4. Generates version from commit history, merge commits, and tags

**Version Derivation**:
- **Source**: Git tags + branch analysis + commit history
- **Format**: Configurable via `GitVersion.yml`
- **Branch-based logic**: `main` → stable, `develop` → prerelease, `feature/*` → prerelease

### Setup Complexity

**NuGet Package**:
```xml
<PackageReference Include="GitVersion.MsBuild" Version="5.12.0" />
```

**Configuration File** (required: `GitVersion.yml`):
```yaml
mode: Mainline
branches:
  main:
    mode: ContinuousDelivery
    tag: ''
  feature:
    mode: ContinuousDelivery
    tag: useBranchName
    increment: Minor
    source-branches: ['main']
```

**CI Integration**:
- More complex setup
- May need to install GitVersion tool separately in CI
- Requires full git history

**Example GitHub Actions workflow**:
```yaml
- uses: actions/checkout@v4
  with:
    fetch-depth: 0

- name: Install GitVersion
  uses: gittools/actions/gitversion/setup@v1
  with:
    versionSpec: '5.x'

- name: Determine Version
  uses: gittools/actions/gitversion/execute@v1
```

### Pros

- **Powerful**: Supports complex branching strategies and workflows
- **Flexible**: Highly configurable for different scenarios
- **Branch-aware**: Different version behavior for different branches
- **Well-established**: Mature tool with extensive documentation
- **Continuous delivery**: Designed for CD pipelines

### Cons

- **Configuration overhead**: Requires `GitVersion.yml` file
- **Complexity**: Steep learning curve, many configuration options
- **CI complexity**: More setup required in CI pipelines
- **Overkill for simple projects**: Too heavy for small single-branch projects
- **Slower builds**: More analysis required during build

### Suitability for Small Single-File Exe

**Rating: Poor (4/10)**

GitVersion is overkill for this use case because:
- Too much complexity for simple tag-based versioning
- Requires configuration file maintenance
- Branching strategy not needed for single-developer project
- Simpler tools achieve the same goal with less overhead

---

## Approach 3: Nerdbank.GitVersioning

### How It Works

Nerdbank.GitVersioning:
1. Reads version from `version.json` file in repo root
2. Calculates build number from git height (commits since version.json change)
3. Optionally uses git tags to override version
4. Provides version as build properties and assembly info

**Version Derivation**:
- **Source**: `version.json` file (primary) + git height
- **Format**: Major.Minor from file, Build from commit count
- **Tag override**: Can override with tags using `^` prefix

### Setup Complexity

**NuGet Package**:
```xml
<PackageReference Include="Nerdbank.GitVersioning" Version="3.6.133" />
```

**Configuration File** (required: `version.json`):
```json
{
  "$schema": "https://raw.githubusercontent.com/dotnet/Nerdbank.GitVersioning/master/src/NerdBank.GitVersioning/cluster.schema.json",
  "version": "1.0",
  "publicReleaseRefSpec": [
    "^refs/heads/master$",
    "^refs/heads/main$"
  ],
  "cloudBuild": {
    "buildNumber": {
      "enabled": true
    }
  }
}
```

**CI Integration**:
- Minimal CI setup required
- Works with partial git history (can be faster)
- Provides cloud build integration for various CI systems

**Example GitHub Actions workflow**:
```yaml
- uses: actions/checkout@v4
  with:
    fetch-depth: 0  # Recommended for accurate height calculation

# No additional setup needed - package handles everything
```

### Pros

- **Reproducible builds**: Version stored in version.json, less dependent on git state
- **Git height**: Automatic build number from commit history
- **Flexible**: Can combine file-based and tag-based versioning
- **Cloud build aware**: Integrates with CI/CD systems
- **Fast**: Efficient version calculation

### Cons

- **File maintenance**: Requires `version.json` file to be updated
- **Two sources of truth**: version.json AND git tags can create confusion
- **Less tag-centric**: Tags are secondary to version.json
- **Learning curve**: Unique versioning model (height-based)

### Suitability for Small Single-File Exe

**Rating: Good (7/10)**

Nerdbank.GitVersioning is suitable but has trade-offs:
- Automatic build numbers are nice for continuous builds
- Requires updating version.json manually before releases
- More file maintenance than MinVer
- Better for projects wanting continuous version increments

---

## Approach 4: Manual MSBuild Property Override

### How It Works

Manual approach:
1. Leave csproj without `<Version>` tag
2. Override version at build time via MSBuild property
3. Extract version from git tag in CI script
4. Pass version to `dotnet build` command

**Version Derivation**:
- **Source**: Manual extraction from git tag in CI script
- **Format**: Custom script to parse tag and format version
- **Build-time override**: Passed via `-p:Version=x.x.x`

### Setup Complexity

**NuGet Package**:
None required

**CI Integration**:
```yaml
# GitHub Actions example
- name: Get version from tag
  id: get_version
  run: |
    # Remove 'v' prefix if present
    VERSION=${GITHUB_REF_NAME#v}
    echo "VERSION=$VERSION" >> $GITHUB_OUTPUT

- name: Build
  run: dotnet build -p:Version=${{ steps.get_version.outputs.VERSION }}

- name: Publish
  run: dotnet publish -p:Version=${{ steps.get_version.outputs.VERSION }}
```

**Local Development**:
- No version shown in local builds (or need manual property passing)
- Can set default version in csproj for local development

### Pros

- **No dependencies**: No external NuGet packages required
- **Full control**: Complete control over version calculation
- **Simple**: No magic, explicit version passing
- **Lightweight**: No build-time version calculation overhead

### Cons

- **Manual maintenance**: Must write and maintain CI scripts
- **Local builds**: Developers get no version in local builds by default
- **Duplication**: Version logic repeated in CI workflow
- **Error-prone**: Manual script maintenance can introduce bugs
- **No local validation**: Can't test version locally without setup

### Suitability for Small Single-File Exe

**Rating: Poor (5/10)**

Manual approach works but is not ideal:
- Requires CI script maintenance
- No version in local builds without extra setup
- Easy to break with script errors
- No benefit over automated tools

---

## Comparison Summary

| Approach | Setup Complexity | Maintenance | CI Integration | Local Builds | Best For |
|----------|------------------|-------------|----------------|--------------|----------|
| MinVer | Very Low | None | Minimal | Full | Small projects, tag-driven releases |
| GitVersion | High | Config file | Complex | Full | Large projects, branching strategies |
| Nerdbank.GitVersioning | Medium | version.json | Moderate | Full | Projects needing build numbers |
| Manual | Low | CI scripts | Manual | None | Quick prototypes, full control needs |

---

## Recommendation for This Project

**Recommended Approach: MinVer**

**Rationale**:

1. **Project characteristics match MinVer strengths**:
   - Small single-developer WPF project
   - Already using git tags for releases
   - Want single source of truth (tags)
   - Need zero-maintenance solution

2. **Minimal setup required**:
   - Add one NuGet package reference
   - Ensure CI fetches tags (one-line change)
   - No configuration files to maintain

3. **Perfect fit for existing workflow**:
   - Already creating tags for releases
   - GitHub workflow already extracts version from tags
   - MinVer uses same tag automatically

4. **Immediate benefits**:
   - exe file properties will show correct version
   - No manual version file updates
   - Local builds also get proper version if tags are present

**Implementation Steps**:

1. Add MinVer package to csproj:
   ```xml
   <PackageReference Include="MinVer" Version="6.0.0" />
   ```

2. Update GitHub workflow to fetch tags:
   ```yaml
   - uses: actions/checkout@v4
     with:
       fetch-depth: 0
   ```

3. Optionally set tag prefix if using `v` prefix:
   ```xml
   <PropertyGroup>
     <MinVerTagPrefix>v</MinVerTagPrefix>
   </PropertyGroup>
   ```

4. Create a git tag for next release:
   ```bash
   git tag v1.0.0
   git push --tags
   ```

**Expected Outcome**:
- When tag `v1.2.3` is pushed, MinVer sets Version to `1.2.3`
- exe file properties show `1.2.3`
- GitHub release artifacts match
- Zero ongoing maintenance

---

## External References

- [MinVer GitHub Repository](https://github.com/adamralph/minver) - Official documentation and source
- [MinVer NuGet Package](https://www.nuget.org/packages/MinVer/) - Package details
- [GitVersion Documentation](https://gitversion.net/docs/) - Comprehensive GitVersion docs
- [Nerdbank.GitVersioning GitHub](https://github.com/dotnet/Nerdbank.GitVersioning) - Official repository
- [MSBuild Properties Documentation](https://learn.microsoft.com/en-us/visualstudio/msbuild/msbuild-properties) - Microsoft docs

---

## Caveats / Not Found

- Did not examine existing GitHub workflow files (would need to adapt checkout step)
- Did not test MinVer with .NET 10 specifically (should work, MinVer targets .NET Standard 2.0)
- No local testing performed - recommendation based on documentation review
- Potential issue: MinVer requires git history; shallow clones won't work (addressed by fetch-depth: 0)