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

        public General_Dial_Base() : base(hasReset: true)
        {
            PluginLog.Info("General_Dial_Base 构造函数开始执行。");
            string jsonContent = ReadEmbeddedJson("Loupedeck.ReaOSCPlugin.General.General_List.json");
            if (String.IsNullOrEmpty(jsonContent))
            { PluginLog.Error("无法加载 General_Dial_Base 的配置。"); return; }

            var groupedConfigs = JsonConvert.DeserializeObject<Dictionary<string, List<ButtonConfig>>>(jsonContent);
            if (groupedConfigs == null)
            { PluginLog.Error("反序列化 General_List.json (旋钮部分) 失败。"); return; }

            foreach (var group in groupedConfigs)
            {
                var groupNameFromJson = group.Key;
                // 【修改】组名中的空格也替换为斜杠
                string groupNameForPath = groupNameFromJson.Replace(" FX", "").Replace(" ", "/");

                foreach (var config in group.Value)
                {
                    if (config.ActionType != "TickDial" && config.ActionType != "ToggleDial")
                        continue;

                    config.GroupName = groupNameFromJson;

                    string actionParameter;
                    string baseOscNameForActionParam;

                    if (config.ActionType == "TickDial")
                    {
                        // 【修改】DisplayName中的空格替换为斜杠
                        baseOscNameForActionParam = config.DisplayName.Replace(" ", "/");
                        actionParameter = $"/{groupNameForPath}/{baseOscNameForActionParam}/TickDialAction";
                    }
                    else // ToggleDial
                    {
                        // 【修改】DisplayName和OscAddress中的空格替换为斜杠
                        baseOscNameForActionParam = String.IsNullOrEmpty(config.OscAddress)
                            ? config.DisplayName.Replace(" ", "/")
                            : config.OscAddress.Replace("/", "_").Replace(" ", "/"); // 如果OscAddress是部分路径，也处理空格
                        actionParameter = $"/{groupNameForPath}/{baseOscNameForActionParam}".Replace("//", "/");
                    }

                    this._dialConfigs[actionParameter] = config;
                    this.AddParameter(actionParameter, config.DisplayName, config.GroupName, $"旋钮调整: {config.DisplayName}");

                    if (config.ActionType == "ToggleDial")
                    {
                        // 【修改】构建fullOscAddress时，空格替换为斜杠
                        string baseOscName = String.IsNullOrEmpty(config.OscAddress) ? config.DisplayName : config.OscAddress;
                        var fullOscAddress = $"/{groupNameForPath}/{baseOscName.Replace(" ", "/")}".Replace("//", "/");
                        this._toggleActiveStates[actionParameter] = OSCStateManager.Instance.GetState(fullOscAddress) > 0.5f;
                    }
                    else if (config.ActionType == "TickDial")
                    {
                        this._lastEventTimes[actionParameter] = DateTime.MinValue;
                    }
                    PluginLog.Verbose($"General_Dial_Base: 已添加参数: {actionParameter} (DisplayName: {config.DisplayName}, Group: {config.GroupName}, Type: {config.ActionType})");
                }
            }
            OSCStateManager.Instance.StateChanged += this.OnOSCStateChanged;
            PluginLog.Info("General_Dial_Base 构造函数执行完毕。");
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
            foreach (var entry in this._dialConfigs)
            {
                var actionParameter = entry.Key;
                var config = entry.Value;
                if (config.ActionType == "ToggleDial")
                {
                    // 【修改】构建比较用的 fullOscAddress 时，空格替换为斜杠
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

        protected override void ApplyAdjustment(string actionParameter, int ticks)
        {
            if (ticks == 0 || !this._dialConfigs.TryGetValue(actionParameter, out var config))
                return;

            // 【修改】组名中的空格替换为斜杠
            string groupNameForPath = config.GroupName.Replace(" FX", "").Replace(" ", "/");

            if (config.ActionType == "TickDial")
            {
                // 【修改】DisplayName中的空格替换为斜杠
                string baseOscName = config.DisplayName.Replace(" ", "/");
                var effectiveIncreasePathPart = String.IsNullOrEmpty(config.IncreaseOSCAddress) ? "Down" : config.IncreaseOSCAddress;
                var effectiveDecreasePathPart = String.IsNullOrEmpty(config.DecreaseOSCAddress) ? "Up" : config.DecreaseOSCAddress;

                // 【修改】路径部分中的空格替换为斜杠
                var increaseAddr = $"/{groupNameForPath}/{baseOscName}/{effectiveIncreasePathPart.Replace(" ", "/")}".Replace("//", "/");
                var decreaseAddr = $"/{groupNameForPath}/{baseOscName}/{effectiveDecreasePathPart.Replace(" ", "/")}".Replace("//", "/");
                var acceleration = config.AccelerationFactor ?? 1.0f;

                // ... (加速逻辑不变) ...
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
                { ReaOSCPlugin.SendOSCMessage(ticks > 0 ? increaseAddr : decreaseAddr, 1f); }
                this.AdjustmentValueChanged(actionParameter);
            }
            else if (config.ActionType == "ToggleDial")
            {
                // 【修改】DisplayName和OscAddress中的空格替换为斜杠
                string baseOscName = String.IsNullOrEmpty(config.OscAddress) ? config.DisplayName : config.OscAddress;
                var oscAddr = $"/{groupNameForPath}/{baseOscName.Replace(" ", "/")}".Replace("//", "/");
                var isActive = this._toggleActiveStates.TryGetValue(actionParameter, out var state) && state;
                float newValue = -1f;
                if (ticks > 0 && !isActive)
                    newValue = 1f;
                else if (ticks < 0 && isActive)
                    newValue = 0f;
                if (newValue >= 0f)
                    ReaOSCPlugin.SendOSCMessage(oscAddr, newValue);
            }
        }

        protected override void RunCommand(string actionParameter)
        {
            if (!this._dialConfigs.TryGetValue(actionParameter, out var config))
                return;

            string groupNameForPath = config.GroupName.Replace(" FX", "").Replace(" ", "/");
            // 【修改】DisplayName中的空格替换为斜杠
            string baseOscNameForReset = config.DisplayName.Replace(" ", "/");

            // 默认Reset规则
            string effectiveResetNamePart = String.IsNullOrEmpty(config.ResetOscAddress) ? "Reset" : config.ResetOscAddress;

            if (!string.IsNullOrEmpty(effectiveResetNamePart))
            {
                // 【修改】路径部分中的空格替换为斜杠
                var resetAddr = $"/{groupNameForPath}/{baseOscNameForReset}/{effectiveResetNamePart.Replace(" ", "/")}".Replace("//", "/");
                PluginLog.Info($"RunCommand (Dial): 发送Reset OSC消息到 '{resetAddr}' (actionParameter: {actionParameter}, DisplayName: {config.DisplayName})");
                ReaOSCPlugin.SendOSCMessage(resetAddr, 1f);
            }
            else
            {
                PluginLog.Verbose($"RunCommand (Dial): actionParameter '{actionParameter}' (DisplayName: {config.DisplayName}) 没有配置有效的ResetOscAddress，也未使用默认Reset。");
            }
        }

        protected override string GetAdjustmentValue(string actionParameter)
        {
            return null;
        }

        protected override BitmapImage GetCommandImage(string actionParameter, PluginImageSize imageSize)
        {
            // ... (此方法内部逻辑与上一版一致，只关注actionParameter对应config的读取) ...
            if (!this._dialConfigs.TryGetValue(actionParameter, out var config))
                return base.GetCommandImage(actionParameter, imageSize);

            using (var bitmapBuilder = new BitmapBuilder(imageSize))
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
                return bitmapBuilder.ToImage();
            }
        }

        // --- 辅助方法 ---
        private int GetAutomaticDialTitleFontSize(String title)
        {
            // ... (此辅助方法保持不变) ...
            if (String.IsNullOrEmpty(title))
            { return 16; }
            var totalLengthWithSpaces = title.Length;
            int effectiveLength;
            if (totalLengthWithSpaces <= 10)
            { effectiveLength = totalLengthWithSpaces; }
            else
            { var words = title.Split(' '); effectiveLength = words.Length > 0 ? words.Max(word => word.Length) : 0; if (effectiveLength == 0 && totalLengthWithSpaces > 0) { effectiveLength = totalLengthWithSpaces; } }
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
            GC.SuppressFinalize(this);
        }
    }
}