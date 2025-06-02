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
        // 可能的值: "TriggerButton", "ToggleButton", "TickDial", "ToggleDial" // 【注释已更新】

        [JsonIgnore]
        public string GroupName { get; set; }

        // === 按钮相关 ===
        public string OscAddress { get; set; }

        // --- ToggleButton 和 ToggleDial 特有 ---
        public string ActiveColor { get; set; }
        public string ActiveTextColor { get; set; }
        public string DeactiveTextColor { get; set; }
        public string ButtonImage { get; set; }

        // === 旋钮相关 ===
        public string IncreaseOSCAddress { get; set; }
        public string DecreaseOSCAddress { get; set; }
        public float? AccelerationFactor { get; set; }

        public string ResetOscAddress { get; set; }

        // === 次要文本 ===
        public string Text { get; set; }
        public string TextColor { get; set; }
        public int? TextSize { get; set; }
        public int? TextX { get; set; }
        public int? TextY { get; set; }
        public int? TextWidth { get; set; }
        public int? TextHeight { get; set; }
    }
}