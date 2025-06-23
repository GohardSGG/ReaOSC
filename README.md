# ReaOSCPlugin

**一个通过 OSC (Open Sound Control) 协议，利用 Loupedeck 控制台来控制 REAPER 数字音频工作站的插件。**

## 核心功能

*   **强大的 REAPER 控制**: 通过 Loupedeck 上的按钮和旋钮，直观、高效地控制 REAPER 的各项功能，包括播放、录制、轨道操作、参数调整、MIDI 编辑、自动化控制等。
*   **高度可配置**: 插件的大部分行为和界面元素（按钮、旋钮、动态文件夹）都通过 JSON 文件进行定义，用户可以根据自己的工作流程进行深度定制。
*   **动态文件夹系统**:
    *   **效果器 (Effect) 和乐器 (Instrument) 浏览器**: 快速浏览、筛选并加载您收藏的效果器和虚拟乐器。支持按品牌、类型过滤，并可分页显示。
    *   **轨道命名 (Track Name)**: 提供预设的轨道名称模板，方便快速为新轨道命名。
    *   **轨道路由 (Track Routing) 与输入输出 (Input Output)**: 动态文件夹用于管理轨道路由和IO设置。
    *   **工程渲染 (Render)**: 集成渲染参数设置和导出功能。
    *   **添加轨道 (Add Track)**: 快速添加不同类型的轨道。
*   **丰富的控件类型**: 支持触发按钮、开关按钮 (Toggle Button)、参数旋钮 (TickDial, ControlDial)、模式切换按钮 (SelectModeButton)、组合按钮 (CombineButton) 等多种交互方式。
*   **OSC 通信与状态同步**:
    *   通过 WebSocket 连接到一个中间服务 (运行在 `ws://localhost:9122`)，该服务负责将插件发送的 OSC 消息转发给 REAPER，并接收 REAPER 的 OSC 反馈。
    *   能够监听并响应 REAPER 通过 OSC 发送的状态更新，并在 Loupedeck 界面上实时反映（例如，按钮的激活状态）。
*   **模式切换**: 许多控件支持模式切换，例如 "FX/Chain Select" 可以在效果器模式和效果链模式间切换，不同模式下按钮功能和 OSC 地址会相应改变。

## 技术栈

*   C# (.NET 8)
*   Loupedeck Plugin SDK
*   OSC (Open Sound Control)
*   WebSocket (用于与 REAPER 的 OSC 桥接服务通信)
*   JSON (用于插件配置)

## 安装与配置

### 依赖项

1.  **Loupedeck 软件** 或 **Logitech Options+** (已安装并运行 LogiPluginService)。
2.  **REAPER** 数字音频工作站，并已正确配置 OSC 以接收控制指令和发送反馈。
3.  **一个 WebSocket 转 OSC 的桥接服务**:
    *   此插件不直接与 REAPER 的 OSC 端口通信，而是通过一个运行在本地 `ws://localhost:9122` 的 WebSocket 服务。
    *   您需要自行搭建或配置这样一个桥接服务，它能够接收来自此插件的 WebSocket 消息（封装了 OSC 数据），并将其作为标准的 OSC 消息发送给 REAPER；同时，它也需要能将从 REAPER 收到的 OSC 反馈消息通过 WebSocket 发送回给此插件。

### 安装插件

