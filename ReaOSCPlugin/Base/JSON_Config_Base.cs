// 文件名: Base/JSON_Config_Base.cs
namespace Loupedeck.ReaOSCPlugin.Base
{
    using System.Collections.Generic;

    using Newtonsoft.Json;

    public class ButtonConfig
    {
        // === 通用配置 ===
        public string DisplayName { get; set; }
        public string Title { get; set; } // 主要用于UI显示，尤其当DisplayName不适合直接显示时
        public string TitleColor { get; set; } // 通用标题颜色
        public string GroupName { get; set; }
        public string ActionType { get; set; }
        // 可能的值: "TriggerButton", "ToggleButton", "TickDial", "ToggleDial", "2ModeTickDial", "SelectModeButton",
        // 【新增】 "ParameterDial", "ParameterButton", "CombineButton"
        public string Description { get; set; }

        // --- OSC 相关 ---
        public string OscAddress { get; set; } // 可选。如果提供，优先作为此控件贡献给CombineButton的路径片段 (经过处理后)

        // --- 模式相关 (主要用于 SelectModeButton 及其控制的按钮) ---
        public string ModeName { get; set; }
        public List<string> Modes { get; set; } // For SelectModeButton: 定义可选模式
        public List<string> Titles { get; set; } // For ParameterDial: 定义可选参数值; For SelectModeButton & controlled buttons: 定义不同模式下的显示标题
        public List<string> OscAddresses { get; set; } // For SelectModeButton & controlled buttons: 定义不同模式下的OSC地址

        // === 按钮相关 ===
        // --- ToggleButton 和 ToggleDial 特有 (也可能被ParameterDial用于不同状态的显示) ---
        public string ActiveColor { get; set; } // ToggleButton ON 状态背景色, ParameterDial 激活状态背景色 (如果适用)
        public string ActiveTextColor { get; set; } // ToggleButton ON 状态文字颜色
        public string DeactiveTextColor { get; set; } // ToggleButton OFF 状态文字颜色

        public string ButtonImage { get; set; } // (目前主要用于 General_Button_Base, 动态文件夹内按钮较少直接用图片)

        // === 旋钮相关 (TickDial, ToggleDial, 2ModeTickDial) ===
        public string IncreaseOSCAddress { get; set; } // 主要用于 TickDial
        public string DecreaseOSCAddress { get; set; } // 主要用于 TickDial
        public float? AccelerationFactor { get; set; } // 主要用于 TickDial
        public string ResetOscAddress { get; set; } // 主要用于 TickDial, ToggleDial 按下时的 OSC

        // --- 次要文本 (可用于所有类型按钮/旋钮的额外小字显示) ---
        public string Text { get; set; }
        public string TextColor { get; set; }
        public int? TextSize { get; set; }
        public int? TextX { get; set; }
        public int? TextY { get; set; }
        public int? TextWidth { get; set; }
        public int? TextHeight { get; set; }

        // === 2ModeTickDial 特有配置 ===
        public string Title_Mode2 { get; set; }
        public string TitleColor_Mode2 { get; set; }
        public string IncreaseOSCAddress_Mode2 { get; set; }
        public string DecreaseOSCAddress_Mode2 { get; set; }
        public string BackgroundColor { get; set; } // 旋钮模式1的背景色 (或通用背景色)
        public string BackgroundColor_Mode2 { get; set; } // 旋钮模式2的背景色

        // === 【新增】ParameterDial 特有 ===
        // Titles 字段已存在，将被 ParameterDial 用作参数值列表
        public string ShowParameterInDial { get; set; } // "Yes" 或 "No". "Yes" 则旋钮UI显示当前选中的Title, "No"则显示固定的Title/DisplayName

        // === 【新增】ParameterButton 特有 ===
        public string ParameterSourceDial { get; set; } // 指向要显示其参数的 ParameterDial 的 DisplayName

        // === 【新增】CombineButton 特有 ===
        // BaseOscPrefix 将由文件夹 GroupName 动态决定，不需要在此配置
        public List<string> ParameterOrder { get; set; } // 定义 CombineButton 收集参数的顺序，值为参与控件的 DisplayName

        // === 【新增】ToggleButton (当参与 CombineButton 时) ===
        // PathSegmentIfOn 也不再需要，ToggleButton ON 时贡献 DisplayName 或 OscAddress (处理后)
    }
}