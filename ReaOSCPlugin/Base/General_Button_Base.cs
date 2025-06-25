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

        [Obsolete]
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
                            String pathSuffix = !String.IsNullOrEmpty(config.OscAddress)
                                ? config.OscAddress.Replace(" ", "_").TrimStart('/')
                                : config.DisplayName.Replace(" ", "_").TrimStart('/');
                            String listenAddress = $"/{groupNameForPath}/{pathSuffix}".Replace("//", "/");

                            if (listenAddress == "/")
                            {
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

        private void OnModeButtonOscStateChanged(Object _, OSCStateManager.StateChangedEventArgs e, ButtonConfig config, String actionParameter)
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
            {
                return;
            }
            if (e.Address == expectedAddress)
            {
                this._logicManager.SetToggleState(actionParameter, e.Value > 0.5f);
                this.ActionImageChanged(actionParameter);
            }
        }

        protected override void RunCommand(String actionParameter)
        {
            if (this._logicManager.GetConfig(actionParameter) is not { } config)
            {
                PluginLog.Warning($"[GeneralButtonBase|RunCommand] Config not found for actionParameter: {actionParameter}. Aborting.");
                return;
            }

            String finalOscAddressToSend = null;
            Single oscValueToSend = 1.0f; // 默认为1.0f，ToggleButton会修改它

            if (config.ActionType == "SelectModeButton")
            {
                this._logicManager.ToggleMode(config.DisplayName);
                // SelectModeButton 通常不发送OSC，仅切换模式并依赖事件刷新UI
                // ActionImageChanged 会由 Logic_Manager 的 ToggleMode 间接触发
                return; 
            }
            else if (!String.IsNullOrEmpty(config.ModeName)) // 处理带模式的按钮
            {
                var modeIndex = this._logicManager.GetCurrentModeIndex(config.ModeName);
                String currentModeStringForLog = this._logicManager.GetCurrentModeString(config.ModeName);

                if (modeIndex == -1)
                {
                    PluginLog.Warning($"[GeneralButtonBase|RunCommand] Button '{actionParameter}' (ModeName: '{config.ModeName}') - Invalid mode index or mode group not found. Current mode for log: '{currentModeStringForLog}'.");
                    // 即使模式无效，也尝试执行按钮的基础动作（例如，切换本地状态）
                    if (config.ActionType == "ToggleButton") 
                    { 
                        this._logicManager.SetToggleState(actionParameter, !this._logicManager.GetToggleState(actionParameter)); 
                        this.ActionImageChanged(actionParameter); // 刷新UI以反映本地状态变化
                    }
                    else if (config.ActionType == "TriggerButton" && this._triggerResetTimers.TryGetValue(actionParameter, out var t)) 
                    { 
                        this._triggerTemporaryActiveStates[actionParameter] = true; 
                        this.ActionImageChanged(actionParameter); 
                        t.Stop(); t.Start(); 
                    }
                    return; // 不发送OSC，因为模式上下文不明确
                }

                string oscAddressTemplate = null;
                if (config.OscAddresses != null && config.OscAddresses.Count > modeIndex && !String.IsNullOrEmpty(config.OscAddresses[modeIndex]))
                {
                    oscAddressTemplate = config.OscAddresses[modeIndex];
                }
                else if (!String.IsNullOrEmpty(config.OscAddress)) // 回退到通用的 OscAddress 字段
                {
                    oscAddressTemplate = config.OscAddress;
                }

                if (String.IsNullOrEmpty(oscAddressTemplate))
                {
                    PluginLog.Warning($"[GeneralButtonBase|RunCommand] Button '{config.DisplayName}' (Mode: '{currentModeStringForLog}') has no valid OSC address template for the current mode. ActionParameter: {actionParameter}");
                    // 仍然执行本地动作
                    if (config.ActionType == "ToggleButton") { this._logicManager.SetToggleState(actionParameter, !this._logicManager.GetToggleState(actionParameter)); this.ActionImageChanged(actionParameter); }
                    else if (config.ActionType == "TriggerButton" && this._triggerResetTimers.TryGetValue(actionParameter, out var t)) { this._triggerTemporaryActiveStates[actionParameter] = true; this.ActionImageChanged(actionParameter); t.Stop(); t.Start(); }
                    return;
                }
                
                // 使用 Logic_Manager_Base 的新方法来解析地址
                finalOscAddressToSend = this._logicManager.GetResolvedOscAddress(config, oscAddressTemplate);
                PluginLog.Info($"[GeneralButtonBase|RunCommand] Mode Button '{config.DisplayName}' (Mode: '{currentModeStringForLog}'). Template: '{oscAddressTemplate}', Resolved OSC: '{finalOscAddressToSend}'. ActionParameter: {actionParameter}");

                if (config.ActionType == "ToggleButton")
                {
                    var currentState = this._logicManager.GetToggleState(actionParameter);
                    oscValueToSend = !currentState ? 1.0f : 0.0f; // 发送新状态对应的值
                    this._logicManager.SetToggleState(actionParameter, !currentState); // 更新本地状态
                    // ActionImageChanged 会在 SetToggleState 内部或通过 CommandStateNeedsRefresh 触发
                }
                else if (config.ActionType == "TriggerButton")
                {
                    oscValueToSend = 1.0f;
                    if (this._triggerResetTimers.TryGetValue(actionParameter, out var timer))
                    {
                        this._triggerTemporaryActiveStates[actionParameter] = true;
                        this.ActionImageChanged(actionParameter); // 立即刷新以显示按下状态
                        timer.Stop();
                        timer.Start();
                    }
                }
            }
            else // 处理普通按钮 (无 ModeName)
            {
                string oscAddressTemplate = config.OscAddress; // 主要使用 OscAddress 字段
                                                               // 如果 OscAddress 为空，GetResolvedOscAddress 内部会回退到基于 DisplayName/Title
                
                finalOscAddressToSend = this._logicManager.GetResolvedOscAddress(config, oscAddressTemplate);
                PluginLog.Info($"[GeneralButtonBase|RunCommand] Normal Button '{config.DisplayName}'. Template: '{oscAddressTemplate ?? "null (will use DisplayName/Title)"}', Resolved OSC: '{finalOscAddressToSend}'. ActionParameter: {actionParameter}");
                
                if (config.ActionType == "ToggleButton")
                {
                    var currentState = this._logicManager.GetToggleState(actionParameter);
                    oscValueToSend = !currentState ? 1.0f : 0.0f;
                    this._logicManager.SetToggleState(actionParameter, !currentState);
                }
                else if (config.ActionType == "TriggerButton")
                {
                    oscValueToSend = 1.0f;
                    if (this._triggerResetTimers.TryGetValue(actionParameter, out var timer))
                    {
                        this._triggerTemporaryActiveStates[actionParameter] = true;
                        this.ActionImageChanged(actionParameter);
                        timer.Stop();
                        timer.Start();
                    }
                }
            }

            // 统一发送 OSC 消息 (如果地址有效)
            if (!String.IsNullOrEmpty(finalOscAddressToSend) && finalOscAddressToSend != "/" && !finalOscAddressToSend.Contains("{mode}"))
            {
                ReaOSCPlugin.SendOSCMessage(finalOscAddressToSend, oscValueToSend);
                PluginLog.Info($"[GeneralButtonBase|RunCommand] OSC Sent: '{finalOscAddressToSend}' -> {oscValueToSend} (Source: '{config.DisplayName}', Type: {config.ActionType})");
            }
            else
            {
                PluginLog.Warning($"[GeneralButtonBase|RunCommand] Invalid or unresolved OSC address for '{config.DisplayName}' (Type: {config.ActionType}). OSC not sent. Resolved address was: '{finalOscAddressToSend ?? "null"}'");
                // 对于 ToggleButton，即使OSC未发送，其本地状态已切换，UI应已通过SetToggleState刷新
                // 对于 TriggerButton，如果OSC未发送，按下效果的UI刷新也已处理
            }
            // 对于非 TriggerButton 的 ToggleButton，其 ActionImageChanged 已由 SetToggleState 内部触发
            // 对于 TriggerButton, ActionImageChanged 已在上面处理瞬时状态
            // SelectModeButton 的刷新由 Logic_Manager 驱动
        }

        protected override BitmapImage GetCommandImage(String actionParameter, PluginImageSize imageSize)
        {
            var config = this._logicManager.GetConfig(actionParameter);
            if (config is null)
            {
                PluginLog.Warning($"[GeneralButtonBase|GetCommandImage] Config not found for actionParameter: {actionParameter}. Returning default image.");
                return PluginImage.DrawElement(imageSize, null, preferIconOnlyForDial: false);
            }

            try
            {
                BitmapImage loadedIcon = PluginImage.TryLoadIcon(config, "GeneralButtonBase");

                Boolean isActive = false;
                String mainTitleToDraw = null;     // 将用于最终绘制的主标题
                String valueText = null;          // 按钮通常不直接显示 valueText
                Int32 currentModeForDrawing = 0; // 主要用于旋钮，按钮通常为0
                
                String preliminaryTitleTemplate; // 用于存储从LogicManager获取的原始模板
                String titleFieldNameForLogic;   // 告知LogicManager要获取哪个字段

                // 首先，解析通用的辅助文本 (config.Text)
                String auxTextToDraw = this._logicManager.ResolveTextWithMode(config, config.Text);

                if (config.ActionType == "SelectModeButton")
                {
                    // 对于 SelectModeButton，其显示的"标题"是当前模式的名称，这部分不通过动态标题OSC获取
                    // 它的模板直接就是当前模式名，然后可能再经过 ResolveTextWithMode (如果模式名本身含{mode}，虽然不太可能)
                    mainTitleToDraw = this._logicManager.GetCurrentModeString(config.DisplayName); 
                    if (String.IsNullOrEmpty(mainTitleToDraw) && config.Modes != null && config.Modes.Any())
                    {
                        mainTitleToDraw = config.Modes.FirstOrDefault(); 
                    }
                    if (String.IsNullOrEmpty(mainTitleToDraw))
                    {
                        mainTitleToDraw = config.DisplayName; 
                    }
                    // SelectModeButton 的 isActive 状态
                    isActive = (config.Modes?.IndexOf(this._logicManager.GetCurrentModeString(config.DisplayName) ?? "") ?? 0) > 0;
                }
                else // 其他类型的按钮 (TriggerButton, ToggleButton)
                {
                    // 确定要获取哪个标题字段的模板
                    // 对于普通按钮，我们主要关心 config.Title
                    // 如果按钮受模式控制且定义了 Titles 列表，则情况会更复杂
                    if (!String.IsNullOrEmpty(config.ModeName) && config.Titles != null && config.Titles.Any())
                    {
                        var modeIndex = this._logicManager.GetCurrentModeIndex(config.ModeName);
                        if (modeIndex != -1 && modeIndex < config.Titles.Count)
                        {
                            titleFieldNameForLogic = "Titles_Element";
                            preliminaryTitleTemplate = this._logicManager.GetCurrentTitleTemplate(actionParameter, titleFieldNameForLogic, config, modeIndex);
                        }
                        else
                        {
                            // 模式索引无效或 Titles 列表对应项为空，回退到 config.Title
                            titleFieldNameForLogic = "Title";
                            preliminaryTitleTemplate = this._logicManager.GetCurrentTitleTemplate(actionParameter, titleFieldNameForLogic, config);
                            PluginLog.Verbose($"[GeneralButtonBase|GetCommandImage] Button '{config.DisplayName}' (ModeName: '{config.ModeName}') - Invalid mode index '{modeIndex}' for Titles list or Titles[{modeIndex}] is empty. Falling back to '{titleFieldNameForLogic}'. Template: '{preliminaryTitleTemplate}'");
                        }
                    }
                    else
                    {
                        // 标准情况：使用 config.Title
                        titleFieldNameForLogic = "Title";
                        preliminaryTitleTemplate = this._logicManager.GetCurrentTitleTemplate(actionParameter, titleFieldNameForLogic, config);
                    }
                    
                    // 使用 ResolveTextWithMode 解析最终标题
                    mainTitleToDraw = this._logicManager.ResolveTextWithMode(config, preliminaryTitleTemplate);

                    // 其他按钮类型的 isActive 判断逻辑 (保持不变)
                    if (config.ActionType == "TriggerButton" || config.ActionType == "CombineButton") // CombineButton 可能也需要瞬时高亮
                    {
                        isActive = this._triggerTemporaryActiveStates.TryGetValue(actionParameter, out var tempState) && tempState;
                    }
                    else if (config.ActionType == "ToggleButton")
                    { 
                        isActive = this._logicManager.GetToggleState(actionParameter); 
                    }
                    
                    // 回退逻辑: 如果 mainTitleToDraw 为空，并且不是特殊的 SelectModeButton，则使用 DisplayName
                    if (String.IsNullOrEmpty(mainTitleToDraw)) // SelectModeButton 已在上面独立处理标题
                    {
                        PluginLog.Info($"[GeneralButtonBase|GetCommandImage] Fallback for '{actionParameter}' (config: '{config.DisplayName}'): mainTitleToDraw was empty or null, using DisplayName: '{config.DisplayName}'");
                        mainTitleToDraw = config.DisplayName;
                    }
                }
                            
                return PluginImage.DrawElement(
                    imageSize,
                    config,
                    mainTitleToDraw,    
                    valueText, 
                    isActive,          
                    currentModeForDrawing,       
                    loadedIcon, 
                    false, 
                    auxTextToDraw,      
                    preferIconOnlyForDial: false 
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