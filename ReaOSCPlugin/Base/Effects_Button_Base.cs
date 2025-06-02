// 文件名: Base/Effects_Button_Base.cs
namespace Loupedeck.ReaOSCPlugin.Base
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Threading.Tasks;

    public class Effects_Button_Base : PluginDynamicCommand
    {
        // 【UI状态回归】
        private readonly Dictionary<string, bool> _temporaryActiveStates = new Dictionary<string, bool>();

        public Effects_Button_Base() : base()
        {
            Logic_Manager_Base.Instance.Initialize();

            foreach (var entry in Logic_Manager_Base.Instance.GetAllConfigs())
            {
                var config = entry.Value;
                if (config.ActionType == null && config.GroupName.Contains("FX"))
                {
                    this.AddParameter(entry.Key, config.DisplayName, config.GroupName, $"插入 {config.DisplayName} 效果器");
                    this._temporaryActiveStates[entry.Key] = false;
                }
            }
        }

        protected override void RunCommand(string actionParameter)
        {
            if (Logic_Manager_Base.Instance.GetConfig(actionParameter) == null)
                return;

            // 调用管理器处理核心逻辑
            Logic_Manager_Base.Instance.ProcessFxButtonPress(actionParameter);

            // 处理UI反馈
            this._temporaryActiveStates[actionParameter] = true;
            this.ActionImageChanged(actionParameter);
            Task.Delay(200).ContinueWith(_ => {
                if (this._temporaryActiveStates.ContainsKey(actionParameter))
                {
                    this._temporaryActiveStates[actionParameter] = false;
                    this.ActionImageChanged(actionParameter);
                }
            });
        }

        // 【UI代码回归】
        protected override BitmapImage GetCommandImage(string actionParameter, PluginImageSize imageSize)
        {
            var config = Logic_Manager_Base.Instance.GetConfig(actionParameter);
            if (config == null)
                return null;

            var isHighlighted = this._temporaryActiveStates.TryGetValue(actionParameter, out var active) && active;
            var backgroundColor = isHighlighted ? new BitmapColor(0x50, 0x50, 0x50) : BitmapColor.Black;

            using (var bitmapBuilder = new BitmapBuilder(imageSize))
            {
                bitmapBuilder.Clear(backgroundColor);
                if (!String.IsNullOrEmpty(config.Title))
                {
                    var titleColor = String.IsNullOrEmpty(config.TitleColor) ? BitmapColor.White : HexToBitmapColor(config.TitleColor);
                    bitmapBuilder.DrawText(text: config.Title, fontSize: this.GetAutomaticTitleFontSize(config.Title), color: titleColor);
                }
                if (!String.IsNullOrEmpty(config.Text))
                {
                    var textColor = String.IsNullOrEmpty(config.TextColor) ? BitmapColor.White : HexToBitmapColor(config.TextColor);
                    bitmapBuilder.DrawText(text: config.Text, x: config.TextX ?? 35, y: config.TextY ?? 55, width: config.TextWidth ?? 14, height: config.TextHeight ?? 14, color: textColor, fontSize: config.TextSize ?? 14);
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
            if (totalLengthWithSpaces <= 10)
            { effectiveLength = totalLengthWithSpaces; }
            else
            { var words = title.Split(' '); effectiveLength = words.Length > 0 ? words.Max(word => word.Length) : 0; if (effectiveLength == 0 && totalLengthWithSpaces > 0) { effectiveLength = totalLengthWithSpaces; } }
            return effectiveLength switch { 1 => 38, 2 => 33, 3 => 31, 4 => 26, 5 => 26, >= 6 and <= 7 => 20, 8 => 18, 9 => 17, 10 => 16, 11 => 13, _ => 18 };
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