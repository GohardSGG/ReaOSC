// 文件名: Base/General_Button_Base.cs
namespace Loupedeck.ReaOSCPlugin.Base
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq; // 【添加】为了 GetAutomaticTitleFontSize 中的 Max()
    using System.Timers; // 【添加】为了 TriggerButton 的瞬时高亮

    // 一个统一的基类，可以处理普通按钮、模式选择按钮和受模式控制的按钮
    public class General_Button_Base : PluginDynamicCommand, IDisposable
    {
        private readonly Logic_Manager_Base _logicManager = Logic_Manager_Base.Instance;
        private readonly Dictionary<string, Action> _modeHandlers = new Dictionary<string, Action>();
        private readonly Dictionary<string, EventHandler<OSCStateManager.StateChangedEventArgs>> _oscHandlers = new Dictionary<string, EventHandler<OSCStateManager.StateChangedEventArgs>>();

        // 用于TriggerButton的瞬时高亮状态和定时器 (来自旧版逻辑)
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
                    else if (config.ActionType == "TriggerButton") // 受模式控制的TriggerButton也应用瞬时高亮
                    {
                        this.InitializeTriggerButtonTimer(actionParameter);
                    }
                }
                else // 普通按钮 (不受模式控制)
                {
                    if (config.ActionType == "ToggleButton")
                    {
                        EventHandler<OSCStateManager.StateChangedEventArgs> oscHandler = (s, e) => {
                            if (e.Address == config.OscAddress)
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

        // 辅助方法：初始化TriggerButton的瞬时高亮定时器
        private void InitializeTriggerButtonTimer(string actionParameter)
        {
            this._triggerTemporaryActiveStates[actionParameter] = false;
            var timer = new Timer(200) { AutoReset = false }; // 瞬时高亮持续时间，旧版为500ms，可调整
            timer.Elapsed += (s, e) => {
                if (this._triggerTemporaryActiveStates.ContainsKey(actionParameter)) // 确保键仍然存在
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
            string expectedAddress;

            if (config.OscAddresses?.Count > modeIndex && modeIndex != -1)
            {
                expectedAddress = config.OscAddresses[modeIndex];
            }
            else if (!string.IsNullOrEmpty(config.OscAddress))
            {
                var currentModeString = this._logicManager.GetCurrentModeString(config.ModeName);
                if (string.IsNullOrEmpty(currentModeString))
                    return;
                expectedAddress = config.OscAddress.Replace("{mode}", currentModeString);
            }
            else
            { return; }

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
                var modeIndex = this._logicManager.GetCurrentModeIndex(config.ModeName);
                if (modeIndex == -1)
                    return;

                string finalOscAddress;
                if (config.OscAddresses?.Count > modeIndex && modeIndex != -1) // 确保列表存在且索引有效
                {
                    finalOscAddress = config.OscAddresses[modeIndex];
                }
                else
                {
                    var currentModeString = this._logicManager.GetCurrentModeString(config.ModeName);
                    if (string.IsNullOrEmpty(currentModeString) || string.IsNullOrEmpty(config.OscAddress))
                        return;
                    finalOscAddress = config.OscAddress.Replace("{mode}", currentModeString);
                }

                if (config.ActionType == "ToggleButton")
                {
                    var currentState = this._logicManager.GetToggleState(actionParameter);
                    ReaOSCPlugin.SendOSCMessage(finalOscAddress, currentState ? 0f : 1f);
                    this._logicManager.SetToggleState(actionParameter, !currentState);
                    this.ActionImageChanged(actionParameter);
                }
                else if (config.ActionType == "TriggerButton") // 受模式控制的 TriggerButton
                {
                    ReaOSCPlugin.SendOSCMessage(finalOscAddress, 1f);
                    if (this._triggerResetTimers.TryGetValue(actionParameter, out var timer))
                    {
                        this._triggerTemporaryActiveStates[actionParameter] = true;
                        this.ActionImageChanged(actionParameter);
                        timer.Stop();
                        timer.Start();
                    }
                }
            }
            else // 普通按钮 (不受模式控制)
            {
                if (config.ActionType == "ToggleButton")
                {
                    var currentState = this._logicManager.GetToggleState(actionParameter);
                    ReaOSCPlugin.SendOSCMessage(config.OscAddress, currentState ? 0f : 1f);
                    this._logicManager.SetToggleState(actionParameter, !currentState);
                    this.ActionImageChanged(actionParameter);
                }
                else if (config.ActionType == "TriggerButton") // 普通 TriggerButton
                {
                    ReaOSCPlugin.SendOSCMessage(config.OscAddress, 1f);
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
                    var isModeActive = config.Modes?.IndexOf(currentModeString) > 0;
                    var bgColor = isModeActive ? HexToBitmapColor(config.ActiveColor) : BitmapColor.Black;
                    var titleColor = isModeActive ? HexToBitmapColor(config.TitleColor) : HexToBitmapColor(config.DeactiveTextColor);
                    bitmapBuilder.Clear(bgColor);
                    bitmapBuilder.DrawText(currentModeString, color: titleColor, fontSize: 23);
                }
                else // 处理 TriggerButton 和 ToggleButton (包括受控和非受控)
                {
                    BitmapColor currentBgColor = BitmapColor.Black;
                    BitmapColor currentTitleColorFromConfig = String.IsNullOrEmpty(config.TitleColor) ? BitmapColor.White : HexToBitmapColor(config.TitleColor); // 用于无图标时的Title
                    BitmapColor finalTitleColor = currentTitleColorFromConfig; // 最终用于绘制标题的颜色
                    bool iconDrawn = false;

                    if (config.ActionType == "TriggerButton")
                    {
                        var isTempActive = this._triggerTemporaryActiveStates.TryGetValue(actionParameter, out var tempState) && tempState;
                        currentBgColor = isTempActive && !String.IsNullOrEmpty(config.ActiveColor) ? HexToBitmapColor(config.ActiveColor) : BitmapColor.Black;
                        finalTitleColor = currentTitleColorFromConfig; // TriggerButton 主标题颜色通常固定
                    }
                    else if (config.ActionType == "ToggleButton")
                    {
                        var isActive = this._logicManager.GetToggleState(actionParameter);
                        currentBgColor = isActive && !String.IsNullOrEmpty(config.ActiveColor) ? HexToBitmapColor(config.ActiveColor) : BitmapColor.Black;

                        // ToggleButton的标题颜色根据激活状态切换 (用于主标题和图标下的DisplayName)
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
                                    int iconWidth = icon.Width * iconHeight / icon.Height; // 保持宽高比
                                    int iconX = (bitmapBuilder.Width - iconWidth) / 2;
                                    int iconY = 8; // 旧版固定值
                                    bitmapBuilder.DrawImage(icon, iconX, iconY, iconWidth, iconHeight);
                                    // 图标下方绘制DisplayName (使用 ToggleButton 状态决定的 finalTitleColor)
                                    bitmapBuilder.DrawText(text: config.DisplayName, x: 0, y: bitmapBuilder.Height - 23, width: bitmapBuilder.Width, height: 20, fontSize: 12, color: finalTitleColor);
                                    iconDrawn = true;
                                }
                            }
                            catch (Exception ex) { PluginLog.Warning(ex, $"加载按钮图标 '{imageName}' 失败 for action '{actionParameter}'."); }
                        }
                    }

                    if (!iconDrawn) // 如果没有绘制图标
                    {
                        bitmapBuilder.Clear(currentBgColor);
                        var titleToDraw = config.Title ?? config.DisplayName;

                        if (!string.IsNullOrEmpty(config.ModeName)) // 如果受模式控制
                        {
                            var modeIndex = this._logicManager.GetCurrentModeIndex(config.ModeName);
                            if (config.Titles?.Count > modeIndex && modeIndex != -1)
                            {
                                titleToDraw = config.Titles[modeIndex];
                            }
                        }

                        if (!String.IsNullOrEmpty(titleToDraw))
                        {
                            // TriggerButton 用其固定标题色，ToggleButton 用其动态标题色
                            bitmapBuilder.DrawText(text: titleToDraw, fontSize: this.GetAutomaticTitleFontSize(titleToDraw), color: finalTitleColor);
                        }

                        // 严格按照旧版逻辑绘制 config.Text
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

                    // 绘制模式小字 (仅当受模式控制且未绘制图标时，以避免与图标下的DisplayName重叠)
                    if (!string.IsNullOrEmpty(config.ModeName) && !iconDrawn)
                    {
                        var currentModeForDisplay = this._logicManager.GetCurrentModeString(config.ModeName);
                        // 使用旧版固定位置 (x: 50, y: 55)。注意: 这可能会与 config.Text 重叠。
                        // 如果希望避免重叠，需要进一步调整布局逻辑，例如仅当 config.Text 为空时才绘制模式小字，或调整其Y坐标。
                        // 当前严格按旧版，它同时绘制两者，可能发生重叠。
                        bitmapBuilder.DrawText(currentModeForDisplay, x: 50, y: 55, width: 14, height: 14, fontSize: 14, color: new BitmapColor(136, 226, 255));
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
                timer.Stop(); // 先停止
                timer.Elapsed -= null; // 尝试移除所有处理器，更安全
                timer.Dispose();
            }
            this._triggerResetTimers.Clear();
            this._triggerTemporaryActiveStates.Clear();
        }

        #region UI辅助方法 (从旧版代码引入)
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
    }
}