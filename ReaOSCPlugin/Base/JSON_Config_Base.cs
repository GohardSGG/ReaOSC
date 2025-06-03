// 文件名: Base/JSON_Config_Base.cs
namespace Loupedeck.ReaOSCPlugin.Base
{
    using System.Collections.Generic;

    using Newtonsoft.Json;

    public class ButtonConfig
    {
        // === 通用配置 ===
        public string DisplayName { get; set; }
        public string Title { get; set; }
        public string TitleColor { get; set; }
        public string GroupName { get; set; }
        public string ActionType { get; set; }
        // 可能的值: "TriggerButton", "ToggleButton", "TickDial", "ToggleDial", "2ModeTickDial", "SelectModeButton"

        // vvvvvvvvvv 新增的属性 vvvvvvvvvv

        /// <summary>
        /// 用于在Loupedeck UI中显示动作的详细描述。
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// 定义此按钮/旋钮所属的模式组名称。
        /// 对于受控按钮，此名称指向它所归属的主管理按钮的 DisplayName。
        /// </summary>
        public string ModeName { get; set; }

        /// <summary>
        /// 仅用于 ActionType 为 "SelectModeButton" 的主管理按钮。
        /// 定义了该模式组拥有的所有模式的列表。
        /// 例如： ["Track", "Take"] 或 ["FX", "Chain"]
        /// </summary>
        public List<string> Modes { get; set; }

        // ^^^^^^^^^^^ 新增的属性 ^^^^^^^^^^^

        // === 按钮相关 ===
        public string OscAddress { get; set; }

        // --- ToggleButton 和 ToggleDial 特有 ---
        public string ActiveColor { get; set; }
        public string ActiveTextColor { get; set; }
        public string DeactiveTextColor { get; set; }
        public string ButtonImage { get; set; }

        // === 旋钮相关 (TickDial, ToggleDial, 2ModeTickDial) ===
        public string IncreaseOSCAddress { get; set; }
        public string DecreaseOSCAddress { get; set; }
        public float? AccelerationFactor { get; set; }
        public string ResetOscAddress { get; set; }

        // --- 次要文本 ---
        public string Text { get; set; }
        public string TextColor { get; set; }
        public int? TextSize { get; set; }
        public int? TextX { get; set; }
        public int? TextY { get; set; }
        public int? TextWidth { get; set; }
        public int? TextHeight { get; set; }

        // =======================================================
        // === 2ModeTickDial 特有配置 ===
        // =======================================================
        public string Title_Mode2 { get; set; }
        public string TitleColor_Mode2 { get; set; }
        public string IncreaseOSCAddress_Mode2 { get; set; }
        public string DecreaseOSCAddress_Mode2 { get; set; }
        public string BackgroundColor { get; set; } // 模式1的背景色
        public string BackgroundColor_Mode2 { get; set; } // 模式2的背景色
    }
}