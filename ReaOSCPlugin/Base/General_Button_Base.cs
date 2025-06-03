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
        private readonly Logic_Manager_Base _logicManager = Logic_Manager_Base.Instance;
        private readonly Dictionary<string, Action> _modeHandlers = new Dictionary<string, Action>();
        private readonly Dictionary<string, EventHandler<OSCStateManager.StateChangedEventArgs>> _oscHandlers = new Dictionary<string, EventHandler<OSCStateManager.StateChangedEventArgs>>();

        private readonly Dictionary<string, bool> _triggerTemporaryActiveStates = new Dictionary<string, bool>();
        private readonly Dictionary<string, Timer> _triggerResetTimers = new Dictionary<string, Timer>();

        public General_Button_Base()
        {
            // 确保 Logic_Manager_Base 已初始化，这样 GetAllConfigs 才能返回正确的配置
            this._logicManager.Initialize();

            foreach (var kvp in this._logicManager.GetAllConfigs())
            {
                var actionParameter = kvp.Key; // 这个 actionParameter 已经由 Logic_Manager 处理过空格了
                var config = kvp.Value;

                if (config.ActionType != "TriggerButton" && config.ActionType != "ToggleButton" && config.ActionType != "SelectModeButton")
                {
                    continue;
                }

                this.AddParameter(actionParameter, config.DisplayName, config.GroupName, config.Description);

                if (config.ActionType == "SelectModeButton")
                {
                    this._logicManager.RegisterModeGroup(config);
                    Action handler = () => this.ActionImageChanged(actionParameter);
                    this._modeHandlers[actionParameter] = handler;
                    this._logicManager.SubscribeToModeChange(config.DisplayName, handler);
                }
                else if (!string.IsNullOrEmpty(config.ModeName)) // 受模式控制的按钮
                {
                    Action handler = () => this.ActionImageChanged(actionParameter);
                    this._modeHandlers[actionParameter] = handler;
                    this._logicManager.SubscribeToModeChange(config.ModeName, handler);

                    if (config.ActionType == "ToggleButton")
                    {
                        // 受模式控制的 ToggleButton 的 OSC 监听地址由 OnModeButtonOscStateChanged 内部逻辑确定
                        EventHandler<OSCStateManager.StateChangedEventArgs> oscHandler = (s, e) => this.OnModeButtonOscStateChanged(s, e, config, actionParameter);
                        this._oscHandlers[actionParameter] = oscHandler;
                        OSCStateManager.Instance.StateChanged += oscHandler;
                    }
                    else if (config.ActionType == "TriggerButton")
                    {
                        this.InitializeTriggerButtonTimer(actionParameter);
                    }
                }
                else // 普通按钮 (不受模式控制)
                {
                    if (config.ActionType == "ToggleButton")
                    {
                        EventHandler<OSCStateManager.StateChangedEventArgs> oscHandler = (s, e) => {
                            // 【修改】普通按钮 OSC 监听地址构建逻辑，确保空格转下划线 "_"
                            string groupNameForPath = config.GroupName.Replace(" ", "_").Trim('/');
                            string pathSuffix;
                            if (!string.IsNullOrEmpty(config.OscAddress))
                            {
                                pathSuffix = config.OscAddress.Replace(" ", "_").TrimStart('/');
                            }
                            else
                            {
                                pathSuffix = config.DisplayName.Replace(" ", "_").TrimStart('/');
                            }
                            string listenAddress = $"/{groupNameForPath}/{pathSuffix}".Replace("//", "/");

                            if (string.IsNullOrEmpty(listenAddress) || listenAddress == "/")
                            {
                                // PluginLog.Warning($"[GeneralButton] 普通ToggleButton '{actionParameter}' 的 OSC 监听地址无效。"); // 可以取消注释进行调试
                                return;
                            }

                            if (e.Address == listenAddress)
                            {
                                this._logicManager.SetToggleState(actionParameter, e.Value > 0.5f);
                                this.ActionImageChanged(actionParameter);
                            }
                        };
                        this._oscHandlers[actionParameter] = oscHandler;
                        OSCStateManager.Instance.StateChanged += oscHandler;
                    }
                    else if (config.ActionType == "TriggerButton")
                    {
                        this.InitializeTriggerButtonTimer(actionParameter);
                    }
                }
            }
        }

        private void InitializeTriggerButtonTimer(string actionParameter)
        {
            this._triggerTemporaryActiveStates[actionParameter] = false;
            var timer = new Timer(200) { AutoReset = false };
            timer.Elapsed += (s, e) => {
                if (this._triggerTemporaryActiveStates.ContainsKey(actionParameter))
                {
                    this._triggerTemporaryActiveStates[actionParameter] = false;
                    this.ActionImageChanged(actionParameter);
                }
            };
            this._triggerResetTimers[actionParameter] = timer;
        }

        // OnModeButtonOscStateChanged 方法处理受模式按钮的 OSC 状态反馈。
        // 其内部地址构建（expectedAddress）也需要确保与发送逻辑一致（如果发送逻辑变了，这里也要对应）
        // 但我们之前确认受模式按钮的 OSC 地址是精确匹配或模板替换，不涉及组名自动拼接和空格替换逻辑。
        // 如果OscAddresses列表或OscAddress模板中本身包含空格，ReaOSCPlugin.SendOSCMessage 中的 CreateOSCMessage 会处理。
        // 所以此方法通常不需要修改空格替换逻辑，除非其依赖的 config.OscAddresses 或 config.OscAddress 字段内容约定改变。
        // 当前版本中，它依赖JSON中提供的精确地址或模板，这些地址应该已经是符合OSC规范的。
        private void OnModeButtonOscStateChanged(object sender, OSCStateManager.StateChangedEventArgs e, ButtonConfig config, string actionParameter)
        {
            var modeIndex = this._logicManager.GetCurrentModeIndex(config.ModeName);
            string expectedAddress = null;

            if (modeIndex != -1)
            {
                if (config.OscAddresses?.Count > modeIndex)
                {
                    expectedAddress = config.OscAddresses[modeIndex]; // 直接使用JSON中定义的地址
                }

                if (string.IsNullOrEmpty(expectedAddress) && !string.IsNullOrEmpty(config.OscAddress))
                {
                    var currentModeString = this._logicManager.GetCurrentModeString(config.ModeName);
                    if (!string.IsNullOrEmpty(currentModeString))
                    {
                        // OscAddress 模板中的 {mode} 会被替换，模板本身应符合OSC规范
                        expectedAddress = config.OscAddress.Replace("{mode}", currentModeString);
                    }
                }
            }
            if (string.IsNullOrEmpty(expectedAddress))
            {
                return;
            }
            if (e.Address == expectedAddress)
            {
                this._logicManager.SetToggleState(actionParameter, e.Value > 0.5f);
                this.ActionImageChanged(actionParameter);
            }
        }

        protected override void RunCommand(string actionParameter)
        {
            if (this._logicManager.GetConfig(actionParameter) is not { } config)
                return;

            if (config.ActionType == "SelectModeButton")
            {
                this._logicManager.ToggleMode(config.DisplayName);
            }
            else if (!string.IsNullOrEmpty(config.ModeName)) // 受模式控制的按钮
            {
                // 受模式控制按钮的OSC地址发送逻辑，依赖于config.OscAddresses或config.OscAddress模板
                // 这些地址应该是预先定义好的，符合OSC规范的。
                // ReaOSCPlugin.SendOSCMessage 内部会处理地址字符串的最终编码。
                // 此处不需要额外的空格替换，因为地址源于JSON中的精确值或模板。
                var modeIndex = this._logicManager.GetCurrentModeIndex(config.ModeName);
                string currentModeStringForLog = this._logicManager.GetCurrentModeString(config.ModeName);

                if (modeIndex == -1)
                {
                    PluginLog.Warning($"[GeneralButton] RunCommand: 按钮 '{actionParameter}' 的模式组 '{config.ModeName}' 返回无效模式索引。");
                    if (config.ActionType == "ToggleButton")
                    { this._logicManager.SetToggleState(actionParameter, !this._logicManager.GetToggleState(actionParameter)); this.ActionImageChanged(actionParameter); }
                    else if (config.ActionType == "TriggerButton" && this._triggerResetTimers.TryGetValue(actionParameter, out var t))
                    { this._triggerTemporaryActiveStates[actionParameter] = true; this.ActionImageChanged(actionParameter); t.Stop(); t.Start(); }
                    return;
                }
                string finalOscAddress = null;
                if (config.OscAddresses?.Count > modeIndex)
                {
                    finalOscAddress = config.OscAddresses[modeIndex];
                }
                if (string.IsNullOrEmpty(finalOscAddress) && !string.IsNullOrEmpty(config.OscAddress))
                {
                    var currentModeString = this._logicManager.GetCurrentModeString(config.ModeName);
                    if (!string.IsNullOrEmpty(currentModeString))
                    {
                        finalOscAddress = config.OscAddress.Replace("{mode}", currentModeString);
                    }
                    else
                    {
                        PluginLog.Warning($"[GeneralButton] RunCommand: 按钮 '{actionParameter}' (模式控制) 无法获取模式 '{config.ModeName}' 的当前字符串。");
                    }
                }
                if (string.IsNullOrEmpty(finalOscAddress))
                {
                    PluginLog.Warning($"[GeneralButton] RunCommand: 按钮 '{actionParameter}' (模式 '{currentModeStringForLog}') 无有效OSC发送地址。不发送OSC。");
                    if (config.ActionType == "ToggleButton")
                    { var currentState = this._logicManager.GetToggleState(actionParameter); this._logicManager.SetToggleState(actionParameter, !currentState); this.ActionImageChanged(actionParameter); }
                    else if (config.ActionType == "TriggerButton" && this._triggerResetTimers.TryGetValue(actionParameter, out var timer))
                    { this._triggerTemporaryActiveStates[actionParameter] = true; this.ActionImageChanged(actionParameter); timer.Stop(); timer.Start(); }
                    return;
                }
                if (config.ActionType == "ToggleButton")
                { var currentState = this._logicManager.GetToggleState(actionParameter); ReaOSCPlugin.SendOSCMessage(finalOscAddress, currentState ? 0f : 1f); this._logicManager.SetToggleState(actionParameter, !currentState); this.ActionImageChanged(actionParameter); }
                else if (config.ActionType == "TriggerButton")
                { ReaOSCPlugin.SendOSCMessage(finalOscAddress, 1f); if (this._triggerResetTimers.TryGetValue(actionParameter, out var timer)) { this._triggerTemporaryActiveStates[actionParameter] = true; this.ActionImageChanged(actionParameter); timer.Stop(); timer.Start(); } }
            }
            else // 普通按钮 (不受模式控制)
            {
                // 【修改】普通按钮 OSC 发送地址构建逻辑，确保空格转下划线 "_"
                string groupNameForPath = config.GroupName.Replace(" ", "_").Trim('/');
                string pathSuffix;
                if (!string.IsNullOrEmpty(config.OscAddress))
                {
                    pathSuffix = config.OscAddress.Replace(" ", "_").TrimStart('/');
                }
                else
                {
                    pathSuffix = config.DisplayName.Replace(" ", "_").TrimStart('/');
                }
                string targetOscAddress = $"/{groupNameForPath}/{pathSuffix}".Replace("//", "/");


                if (string.IsNullOrEmpty(targetOscAddress) || targetOscAddress == "/")
                {
                    PluginLog.Error($"[GeneralButton] RunCommand: 按钮 '{actionParameter}' (普通) 的目标 OSC 地址无效 ({targetOscAddress})。不发送OSC。");
                    if (config.ActionType == "ToggleButton")
                    { this._logicManager.SetToggleState(actionParameter, !this._logicManager.GetToggleState(actionParameter)); this.ActionImageChanged(actionParameter); }
                    else if (config.ActionType == "TriggerButton" && this._triggerResetTimers.TryGetValue(actionParameter, out var t))
                    { this._triggerTemporaryActiveStates[actionParameter] = true; this.ActionImageChanged(actionParameter); t.Stop(); t.Start(); }
                    return;
                }

                if (config.ActionType == "ToggleButton")
                {
                    var currentState = this._logicManager.GetToggleState(actionParameter);
                    ReaOSCPlugin.SendOSCMessage(targetOscAddress, currentState ? 0f : 1f);
                    PluginLog.Info($"[GeneralButton] ToggleButton '{actionParameter}' SENT to '{targetOscAddress}' value {(currentState ? 0f : 1f)}");
                    this._logicManager.SetToggleState(actionParameter, !currentState);
                    this.ActionImageChanged(actionParameter);
                }
                else if (config.ActionType == "TriggerButton")
                {
                    ReaOSCPlugin.SendOSCMessage(targetOscAddress, 1f);
                    PluginLog.Info($"[GeneralButton] TriggerButton '{actionParameter}' SENT to '{targetOscAddress}' value 1f");
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
            if (this._logicManager.GetConfig(actionParameter) is not { } config)
                return base.GetCommandImage(actionParameter, imageSize);

            using (var bitmapBuilder = new BitmapBuilder(imageSize))
            {
                if (config.ActionType == "SelectModeButton")
                {
                    var currentModeString = this._logicManager.GetCurrentModeString(config.DisplayName);
                    bool isModeConsideredActive = false;
                    if (config.Modes != null && config.Modes.Count > 0)
                    {
                        int currentModeIndexInList = config.Modes.IndexOf(currentModeString);
                        isModeConsideredActive = currentModeIndexInList > 0;
                    }
                    var bgColor = isModeConsideredActive && !String.IsNullOrEmpty(config.ActiveColor) ? HexToBitmapColor(config.ActiveColor) : BitmapColor.Black;
                    var titleColor = isModeConsideredActive && !String.IsNullOrEmpty(config.TitleColor)
                        ? HexToBitmapColor(config.TitleColor)
                        : (String.IsNullOrEmpty(config.DeactiveTextColor) ? BitmapColor.White : HexToBitmapColor(config.DeactiveTextColor));
                    bitmapBuilder.Clear(bgColor);
                    bitmapBuilder.DrawText(currentModeString ?? config.Modes?.FirstOrDefault() ?? config.DisplayName, color: titleColor, fontSize: 23);
                }
                else
                {
                    BitmapColor currentBgColor = BitmapColor.Black;
                    BitmapColor currentTitleColorFromConfig = String.IsNullOrEmpty(config.TitleColor) ? BitmapColor.White : HexToBitmapColor(config.TitleColor);
                    BitmapColor finalTitleColor = currentTitleColorFromConfig;
                    bool iconDrawn = false;

                    if (config.ActionType == "TriggerButton")
                    {
                        var isTempActive = this._triggerTemporaryActiveStates.TryGetValue(actionParameter, out var tempState) && tempState;
                        currentBgColor = isTempActive && !String.IsNullOrEmpty(config.ActiveColor) ? HexToBitmapColor(config.ActiveColor) : BitmapColor.Black;
                        finalTitleColor = currentTitleColorFromConfig;
                    }
                    else if (config.ActionType == "ToggleButton")
                    {
                        var isActive = this._logicManager.GetToggleState(actionParameter);
                        currentBgColor = isActive && !String.IsNullOrEmpty(config.ActiveColor) ? HexToBitmapColor(config.ActiveColor) : BitmapColor.Black;
                        finalTitleColor = isActive
                            ? (String.IsNullOrEmpty(config.ActiveTextColor) ? BitmapColor.White : HexToBitmapColor(config.ActiveTextColor))
                            : (String.IsNullOrEmpty(config.DeactiveTextColor) ? BitmapColor.White : HexToBitmapColor(config.DeactiveTextColor));

                        var imageName = String.IsNullOrEmpty(config.ButtonImage) ? $"{config.DisplayName.Replace(" ", "_")}.png" : config.ButtonImage; // 空格转下划线查找图片
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
                                    bitmapBuilder.DrawText(text: config.DisplayName, x: 0, y: bitmapBuilder.Height - 23, width: bitmapBuilder.Width, height: 20, fontSize: 12, color: finalTitleColor);
                                    iconDrawn = true;
                                }
                            }
                            catch (Exception ex) { PluginLog.Warning(ex, $"加载按钮图标 '{imageName}' 失败 for action '{actionParameter}'."); }
                        }
                    }

                    if (!iconDrawn)
                    {
                        bitmapBuilder.Clear(currentBgColor);
                        var titleToDraw = config.Title ?? config.DisplayName;

                        if (!string.IsNullOrEmpty(config.ModeName))
                        {
                            var modeIndex = this._logicManager.GetCurrentModeIndex(config.ModeName);
                            if (config.Titles?.Count > modeIndex && modeIndex != -1 && !string.IsNullOrEmpty(config.Titles[modeIndex]))
                            { titleToDraw = config.Titles[modeIndex]; }
                        }

                        if (!String.IsNullOrEmpty(titleToDraw))
                        {
                            bitmapBuilder.DrawText(text: titleToDraw, fontSize: this.GetAutomaticTitleFontSize(titleToDraw), color: finalTitleColor);
                        }

                        if (!String.IsNullOrEmpty(config.Text))
                        {
                            bitmapBuilder.DrawText(
                                text: config.Text,
                                x: config.TextX ?? 50,
                                y: config.TextY ?? 55,
                                width: config.TextWidth ?? 14,
                                height: config.TextHeight ?? 14,
                                color: String.IsNullOrEmpty(config.TextColor) ? BitmapColor.White : HexToBitmapColor(config.TextColor),
                                fontSize: config.TextSize ?? 14
                            );
                        }
                    }

                    if (!string.IsNullOrEmpty(config.ModeName) && !iconDrawn)
                    {
                        var currentModeForDisplay = this._logicManager.GetCurrentModeString(config.ModeName);
                        if (!string.IsNullOrEmpty(currentModeForDisplay))
                        { bitmapBuilder.DrawText(currentModeForDisplay, x: 50, y: 55, width: 14, height: 14, fontSize: 14, color: new BitmapColor(136, 226, 255)); }
                    }
                }
                return bitmapBuilder.ToImage();
            }
        }

        public void Dispose()
        {
            foreach (var kvp in this._logicManager.GetAllConfigs())
            {
                var actionParameter = kvp.Key;
                var config = kvp.Value;
                if (this._modeHandlers.TryGetValue(actionParameter, out var handler))
                {
                    var modeName = config.ActionType == "SelectModeButton" ? config.DisplayName : config.ModeName;
                    if (!string.IsNullOrEmpty(modeName))
                    {
                        this._logicManager.UnsubscribeFromModeChange(modeName, handler);
                    }
                }
                if (this._oscHandlers.TryGetValue(actionParameter, out var oscHandler))
                {
                    OSCStateManager.Instance.StateChanged -= oscHandler;
                }
            }
            this._modeHandlers.Clear();
            this._oscHandlers.Clear();

            foreach (var timer in this._triggerResetTimers.Values)
            {
                timer.Stop();
                timer.Elapsed -= null;
                timer.Dispose();
            }
            this._triggerResetTimers.Clear();
            this._triggerTemporaryActiveStates.Clear();
        }

        #region UI辅助方法 
        private int GetAutomaticTitleFontSize(String title) { if (String.IsNullOrEmpty(title)) return 23; var totalLengthWithSpaces = title.Length; int effectiveLength; if (totalLengthWithSpaces <= 8) { effectiveLength = totalLengthWithSpaces; } else { var words = title.Split(' '); effectiveLength = words.Length > 0 ? words.Max(word => word.Length) : 0; if (effectiveLength == 0 && totalLengthWithSpaces > 0) { effectiveLength = totalLengthWithSpaces; } } return effectiveLength switch { 1 => 38, 2 => 33, 3 => 31, 4 => 26, 5 => 23, 6 => 22, 7 => 20, 8 => 18, 9 => 17, 10 => 16, 11 => 13, _ => 18 }; }
        private static BitmapColor HexToBitmapColor(string hexColor) { if (String.IsNullOrEmpty(hexColor) || !hexColor.StartsWith("#")) return BitmapColor.White; var hex = hexColor.Substring(1); if (hex.Length != 6) return BitmapColor.White; try { var r = (byte)Int32.Parse(hex.Substring(0, 2), NumberStyles.HexNumber); var g = (byte)Int32.Parse(hex.Substring(2, 2), NumberStyles.HexNumber); var b = (byte)Int32.Parse(hex.Substring(4, 2), NumberStyles.HexNumber); return new BitmapColor(r, g, b); } catch { return BitmapColor.Red; } }
        #endregion
    }
}