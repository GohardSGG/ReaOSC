namespace Loupedeck.ReaOSCPlugin.Base
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Threading.Tasks;

    using Newtonsoft.Json;

    public class Effects_Button_Base : PluginDynamicCommand
    {
        // --- 其他部分代码（构造函数, RunCommand等）保持不变 ---
        #region Unchanged Code
        private readonly Dictionary<string, ButtonConfig> _buttonConfigs = new Dictionary<string, ButtonConfig>();
        private readonly Dictionary<string, bool> _temporaryActiveStates = new Dictionary<string, bool>();

        public Effects_Button_Base() : base()
        {
            var jsonContent = PluginResources.ReadTextFile("Effects_List.json");
            if (String.IsNullOrEmpty(jsonContent))
            { PluginLog.Error("无法读取或解析 Effects_List.json。"); return; }
            var groupedConfigs = JsonConvert.DeserializeObject<Dictionary<string, List<ButtonConfig>>>(jsonContent);
            foreach (var group in groupedConfigs)
            {
                var groupName = group.Key;
                foreach (var config in group.Value)
                {
                    var description = $"插入 {config.DisplayName} 效果器";
                    var oscPath = $"Add/{groupName.Replace(" FX", "")}/{config.DisplayName}";
                    var actionParameter = oscPath;
                    this._buttonConfigs[actionParameter] = config;
                    this._temporaryActiveStates[actionParameter] = false;
                    this.AddParameter(actionParameter, config.DisplayName, groupName, description);
                }
            }
        }

        private static BitmapColor HexToBitmapColor(string hexColor)
        {
            if (String.IsNullOrEmpty(hexColor) || !hexColor.StartsWith("#"))
            { return BitmapColor.White; }
            var hex = hexColor.Substring(1);
            if (hex.Length != 6)
            { return BitmapColor.White; }
            try
            {
                var r = (byte)Int32.Parse(hex.Substring(0, 2), NumberStyles.HexNumber);
                var g = (byte)Int32.Parse(hex.Substring(2, 2), NumberStyles.HexNumber);
                var b = (byte)Int32.Parse(hex.Substring(4, 2), NumberStyles.HexNumber);
                return new BitmapColor(r, g, b);
            }
            catch (Exception ex)
            {
                PluginLog.Error(ex, $"无法解析十六进制颜色: {hexColor}");
                return BitmapColor.Red;
            }
        }

        protected override void RunCommand(string actionParameter)
        {
            if (!this._buttonConfigs.ContainsKey(actionParameter))
                return;
            ReaOSCPlugin.SendFXMessage(actionParameter, 1);
            PluginLog.Info($"已触发效果器添加请求: {actionParameter}");
            this._temporaryActiveStates[actionParameter] = true;
            this.ActionImageChanged(actionParameter);
            Task.Delay(200).ContinueWith(_ => { this._temporaryActiveStates[actionParameter] = false; this.ActionImageChanged(actionParameter); });
        }

        private int GetAutomaticTitleFontSize(String title)
        {
            if (String.IsNullOrEmpty(title))
            { return 23; }
            var words = title.Split(' ');
            var maxLength = words.Max(word => word.Length);
            return maxLength switch
            {
                1 => 38,
                2 => 33,
                3 => 31,
                4 => 29,
                5 => 26,
                >= 6 and <= 7 => 20,
                8 => 18,
                9 => 17,
                10 => 16,
                11 => 13,
                _ => 11
            };
        }
        #endregion

        /// <summary>
        /// 当Loupedeck需要绘制按钮时，此方法会被调用。
        /// </summary>
        protected override BitmapImage GetCommandImage(string actionParameter, PluginImageSize imageSize)
        {
            if (!this._buttonConfigs.TryGetValue(actionParameter, out var config))
            {
                return base.GetCommandImage(actionParameter, imageSize);
            }

            var isHighlighted = this._temporaryActiveStates[actionParameter];
            var backgroundColor = BitmapColor.Black;
            // 【修改】高亮颜色现在是硬编码的固定值
            var highlightColor = new BitmapColor(0x50, 0x50, 0x50);

            using (var bitmapBuilder = new BitmapBuilder(imageSize))
            {
                bitmapBuilder.Clear(isHighlighted ? highlightColor : backgroundColor);

                // 绘制主标题
                if (!String.IsNullOrEmpty(config.Title))
                {
                    var autoTitleSize = this.GetAutomaticTitleFontSize(config.Title);
                    var titleColor = String.IsNullOrEmpty(config.TitleColor) ? BitmapColor.White : HexToBitmapColor(config.TitleColor);
                    bitmapBuilder.DrawText(text: config.Title, fontSize: autoTitleSize, color: titleColor);
                }

                // 绘制次要文本
                if (!String.IsNullOrEmpty(config.Text))
                {
                    var textSize = config.TextSize ?? 14;
                    var textX = config.TextX ?? 35;
                    var textY = config.TextY ?? 55;
                    var textWidth = config.TextWidth ?? 14;
                    var textHeight = config.TextHeight ?? 14;
                    bitmapBuilder.DrawText(
                        text: config.Text,
                        x: textX,
                        y: textY,
                        width: textWidth,
                        height: textHeight,
                        color: HexToBitmapColor(config.TextColor),
                        fontSize: textSize
                    );
                }
                return bitmapBuilder.ToImage();
            }
        }
    }
}