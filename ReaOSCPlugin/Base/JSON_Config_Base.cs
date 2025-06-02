// 文件名: Base/JSON_Config_Base.cs
namespace Loupedeck.ReaOSCPlugin.Base
{
    using Newtonsoft.Json;

    public class ButtonConfig
    {
        // === 通用配置 ===
        public string DisplayName { get; set; }
        public string Title { get; set; }
        public string TitleColor { get; set; }

        public string ActionType { get; set; }
        // 可能的值: "TriggerButton", "ToggleButton", "TickDial", "ToggleDial", "2ModeTickDial" // 【注释已更新】

        [JsonIgnore]
        public string GroupName { get; set; }

        // === 按钮相关 ===
        public string OscAddress { get; set; }

        // --- ToggleButton 和 ToggleDial 特有 ---
        public string ActiveColor { get; set; }
        public string ActiveTextColor { get; set; }
        public string DeactiveTextColor { get; set; }
        public string ButtonImage { get; set; }

        // === 旋钮相关 (TickDial, ToggleDial, 2ModeTickDial) ===
        public string IncreaseOSCAddress { get; set; } // TickDial/2ModeTickDial模式1 的增加地址
        public string DecreaseOSCAddress { get; set; } // TickDial/2ModeTickDial模式1 的减少地址
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
        // === 新增: 2ModeTickDial 特有配置 ===
        // =======================================================
        public string Title_Mode2 { get; set; }
        public string TitleColor_Mode2 { get; set; }
        public string IncreaseOSCAddress_Mode2 { get; set; }
        public string DecreaseOSCAddress_Mode2 { get; set; }
        public string BackgroundColor { get; set; } // 模式1的背景色
        public string BackgroundColor_Mode2 { get; set; } // 模式2的背景色
    }
}