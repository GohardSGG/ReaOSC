// 文件名: Base/Logic_Manager_Base.cs
// 【修正OSC地址构建规则，确保基路径为JSON中的GroupName，而不是动态文件夹名称】
namespace Loupedeck.ReaOSCPlugin.Base
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Text.RegularExpressions;

    using Newtonsoft.Json;

    public class ButtonConfig
    {
        // === 通用配置 ===
        public string DisplayName { get; set; }
        public string Title { get; set; } // 主要用于UI显示，尤其当DisplayName不适合直接显示时
        public string TitleColor { get; set; } // 通用标题颜色
        public string GroupName { get; set; }
        public string ActionType { get; set; }
        // 可能的值: "TriggerButton", "ToggleButton", "TickDial", "ToggleDial", "2ModeTickDial", "SelectModeButton",
        // 【新增】 "ParameterDial", "ParameterButton", "CombineButton"
        public string Description { get; set; }

        // --- OSC 相关 ---
        public string OscAddress { get; set; } // 可选。如果提供，优先作为此控件贡献给CombineButton的路径片段 (经过处理后)

        // --- 模式相关 (主要用于 SelectModeButton 及其控制的按钮) ---
        public string ModeName { get; set; }
        public List<string> Modes { get; set; } // For SelectModeButton: 定义可选模式
        public List<string> Titles { get; set; } // For ParameterDial: 定义可选参数值; For SelectModeButton & controlled buttons: 定义不同模式下的显示标题
        public List<string> OscAddresses { get; set; } // For SelectModeButton & controlled buttons: 定义不同模式下的OSC地址

        // === 按钮相关 ===
        // --- ToggleButton 和 ToggleDial 特有 (也可能被ParameterDial用于不同状态的显示) ---
        public string ActiveColor { get; set; } // ToggleButton ON 状态背景色, ParameterDial 激活状态背景色 (如果适用)
        public string ActiveTextColor { get; set; } // ToggleButton ON 状态文字颜色
        public string DeactiveTextColor { get; set; } // ToggleButton OFF 状态文字颜色

        public string ButtonImage { get; set; } // (目前主要用于 General_Button_Base, 动态文件夹内按钮较少直接用图片)

        // === 旋钮相关 (TickDial, ToggleDial, 2ModeTickDial) ===
        public string IncreaseOSCAddress { get; set; } // 主要用于 TickDial
        public string DecreaseOSCAddress { get; set; } // 主要用于 TickDial
        public float? AccelerationFactor { get; set; } // 主要用于 TickDial
        public string ResetOscAddress { get; set; } // 主要用于 TickDial, ToggleDial 按下时的 OSC

        // --- 次要文本 (可用于所有类型按钮/旋钮的额外小字显示) ---
        public string Text { get; set; }
        public string TextColor { get; set; }
        public int? TextSize { get; set; }
        public int? TextX { get; set; }
        public int? TextY { get; set; }
        public int? TextWidth { get; set; }
        public int? TextHeight { get; set; }

        // === 2ModeTickDial 特有配置 ===
        public string Title_Mode2 { get; set; }
        public string TitleColor_Mode2 { get; set; }
        public string IncreaseOSCAddress_Mode2 { get; set; }
        public string DecreaseOSCAddress_Mode2 { get; set; }
        public string BackgroundColor { get; set; } // 旋钮模式1的背景色 (或通用背景色)
        public string BackgroundColor_Mode2 { get; set; } // 旋钮模式2的背景色

        // === 【新增】ParameterDial 特有 ===
        // Titles 字段已存在，将被 ParameterDial 用作参数值列表
        public string ShowParameterInDial { get; set; } // "Yes" 或 "No". "Yes" 则旋钮UI显示当前选中的Title, "No"则显示固定的Title/DisplayName

        // === 【新增】ParameterButton 特有 ===
        public string ParameterSourceDial { get; set; } // 指向要显示其参数的 ParameterDial 的 DisplayName

        // === 【新增】CombineButton 特有 ===
        // BaseOscPrefix 将由文件夹 GroupName 动态决定，不需要在此配置
        public List<string> ParameterOrder { get; set; } // 定义 CombineButton 收集参数的顺序，值为参与控件的 DisplayName

        // === 【新增】ToggleButton (当参与 CombineButton 时) ===
        // PathSegmentIfOn 也不再需要，ToggleButton ON 时贡献 DisplayName 或 OscAddress (处理后)
        
        // === 【新增】动态文件夹内容定义 ===
        public FolderContentConfig Content { get; set; }
    }

    public class FolderContentConfig
    {
        public List<ButtonConfig> Buttons { get; set; } = new List<ButtonConfig>();
        public List<ButtonConfig> Dials { get; set; } = new List<ButtonConfig>();
    }

    public class Logic_Manager_Base : IDisposable
    {
        private static readonly Lazy<Logic_Manager_Base> _instance = new Lazy<Logic_Manager_Base>(() => new Logic_Manager_Base());
        public static Logic_Manager_Base Instance => _instance.Value;

        private readonly Dictionary<string, ButtonConfig> _allConfigs = new Dictionary<string, ButtonConfig>();
        private readonly Dictionary<string, bool> _toggleStates = new Dictionary<string, bool>();
        private readonly Dictionary<string, int> _dialModes = new Dictionary<string, int>();
        
        // 【新增】用于存储动态文件夹内容的字典
        private readonly Dictionary<string, FolderContentConfig> _folderContents = new Dictionary<string, FolderContentConfig>();
        
        private bool _isInitialized = false;

        private readonly Dictionary<string, List<string>> _modeOptions = new Dictionary<string, List<string>>();
        private readonly Dictionary<string, int> _currentModes = new Dictionary<string, int>();
        private readonly Dictionary<string, Action> _modeChangedEvents = new Dictionary<string, Action>();

        private readonly Dictionary<string, string> _oscAddressToActionParameterMap = new Dictionary<string, string>();
        public event EventHandler<string> CommandStateNeedsRefresh;

        private readonly Dictionary<string, int> _parameterDialSelectedIndexes = new Dictionary<string, int>();

        private Logic_Manager_Base() { }

        public void Initialize()
        {
            if (this._isInitialized)
                return;
            PluginLog.Info("[LogicManager] 开始初始化...");
            this.LoadAllConfigs();
            OSCStateManager.Instance.StateChanged += this.OnOSCStateChanged;
            this._isInitialized = true;
            PluginLog.Info($"[LogicManager] 初始化成功。加载了 {_allConfigs.Count} 个配置项。");
        }

        #region 模式管理
        public void RegisterModeGroup(ButtonConfig config) { var modeName = config.DisplayName; var modes = config.Modes; if (string.IsNullOrEmpty(modeName) || modes == null || modes.Count == 0) return; if (!_modeOptions.ContainsKey(modeName)) { _modeOptions[modeName] = modes; _currentModes[modeName] = 0; _modeChangedEvents[modeName] = null; PluginLog.Info($"[LogicManager][Mode] 模式组 '{modeName}' 已注册，包含模式: {string.Join(", ", modes)}"); } }
        public void ToggleMode(string modeName) { if (_currentModes.TryGetValue(modeName, out _) && _modeOptions.TryGetValue(modeName, out var options)) { _currentModes[modeName] = (_currentModes[modeName] + 1) % options.Count; _modeChangedEvents[modeName]?.Invoke(); PluginLog.Info($"[LogicManager][Mode] 模式组 '{modeName}' 已切换到: {GetCurrentModeString(modeName)}"); this.CommandStateNeedsRefresh?.Invoke(this, GetActionParameterForModeController(modeName)); } }
        private string GetActionParameterForModeController(string modeName) => this._allConfigs.FirstOrDefault(kvp => kvp.Value.ActionType == "SelectModeButton" && kvp.Value.DisplayName == modeName).Key;
        public string GetCurrentModeString(string modeName) { if (_currentModes.TryGetValue(modeName, out var currentIndex) && _modeOptions.TryGetValue(modeName, out var options) && currentIndex >= 0 && currentIndex < options.Count) { return options[currentIndex]; } return string.Empty; }
        public int GetCurrentModeIndex(string modeName) { return _currentModes.TryGetValue(modeName, out var currentIndex) ? currentIndex : -1; }
        public void SubscribeToModeChange(string modeName, Action handler) { if (string.IsNullOrEmpty(modeName) || handler == null) return; if (!_modeChangedEvents.ContainsKey(modeName)) _modeChangedEvents[modeName] = null; _modeChangedEvents[modeName] += handler; }
        public void UnsubscribeFromModeChange(string modeName, Action handler) { if (string.IsNullOrEmpty(modeName) || handler == null) return; if (_modeChangedEvents.ContainsKey(modeName)) { _modeChangedEvents[modeName] -= handler; } }
        #endregion

        #region 配置加载
        private void LoadAllConfigs()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var generalConfigs = this.LoadAndDeserialize<Dictionary<string, List<ButtonConfig>>>(assembly, "Loupedeck.ReaOSCPlugin.General.General_List.json");
            this.ProcessGroupedConfigs(generalConfigs, isFx: false);
            
            // 【重要】不再加载任何旧的Effects_List.json
            
            // 【重构】加载统一的 Dynamic_List.json，它现在包含了文件夹的入口定义和内容
            var dynamicFolderDefs = this.LoadAndDeserialize<List<ButtonConfig>>(assembly, "Loupedeck.ReaOSCPlugin.Dynamic.Dynamic_List.json");
            if (dynamicFolderDefs != null)
            {
                var folderEntriesToRegister = new List<ButtonConfig>();
                foreach (var folderDef in dynamicFolderDefs)
                {
                    // 为文件夹入口按钮设置固定的GroupName
                    folderDef.GroupName = "Dynamic";

                    if (folderDef.Content != null)
                    {
                        // 1. 将内容（Buttons和Dials）存储起来，供Dynamic_Folder_Base以后按名字查找
                        this._folderContents[folderDef.DisplayName] = folderDef.Content;

                        // 2. 将文件夹内容中的所有按钮和旋钮都注册到全局配置 _allConfigs 中
                        //    这样 Dynamic_Folder_Base 在填充时才能通过 DisplayName 和 GroupName 找到它们
                        this.ProcessFolderContentConfigs(folderDef.Content);
                    }
                    
                    // 3. 将文件夹入口按钮本身（不含Content）添加到待注册列表
                    var folderEntry = folderDef;
                    folderEntry.Content = null; // 确保不把内容本身当作一个配置项
                    folderEntriesToRegister.Add(folderEntry);
                }
                // 4. 统一注册所有文件夹的入口按钮
                this.RegisterConfigs(folderEntriesToRegister, isFx: false, isDynamicFolderEntry: true);
            }
        }
        private T LoadAndDeserialize<T>(Assembly assembly, string resourceName) where T : class { try { using (var stream = assembly.GetManifestResourceStream(resourceName)) { if (stream == null) { PluginLog.Error($"[LogicManager] 无法找到嵌入资源: {resourceName}"); return null; } using (var reader = new StreamReader(stream)) { return JsonConvert.DeserializeObject<T>(reader.ReadToEnd()); } } } catch (Exception ex) { PluginLog.Error(ex, $"[LogicManager] 读取或解析资源 '{resourceName}' 失败。"); return null; } }
        private void ProcessGroupedConfigs(Dictionary<string, List<ButtonConfig>> groupedConfigs, bool isFx) { if (groupedConfigs == null) return; foreach (var group in groupedConfigs) { var configs = group.Value.Select(config => { config.GroupName = group.Key; return config; }).ToList(); this.RegisterConfigs(configs, isFx); } }
        private void ProcessFolderContentConfigs(FolderContentConfig folderContent) { if (folderContent == null) return; this.RegisterConfigs(folderContent.Buttons, isFx: false); this.RegisterConfigs(folderContent.Dials, isFx: false); }

        private void RegisterConfigs(List<ButtonConfig> configs, bool isFx, bool isDynamicFolderEntry = false)
        {
            if (configs == null)
                return;
            foreach (var config in configs)
            {
                if (string.IsNullOrEmpty(config.GroupName))
                { PluginLog.Warning($"[LogicManager] 配置 '{config.DisplayName}' 缺少 GroupName，已跳过。"); continue; }
                string actionParameter;
                if (isDynamicFolderEntry)
                { actionParameter = config.DisplayName; }
                else
                {
                    string groupNameForPath = SanitizeOscPathSegment(config.GroupName);
                    string displayNameForPath = SanitizeOscPathSegment(config.DisplayName);
                    actionParameter = $"/{groupNameForPath}/{displayNameForPath}";
                    if (config.ActionType != null && config.ActionType.Contains("Dial"))
                        actionParameter += "/DialAction";
                }
                actionParameter = actionParameter.Replace("//", "/").TrimEnd('/');
                if (this._allConfigs.ContainsKey(actionParameter))
                { continue; }
                this._allConfigs[actionParameter] = config;

                if (config.ActionType == "ToggleButton" || config.ActionType == "ToggleDial")
                { this._toggleStates[actionParameter] = false; }
                else if (config.ActionType == "2ModeTickDial")
                { this._dialModes[actionParameter] = 0; }
                else if (config.ActionType == "ParameterDial")
                { this._parameterDialSelectedIndexes[actionParameter] = 0; }

                string effectiveOscAddressForStateListener = null;
                if (!string.IsNullOrEmpty(config.OscAddress))
                {
                    // 【修正】确保用于监听的地址是基于JSON GroupName（如果OscAddress是相对的）
                    // 或者如果OscAddress是绝对的，则直接使用。
                    // DetermineOscAddressForAction 会处理这个问题。
                    effectiveOscAddressForStateListener = DetermineOscAddressForAction(config, config.GroupName, config.OscAddress);
                }
                else if (config.ActionType == "ToggleButton" || config.ActionType == "ToggleDial")
                {
                    effectiveOscAddressForStateListener = DetermineOscAddressForAction(config, config.GroupName); // 使用JSON GroupName
                }

                if (!string.IsNullOrEmpty(effectiveOscAddressForStateListener) && effectiveOscAddressForStateListener != "/")
                {
                    if (config.ActionType == "ToggleButton" || config.ActionType == "ToggleDial")
                    {
                        this._oscAddressToActionParameterMap[effectiveOscAddressForStateListener] = actionParameter;
                        // 初始化状态时也考虑当前OSC设备的状态
                        this._toggleStates[actionParameter] = OSCStateManager.Instance.GetState(effectiveOscAddressForStateListener) > 0.5f;
                    }
                }
            }
        }
        #endregion

        #region 公共访问与核心逻辑

        public IReadOnlyDictionary<string, ButtonConfig> GetAllConfigs() => this._allConfigs;
        public ButtonConfig GetConfig(string actionParameter) => this._allConfigs.TryGetValue(actionParameter, out var c) ? c : null;
        
        // 【新增】公共方法，用于按文件夹名称获取其内容
        public FolderContentConfig GetFolderContent(string folderName) => this._folderContents.TryGetValue(folderName, out var c) ? c : null;
        
        public ButtonConfig GetConfigByDisplayName(string groupName, string displayName)
        {
            if (groupName == "Dynamic")
            { return this._allConfigs.TryGetValue(displayName, out var c) && c.GroupName == groupName ? c : null; }

            // 构建基于JSON GroupName的actionParameter进行精确查找
            string actionParameterKey = $"/{SanitizeOscPathSegment(groupName)}/{SanitizeOscPathSegment(displayName)}";
            string actionParameterKeyDial = actionParameterKey + "/DialAction";

            if (this._allConfigs.TryGetValue(actionParameterKey, out var configButton))
                return configButton;
            if (this._allConfigs.TryGetValue(actionParameterKeyDial, out var configDial))
                return configDial;

            // Fallback: 遍历查找 (通常不应依赖此)
            return this._allConfigs.Values.FirstOrDefault(c => c.GroupName == groupName && c.DisplayName == displayName);
        }

        private void OnOSCStateChanged(object sender, OSCStateManager.StateChangedEventArgs e)
        {
            if (this._oscAddressToActionParameterMap.TryGetValue(e.Address, out string mappedActionParameter))
            {
                var config = GetConfig(mappedActionParameter);
                if (config == null)
                    return;
                if (config.ActionType == "ToggleButton" || config.ActionType == "ToggleDial")
                {
                    var newState = e.Value > 0.5f;
                    if (!this._toggleStates.ContainsKey(mappedActionParameter) || this._toggleStates[mappedActionParameter] != newState)
                    {
                        this._toggleStates[mappedActionParameter] = newState;
                        this.CommandStateNeedsRefresh?.Invoke(this, mappedActionParameter);
                    }
                }
            }
        }

        public bool GetToggleState(string actionParameter) => this._toggleStates.TryGetValue(actionParameter, out var s) && s;
        public void SetToggleState(string actionParameter, bool state)
        {
            this._toggleStates[actionParameter] = state;
            this.CommandStateNeedsRefresh?.Invoke(this, actionParameter); // 【修正】确保手动设置状态也刷新UI
        }
        public int GetDialMode(string actionParameter) => this._dialModes.TryGetValue(actionParameter, out var m) ? m : 0;
        public int GetParameterDialSelectedIndex(string actionParameter) => this._parameterDialSelectedIndexes.TryGetValue(actionParameter, out var index) ? index : 0;
        public string GetParameterDialSelectedTitle(string actionParameter)
        {
            var config = GetConfig(actionParameter);
            if (config?.ActionType == "ParameterDial" && config.Titles != null && config.Titles.Count > 0)
            {
                var index = GetParameterDialSelectedIndex(actionParameter);
                if (index >= 0 && index < config.Titles.Count)
                { return config.Titles[index]; }
            }
            return null;
        }

        public bool ProcessUserAction(string actionParameter, string dynamicFolderDisplayName = null)
        {
            var config = GetConfig(actionParameter);
            if (config == null)
            { PluginLog.Warning($"[LogicManager] ProcessUserAction: 未找到配置 for '{actionParameter}'"); return false; }

            bool needsUiRefresh = false;
            switch (config.ActionType)
            {
                case "ToggleButton":
                    SetToggleState(actionParameter, !GetToggleState(actionParameter)); // SetToggleState内部会调用CommandStateNeedsRefresh
                    // 【修正OSC地址规则】使用控件自身的JSON GroupName
                    string toggleOscAddress = DetermineOscAddressForAction(config, config.GroupName);
                    if (!string.IsNullOrEmpty(toggleOscAddress) && toggleOscAddress != "/")
                    { ReaOSCPlugin.SendOSCMessage(toggleOscAddress, GetToggleState(actionParameter) ? 1.0f : 0.0f); }
                    else
                    { PluginLog.Warning($"[LogicManager] ToggleButton '{actionParameter}' 无法确定有效OSC地址。"); }
                    break;
                case "TriggerButton":
                    // 【修正OSC地址规则】使用控件自身的JSON GroupName
                    string triggerOscAddress = DetermineOscAddressForAction(config, config.GroupName);
                    if (!string.IsNullOrEmpty(triggerOscAddress) && triggerOscAddress != "/")
                    {
                        ReaOSCPlugin.SendOSCMessage(triggerOscAddress, 1.0f);
                        PluginLog.Info($"[LogicManager] TriggerButton '{actionParameter}' SENT to '{triggerOscAddress}' (using JSON GroupName '{config.GroupName}')");
                    }
                    else
                    { PluginLog.Warning($"[LogicManager] TriggerButton '{actionParameter}' (JSON GroupName '{config.GroupName}') 无法确定有效OSC地址。"); }
                    this.CommandStateNeedsRefresh?.Invoke(this, actionParameter);
                    break;
                case "CombineButton":
                    // 【修正OSC地址规则】ProcessCombineButtonAction将使用combineButtonConfig.GroupName
                    // dynamicFolderDisplayName 参数仅用于日志或特定上下文，不再用于OSC路径或依赖查找
                    ProcessCombineButtonAction(config, dynamicFolderDisplayName);
                    this.CommandStateNeedsRefresh?.Invoke(this, actionParameter);
                    break;
                case "SelectModeButton":
                    this.ToggleMode(config.DisplayName);
                    needsUiRefresh = true;
                    break;
                default:
                    PluginLog.Warning($"[LogicManager] ProcessUserAction: 未处理的 ActionType '{config.ActionType}' for '{actionParameter}'");
                    break;
            }
            return needsUiRefresh;
        }

        public void ProcessGeneralButtonPress(ButtonConfig config, string actionParameterKey)
        {
            if (config == null)
            {
                config = GetConfig(actionParameterKey); // 如果传入的是key而非config对象
                if (config == null)
                {
                    PluginLog.Error($"[LogicManager] ProcessGeneralButtonPress: Config not found for key '{actionParameterKey}'.");
                    return;
                }
            }

            float valueToSend = 1f;
            if (config.ActionType == "ToggleButton")
            {
                SetToggleState(actionParameterKey, !GetToggleState(actionParameterKey)); // 使用actionParameterKey作为状态键
                valueToSend = GetToggleState(actionParameterKey) ? 1f : 0f;
            }
            // 【修正OSC地址规则】使用控件自身的JSON GroupName
            string oscAddressToSend = DetermineOscAddressForAction(config, config.GroupName);
            if (!string.IsNullOrEmpty(oscAddressToSend) && oscAddressToSend != "/")
            { ReaOSCPlugin.SendOSCMessage(oscAddressToSend, valueToSend); }
            else
            { PluginLog.Warning($"[LogicManager] GeneralButtonPress '{actionParameterKey}' (JSON GroupName '{config.GroupName}') 无法确定有效OSC地址。"); }
        }
        public void ProcessFxButtonPress(string actionParameter) => ReaOSCPlugin.SendFXMessage(actionParameter, 1);


        public void ProcessDialAdjustment(string globalActionParameter, int ticks)
        {
            var config = GetConfig(globalActionParameter);
            if (config == null)
            { PluginLog.Warning($"[LogicManager] ProcessDialAdjustment (New): Config not found for '{globalActionParameter}'"); return; }

            switch (config.ActionType)
            {
                case "ParameterDial":
                    if (config.Titles == null || config.Titles.Count == 0)
                        break;
                    var currentIndex = GetParameterDialSelectedIndex(globalActionParameter);
                    currentIndex += ticks;
                    if (currentIndex >= config.Titles.Count)
                        currentIndex = 0;
                    else if (currentIndex < 0)
                        currentIndex = config.Titles.Count - 1;
                    this._parameterDialSelectedIndexes[globalActionParameter] = currentIndex;
                    this.CommandStateNeedsRefresh?.Invoke(this, globalActionParameter);
                    NotifyLinkedParameterButtons(config.DisplayName, config.GroupName); // GroupName是JSON GroupName
                    break;
                // 【修正OSC地址规则】确保TickDial, 2ModeTickDial, ToggleDial的OSC地址基于JSON GroupName
                case "TickDial":
                case "2ModeTickDial":
                    // 这些类型通常在旧版ProcessDialAdjustment中处理OSC，
                    // 但如果新路径也可能调整它们，需要在这里发送OSC
                    // 调用ProcessLegacyDialAdjustmentInternal以复用其OSC发送逻辑，它已使用JSON GroupName
                    ProcessLegacyDialAdjustmentInternal(config, ticks, globalActionParameter, null); // lastEventTimes 可能需要传递或处理
                    break;
                case "ToggleDial":
                    ProcessLegacyToggleDialAdjustmentInternal(config, ticks, globalActionParameter);
                    break;
                default:
                    PluginLog.Warning($"[LogicManager] ProcessDialAdjustment (New): 未处理的 ActionType '{config.ActionType}' for '{globalActionParameter}'");
                    break;
            }
        }

        public void ProcessDialAdjustment(ButtonConfig config, int ticks, string actionParameter, Dictionary<string, DateTime> lastEventTimes)
        {
            if (config == null)
            { config = GetConfig(actionParameter); if (config == null) { PluginLog.Error($"[LogicManager] ProcessDialAdjustment (Legacy): Config is null and not found for '{actionParameter}'."); return; } }

            switch (config.ActionType)
            {
                case "TickDial":
                case "2ModeTickDial":
                    ProcessLegacyDialAdjustmentInternal(config, ticks, actionParameter, lastEventTimes);
                    break;
                case "ToggleDial":
                    ProcessLegacyToggleDialAdjustmentInternal(config, ticks, actionParameter);
                    break;
                case "ParameterDial":
                    this.ProcessDialAdjustment(actionParameter, ticks);
                    break;
                default:
                    PluginLog.Warning($"[LogicManager] ProcessDialAdjustment (Legacy): 未处理的 ActionType '{config.ActionType}' for '{actionParameter}'");
                    break;
            }
        }

        public bool ProcessDialPress(string globalActionParameter)
        {
            var config = GetConfig(globalActionParameter);
            if (config == null)
            { PluginLog.Warning($"[LogicManager] ProcessDialPress (New): Config not found for '{globalActionParameter}'"); return false; }

            bool uiShouldRefresh = false;
            switch (config.ActionType)
            {
                case "ParameterDial":
                    break;
                // 【修正OSC地址规则】确保2ModeTickDial, TickDial, ToggleDial的Reset OSC地址基于JSON GroupName
                case "2ModeTickDial":
                    this._dialModes[globalActionParameter] = (this.GetDialMode(globalActionParameter) + 1) % 2;
                    uiShouldRefresh = true;
                    this.CommandStateNeedsRefresh?.Invoke(this, globalActionParameter);
                    // 其ResetOSCAddress发送逻辑也应遵循规则
                    if (!string.IsNullOrEmpty(config.ResetOscAddress))
                    { SendResetOscForDial(config, globalActionParameter); }
                    break;
                case "TickDial":
                case "ToggleDial":
                    if (!string.IsNullOrEmpty(config.ResetOscAddress))
                    { SendResetOscForDial(config, globalActionParameter); }
                    break;
            }
            if (uiShouldRefresh && config.ActionType != "2ModeTickDial") // 2ModeTickDial 已在内部调用
                this.CommandStateNeedsRefresh?.Invoke(this, globalActionParameter);
            return uiShouldRefresh;
        }

        private void SendResetOscForDial(ButtonConfig config, string actionParameterKey)
        {
            // 【新增辅助方法】用于发送旋钮按下的Reset OSC消息，确保使用JSON GroupName
            string fullResetAddress = DetermineOscAddressForAction(config, config.GroupName, config.ResetOscAddress);
            if (!string.IsNullOrEmpty(fullResetAddress) && fullResetAddress != "/")
            { ReaOSCPlugin.SendOSCMessage(fullResetAddress, 1f); }
            else
            { PluginLog.Warning($"[LogicManager] Dial Reset '{actionParameterKey}' (JSON GroupName '{config.GroupName}') 生成的 OSC 地址无效。"); }
            this.CommandStateNeedsRefresh?.Invoke(this, actionParameterKey);
        }


        public bool ProcessDialPress(ButtonConfig config, string actionParameter)
        {
            if (config == null)
            { config = GetConfig(actionParameter); if (config == null) { PluginLog.Error($"[LogicManager] ProcessDialPress (Legacy): Config is null and not found for '{actionParameter}'."); return false; } }

            bool modeChanged = false;
            if (config.ActionType == "2ModeTickDial")
            {
                this._dialModes[actionParameter] = (this.GetDialMode(actionParameter) + 1) % 2;
                modeChanged = true;
                this.CommandStateNeedsRefresh?.Invoke(this, actionParameter);
            }
            // 【修正OSC地址规则】使用辅助方法或直接确保 DetermineOscAddressForAction 使用 config.GroupName
            if (!string.IsNullOrEmpty(config.ResetOscAddress) &&
                (config.ActionType == "TickDial" || config.ActionType == "ToggleDial" || config.ActionType == "2ModeTickDial"))
            {
                SendResetOscForDial(config, actionParameter);
            }
            else if (config.ActionType == "ParameterDial")
            {
                return this.ProcessDialPress(actionParameter);
            }
            return modeChanged;
        }

        private void ProcessCombineButtonAction(ButtonConfig combineButtonConfig, string dynamicFolderDisplayNameForLog) // dynamicFolderDisplayNameForLog 用于日志，不再用于OSC路径
        {
            if (combineButtonConfig.ParameterOrder == null || combineButtonConfig.ParameterOrder.Count == 0)
            { PluginLog.Warning($"[LogicManager] CombineButton '{combineButtonConfig.DisplayName}' (JSON Group: '{combineButtonConfig.GroupName}') has no ParameterOrder. Folder context: '{dynamicFolderDisplayNameForLog}'."); return; }

            // 【修正OSC地址规则】最终OSC地址的基路径使用CombineButton的JSON GroupName
            string oscMessageBasePath = SanitizeOscPathSegment(combineButtonConfig.GroupName);
            List<string> pathSegmentsForOsc = new List<string> { oscMessageBasePath };

            // 【修正依赖控件查找规则】依赖控件的actionParameter也是基于其JSON GroupName注册的。
            // CombineButton与其依赖控件在同一个_List.json中，共享相同的JSON GroupName。
            string lookupKeyBasePathForDependents = SanitizeOscPathSegment(combineButtonConfig.GroupName);

            foreach (var targetDisplayNameInOrder in combineButtonConfig.ParameterOrder)
            {
                string controlActionParameterKey = $"/{lookupKeyBasePathForDependents}/{SanitizeOscPathSegment(targetDisplayNameInOrder)}";
                string controlActionParameterKeyDial = controlActionParameterKey + "/DialAction";

                ButtonConfig targetConfig = GetConfig(controlActionParameterKey) ?? GetConfig(controlActionParameterKeyDial);

                if (targetConfig == null)
                {
                    // 【修正Fallback的比较】应该用 combineButtonConfig.GroupName (即 lookupKeyBasePathForDependents)
                    targetConfig = _allConfigs.Values.FirstOrDefault(c => c.GroupName == combineButtonConfig.GroupName && c.DisplayName == targetDisplayNameInOrder);
                    if (targetConfig == null)
                    {
                        PluginLog.Error($"[LogicManager] CombineButton '{combineButtonConfig.DisplayName}': Cannot find dependent control '{targetDisplayNameInOrder}' using key pattern '/{lookupKeyBasePathForDependents}/{SanitizeOscPathSegment(targetDisplayNameInOrder)}' or by direct Group/Display match. Folder context: '{dynamicFolderDisplayNameForLog}'.");
                        continue;
                    }
                }

                string originalSegmentValue = null;
                bool addSegment = false;
                // targetGlobalActionParam 就是我们用来成功找到 targetConfig 的键
                string targetGlobalActionParam = (GetConfig(controlActionParameterKey) != null) ? controlActionParameterKey : controlActionParameterKeyDial;
                // 如果是通过 fallback 找到的，需要重新确定其 globalActionParameter
                if (GetConfig(controlActionParameterKey) == null && GetConfig(controlActionParameterKeyDial) == null && targetConfig != null)
                {
                    targetGlobalActionParam = GetActionParameterFromConfig(targetConfig, targetConfig.GroupName); // 使用它自己的JSON GroupName
                }


                if (targetConfig.ActionType == "ParameterDial")
                {
                    originalSegmentValue = GetParameterDialSelectedTitle(targetGlobalActionParam);
                    addSegment = !string.IsNullOrEmpty(originalSegmentValue);
                }
                else if (targetConfig.ActionType == "ToggleButton")
                {
                    bool isToggleOn = GetToggleState(targetGlobalActionParam);
                    if (isToggleOn)
                    {
                        // ToggleButton贡献给CombineButton的路径片段：优先用OscAddress字段，否则用DisplayName
                        originalSegmentValue = !string.IsNullOrEmpty(targetConfig.OscAddress) ? targetConfig.OscAddress : targetConfig.DisplayName;
                        addSegment = true;
                    }
                }

                if (addSegment && !string.IsNullOrEmpty(originalSegmentValue))
                {
                    pathSegmentsForOsc.Add(SanitizeOscPathSegment(originalSegmentValue));
                }
            }

            if (pathSegmentsForOsc.Count > ((string.IsNullOrEmpty(oscMessageBasePath) || oscMessageBasePath == "/") ? 0 : 1))
            {
                string finalOscAddress = "/" + string.Join("/", pathSegmentsForOsc);
                finalOscAddress = finalOscAddress.Replace("//", "/");
                if (finalOscAddress == "/" && pathSegmentsForOsc.Count == 1 && (string.IsNullOrEmpty(pathSegmentsForOsc[0]) || pathSegmentsForOsc[0] == "/"))
                {
                    PluginLog.Warning($"[LogicManager] CombineButton '{combineButtonConfig.DisplayName}': Generated OSC address was empty or just '/'. OSC not sent.");
                }
                else
                {
                    ReaOSCPlugin.SendOSCMessage(finalOscAddress, 1.0f);
                    PluginLog.Info($"[LogicManager] CombineButton '{combineButtonConfig.DisplayName}' SENT to '{finalOscAddress}'");
                }
            }
            else
            {
                PluginLog.Warning($"[LogicManager] CombineButton '{combineButtonConfig.DisplayName}': No valid segments to send OSC message. Base path was '{oscMessageBasePath}'. OSC not sent.");
            }
        }

        // GetActionParameterFromConfig: 构建actionParameter时，传入的folderGroupName应为控件的JSON GroupName
        private string GetActionParameterFromConfig(ButtonConfig config, string jsonGroupName)
        {
            string groupPart = SanitizeOscPathSegment(jsonGroupName); // 参数重命名为jsonGroupName
            string namePart = SanitizeOscPathSegment(config.DisplayName);
            string key = $"/{groupPart}/{namePart}";
            if (config.ActionType != null && config.ActionType.Contains("Dial"))
                key += "/DialAction";
            return key.Replace("//", "/");
        }

        private void NotifyLinkedParameterButtons(string sourceDialDisplayName, string sourceDialJsonGroupName) // 参数改为sourceDialJsonGroupName
        {
            foreach (var kvp in this._allConfigs)
            {
                var config = kvp.Value;
                // ParameterButton的GroupName也应是其JSON中定义的GroupName
                if (config.ActionType == "ParameterButton" && config.GroupName == sourceDialJsonGroupName && config.ParameterSourceDial == sourceDialDisplayName)
                {
                    this.CommandStateNeedsRefresh?.Invoke(this, kvp.Key);
                }
            }
        }

        #endregion

        #region OSC地址和路径片段处理辅助方法
        public static string SanitizeOscPathSegment(string segment) { if (string.IsNullOrEmpty(segment)) return ""; var sanitized = segment.Replace(" ", "_"); sanitized = Regex.Replace(sanitized, @"[^\w_/-]", ""); return sanitized.Trim('/'); }
        public static string SanitizeOscAddress(string address) { if (string.IsNullOrEmpty(address)) return "/"; var sanitized = address.Replace(" ", "_"); sanitized = Regex.Replace(sanitized, @"[^\w_/-]", ""); if (!sanitized.StartsWith("/")) sanitized = "/" + sanitized; return sanitized.Replace("//", "/"); }

        // 【修正OSC地址规则】DetermineOscAddressForAction的第二个参数明确为jsonGroupNameForOsc
        private string DetermineOscAddressForAction(ButtonConfig config, string jsonGroupNameForOsc, string explicitOscAddressField = null)
        {
            string basePath = SanitizeOscPathSegment(jsonGroupNameForOsc);
            string actionPath;

            if (!string.IsNullOrEmpty(explicitOscAddressField))
            { actionPath = explicitOscAddressField; } // explicitOscAddressField 可能已经是完整路径或片段
            else if (!string.IsNullOrEmpty(config.OscAddress))
            { actionPath = config.OscAddress; } // config.OscAddress 可能是完整路径或片段
            else
            { actionPath = config.DisplayName; }

            // Sanitize actionPath only if it's not meant to be an absolute path override
            // If actionPath starts with '/', treat it as an absolute path that overrides basePath.
            var sanitizedActionPath = SanitizeOscPathSegment(actionPath); // Sanitize after check

            if (actionPath.StartsWith("/"))
            {
                return SanitizeOscAddress(actionPath); // actionPath已经是完整路径, 清理并返回
            }

            if (string.IsNullOrEmpty(basePath) || basePath == "/") // 如果 basePath 为空或仅为根
            {
                return $"/{sanitizedActionPath}".Replace("//", "/");
            }
            return $"/{basePath}/{sanitizedActionPath}".Replace("//", "/");
        }
        #endregion

        #region 旧的旋钮处理逻辑 (内部实现) - 这些方法已经使用config.GroupName，符合规则
        private void ProcessLegacyDialAdjustmentInternal(ButtonConfig config, int ticks, string actionParameter, Dictionary<string, DateTime> lastEventTimes)
        {
            string groupNameForPath = SanitizeOscPathSegment(config.GroupName); // 使用JSON GroupName
            string displayNameForPath = SanitizeOscPathSegment(config.DisplayName);

            string jsonIncreaseAddress, jsonDecreaseAddress;
            if (config.ActionType == "2ModeTickDial" && this.GetDialMode(actionParameter) == 1)
            { jsonIncreaseAddress = config.IncreaseOSCAddress_Mode2; jsonDecreaseAddress = config.DecreaseOSCAddress_Mode2; }
            else
            { jsonIncreaseAddress = config.IncreaseOSCAddress; jsonDecreaseAddress = config.DecreaseOSCAddress; }

            string finalIncreaseOscAddress, finalDecreaseOscAddress;
            if (!string.IsNullOrEmpty(jsonIncreaseAddress))
            { finalIncreaseOscAddress = DetermineOscAddressForAction(config, config.GroupName, jsonIncreaseAddress); } // 使用JSON GroupName
            else
            { finalIncreaseOscAddress = $"/{groupNameForPath}/{displayNameForPath}/Right".Replace("//", "/"); }
            if (!string.IsNullOrEmpty(jsonDecreaseAddress))
            { finalDecreaseOscAddress = DetermineOscAddressForAction(config, config.GroupName, jsonDecreaseAddress); } // 使用JSON GroupName
            else
            { finalDecreaseOscAddress = $"/{groupNameForPath}/{displayNameForPath}/Left".Replace("//", "/"); }

            float acceleration = 1.0f;
            if (lastEventTimes != null && config.AccelerationFactor.HasValue && lastEventTimes.TryGetValue(actionParameter, out var lastTime))
            { var timeDiff = (float)(DateTime.Now - lastTime).TotalMilliseconds; if (timeDiff < 100) { acceleration = config.AccelerationFactor.Value; } }

            string targetAddress = ticks > 0 ? finalIncreaseOscAddress : finalDecreaseOscAddress;
            if (!string.IsNullOrEmpty(targetAddress) && targetAddress != "/")
            { ReaOSCPlugin.SendOSCMessage(targetAddress, Math.Abs(ticks) * acceleration); }
            else
            { PluginLog.Warning($"[LogicManager] LegacyDial '{actionParameter}' 最终 OSC 地址无效: '{targetAddress}'"); }
        }

        private void ProcessLegacyToggleDialAdjustmentInternal(ButtonConfig config, int ticks, string actionParameter)
        {
            string toggleDialAddress = DetermineOscAddressForAction(config, config.GroupName); // 使用JSON GroupName
            if (string.IsNullOrEmpty(toggleDialAddress) || toggleDialAddress == "/")
            { PluginLog.Warning($"[LogicManager] LegacyToggleDial '{actionParameter}' 生成的 OSC 地址无效。"); return; }
            float valueToSend = (ticks > 0) ? 1.0f : 0.0f;
            ReaOSCPlugin.SendOSCMessage(toggleDialAddress, valueToSend);
        }
        #endregion

        public void Dispose()
        {
            OSCStateManager.Instance.StateChanged -= this.OnOSCStateChanged;
            if (this.CommandStateNeedsRefresh != null)
            { foreach (Delegate d in this.CommandStateNeedsRefresh.GetInvocationList()) { this.CommandStateNeedsRefresh -= (EventHandler<string>)d; } }
            this._allConfigs.Clear();
            this._toggleStates.Clear();
            this._dialModes.Clear();
            this._parameterDialSelectedIndexes.Clear();
            this._oscAddressToActionParameterMap.Clear();
            this._modeOptions.Clear();
            this._currentModes.Clear();
            this._modeChangedEvents.Clear();
            this._isInitialized = false;
            PluginLog.Info("[LogicManager] Disposed.");
        }
    }
}