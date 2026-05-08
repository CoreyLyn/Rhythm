# Rhythm 主窗口视觉与交互优化 v2

## Goal

提升 Rhythm 主窗口（todo 列表卡片）的视觉与交互质感。当前 CheckBox 使用 WPF 系统默认渲染，在透明深色窗口上观感生硬，与 macOS-like 的整体设计语言不一致。本次改造聚焦在 ControlTemplate 重写、字体层级、已完成态、空状态、微交互动画以及 ContextMenu 暗色样式，不改变状态机、持久化与 Win32 集成。

## Requirements

**MVP 范围（用户已确认）**：

- ✅ **R1** CheckBox 自定义 ControlTemplate（圆角 4px / 描边 #5A5A5E / 选中态填充 #0A84FF + 白勾）
- ✅ **R2** CheckBox hover 态（描边升亮至 #8E8E93）
- ✅ **R3** CheckBox 选中动画（150ms ColorAnimation + 勾号 ScaleTransform 0→1）
- ✅ **R4** 字号 14→15、字重 Regular→Medium、显式中文字体回退 `Microsoft YaHei UI, Segoe UI`
- ✅ **R5** 整卡可点（HorizontalContentAlignment=Stretch + 透明 Border 包裹）
- ✅ **R6** 已完成态整行 Opacity 0.55（DataTrigger 绑 IsCompleted）
- ✅ **R7** 空状态接 `HasItems` → 显示「今天没有事项」（EmptyHintStyle 已定义，需挂到 ItemsControl.Template）
- ✅ **R8** ContextMenu 暗色 ResourceDictionary（Background / Foreground / Hover 与主窗 BrushKey 联动）

**选中色决策**：沿用既有 `AccentBrush #0A84FF`（iOS System Blue Dark），与 macOS/iOS Reminders 一致。

## Acceptance Criteria

- [ ] CheckBox 在未选/hover/已选三态下视觉清晰，圆角 4px，选中后填充 #0A84FF + 白勾
- [ ] 选中切换有 150ms 动画（颜色 + 勾号缩放）
- [ ] 字号 15 / FontWeight=Medium / 中文字体回退正确
- [ ] 点击整张卡片任意位置 = 切换勾选
- [ ] 已完成项整行 Opacity 0.55，与未完成项拉开层次
- [ ] 列表为空时显示「今天没有事项」居中灰色提示
- [ ] 右键弹出的 ContextMenu 为暗色样式，与主窗背景协调
- [ ] 现有 xUnit 测试全绿（状态机/持久化层不动）
- [ ] 手动验收清单全过：勾选/取消/键盘/hover/已完成态/空列表/中英文混排/右键菜单

## Definition of Done

- ControlTemplate 在 `Themes/Dark.xaml` 内集中，不污染 Code-Behind
- ContextMenu Style 也在 `Themes/Dark.xaml` 内，复用 BrushKey
- 不引入新 NuGet 依赖
- Lint / `dotnet build` / `dotnet test` green
- 视觉细节由用户手动验收（main agent 不声称已验，spec 第 6 节明确要求）

## Technical Approach

### 文件改动（2 处）

1. **`src/Rhythm/Themes/Dark.xaml`**
   - 新增 `ItemCheckBoxStyle` 的 ControlTemplate（含 Border + Path 勾号 + VisualStateManager）
   - 新增 ContextMenu Style（TargetType=ContextMenu / MenuItem）
   - 复用既有 BrushKey：WindowBackgroundBrush / ForegroundBrush / MutedForegroundBrush / AccentBrush / DividerBrush

2. **`src/Rhythm/MainWindow.xaml`**
   - ItemsControl 加 Template，内嵌 ItemsPresenter + 空状态 TextBlock（绑 `HasItems` 反转可见性）
   - 可能需要在 App.xaml 注册 `BoolToVisibilityConverter`（或用 DataTrigger 直接控制 Visibility）

### CheckBox ControlTemplate 结构（伪代码）

