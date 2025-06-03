// 文件名: Base/General_Button_Base.cs
// 【无需额外 using】

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
            foreach (var kvp in this._logicManager.GetAllConfigs())
            {
                var actionParameter = kvp.Key;
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
                            // 【修改】普通按钮 OSC 地址构建逻辑 (监听)
                            string groupNameForPath = config.GroupName.Replace(" ", "/").Trim('/');
                            string pathSuffix;
                            if (!string.IsNullOrEmpty(config.OscAddress))
                            {
                                pathSuffix = config.OscAddress.Replace(" ", "/").TrimStart('/');
                            }
                            else
                            {
                                pathSuffix = config.DisplayName.Replace(" ", "/").TrimStart('/');
                            }
                            // 确保groupNameForPath和pathSuffix之间只有一个斜杠，并且整体以斜杠开头
                            string listenAddress = $"/{groupNameForPath}/{pathSuffix}";
                            listenAddress = listenAddress.Replace("//", "/");


                            if (string.IsNullOrEmpty(listenAddress) || listenAddress == "/")
                            {
                                PluginLog.Warning($"[GeneralButton] 普通ToggleButton '{actionParameter}' 的 OSC 监听地址无效。");
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

        private void InitializeTriggerButtonTimer(string actionParameter) // 【无改动】
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

        private void OnModeButtonOscStateChanged(object sender, OSCStateManager.StateChangedEventArgs e, ButtonConfig config, string actionParameter) // 【无改动, 已符合预期】
        {
            var modeIndex = this._logicManager.GetCurrentModeIndex(config.ModeName);
            string expectedAddress = null;

            if (modeIndex != -1)
            {
                if (config.OscAddresses?.Count > modeIndex)
                {
                    expectedAddress = config.OscAddresses[modeIndex];
                }

                if (string.IsNullOrEmpty(expectedAddress) && !string.IsNullOrEmpty(config.OscAddress))
                {
                    var currentModeString = this._logicManager.GetCurrentModeString(config.ModeName);
                    if (!string.IsNullOrEmpty(currentModeString))
                    {
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

            if (config.ActionType == "SelectModeButton") // 【无改动】
            {
                this._logicManager.ToggleMode(config.DisplayName);
            }
            else if (!string.IsNullOrEmpty(config.ModeName)) // 受模式控制的按钮 【无改动, 已符合预期】
            {
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
                // 【修改】普通按钮 OSC 地址构建逻辑 (发送)
                string groupNameForPath = config.GroupName.Replace(" ", "/").Trim('/');
                string pathSuffix;
                if (!string.IsNullOrEmpty(config.OscAddress))
                {
                    pathSuffix = config.OscAddress.Replace(" ", "/").TrimStart('/');
                }
                else
                {
                    pathSuffix = config.DisplayName.Replace(" ", "/").TrimStart('/');
                }
                // 确保groupNameForPath和pathSuffix之间只有一个斜杠，并且整体以斜杠开头
                string targetOscAddress = $"/{groupNameForPath}/{pathSuffix}";
                targetOscAddress = targetOscAddress.Replace("//", "/");


                if (string.IsNullOrEmpty(targetOscAddress) || targetOscAddress == "/")
                {
                    PluginLog.Error($"[GeneralButton] RunCommand: 按钮 '{actionParameter}' (普通) 的目标 OSC 地址无效。不发送OSC。");
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
                    this._logicManager.SetToggleState(actionParameter, !currentState);
                    this.ActionImageChanged(actionParameter);
                }
                else if (config.ActionType == "TriggerButton")
                {
                    ReaOSCPlugin.SendOSCMessage(targetOscAddress, 1f);
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

        protected override BitmapImage GetCommandImage(string actionParameter, PluginImageSize imageSize) // 【无改动】
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
                        { bitmapBuilder.DrawText(text: titleToDraw, fontSize: this.GetAutomaticTitleFontSize(titleToDraw), color: finalTitleColor); }
                        if (!String.IsNullOrEmpty(config.Text))
                        { bitmapBuilder.DrawText(text: config.Text, x: config.TextX ?? 50, y: config.TextY ?? 55, width: config.TextWidth ?? 14, height: config.TextHeight ?? 14, color: String.IsNullOrEmpty(config.TextColor) ? BitmapColor.White : HexToBitmapColor(config.TextColor), fontSize: config.TextSize ?? 14); }
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

        public void Dispose() // 【无改动】
        {
            foreach (var kvp in this._logicManager.GetAllConfigs())
            {
                var actionParameter = kvp.Key;
                var config = kvp.Value;
                if (this._modeHandlers.TryGetValue(actionParameter, out var handler))
                { var modeName = config.ActionType == "SelectModeButton" ? config.DisplayName : config.ModeName; if (!string.IsNullOrEmpty(modeName)) { this._logicManager.UnsubscribeFromModeChange(modeName, handler); } }
                if (this._oscHandlers.TryGetValue(actionParameter, out var oscHandler))
                { OSCStateManager.Instance.StateChanged -= oscHandler; }
            }
            this._modeHandlers.Clear();
            this._oscHandlers.Clear();
            foreach (var timer in this._triggerResetTimers.Values)
            { timer.Stop(); timer.Elapsed -= null; timer.Dispose(); }
            this._triggerResetTimers.Clear();
            this._triggerTemporaryActiveStates.Clear();
        }

        #region UI辅助方法 // 【无改动】
        private int GetAutomaticTitleFontSize(String title) { if (String.IsNullOrEmpty(title)) return 23; var totalLengthWithSpaces = title.Length; int effectiveLength; if (totalLengthWithSpaces <= 8) { effectiveLength = totalLengthWithSpaces; } else { var words = title.Split(' '); effectiveLength = words.Length > 0 ? words.Max(word => word.Length) : 0; if (effectiveLength == 0 && totalLengthWithSpaces > 0) { effectiveLength = totalLengthWithSpaces; } } if (effectiveLength <= 1) return 38; if (effectiveLength == 2) return 33; if (effectiveLength == 3) return 31; if (effectiveLength == 4) return 26; if (effectiveLength == 5) return 23; if (effectiveLength == 6) return 22; if (effectiveLength == 7) return 20; if (effectiveLength == 8) return 18; if (effectiveLength == 9) return 17; if (effectiveLength == 10) return 16; if (effectiveLength == 11) return 13; return 18; }
        private static BitmapColor HexToBitmapColor(string hexColor) { if (String.IsNullOrEmpty(hexColor) || !hexColor.StartsWith("#")) return BitmapColor.White; var hex = hexColor.Substring(1); if (hex.Length != 6) return BitmapColor.White; try { var r = (byte)Int32.Parse(hex.Substring(0, 2), NumberStyles.HexNumber); var g = (byte)Int32.Parse(hex.Substring(2, 2), NumberStyles.HexNumber); var b = (byte)Int32.Parse(hex.Substring(4, 2), NumberStyles.HexNumber); return new BitmapColor(r, g, b); } catch { return BitmapColor.Red; } }
        #endregion
    }
}