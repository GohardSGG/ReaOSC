// 文件名: Base/General_Button_Base.cs
namespace Loupedeck.ReaOSCPlugin.Base
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Threading.Tasks;
    using System.Timers;

    using Loupedeck.ReaOSCPlugin;
    using Newtonsoft.Json;
    using Loupedeck;

    public class General_Button_Base : PluginDynamicCommand, IDisposable
    {
        protected readonly Dictionary<string, ButtonConfig> _buttonConfigs = new Dictionary<string, ButtonConfig>();
        private readonly Dictionary<string, bool> _toggleActiveStates = new Dictionary<string, bool>();
        private readonly Dictionary<string, bool> _triggerTemporaryActiveStates = new Dictionary<string, bool>();
        private readonly Dictionary<string, System.Timers.Timer> _triggerResetTimers = new Dictionary<string, System.Timers.Timer>();

        public General_Button_Base() : base()
        {
            PluginLog.Info("General_Button_Base 构造函数开始执行。");
            string jsonContent = ReadEmbeddedJson("Loupedeck.ReaOSCPlugin.General.General_List.json");
            if (String.IsNullOrEmpty(jsonContent))
            { PluginLog.Error("无法加载 General_Button_Base 的配置。"); return; }

            var groupedConfigs = JsonConvert.DeserializeObject<Dictionary<string, List<ButtonConfig>>>(jsonContent);
            if (groupedConfigs == null)
            { PluginLog.Error("反序列化 General_List.json (按钮部分) 失败。"); return; }

            foreach (var group in groupedConfigs)
            {
                var groupNameFromJson = group.Key;
                // 【修改】组名中的空格也替换为斜杠，并移除 " FX" 后缀
                string groupNameForPath = groupNameFromJson.Replace(" FX", "").Replace(" ", "/");

                foreach (var config in group.Value)
                {
                    if (config.ActionType != "TriggerButton" && config.ActionType != "ToggleButton")
                        continue;

                    config.GroupName = groupNameFromJson;

                    // 【修改】baseOscName 中的空格替换为斜杠
                    string baseOscName = String.IsNullOrEmpty(config.OscAddress)
                        ? config.DisplayName.Replace(" ", "/")
                        : config.OscAddress.Replace(" ", "/");
                    // 确保路径以斜杠开头且没有双斜杠
                    string fullOscAddress = $"/{groupNameForPath}/{baseOscName}".Replace("//", "/");
                    var actionParameter = fullOscAddress;

                    this._buttonConfigs[actionParameter] = config;
                    this.AddParameter(actionParameter, config.DisplayName, config.GroupName, $"按钮操作: {config.DisplayName}");

                    if (config.ActionType == "ToggleButton")
                    {
                        this._toggleActiveStates[actionParameter] = OSCStateManager.Instance.GetState(fullOscAddress) > 0.5f;
                    }
                    else if (config.ActionType == "TriggerButton")
                    {
                        this._triggerTemporaryActiveStates[actionParameter] = false;
                        var timer = new System.Timers.Timer(500) { AutoReset = false };
                        timer.Elapsed += (sender, e) =>
                        {
                            if (this._triggerTemporaryActiveStates.ContainsKey(actionParameter))
                            {
                                this._triggerTemporaryActiveStates[actionParameter] = false;
                                this.ActionImageChanged(actionParameter);
                            }
                        };
                        this._triggerResetTimers[actionParameter] = timer;
                    }
                    PluginLog.Verbose($"General_Button_Base: 已添加参数: {actionParameter} (DisplayName: {config.DisplayName}, Type: {config.ActionType})");
                }
            }
            OSCStateManager.Instance.StateChanged += this.OnOSCStateChanged;
            PluginLog.Info("General_Button_Base 构造函数执行完毕。");
        }

        private string ReadEmbeddedJson(string resourceName)
        {
            // ... (此辅助方法保持不变) ...
            string jsonContent = "";
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                PluginLog.Info($"尝试读取嵌入资源: {resourceName}");
                using (Stream stream = assembly.GetManifestResourceStream(resourceName))
                {
                    if (stream == null)
                    {
                        PluginLog.Error($"无法找到嵌入资源: '{resourceName}'.");
                        PluginLog.Info("项目中可用的嵌入资源名称列表:");
                        foreach (var name in assembly.GetManifestResourceNames())
                        { PluginLog.Info($"- {name}"); }
                        return null;
                    }
                    using (StreamReader reader = new StreamReader(stream))
                    { jsonContent = reader.ReadToEnd(); }
                }
                if (String.IsNullOrEmpty(jsonContent))
                { PluginLog.Error($"嵌入资源 {resourceName} 内容为空。"); return null; }
                PluginLog.Info($"成功从嵌入资源 {resourceName} 读取内容。");
            }
            catch (Exception ex) { PluginLog.Error(ex, $"读取嵌入的JSON文件 '{resourceName}' 时出错。"); return null; }
            return jsonContent;
        }

        private void OnOSCStateChanged(object sender, OSCStateManager.StateChangedEventArgs e)
        {
            foreach (var entry in this._buttonConfigs)
            {
                var actionParameter = entry.Key;
                var config = entry.Value;
                if (config.ActionType == "ToggleButton")
                {
                    // actionParameter 就是 fullOscAddress，在构造函数中已经正确处理了空格为斜杠
                    if (e.Address == actionParameter)
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

        protected override void RunCommand(string actionParameter)
        {
            if (!this._buttonConfigs.TryGetValue(actionParameter, out var config))
                return;

            // actionParameter 就是已经包含了正确路径（空格转为斜杠）的 fullOscAddress
            string fullOscAddress = actionParameter;

            if (config.ActionType == "TriggerButton" || config.ActionType == "ToggleButton")
            {
                float valueToSend = 1f;
                if (config.ActionType == "ToggleButton")
                {
                    var isActive = this._toggleActiveStates.TryGetValue(actionParameter, out var state) && state;
                    valueToSend = isActive ? 0f : 1f;
                }

                ReaOSCPlugin.SendOSCMessage(fullOscAddress, valueToSend);

                if (config.ActionType == "TriggerButton")
                {
                    if (this._triggerResetTimers.TryGetValue(actionParameter, out var timer))
                    {
                        this._triggerTemporaryActiveStates[actionParameter] = true;
                        this.ActionImageChanged(actionParameter);
                        timer.Stop();
                        timer.Start();
                    }
                }
            }
        }

        protected override BitmapImage GetCommandImage(string actionParameter, PluginImageSize imageSize)
        {
            // ... (此方法内部逻辑与上一版一致，只关注actionParameter对应config的读取) ...
            if (!this._buttonConfigs.TryGetValue(actionParameter, out var config))
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
                    var isActive = this._toggleActiveStates.TryGetValue(actionParameter, out var toggleState) && toggleState;
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
                            else
                            { PluginLog.Warning($"图标资源 '{imageName}' 未找到。"); }
                        }
                        catch (Exception ex) { PluginLog.Warning(ex, $"加载按钮图标 '{imageName}' 失败。"); }
                    }
                }

                if (!iconDrawn)
                {
                    bitmapBuilder.Clear(currentBgColor);
                    if (!String.IsNullOrEmpty(config.Title))
                    {
                        var autoTitleSize = this.GetAutomaticTitleFontSize(config.Title);
                        bitmapBuilder.DrawText(text: config.Title, fontSize: autoTitleSize, color: currentTitleColor);
                    }
                    if (!String.IsNullOrEmpty(config.Text))
                    {
                        var textSize = config.TextSize ?? 14;
                        var textX = config.TextX ?? 35;
                        var textY = config.TextY ?? 55;
                        var textWidth = config.TextWidth ?? 14;
                        var textHeight = config.TextHeight ?? 14;
                        var textColor = String.IsNullOrEmpty(config.TextColor) ? BitmapColor.White : HexToBitmapColor(config.TextColor);
                        bitmapBuilder.DrawText(text: config.Text, x: textX, y: textY, width: textWidth, height: textHeight, color: textColor, fontSize: textSize);
                    }
                }
                return bitmapBuilder.ToImage();
            }
        }

        private int GetAutomaticTitleFontSize(String title)
        {
            // ... (此辅助方法保持不变) ...
            if (String.IsNullOrEmpty(title))
            { return 23; }
            var totalLengthWithSpaces = title.Length;
            int effectiveLength;
            if (totalLengthWithSpaces <= 10)
            { effectiveLength = totalLengthWithSpaces; }
            else
            { var words = title.Split(' '); effectiveLength = words.Length > 0 ? words.Max(word => word.Length) : 0; if (effectiveLength == 0 && totalLengthWithSpaces > 0) { effectiveLength = totalLengthWithSpaces; } }
            return effectiveLength switch
            {
                1 => 38,
                2 => 33,
                3 => 31,
                4 => 26,
                5 => 26,
                >= 6 and <= 7 => 20,
                8 => 18,
                9 => 17,
                10 => 16,
                11 => 13,
                _ => 18
            };
        }

        private static BitmapColor HexToBitmapColor(string hexColor)
        {
            // ... (此辅助方法保持不变，确保返回 BitmapColor.Red) ...
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
            catch (Exception ex) { PluginLog.Error(ex, $"解析十六进制颜色失败: {hexColor}"); return BitmapColor.Red; }
        }

        public void Dispose()
        {
            OSCStateManager.Instance.StateChanged -= this.OnOSCStateChanged;
            foreach (var timer in this._triggerResetTimers.Values)
            { timer.Dispose(); }
            this._triggerResetTimers.Clear();
            GC.SuppressFinalize(this);
        }
    }
}