```xml
<ControlTemplate TargetType="CheckBox">
  <Grid Background="Transparent" Cursor="Hand">
    <Border x:Name="CheckBoxBorder" Width="18" Height="18" CornerRadius="4"
            BorderBrush="#5A5A5E" BorderThickness="1" Background="Transparent">
      <Path x:Name="CheckMark" Data="M 4,9 L 7,12 L 14,5" Stroke="White" StrokeThickness="2"
            Visibility="Collapsed" RenderTransformOrigin="0.5,0.5">
        <Path.RenderTransform>
          <ScaleTransform ScaleX="0" ScaleY="0"/>
        </Path.RenderTransform>
      </Path>
    </Border>
    <ContentPresenter Margin="8,0,0,0" VerticalAlignment="Center"/>
  </Grid>
  <ControlTemplate.Triggers>
    <Trigger Property="IsMouseOver" Value="True">
      <Setter TargetName="CheckBoxBorder" Property="BorderBrush" Value="#8E8E93"/>
    </Trigger>
    <Trigger Property="IsChecked" Value="True">
      <Setter TargetName="CheckBoxBorder" Property="Background" Value="{StaticResource AccentBrush}"/>
      <Setter TargetName="CheckMark" Property="Visibility" Value="Visible"/>
      <Trigger.EnterActions>
        <BeginStoryboard>
          <Storyboard>
            <ColorAnimation Storyboard.TargetName="CheckBoxBorder" ... Duration="0:0:0.15"/>
            <DoubleAnimation Storyboard.TargetProperty="(Path.RenderTransform).(ScaleTransform.ScaleX)" To="1" Duration="0:0:0.15"/>
            <DoubleAnimation Storyboard.TargetProperty="(Path.RenderTransform).(ScaleTransform.ScaleY)" To="1" Duration="0:0:0.15"/>
          </Storyboard>
        </BeginStoryboard>
      </Trigger.EnterActions>
    </Trigger>
  </ControlTemplate.Triggers>
</ControlTemplate>
```

### ContextMenu Style 结构

```xml
<Style TargetType="ContextMenu">
  <Setter Property="Background" Value="{StaticResource WindowBackgroundBrush}"/>
  <Setter Property="BorderBrush" Value="{StaticResource DividerBrush}"/>
  <Setter Property="BorderThickness" Value="1"/>
  <Setter Property="Padding" Value="4"/>
</Style>
<Style TargetType="MenuItem">
  <Setter Property="Foreground" Value="{StaticResource ForegroundBrush}"/>
  <Setter Property="Background" Value="Transparent"/>
  <Style.Triggers>
    <Trigger Property="IsMouseOver" Value="True">
      <Setter Property="Background" Value="{StaticResource DividerBrush}"/>
    </Trigger>
  </Style.Triggers>
</Style>
```

## Decision (ADR-lite)

**Context**: CheckBox 选中色可选 iOS Blue / Purple / Green / 无色。  
**Decision**: 沿用既有 `AccentBrush #0A84FF`（iOS System Blue Dark）。  
**Consequences**: 与 macOS/iOS Reminders 一致，色调压力低，品牌关联 Apple 生态。未来若需换色，只需改 AccentBrush 定义即可。

## Out of Scope

- ❌ Mica/Acrylic 系统级背景（与 `AllowsTransparency=True` 互斥，需重写整体窗口架构，留待 v3）
- ❌ EditItemsWindow 视觉同步（工作量大，v3 统一重做）
- ❌ TextWrapping / ScrollViewer / 高 DPI 适配（边界场景，v3）
- ❌ 卡片 hover 整体提亮（R8）、Space 键切换（R9）、项间分隔线（R10）、fade-in 入场（R11）
- ❌ 拖拽排序 / 多选 / 右键单项菜单
- ❌ 状态机、持久化、托盘、自启相关代码
- ❌ 主题切换（Light/Dark 切换）—— 当前仅 Dark
- ❌ 多语言 i18n

## Technical Notes

### 涉及文件
- `src/Rhythm/Themes/Dark.xaml` —— ControlTemplate / Style / DataTrigger
- `src/Rhythm/MainWindow.xaml` —— ItemsControl Template（接 HasItems 空状态）
- 可能需要轻量改动 `src/Rhythm/App.xaml`（注册 BoolToVisibilityConverter，若用 DataTrigger 则不需要）

### 设计参考
- macOS Reminders（CheckBox 圆形）/ Things 3（CheckBox 方圆）/ Apple HIG SF Pro spacing
- 当前 AccentBrush `#0A84FF` 即 iOS System Blue (Dark)

### Spec 引用
- `.trellis/spec/frontend/wpf-desktop-app.md` §6 Tests Required（视觉项需用户手动验收）
- `.trellis/spec/frontend/wpf-desktop-app.md` §7 Wrong vs Correct（Mica 互斥规则）

### ViewModel 现状
- `ItemViewModel.IsCompleted` 双向已通
- `MainViewModel.HasItems` 已存在，XAML 需绑定

### 架构约束
- `AllowsTransparency=True` 与 DWM Mica/Acrylic **互斥** → 本次不动背景 backdrop
- 状态机/持久化/托盘/自启/HWND_BOTTOM 均已稳定，本次不碰
