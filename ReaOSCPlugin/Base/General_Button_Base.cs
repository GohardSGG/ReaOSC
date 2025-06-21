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
                        string groupNameForPath = config.GroupName.Replace(" ", "_").Trim('/');
                        string pathAfterMode = config.OscAddress.Replace("{mode}", currentModeString).TrimStart('/');
                        expectedAddress = $"/{groupNameForPath}/{pathAfterMode}".Replace("//", "/");
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
                    {
                        string groupNameForPath = config.GroupName.Replace(" ", "_").Trim('/');
                        string pathAfterMode = config.OscAddress.Replace("{mode}", currentModeString).TrimStart('/');
                        finalOscAddress = $"/{groupNameForPath}/{pathAfterMode}".Replace("//", "/");
                    }
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
            var config = this._logicManager.GetConfig(actionParameter);
            if (config == null)
            {
                PluginLog.Warning($"[GeneralButtonBase|GetCommandImage] Config not found for actionParameter: {actionParameter}. Returning default image.");
                // 返回一个由 PluginImage 生成的默认错误图像，而不是 base.GetCommandImage
                return PluginImage.DrawElement(imageSize, null); // PluginImage 会处理 config 为 null 的情况
            }

            try
            {
                BitmapImage customIcon = null;
                // 1. 图标加载逻辑 (统一)
                string imagePathToLoad = !String.IsNullOrEmpty(config.ButtonImage) ? config.ButtonImage : null;
                if (string.IsNullOrEmpty(imagePathToLoad))
                {
                    // 尝试根据 DisplayName 推断: xxx.png (严格匹配)
                    // 注意: actionParameter 是 /GroupName/DisplayName 格式，需要提取DisplayName部分
                    var displayNameFromActionParam = actionParameter.Split('/').LastOrDefault();
                    if (!string.IsNullOrEmpty(displayNameFromActionParam) && 
                        Logic_Manager_Base.SanitizeOscPathSegment(displayNameFromActionParam) == Logic_Manager_Base.SanitizeOscPathSegment(config.DisplayName)) //确保比较的是处理过的名字
                    {
                        imagePathToLoad = $"{Logic_Manager_Base.SanitizeOscPathSegment(config.DisplayName)}.png";
                    }
                }

                if (!string.IsNullOrEmpty(imagePathToLoad))
                {
                    try
                    {
                        customIcon = PluginResources.ReadImage(imagePathToLoad);
                    }
                    catch (Exception ex)
                    {
                        PluginLog.Error(ex, $"[GeneralButtonBase|GetCommandImage] Failed to load icon '{imagePathToLoad}' for action '{actionParameter}'. Will draw text only.");
                        customIcon = null;
                    }
                }

                // 2. 确定状态和动态文本
                bool isActive = false;
                string mainTitleOverride = null;
                string valueText = null; // 用于可能的次要文本，如模式显示
                int currentModeForDrawing = 0; // 对于按钮，模式主要影响 isActive 或 mainTitle
                string actualAuxTextToDraw = config.Text; // Default to config.Text

                // 1. Determine mainTitleOverride based on ModeName and Titles array first
                if (!String.IsNullOrEmpty(config.ModeName) && config.Titles != null && config.Titles.Any())
                {
                    var modeIndex = this._logicManager.GetCurrentModeIndex(config.ModeName);
                    if (modeIndex != -1 && config.Titles.Count > modeIndex && !String.IsNullOrEmpty(config.Titles[modeIndex]))
                    {
                        mainTitleOverride = config.Titles[modeIndex];
                    }
                    else 
                    {
                        mainTitleOverride = config.Title ?? config.DisplayName; // Fallback to default title
                    }
                }
                else
                {
                    mainTitleOverride = config.Title ?? config.DisplayName; // Not mode-controlled title or no Titles array
                }

                // 2. Determine isActive and potentially update actualAuxTextToDraw based on ActionType and ModeName
                if (config.ActionType == "SelectModeButton")
                {
                    // For SelectModeButton, mainTitleOverride is always the current mode string.
                    mainTitleOverride = this._logicManager.GetCurrentModeString(config.DisplayName) ?? config.Modes?.FirstOrDefault() ?? config.DisplayName;
                    isActive = (config.Modes?.IndexOf(this._logicManager.GetCurrentModeString(config.DisplayName) ?? "") ?? 0) > 0;
                    if (config.Text == "{mode}")
                    {
                        actualAuxTextToDraw = mainTitleOverride; 
                    }
                }
                else if (config.ActionType == "TriggerButton")
                {
                    isActive = this._triggerTemporaryActiveStates.TryGetValue(actionParameter, out var tempState) && tempState;
                    // mainTitleOverride is already set based on mode/titles or default
                    if (!String.IsNullOrEmpty(config.ModeName) && config.Text == "{mode}")
                    {
                        actualAuxTextToDraw = this._logicManager.GetCurrentModeString(config.ModeName) ?? "";
                    }
                }
                else if (config.ActionType == "ToggleButton")
                {
                    isActive = this._logicManager.GetToggleState(actionParameter);
                    // mainTitleOverride is already set based on mode/titles or default
                    if (!String.IsNullOrEmpty(config.ModeName) && config.Text == "{mode}")
                    {
                        actualAuxTextToDraw = this._logicManager.GetCurrentModeString(config.ModeName) ?? "";
                    }
                }
                // else: For other action types, mainTitleOverride and actualAuxTextToDraw are already set correctly
                // based on the initial mode/titles check and default config.Text handling.
                
                // 调用 PluginImage.DrawElement
                return PluginImage.DrawElement(
                    imageSize,
                    config,
                    mainTitleOverride,
                    valueText, // 传递 valueText, PluginImage 会处理其绘制
                    isActive,
                    currentModeForDrawing, // 对于按钮，模式通常不直接改变背景/前景，而是通过isActive或title
                    customIcon,
                    false, // forceTextOnly, 默认不强制
                    actualAuxTextToDraw // Pass the resolved text
                );
            }
            catch (Exception ex)
            {
                PluginLog.Error(ex, $"[GeneralButtonBase|GetCommandImage] Unhandled exception for action '{actionParameter}'.");
                // 返回一个由 PluginImage 生成的错误图像
                return PluginImage.DrawElement(imageSize, config, "ERR!", isActive:true, actualAuxText: config?.Text); // config可能为null，所以传一个简单的错误提示
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
        // 【移除】GetAutomaticTitleFontSize 和 HexToBitmapColor，因为它们已移至 PluginImage.cs
        // private int GetAutomaticTitleFontSize(String title) { ... }
        // private static BitmapColor HexToBitmapColor(string hexColor) { ... }
        #endregion
    }
}