*   **官方市场 (推荐)**: 如果插件已发布到 Loupedeck Marketplace，请通过市场直接安装。
*   **手动安装**:
    1.  从 Releases 或源码编译获取插件包 (`.lplug4` 文件) 或编译后的输出文件夹。
    2.  如果为 `.lplug4` 文件，双击安装。
    3.  如果为编译输出，将输出目录下的所有文件及文件夹复制到您的 Loupedeck 插件目录，并在该目录下创建一个名为 `ReaOSCPlugin.link` 的文本文件，文件内容为插件 `ReaOSCPlugin.dll` 所在的完整路径 (例如：`C:\Your\Path\To\ReaOSCPlugin\ReaOSCPlugin.dll`)。
        *   Windows 插件目录: `%LocalAppData%\Logi\LogiPluginService\Plugins\`
        *   macOS 插件目录: `~/Library/Application Support/Logi/LogiPluginService/Plugins/`

### 插件配置与自定义

本插件的核心设计理念是高度可配置性，允许用户通过修改 JSON 文件来定制插件行为。

1.  **内置配置**:
    *   插件内置了所有功能的默认 JSON 配置文件，位于插件安装目录下的 `ReaOSCPlugin/Dynamic/` 和 `ReaOSCPlugin/General/` 子目录中。**不建议直接修改这些内置文件，因为插件更新时可能会覆盖它们。**

2.  **用户自定义配置 (推荐)**:
    *   要覆盖内置配置，请在以下用户特定目录中创建与内置配置文件**同名**的 JSON 文件：
        *   **路径**: `%LocalAppData%\Loupedeck\Plugins\ReaOSC\` (Windows) 或 `~/Library/Application Support/Loupedeck/Plugins/ReaOSC/` (macOS - 请根据实际情况调整此macOS路径，通常与Windows的LocalApplicationData对应)。
        *   **子目录结构**: 在上述 `ReaOSC` 目录下，您需要创建与插件内部结构相对应的子文件夹，例如 `General` 和 `Dynamic`，然后将您的自定义 JSON 文件放入其中。
            *   例如，要自定义通用按钮，您可以在 `.../ReaOSC/General/General_List.json` 创建您的版本。
            *   要自定义 "Effect" 动态文件夹的数据源，可以在 `.../ReaOSC/Dynamic/Effect_List.json` 创建您的版本。
    *   插件启动时会优先加载用户目录下的配置文件。如果用户目录下不存在某个配置文件，则会使用插件内置的默认版本。

3.  **自定义图标**:
    *   插件支持从外部加载自定义图标 (PNG格式)。
    *   **路径**: `%LocalAppData%\Loupedeck\Plugins\ReaOSC\Icon\` (Windows) 或对应的 macOS 路径。
    *   **命名**: 将您的 PNG 图标文件放置在此目录，并确保其文件名与对应控件在 JSON 配置中 `ButtonImage` 字段指定的值（或基于 `DisplayName` 自动推断的名称，例如 `Play.png`）一致。

## 主要功能模块详解

*   **General (`General_List.json`)**: 定义了全局可用的通用按钮和旋钮，例如：
    *   编辑功能: Item Volume, Smart Snap, Auto Fade, Glue, Reverse 等。
    *   MIDI功能: Quantize, Humanize, Snap, Scale, Arpeggio, Legato 等。
    *   自动化: Touch, Trim, Write, Latch, Preview, Read 等。
    *   FX (效果器) 控制: Bypass, Offline, Parallel, Show, 以及 FX/Chain 模式切换。
    *   包络 (Envelope) 控制: Track/Take 模式切换，Volume, Pan, Width/Pitch 包络开关。
    *   通用播放控制: Play, Repeat, Record, Project Setting 等。
    *   标记 (Marker) 与区域 (Region) 操作。
    *   视图 (View) 控制: Scroll, Mixer, Big Clock, Track Manager 等。
    *   轨道 (Track) 控制: Solo, Mute, Arm, Monitor, Pan, Volume 等。
*   **Dynamic (`Dynamic_List.json` 及对应的 `*_List.json` 数据文件)**: 定义了动态文件夹及其内容。
    *   `Add Track`: 添加各类轨道。
    *   `Render`: 渲染设置与导出。
    *   `Effect`: 效果器列表，支持按品牌 (Brand) 和类型 (Type) 过滤，数据源为 `Effect_List.json`。
    *   `Instrument`: 乐器列表，支持按品牌和类型过滤，数据源为 `Instrument_List.json`。
    *   `Track Name`: 轨道命名模板，数据源为 `Track_Name_List.json`。
    *   `Track Routing`: 轨道路由控制。
    *   `Input Output`: 输入输出控制。
*   **OSC 地址与参数**: JSON 配置文件中大量使用了 `OscAddress`, `IncreaseOSCAddress`, `DecreaseOSCAddress`, `ResetOscAddress`, `Titles`, `Modes`, `OscAddresses` 等字段来精确定义每个控件与 REAPER 的 OSC 通信方式。支持 `{mode}` 占位符，用于根据当前模式动态生成 OSC 地址。

## 开发者

*   SGGGGG

---

*请注意检查并确认上述 macOS 用户配置路径的准确性。*
