// 文件名: Base/General_Button_Base.cs
// 【基于您上传的文件进行修改，仅移除SVG加载，保留PNG加载和原有绘制逻辑】
namespace Loupedeck.ReaOSCPlugin.Base
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO; // 确保 System.IO 的 using 存在，因为用到了 Path.GetFileNameWithoutExtension
    using System.Linq;
    using System.Timers;
    // System.Drawing 相关 using 不是必需的，因为 BitmapBuilder 使用 Loupedeck.BitmapColor

    public class General_Button_Base : PluginDynamicCommand, IDisposable
    {
        private readonly Logic_Manager_Base _logicManager = Logic_Manager_Base.Instance;
        private readonly Dictionary<string, Action> _modeHandlers = new Dictionary<string, Action>();
        private readonly Dictionary<string, EventHandler<OSCStateManager.StateChangedEventArgs>> _oscHandlers = new Dictionary<string, EventHandler<OSCStateManager.StateChangedEventArgs>>();

        private readonly Dictionary<string, bool> _triggerTemporaryActiveStates = new Dictionary<string, bool>();
        private readonly Dictionary<string, Timer> _triggerResetTimers = new Dictionary<string, Timer>();

        public General_Button_Base()
        {
            this._logicManager.Initialize();

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
                else if (!string.IsNullOrEmpty(config.ModeName))
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
                else
                {
                    if (config.ActionType == "ToggleButton")
                    {
                        EventHandler<OSCStateManager.StateChangedEventArgs> oscHandler = (s, e) => {
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
                                return;

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

        private void OnModeButtonOscStateChanged(object sender, OSCStateManager.StateChangedEventArgs e, ButtonConfig config, string actionParameter)
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
                return;
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
            else if (!string.IsNullOrEmpty(config.ModeName))
            {
                var modeIndex = this._logicManager.GetCurrentModeIndex(config.ModeName);
                string currentModeStringForLog = this._logicManager.GetCurrentModeString(config.ModeName);
                if (modeIndex == -1)
                {
                    PluginLog.Warning($"[RunCommand] Button '{actionParameter}' ModeGroup '{config.ModeName}' invalid index.");
                    if (config.ActionType == "ToggleButton")
                    { this._logicManager.SetToggleState(actionParameter, !this._logicManager.GetToggleState(actionParameter)); this.ActionImageChanged(actionParameter); }
                    else if (config.ActionType == "TriggerButton" && this._triggerResetTimers.TryGetValue(actionParameter, out var t))
                    { this._triggerTemporaryActiveStates[actionParameter] = true; this.ActionImageChanged(actionParameter); t.Stop(); t.Start(); }
                    return;
                }
                string finalOscAddress = null;
                if (config.OscAddresses?.Count > modeIndex)
                { finalOscAddress = config.OscAddresses[modeIndex]; }
                if (string.IsNullOrEmpty(finalOscAddress) && !string.IsNullOrEmpty(config.OscAddress))
                {
                    var currentModeString = this._logicManager.GetCurrentModeString(config.ModeName);
                    if (!string.IsNullOrEmpty(currentModeString))
                    { finalOscAddress = config.OscAddress.Replace("{mode}", currentModeString); }
                }
                if (string.IsNullOrEmpty(finalOscAddress))
                {
                    PluginLog.Warning($"[RunCommand] Button '{actionParameter}' (Mode '{currentModeStringForLog}') no valid OSC address.");
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
            else
            {
                string groupNameForPath = config.GroupName.Replace(" ", "_").Trim('/');
                string pathSuffix = (!string.IsNullOrEmpty(config.OscAddress)) ? config.OscAddress.Replace(" ", "_").TrimStart('/') : config.DisplayName.Replace(" ", "_").TrimStart('/');
                string targetOscAddress = $"/{groupNameForPath}/{pathSuffix}".Replace("//", "/");

                if (string.IsNullOrEmpty(targetOscAddress) || targetOscAddress == "/")
                {
                    PluginLog.Error($"[RunCommand] Button '{actionParameter}' (Normal) invalid target OSC address '{targetOscAddress}'.");
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
                }
                else if (config.ActionType == "TriggerButton")
                {
                    ReaOSCPlugin.SendOSCMessage(targetOscAddress, 1f);
                    if (this._triggerResetTimers.TryGetValue(actionParameter, out var timer))
                    {
                        this._triggerTemporaryActiveStates[actionParameter] = true;
                        timer.Stop();
                        timer.Start();
                    }
                }
                this.ActionImageChanged(actionParameter);
            }
        }

        protected override BitmapImage GetCommandImage(string actionParameter, PluginImageSize imageSize)
        {
            if (this._logicManager.GetConfig(actionParameter) is not { } config)
            {
                // 【基于您上传的文件】返回基类图像或一个更明确的错误图像
                PluginLog.Warning($"[GetCommandImage-PNGOnly] Config not found for actionParameter: {actionParameter}. Returning base.GetCommandImage.");
                return base.GetCommandImage(actionParameter, imageSize);
            }

            try
            {
                // 【最终修复方案】 整合"精准匹配"逻辑与完整的UI绘制逻辑

                // 1. 优先使用在JSON中明确指定的 ButtonImage
                string imagePathToLoad = !string.IsNullOrEmpty(config.ButtonImage) ? config.ButtonImage : null;

                // 2. 如果没有指定，则根据 DisplayName 推断，但必须进行严格匹配检查
                if (string.IsNullOrEmpty(imagePathToLoad))
                {
                    var actionNameFromParam = actionParameter.Split('/').LastOrDefault()?.Replace("_", " ");
                    if (!string.IsNullOrEmpty(actionNameFromParam) && actionNameFromParam.Equals(config.DisplayName, StringComparison.OrdinalIgnoreCase))
                    {
                        imagePathToLoad = $"{config.DisplayName.Replace(" ", "_")}.png";
                    }
                }

                BitmapImage icon = null;
                if (!string.IsNullOrEmpty(imagePathToLoad))
                {
                    try
                    {
                        icon = PluginResources.ReadImage(imagePathToLoad);
                    }
                    catch (Exception ex)
                    {
                        PluginLog.Error(ex, $"[GetCommandImage] 加载图标 '{imagePathToLoad}' 失败 for action '{actionParameter}'.");
                        icon = null;
                    }
                }

                // --- 恢复完整的UI绘制逻辑 ---
                using (var bitmapBuilder = new BitmapBuilder(imageSize))
                {
                    // 颜色和状态逻辑
                    BitmapColor currentBgColor = BitmapColor.Black;
                    BitmapColor finalTitleColor = String.IsNullOrEmpty(config.TitleColor) ? BitmapColor.White : HexToBitmapColor(config.TitleColor);
                    bool isIconDrawn = false;

                    if (config.ActionType == "SelectModeButton")
                    {
                        var currentModeString = this._logicManager.GetCurrentModeString(config.DisplayName);
                        bool isModeConsideredActive = config.Modes?.IndexOf(currentModeString) > 0;

                        currentBgColor = isModeConsideredActive && !String.IsNullOrEmpty(config.ActiveColor) ? HexToBitmapColor(config.ActiveColor) : BitmapColor.Black;
                        finalTitleColor = isModeConsideredActive && !String.IsNullOrEmpty(config.TitleColor)
                            ? HexToBitmapColor(config.TitleColor)
                            : (String.IsNullOrEmpty(config.DeactiveTextColor) ? BitmapColor.White : HexToBitmapColor(config.DeactiveTextColor));
                        bitmapBuilder.Clear(currentBgColor);
                        bitmapBuilder.DrawText(currentModeString ?? config.Modes?.FirstOrDefault() ?? config.DisplayName, color: finalTitleColor, fontSize: 23);
                        return bitmapBuilder.ToImage(); 
                    }
                    else if (config.ActionType == "TriggerButton")
                    {
                        var isTempActive = this._triggerTemporaryActiveStates.TryGetValue(actionParameter, out var tempState) && tempState;
                        currentBgColor = isTempActive && !String.IsNullOrEmpty(config.ActiveColor) ? HexToBitmapColor(config.ActiveColor) : BitmapColor.Black;
                    }
                    else if (config.ActionType == "ToggleButton")
                    {
                        var isActive = this._logicManager.GetToggleState(actionParameter);
                        currentBgColor = isActive && !String.IsNullOrEmpty(config.ActiveColor) ? HexToBitmapColor(config.ActiveColor) : BitmapColor.Black;
                        finalTitleColor = isActive
                            ? (String.IsNullOrEmpty(config.ActiveTextColor) ? BitmapColor.White : HexToBitmapColor(config.ActiveTextColor))
                            : (String.IsNullOrEmpty(config.DeactiveTextColor) ? BitmapColor.White : HexToBitmapColor(config.DeactiveTextColor));
                    }

                    bitmapBuilder.Clear(currentBgColor);

                    if (icon != null)
                    {
                        int iconHeight = 46;
                        int iconWidth = (icon.Height > 0) ? (icon.Width * iconHeight / icon.Height) : icon.Width;
                        iconWidth = Math.Max(1, iconWidth);
                        iconHeight = (icon.Width > 0 && iconWidth == icon.Width) ? icon.Height : iconHeight;

                        int iconX = (bitmapBuilder.Width - iconWidth) / 2;
                        int iconY = 8;
                        bitmapBuilder.DrawImage(icon, iconX, iconY, iconWidth, iconHeight);
                        
                        bitmapBuilder.DrawText(text: config.DisplayName, x: 0, y: bitmapBuilder.Height - 23, width: bitmapBuilder.Width, height: 20, fontSize: 12, color: finalTitleColor);
                        
                        isIconDrawn = true;
                    }

                    if (!isIconDrawn)
                    {
                        var titleToDraw = config.Title ?? config.DisplayName;

                        if (!string.IsNullOrEmpty(config.ModeName))
                        {
                            var modeIndex = this._logicManager.GetCurrentModeIndex(config.ModeName);
                            if (config.Titles?.Count > modeIndex && modeIndex != -1 && !string.IsNullOrEmpty(config.Titles[modeIndex]))
                            { titleToDraw = config.Titles[modeIndex]; }
                        }

                        if (!String.IsNullOrEmpty(titleToDraw) && icon == null)
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

                    if (!string.IsNullOrEmpty(config.ModeName) && icon == null)
                    {
                        var currentModeForDisplay = this._logicManager.GetCurrentModeString(config.ModeName);
                        if (!string.IsNullOrEmpty(currentModeForDisplay))
                        { bitmapBuilder.DrawText(currentModeForDisplay, x: 50, y: 55, width: 14, height: 14, fontSize: 14, color: new BitmapColor(136, 226, 255)); }
                    }

                    return bitmapBuilder.ToImage();
                }
            }
            catch (Exception ex)
            {
                PluginLog.Error(ex, $"[GetCommandImage] 未处理的异常 for action '{actionParameter}'.");
                using (var errorBitmap = new BitmapBuilder(imageSize))
                {
                    errorBitmap.Clear(BitmapColor.Red);
                    errorBitmap.DrawText("ERR!");
                    return errorBitmap.ToImage();
                }
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
                // 正确移除事件处理器比较复杂，简单 Dispose 即可
                timer.Dispose();
            }
            this._triggerResetTimers.Clear();
            this._triggerTemporaryActiveStates.Clear();
            PluginLog.Info("[GeneralButtonBase] Disposed.");
        }

        #region UI辅助方法 
        private int GetAutomaticTitleFontSize(String title) { if (String.IsNullOrEmpty(title)) return 23; var totalLengthWithSpaces = title.Length; int effectiveLength; if (totalLengthWithSpaces <= 8) { effectiveLength = totalLengthWithSpaces; } else { var words = title.Split(' '); effectiveLength = words.Length > 0 ? words.Max(word => word.Length) : 0; if (effectiveLength == 0 && totalLengthWithSpaces > 0) { effectiveLength = totalLengthWithSpaces; } } return effectiveLength switch { 1 => 38, 2 => 33, 3 => 31, 4 => 26, 5 => 23, 6 => 22, 7 => 20, 8 => 18, 9 => 17, 10 => 16, 11 => 13, _ => 18 }; }
        // 【修正】HexToBitmapColor 支持 8 位 Hex (RRGGBBAA)
        private static BitmapColor HexToBitmapColor(string hexColor)
        {
            if (String.IsNullOrEmpty(hexColor) || !hexColor.StartsWith("#"))
                return BitmapColor.White;
            var hex = hexColor.Substring(1);
            if (hex.Length != 6 && hex.Length != 8)
                return BitmapColor.White;
            try
            {
                var r = (byte)Int32.Parse(hex.Substring(0, 2), NumberStyles.HexNumber);
                var g = (byte)Int32.Parse(hex.Substring(2, 2), NumberStyles.HexNumber);
                var b = (byte)Int32.Parse(hex.Substring(4, 2), NumberStyles.HexNumber);
                if (hex.Length == 8) // 支持 alpha 通道
                {
                    var a = (byte)Int32.Parse(hex.Substring(6, 2), NumberStyles.HexNumber);
                    return new BitmapColor(r, g, b, a);
                }
                return new BitmapColor(r, g, b);
            }
            catch { return BitmapColor.Red; } // 错误时返回红色，易于识别
        }
        #endregion
    }
}