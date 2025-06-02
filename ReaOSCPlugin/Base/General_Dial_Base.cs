// 文件名: Base/General_Dial_Base.cs
namespace Loupedeck.ReaOSCPlugin.Base
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Reflection;

    using Loupedeck.ReaOSCPlugin;
    using Newtonsoft.Json;
    using Loupedeck;

    public class General_Dial_Base : PluginDynamicAdjustment, IDisposable
    {
        protected readonly Dictionary<string, ButtonConfig> _dialConfigs = new Dictionary<string, ButtonConfig>();
        private readonly Dictionary<string, bool> _toggleActiveStates = new Dictionary<string, bool>();
        private readonly Dictionary<string, DateTime> _lastEventTimes = new Dictionary<string, DateTime>();
        private readonly Dictionary<string, int> _dialModes = new Dictionary<string, int>();

        public General_Dial_Base() : base(hasReset: true)
        {
            PluginLog.Info("General_Dial_Base 构造函数开始执行。");
            string jsonContent = ReadEmbeddedJson("Loupedeck.ReaOSCPlugin.General.General_List.json");
            if (String.IsNullOrEmpty(jsonContent))
            {
                PluginLog.Error("无法加载 General_Dial_Base 的配置。");
                return;
            }

            var groupedConfigs = JsonConvert.DeserializeObject<Dictionary<string, List<ButtonConfig>>>(jsonContent);
            if (groupedConfigs == null)
            {
                PluginLog.Error("反序列化 General_List.json (旋钮部分) 失败。");
                return;
            }

            foreach (var group in groupedConfigs)
            {
                var groupNameFromJson = group.Key;
                string groupNameForPath = groupNameFromJson.Replace(" FX", "").Replace(" ", "/");

                foreach (var config in group.Value)
                {
                    if (config.ActionType != "TickDial" && config.ActionType != "ToggleDial" && config.ActionType != "2ModeTickDial")
                        continue;

                    config.GroupName = groupNameFromJson;
                    string baseOscNameForActionParam = config.DisplayName.Replace(" ", "/");
                    string actionParameter = $"/{groupNameForPath}/{baseOscNameForActionParam}";

                    if (config.ActionType == "TickDial")
                        actionParameter += "/TickDialAction";
                    if (config.ActionType == "2ModeTickDial")
                        actionParameter += "/2ModeTickDialAction";

                    this._dialConfigs[actionParameter] = config;
                    this.AddParameter(actionParameter, config.DisplayName, config.GroupName, $"旋钮调整: {config.DisplayName}");

                    if (config.ActionType == "ToggleDial")
                    {
                        string baseOscName = String.IsNullOrEmpty(config.OscAddress) ? config.DisplayName : config.OscAddress;
                        var fullOscAddress = $"/{groupNameForPath}/{baseOscName.Replace(" ", "/")}".Replace("//", "/");
                        this._toggleActiveStates[actionParameter] = OSCStateManager.Instance.GetState(fullOscAddress) > 0.5f;
                    }
                    else if (config.ActionType == "TickDial")
                    {
                        this._lastEventTimes[actionParameter] = DateTime.MinValue;
                    }
                    else if (config.ActionType == "2ModeTickDial")
                    {
                        this._dialModes[actionParameter] = 0;
                    }
                    PluginLog.Verbose($"General_Dial_Base: 已添加参数: {actionParameter} (DisplayName: {config.DisplayName}, Group: {config.GroupName}, Type: {config.ActionType})");
                }
            }
            OSCStateManager.Instance.StateChanged += this.OnOSCStateChanged;
            PluginLog.Info("General_Dial_Base 构造函数执行完毕。");
        }

        protected override void ApplyAdjustment(string actionParameter, int ticks)
        {
            if (ticks == 0 || !this._dialConfigs.TryGetValue(actionParameter, out var config))
                return;

            string groupNameForPath = config.GroupName.Replace(" FX", "").Replace(" ", "/");
            string oscAddress = "";

            if (config.ActionType == "TickDial" || config.ActionType == "2ModeTickDial")
            {
                var currentMode = this._dialModes.TryGetValue(actionParameter, out var mode) ? mode : 0;
                var modeTitle = (config.ActionType == "2ModeTickDial" && currentMode == 1)
                    ? config.Title_Mode2
                    : config.Title;

                if (String.IsNullOrEmpty(modeTitle))
                {
                    modeTitle = config.DisplayName;
                }

                var actionSuffix = ticks > 0 ? "Up" : "Down";

                oscAddress = $"/{groupNameForPath}/{modeTitle.Replace(" ", "/")}/{actionSuffix}".Replace("//", "/");

                if (ticks > 0 && !String.IsNullOrEmpty(config.IncreaseOSCAddress))
                {
                    oscAddress = config.IncreaseOSCAddress;
                }
                else if (ticks < 0 && !String.IsNullOrEmpty(config.DecreaseOSCAddress))
                {
                    oscAddress = config.DecreaseOSCAddress;
                }

                if (!String.IsNullOrEmpty(oscAddress))
                {
                    var acceleration = config.AccelerationFactor ?? 1.0f;
                    var lastEventTime = this._lastEventTimes.TryGetValue(actionParameter, out var time) ? time : DateTime.MinValue;
                    var now = DateTime.Now;
                    var elapsed = now - lastEventTime;
                    if (lastEventTime == DateTime.MinValue)
                        elapsed = TimeSpan.FromSeconds(1);
                    this._lastEventTimes[actionParameter] = now;

                    double speedFactor = 1.0 / Math.Max(elapsed.TotalSeconds, 0.02);
                    int baseCount = Math.Abs(ticks);
                    int totalCount = (int)(baseCount * acceleration * speedFactor);
                    totalCount = Math.Clamp(totalCount, 1, 10);

                    for (int i = 0; i < totalCount; i++)
                    {
                        ReaOSCPlugin.SendOSCMessage(oscAddress, 1f);
                    }
                    this.AdjustmentValueChanged(actionParameter);
                }
            }
            else if (config.ActionType == "ToggleDial")
            {
                string baseOscName = String.IsNullOrEmpty(config.OscAddress) ? config.DisplayName : config.OscAddress;
                var addr = $"/{groupNameForPath}/{baseOscName.Replace(" ", "/")}".Replace("//", "/");
                var isActive = this._toggleActiveStates.TryGetValue(actionParameter, out var state) && state;
                float newValue = -1f;
                if (ticks > 0 && !isActive)
                    newValue = 1f;
                else if (ticks < 0 && isActive)
                    newValue = 0f;
                if (newValue >= 0f)
                    ReaOSCPlugin.SendOSCMessage(addr, newValue);
            }
        }

        protected override void RunCommand(string actionParameter)
        {
            if (!this._dialConfigs.TryGetValue(actionParameter, out var config))
                return;

            if (config.ActionType == "2ModeTickDial")
            {
                if (this._dialModes.ContainsKey(actionParameter))
                {
                    this._dialModes[actionParameter] = 1 - this._dialModes[actionParameter];
                    this.ActionImageChanged(actionParameter);
                }
                return;
            }

            string groupNameForPath = config.GroupName.Replace(" FX", "").Replace(" ", "/");
            string baseOscNameForReset = config.DisplayName.Replace(" ", "/");
            string effectiveResetNamePart = String.IsNullOrEmpty(config.ResetOscAddress) ? "Reset" : config.ResetOscAddress;

            if (!string.IsNullOrEmpty(effectiveResetNamePart))
            {
                var resetAddr = $"/{groupNameForPath}/{baseOscNameForReset}/{effectiveResetNamePart.Replace(" ", "/")}".Replace("//", "/");
                PluginLog.Info($"RunCommand (Dial): 发送Reset OSC消息到 '{resetAddr}'");
                ReaOSCPlugin.SendOSCMessage(resetAddr, 1f);
            }
            else
            {
                PluginLog.Verbose($"RunCommand (Dial): '{actionParameter}' 没有配置有效的ResetOscAddress。");
            }
        }

        protected override string GetAdjustmentValue(string actionParameter) => null;

        protected override BitmapImage GetCommandImage(string actionParameter, PluginImageSize imageSize)
        {
            if (!this._dialConfigs.TryGetValue(actionParameter, out var config))
                return base.GetCommandImage(actionParameter, imageSize);

            using (var bitmapBuilder = new BitmapBuilder(imageSize))
            {
                if (config.ActionType == "2ModeTickDial")
                {
                    var currentMode = this._dialModes.TryGetValue(actionParameter, out var mode) ? mode : 0;
                    var title = currentMode == 0 ? config.Title : config.Title_Mode2;
                    var titleColor = currentMode == 0
                        ? (String.IsNullOrEmpty(config.TitleColor) ? BitmapColor.White : HexToBitmapColor(config.TitleColor))
                        : (String.IsNullOrEmpty(config.TitleColor_Mode2) ? BitmapColor.White : HexToBitmapColor(config.TitleColor_Mode2));
                    var bgColor = currentMode == 0
                        ? (String.IsNullOrEmpty(config.BackgroundColor) ? BitmapColor.Black : HexToBitmapColor(config.BackgroundColor))
                        : (String.IsNullOrEmpty(config.BackgroundColor_Mode2) ? BitmapColor.Black : HexToBitmapColor(config.BackgroundColor_Mode2));

                    bitmapBuilder.Clear(bgColor);
                    if (!String.IsNullOrEmpty(title))
                    {
                        var autoTitleSize = this.GetAutomaticDialTitleFontSize(title);
                        bitmapBuilder.DrawText(text: title, fontSize: autoTitleSize, color: titleColor);
                    }
                }
                else
                {
                    BitmapColor currentBgColor = BitmapColor.Black;
                    BitmapColor currentTitleColor = String.IsNullOrEmpty(config.TitleColor) ? BitmapColor.White : HexToBitmapColor(config.TitleColor);

                    if (config.ActionType == "ToggleDial")
                    {
                        var isActive = this._toggleActiveStates.TryGetValue(actionParameter, out var state) && state;
                        currentBgColor = isActive && !String.IsNullOrEmpty(config.ActiveColor) ? HexToBitmapColor(config.ActiveColor) : BitmapColor.Black;
                        currentTitleColor = isActive
                           ? (String.IsNullOrEmpty(config.ActiveTextColor) ? BitmapColor.White : HexToBitmapColor(config.ActiveTextColor))
                           : (String.IsNullOrEmpty(config.DeactiveTextColor) ? BitmapColor.White : HexToBitmapColor(config.DeactiveTextColor));
                    }

                    bitmapBuilder.Clear(currentBgColor);

                    if (!String.IsNullOrEmpty(config.Title))
                    {
                        var autoTitleSize = this.GetAutomaticDialTitleFontSize(config.Title);
                        bitmapBuilder.DrawText(text: config.Title, fontSize: autoTitleSize, color: currentTitleColor);
                    }

                    if (!String.IsNullOrEmpty(config.Text))
                    {
                        var textSize = config.TextSize ?? 12;
                        var textX = config.TextX ?? 5;
                        var textY = config.TextY ?? (bitmapBuilder.Height - 20);
                        var textWidth = config.TextWidth ?? (bitmapBuilder.Width - 10);
                        var textHeight = config.TextHeight ?? 18;
                        var textColor = String.IsNullOrEmpty(config.TextColor) ? BitmapColor.White : HexToBitmapColor(config.TextColor);
                        bitmapBuilder.DrawText(text: config.Text, x: textX, y: textY, width: textWidth, height: textHeight, color: textColor, fontSize: textSize);
                    }
                }
                return bitmapBuilder.ToImage();
            }
        }

        private void OnOSCStateChanged(object sender, OSCStateManager.StateChangedEventArgs e)
        {
            foreach (var entry in this._dialConfigs)
            {
                var actionParameter = entry.Key;
                var config = entry.Value;
                if (config.ActionType == "ToggleDial")
                {
                    string groupNameForPath = config.GroupName.Replace(" FX", "").Replace(" ", "/");
                    string baseOscName = String.IsNullOrEmpty(config.OscAddress) ? config.DisplayName : config.OscAddress;
                    var fullOscAddress = $"/{groupNameForPath}/{baseOscName.Replace(" ", "/")}".Replace("//", "/");

                    if (e.Address == fullOscAddress)
                    {
                        var newState = e.Value > 0.5f;
                        if (this._toggleActiveStates.TryGetValue(actionParameter, out var currentState) && currentState != newState)
                        {
                            this._toggleActiveStates[actionParameter] = newState;
                            this.ActionImageChanged(actionParameter);
                        }
                        break;
                    }
                }
            }
        }

        private string ReadEmbeddedJson(string resourceName)
        {
            string jsonContent = "";
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                using (Stream stream = assembly.GetManifestResourceStream(resourceName))
                {
                    if (stream == null)
                    {
                        PluginLog.Error($"无法找到嵌入资源: '{resourceName}'.");
                        return null;
                    }
                    using (StreamReader reader = new StreamReader(stream))
                    {
                        jsonContent = reader.ReadToEnd();
                    }
                }
                if (String.IsNullOrEmpty(jsonContent))
                {
                    PluginLog.Error($"嵌入资源 {resourceName} 内容为空。");
                    return null;
                }
            }
            catch (Exception ex)
            {
                PluginLog.Error(ex, $"读取嵌入的JSON文件 '{resourceName}' 时出错。");
                return null;
            }
            return jsonContent;
        }

        private int GetAutomaticDialTitleFontSize(String title)
        {
            if (String.IsNullOrEmpty(title))
            {
                return 16;
            }
            var totalLengthWithSpaces = title.Length;
            int effectiveLength;
            if (totalLengthWithSpaces <= 10)
            {
                effectiveLength = totalLengthWithSpaces;
            }
            else
            {
                var words = title.Split(' ');
                effectiveLength = words.Length > 0 ? words.Max(word => word.Length) : 0;
                if (effectiveLength == 0 && totalLengthWithSpaces > 0)
                {
                    effectiveLength = totalLengthWithSpaces;
                }
            }
            return effectiveLength switch
            {
                1 => 26,
                2 => 23,
                3 => 21,
                4 => 19,
                5 => 17,
                >= 6 and <= 7 => 15,
                8 => 13,
                9 => 12,
                10 => 11,
                11 => 10,
                _ => 9
            };
        }

        private static BitmapColor HexToBitmapColor(string hexColor)
        {
            if (String.IsNullOrEmpty(hexColor) || !hexColor.StartsWith("#"))
            {
                return BitmapColor.White;
            }
            var hex = hexColor.Substring(1);
            if (hex.Length != 6)
            {
                return BitmapColor.White;
            }
            try
            {
                var r = (byte)Int32.Parse(hex.Substring(0, 2), NumberStyles.HexNumber);
                var g = (byte)Int32.Parse(hex.Substring(2, 2), NumberStyles.HexNumber);
                var b = (byte)Int32.Parse(hex.Substring(4, 2), NumberStyles.HexNumber);
                return new BitmapColor(r, g, b);
            }
            catch (Exception ex)
            {
                PluginLog.Error(ex, $"解析十六进制颜色失败: {hexColor}");
                return BitmapColor.Red;
            }
        }

        public void Dispose()
        {
            OSCStateManager.Instance.StateChanged -= this.OnOSCStateChanged;
            GC.SuppressFinalize(this);
        }
    }
}