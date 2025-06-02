// 文件名: Base/General_Button_Base.cs
namespace Loupedeck.ReaOSCPlugin.Base
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Timers;

    public class General_Button_Base : PluginDynamicCommand, IDisposable
    {
        // 【UI状态回归】瞬时高亮相关的状态和定时器由本类管理
        private readonly Dictionary<string, bool> _triggerTemporaryActiveStates = new Dictionary<string, bool>();
        private readonly Dictionary<string, Timer> _triggerResetTimers = new Dictionary<string, Timer>();

        public General_Button_Base() : base()
        {
            Logic_Manager_Base.Instance.Initialize();

            foreach (var entry in Logic_Manager_Base.Instance.GetAllConfigs())
            {
                var config = entry.Value;
                if (config.ActionType == "TriggerButton" || config.ActionType == "ToggleButton")
                {
                    this.AddParameter(entry.Key, config.DisplayName, config.GroupName, $"按钮操作: {config.DisplayName}");

                    if (config.ActionType == "TriggerButton")
                    {
                        this._triggerTemporaryActiveStates[entry.Key] = false;
                        var timer = new Timer(500) { AutoReset = false };
                        timer.Elapsed += (s, e) => {
                            this._triggerTemporaryActiveStates[entry.Key] = false;
                            this.ActionImageChanged(entry.Key);
                        };
                        this._triggerResetTimers[entry.Key] = timer;
                    }
                }
            }
            // 订阅事件，仅用于触发重绘
            OSCStateManager.Instance.StateChanged += (s, e) => {
                if (Logic_Manager_Base.Instance.GetConfig(e.Address)?.ActionType == "ToggleButton")
                {
                    this.ActionImageChanged(e.Address);
                }
            };
        }

        protected override void RunCommand(string actionParameter)
        {
            var config = Logic_Manager_Base.Instance.GetConfig(actionParameter);
            if (config == null)
                return;

            // 调用管理器处理核心逻辑
            Logic_Manager_Base.Instance.ProcessGeneralButtonPress(config, actionParameter);

            // 处理UI反馈
            if (config.ActionType == "TriggerButton" && this._triggerResetTimers.TryGetValue(actionParameter, out var timer))
            {
                this._triggerTemporaryActiveStates[actionParameter] = true;
                this.ActionImageChanged(actionParameter);
                timer.Stop();
                timer.Start();
            }
        }

        // 【UI代码回归】图像生成逻辑由本类负责
        protected override BitmapImage GetCommandImage(string actionParameter, PluginImageSize imageSize)
        {
            var config = Logic_Manager_Base.Instance.GetConfig(actionParameter);
            if (config == null)
                return base.GetCommandImage(actionParameter, imageSize);

            using (var bitmapBuilder = new BitmapBuilder(imageSize))
            {
                BitmapColor currentBgColor = BitmapColor.Black;
                BitmapColor currentTitleColor = String.IsNullOrEmpty(config.TitleColor) ? BitmapColor.White : HexToBitmapColor(config.TitleColor);
                bool iconDrawn = false;

                if (config.ActionType == "TriggerButton")
                {
                    var isTempActive = this._triggerTemporaryActiveStates.TryGetValue(actionParameter, out var tempState) && tempState;
                    currentBgColor = isTempActive && !String.IsNullOrEmpty(config.ActiveColor) ? HexToBitmapColor(config.ActiveColor) : BitmapColor.Black;
                }
                else if (config.ActionType == "ToggleButton")
                {
                    var isActive = Logic_Manager_Base.Instance.GetToggleState(actionParameter);
                    currentBgColor = isActive && !String.IsNullOrEmpty(config.ActiveColor) ? HexToBitmapColor(config.ActiveColor) : BitmapColor.Black;
                    currentTitleColor = isActive
                        ? (String.IsNullOrEmpty(config.ActiveTextColor) ? BitmapColor.White : HexToBitmapColor(config.ActiveTextColor))
                        : (String.IsNullOrEmpty(config.DeactiveTextColor) ? BitmapColor.White : HexToBitmapColor(config.DeactiveTextColor));

                    var imageName = String.IsNullOrEmpty(config.ButtonImage) ? $"{config.DisplayName}.png" : config.ButtonImage;
                    if (!String.IsNullOrEmpty(imageName))
                    {
                        try
                        {
                            var icon = PluginResources.ReadImage(imageName);
                            if (icon != null)
                            {
                                bitmapBuilder.Clear(currentBgColor);
                                int iconHeight = 46;
                                int iconWidth = icon.Width * iconHeight / icon.Height;
                                int iconX = (bitmapBuilder.Width - iconWidth) / 2;
                                int iconY = 8;
                                bitmapBuilder.DrawImage(icon, iconX, iconY, iconWidth, iconHeight);
                                bitmapBuilder.DrawText(text: config.DisplayName, x: 0, y: bitmapBuilder.Height - 23, width: bitmapBuilder.Width, height: 20, fontSize: 12, color: currentTitleColor);
                                iconDrawn = true;
                            }
                        }
                        catch { }
                    }
                }

                if (!iconDrawn)
                {
                    bitmapBuilder.Clear(currentBgColor);
                    if (!String.IsNullOrEmpty(config.Title))
                    {
                        bitmapBuilder.DrawText(text: config.Title, fontSize: this.GetAutomaticTitleFontSize(config.Title), color: currentTitleColor);
                    }
                    if (!String.IsNullOrEmpty(config.Text))
                    {
                        bitmapBuilder.DrawText(text: config.Text, x: config.TextX ?? 50, y: config.TextY ?? 55, width: config.TextWidth ?? 14, height: config.TextHeight ?? 14, color: String.IsNullOrEmpty(config.TextColor) ? BitmapColor.White : HexToBitmapColor(config.TextColor), fontSize: config.TextSize ?? 14);
                    }
                }
                return bitmapBuilder.ToImage();
            }
        }

        #region UI辅助方法
        private int GetAutomaticTitleFontSize(String title)
        {
            if (String.IsNullOrEmpty(title))
                return 23;
            var totalLengthWithSpaces = title.Length;
            int effectiveLength;
            if (totalLengthWithSpaces <= 8)
            { effectiveLength = totalLengthWithSpaces; }
            else
            { var words = title.Split(' '); effectiveLength = words.Length > 0 ? words.Max(word => word.Length) : 0; if (effectiveLength == 0 && totalLengthWithSpaces > 0) { effectiveLength = totalLengthWithSpaces; } }
            return effectiveLength switch { 1 => 38, 2 => 33, 3 => 31, 4 => 26, 5 => 23, 6 => 22, 7 => 20, 8 => 18, 9 => 17, 10 => 16, 11 => 13, _ => 18 };
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

        public void Dispose()
        {
            foreach (var timer in this._triggerResetTimers.Values)
                timer.Dispose();
            this._triggerResetTimers.Clear();
        }
    }
}