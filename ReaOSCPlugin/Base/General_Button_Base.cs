// 文件名: Base/General_Button_Base.cs
// 【基于您上传的文件进行修改，仅移除SVG加载，保留PNG加载和原有绘制逻辑】
// 【重构】: GetCommandImage 方法现在使用 PluginImage.DrawElement 进行绘制
// 【修正】: 使用 Logic_Manager_Base.SanitizeOscPathSegment 替换 SanitizeHelper
// 【新增】: 为受模式控制且 config.Text == "{mode}" 的按钮解析并传递实际模式名给 PluginImage.DrawElement
namespace Loupedeck.ReaOSCPlugin.Base
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO; // 确保 System.IO 的 using 存在，因为用到了 Path.GetFileNameWithoutExtension
    using System.Linq;
    using System.Timers;
    // System.Drawing 相关 using 不是必需的，因为 BitmapBuilder 使用 Loupedeck.BitmapColor

    using Loupedeck.ReaOSCPlugin.Helpers; // 【新增】引用新的绘图类

    public class General_Button_Base : PluginDynamicCommand, IDisposable
    {
        private readonly Logic_Manager_Base _logicManager = Logic_Manager_Base.Instance;
        private readonly Dictionary<String, Action> _modeHandlers = new Dictionary<String, Action>();
        private readonly Dictionary<String, EventHandler<OSCStateManager.StateChangedEventArgs>> _oscHandlers = new Dictionary<String, EventHandler<OSCStateManager.StateChangedEventArgs>>();

        private readonly Dictionary<String, Boolean> _triggerTemporaryActiveStates = new Dictionary<String, Boolean>();
        private readonly Dictionary<String, Timer> _triggerResetTimers = new Dictionary<String, Timer>();

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
                else if (!String.IsNullOrEmpty(config.ModeName))
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
                            String groupNameForPath = config.GroupName.Replace(" ", "_").Trim('/');
                            String pathSuffix;
                            if (!String.IsNullOrEmpty(config.OscAddress))
                            {
                                pathSuffix = config.OscAddress.Replace(" ", "_").TrimStart('/');
                            }
                            else
                            {
                                pathSuffix = config.DisplayName.Replace(" ", "_").TrimStart('/');
                            }
                            String listenAddress = $"/{groupNameForPath}/{pathSuffix}".Replace("//", "/");

                            if (String.IsNullOrEmpty(listenAddress) || listenAddress == "/")
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

        private void InitializeTriggerButtonTimer(String actionParameter)
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

        private void OnModeButtonOscStateChanged(object sender, OSCStateManager.StateChangedEventArgs e, ButtonConfig config, String actionParameter)
        {
            var modeIndex = this._logicManager.GetCurrentModeIndex(config.ModeName);
            String expectedAddress = null;

            if (modeIndex != -1)
            {
                if (config.OscAddresses?.Count > modeIndex)
                {
                    expectedAddress = config.OscAddresses[modeIndex];
                }
                if (String.IsNullOrEmpty(expectedAddress) && !String.IsNullOrEmpty(config.OscAddress))
                {
                    String currentModeString = this._logicManager.GetCurrentModeString(config.ModeName);
                    if (!String.IsNullOrEmpty(currentModeString))
                    {
                        String groupNameForPath = config.GroupName.Replace(" ", "_").Trim('/');
                        String pathAfterMode = config.OscAddress.Replace("{mode}", currentModeString).TrimStart('/');
                        expectedAddress = $"/{groupNameForPath}/{pathAfterMode}".Replace("//", "/");
                    }
                }
            }
            if (String.IsNullOrEmpty(expectedAddress))
                return;
            if (e.Address == expectedAddress)
            {
                this._logicManager.SetToggleState(actionParameter, e.Value > 0.5f);
                this.ActionImageChanged(actionParameter);
            }
        }

        protected override void RunCommand(String actionParameter)
        {
            if (this._logicManager.GetConfig(actionParameter) is not { } config)
                return;

            if (config.ActionType == "SelectModeButton")
            {
                this._logicManager.ToggleMode(config.DisplayName);
            }
            else if (!String.IsNullOrEmpty(config.ModeName))
            {
                var modeIndex = this._logicManager.GetCurrentModeIndex(config.ModeName);
                String currentModeStringForLog = this._logicManager.GetCurrentModeString(config.ModeName);
                if (modeIndex == -1)
                {
                    PluginLog.Warning($"[RunCommand] Button '{actionParameter}' ModeGroup '{config.ModeName}' invalid index.");
                    if (config.ActionType == "ToggleButton")
                    { this._logicManager.SetToggleState(actionParameter, !this._logicManager.GetToggleState(actionParameter)); this.ActionImageChanged(actionParameter); }
                    else if (config.ActionType == "TriggerButton" && this._triggerResetTimers.TryGetValue(actionParameter, out var t))
                    { this._triggerTemporaryActiveStates[actionParameter] = true; this.ActionImageChanged(actionParameter); t.Stop(); t.Start(); }
                    return;
                }
                String finalOscAddress = null;
                if (config.OscAddresses?.Count > modeIndex)
                { finalOscAddress = config.OscAddresses[modeIndex]; }
                if (String.IsNullOrEmpty(finalOscAddress) && !String.IsNullOrEmpty(config.OscAddress))
                {
                    String currentModeString = this._logicManager.GetCurrentModeString(config.ModeName);
                    if (!String.IsNullOrEmpty(currentModeString))
                    {
                        String groupNameForPath = config.GroupName.Replace(" ", "_").Trim('/');
                        String pathAfterMode = config.OscAddress.Replace("{mode}", currentModeString).TrimStart('/');
                        finalOscAddress = $"/{groupNameForPath}/{pathAfterMode}".Replace("//", "/");
                    }
                }
                if (String.IsNullOrEmpty(finalOscAddress))
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
                String groupNameForPath = config.GroupName.Replace(" ", "_").Trim('/');
                String pathSuffix = (!String.IsNullOrEmpty(config.OscAddress)) ? config.OscAddress.Replace(" ", "_").TrimStart('/') : config.DisplayName.Replace(" ", "_").TrimStart('/');
                String targetOscAddress = $"/{groupNameForPath}/{pathSuffix}".Replace("//", "/");

                if (String.IsNullOrEmpty(targetOscAddress) || targetOscAddress == "/")
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

        protected override BitmapImage GetCommandImage(String actionParameter, PluginImageSize imageSize)
        {
            var config = this._logicManager.GetConfig(actionParameter);
            if (config == null)
            {
                PluginLog.Warning($"[GeneralButtonBase|GetCommandImage] Config not found for actionParameter: {actionParameter}. Returning default image.");
                return PluginImage.DrawElement(imageSize, null, preferIconOnlyForDial: false);
            }

            try
            {
                BitmapImage loadedIcon = PluginImage.TryLoadIcon(config, "GeneralButtonBase");

                // 准备 PluginImage.DrawElement 的其他参数 (大部分与之前 GetCommandImage 中手动绘制前的准备逻辑一致)
                Boolean isActive = false;
                String mainTitleOverride = null;
                String valueText = null; // 按钮通常不直接显示 valueText
                Int32 currentModeForDrawing = 0; 
                String actualAuxTextToDraw = config.Text; // PluginImage.DrawElement 在图标模式下不绘制此项

                // 确定 mainTitleOverride (基于模式或默认)
                if (!String.IsNullOrEmpty(config.ModeName) && config.Titles != null && config.Titles.Any())
                {
                    var modeIndex = this._logicManager.GetCurrentModeIndex(config.ModeName);
                    if (modeIndex != -1 && config.Titles.Count > modeIndex && !String.IsNullOrEmpty(config.Titles[modeIndex]))
                    { mainTitleOverride = config.Titles[modeIndex]; }
                    else 
                    { mainTitleOverride = config.Title ?? config.DisplayName; }
                }
                else
                { mainTitleOverride = config.Title ?? config.DisplayName; }

                // 确定 isActive 和 actualAuxTextToDraw (基于 ActionType 和模式)
                if (config.ActionType == "SelectModeButton")
                {
                    mainTitleOverride = this._logicManager.GetCurrentModeString(config.DisplayName) ?? config.Modes?.FirstOrDefault() ?? config.DisplayName;
                    isActive = (config.Modes?.IndexOf(this._logicManager.GetCurrentModeString(config.DisplayName) ?? "") ?? 0) > 0;
                    if (config.Text == "{mode}") { actualAuxTextToDraw = mainTitleOverride; }
                }
                else if (config.ActionType == "TriggerButton")
                {
                    isActive = this._triggerTemporaryActiveStates.TryGetValue(actionParameter, out var tempState) && tempState;
                    if (!String.IsNullOrEmpty(config.ModeName) && config.Text == "{mode}")
                    { actualAuxTextToDraw = this._logicManager.GetCurrentModeString(config.ModeName) ?? ""; }
                }
                else if (config.ActionType == "ToggleButton")
                {
                    isActive = this._logicManager.GetToggleState(actionParameter);
                    if (!String.IsNullOrEmpty(config.ModeName) && config.Text == "{mode}")
                    { actualAuxTextToDraw = this._logicManager.GetCurrentModeString(config.ModeName) ?? ""; }
                }
                // ParameterButton 的 mainTitleOverride 由 General_Folder_Base.GetCommandImage 处理，此处不直接覆盖
                // 对于 General_Button_Base，如果它要独立支持 ParameterButton，需要类似逻辑，但当前它不直接处理此类型按钮的标题
                
                return PluginImage.DrawElement(
                    imageSize,
                    config,
                    mainTitleOverride,
                    valueText, 
                    isActive,
                    currentModeForDrawing, 
                    loadedIcon, // 使用加载的图标
                    false, // forceTextOnly
                    actualAuxTextToDraw, // PluginImage.DrawElement 在按钮图标模式下不绘制它
                    preferIconOnlyForDial: false // 按钮遵循图标+文字规则
                );
            }
            catch (Exception ex)
            {
                PluginLog.Error(ex, $"[GeneralButtonBase|GetCommandImage] Unhandled exception for action '{actionParameter}'.");
                return PluginImage.DrawElement(imageSize, config, "ERR!", isActive:true, preferIconOnlyForDial: false);
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
                    if (!String.IsNullOrEmpty(modeName))
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

    }
}