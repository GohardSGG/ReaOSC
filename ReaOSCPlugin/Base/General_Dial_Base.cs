// 文件名: Base/General_Dial_Base.cs
namespace Loupedeck.ReaOSCPlugin.Base
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;

    public class General_Dial_Base : PluginDynamicAdjustment
    {
        // 【UI状态回归】加速度计算所需的时间戳由本类管理
        private readonly Dictionary<string, DateTime> _lastEventTimes = new Dictionary<string, DateTime>();

        public General_Dial_Base() : base(hasReset: true)
        {
            Logic_Manager_Base.Instance.Initialize();

            foreach (var entry in Logic_Manager_Base.Instance.GetAllConfigs())
            {
                var config = entry.Value;
                if (config.ActionType == "TickDial" || config.ActionType == "ToggleDial" || config.ActionType == "2ModeTickDial")
                {
                    this.AddParameter(entry.Key, config.DisplayName, config.GroupName, $"旋钮调整: {config.DisplayName}");
                    if (config.ActionType == "TickDial")
                    {
                        this._lastEventTimes[entry.Key] = DateTime.MinValue;
                    }
                }
            }
            OSCStateManager.Instance.StateChanged += (s, e) => {
                if (Logic_Manager_Base.Instance.GetConfig(e.Address)?.ActionType == "ToggleDial")
                {
                    this.ActionImageChanged(e.Address);
                }
            };
        }

        protected override void ApplyAdjustment(string actionParameter, int ticks)
        {
            var config = Logic_Manager_Base.Instance.GetConfig(actionParameter);
            if (config == null)
                return;

            // 调用管理器处理核心逻辑，将UI相关的状态传入
            Logic_Manager_Base.Instance.ProcessDialAdjustment(config, ticks, actionParameter, this._lastEventTimes);

            this.AdjustmentValueChanged(actionParameter);
        }

        protected override void RunCommand(string actionParameter)
        {
            var config = Logic_Manager_Base.Instance.GetConfig(actionParameter);
            if (config == null)
                return;

            // 调用管理器处理核心逻辑，如果状态改变则重绘
            if (Logic_Manager_Base.Instance.ProcessDialPress(config, actionParameter))
            {
                this.ActionImageChanged(actionParameter);
            }
        }

        protected override string GetAdjustmentValue(string actionParameter) => null;

        // 【UI代码回归】
        protected override BitmapImage GetCommandImage(string actionParameter, PluginImageSize imageSize)
        {
            var config = Logic_Manager_Base.Instance.GetConfig(actionParameter);
            if (config == null)
                return null;

            using (var bitmapBuilder = new BitmapBuilder(imageSize))
            {
                if (config.ActionType == "2ModeTickDial")
                {
                    var currentMode = Logic_Manager_Base.Instance.GetDialMode(actionParameter);
                    var title = currentMode == 0 ? config.Title : config.Title_Mode2;
                    var titleColor = currentMode == 0 ? (String.IsNullOrEmpty(config.TitleColor) ? BitmapColor.White : HexToBitmapColor(config.TitleColor)) : (String.IsNullOrEmpty(config.TitleColor_Mode2) ? BitmapColor.White : HexToBitmapColor(config.TitleColor_Mode2));
                    var bgColor = currentMode == 0 ? (String.IsNullOrEmpty(config.BackgroundColor) ? BitmapColor.Black : HexToBitmapColor(config.BackgroundColor)) : (String.IsNullOrEmpty(config.BackgroundColor_Mode2) ? BitmapColor.Black : HexToBitmapColor(config.BackgroundColor_Mode2));

                    bitmapBuilder.Clear(bgColor);
                    if (!String.IsNullOrEmpty(title))
                    {
                        bitmapBuilder.DrawText(text: title, fontSize: this.GetAutomaticDialTitleFontSize(title), color: titleColor);
                    }
                }
                else
                {
                    BitmapColor currentBgColor = BitmapColor.Black;
                    BitmapColor currentTitleColor = String.IsNullOrEmpty(config.TitleColor) ? BitmapColor.White : HexToBitmapColor(config.TitleColor);

                    if (config.ActionType == "ToggleDial")
                    {
                        var isActive = Logic_Manager_Base.Instance.GetToggleState(actionParameter);
                        currentBgColor = isActive && !String.IsNullOrEmpty(config.ActiveColor) ? HexToBitmapColor(config.ActiveColor) : BitmapColor.Black;
                        currentTitleColor = isActive
                           ? (String.IsNullOrEmpty(config.ActiveTextColor) ? BitmapColor.White : HexToBitmapColor(config.ActiveTextColor))
                           : (String.IsNullOrEmpty(config.DeactiveTextColor) ? BitmapColor.White : HexToBitmapColor(config.DeactiveTextColor));
                    }

                    bitmapBuilder.Clear(currentBgColor);
                    if (!String.IsNullOrEmpty(config.Title))
                    {
                        bitmapBuilder.DrawText(text: config.Title, fontSize: this.GetAutomaticDialTitleFontSize(config.Title), color: currentTitleColor);
                    }
                    if (!String.IsNullOrEmpty(config.Text))
                    {
                        bitmapBuilder.DrawText(text: config.Text, x: config.TextX ?? 5, y: config.TextY ?? (bitmapBuilder.Height - 20), width: config.TextWidth ?? (bitmapBuilder.Width - 10), height: config.TextHeight ?? 18, color: String.IsNullOrEmpty(config.TextColor) ? BitmapColor.White : HexToBitmapColor(config.TextColor), fontSize: config.TextSize ?? 12);
                    }
                }
                return bitmapBuilder.ToImage();
            }
        }

        #region UI辅助方法
        private int GetAutomaticDialTitleFontSize(String title)
        {
            if (String.IsNullOrEmpty(title))
                return 16;
            var totalLengthWithSpaces = title.Length;
            int effectiveLength;
            if (totalLengthWithSpaces <= 10)
            { effectiveLength = totalLengthWithSpaces; }
            else
            { var words = title.Split(' '); effectiveLength = words.Length > 0 ? words.Max(word => word.Length) : 0; if (effectiveLength == 0 && totalLengthWithSpaces > 0) { effectiveLength = totalLengthWithSpaces; } }
            return effectiveLength switch { 1 => 26, 2 => 23, 3 => 21, 4 => 19, 5 => 17, >= 6 and <= 7 => 15, 8 => 13, 9 => 12, 10 => 11, 11 => 10, _ => 9 };
        }

        private static BitmapColor HexToBitmapColor(string hexColor)
        {
            if (String.IsNullOrEmpty(hexColor) || !hexColor.StartsWith("#"))
                return BitmapColor.White;
            var hex = hexColor.Substring(1);
            if (hex.Length != 6)
                return BitmapColor.White;
            try
            {
                var r = (byte)Int32.Parse(hex.Substring(0, 2), NumberStyles.HexNumber);
                var g = (byte)Int32.Parse(hex.Substring(2, 2), NumberStyles.HexNumber);
                var b = (byte)Int32.Parse(hex.Substring(4, 2), NumberStyles.HexNumber);
                return new BitmapColor(r, g, b);
            }
            catch { return BitmapColor.Red; }
        }
        #endregion
    }
}