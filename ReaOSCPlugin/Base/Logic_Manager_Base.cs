// 文件名: Base/Logic_Manager_Base.cs
namespace Loupedeck.ReaOSCPlugin.Base
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;

    using Newtonsoft.Json;

    // 定义了动态文件夹内容配置的结构，包含按钮和旋钮列表
    public class FolderContentConfig
    {
        public List<ButtonConfig> Buttons { get; set; } = new List<ButtonConfig>();
        public List<ButtonConfig> Dials { get; set; } = new List<ButtonConfig>();
    }

    // 逻辑管理器基类，单例模式，负责加载配置、管理状态和处理核心逻辑
    public class Logic_Manager_Base : IDisposable
    {
        // 单例实例
        private static readonly Lazy<Logic_Manager_Base> _instance = new Lazy<Logic_Manager_Base>(() => new Logic_Manager_Base());
        public static Logic_Manager_Base Instance => _instance.Value;

        // 存储所有加载的按钮/旋钮配置，键为 actionParameter
        private readonly Dictionary<string, ButtonConfig> _allConfigs = new Dictionary<string, ButtonConfig>();
        // 存储 ToggleButton 和 ToggleDial 的开关状态
        private readonly Dictionary<string, bool> _toggleStates = new Dictionary<string, bool>();
        // 存储 2ModeTickDial 的当前模式 (0 或 1)
        private readonly Dictionary<string, int> _dialModes = new Dictionary<string, int>();
        // 初始化标记，防止重复初始化
        private bool _isInitialized = false;

        // --- 模式管理核心数据结构 ---
        // 存储每个模式组 (modeName) 及其包含的所有模式选项 (List<string>)
        private readonly Dictionary<string, List<string>> _modeOptions = new Dictionary<string, List<string>>();
        // 存储每个模式组当前激活的模式索引
        private readonly Dictionary<string, int> _currentModes = new Dictionary<string, int>();
        // 存储每个模式组状态变更时需要触发的回调事件
        private readonly Dictionary<string, Action> _modeChangedEvents = new Dictionary<string, Action>();

        // 私有构造函数，确保单例
        private Logic_Manager_Base() { }

        // 初始化管理器，加载所有配置并订阅OSC状态变化
        public void Initialize()
        {
            if (this._isInitialized) // 如果已初始化，则直接返回
                return;
            PluginLog.Info("[LogicManager] 开始初始化...");
            this.LoadAllConfigs(); // 加载所有JSON配置文件
            OSCStateManager.Instance.StateChanged += this.OnOSCStateChanged; // 订阅OSC状态变化事件
            this._isInitialized = true;
            PluginLog.Info($"[LogicManager] 初始化成功。加载了 {_allConfigs.Count} 个配置项。");
        }

        #region 模式管理 (完整)

        // 注册一个模式组，通常由一个 ActionType 为 "SelectModeButton" 的按钮调用
        public void RegisterModeGroup(ButtonConfig config)
        {
            var modeName = config.DisplayName; // 使用按钮的 DisplayName 作为模式组的唯一名称
            var modes = config.Modes;          // 从配置中获取该模式组包含的所有模式字符串列表

            if (string.IsNullOrEmpty(modeName) || modes == null || modes.Count == 0) // 基本校验
                return;

            if (!_modeOptions.ContainsKey(modeName)) // 如果该模式组尚未注册
            {
                _modeOptions[modeName] = modes;         // 存储模式选项
                _currentModes[modeName] = 0;            // 默认激活第一个模式 (索引0)
                _modeChangedEvents[modeName] = null;    // 初始化事件委托
                PluginLog.Info($"[LogicManager] 模式组 '{modeName}' 已注册，包含模式: {string.Join(", ", modes)}");
            }
        }

        // 切换指定模式组的当前模式到下一个模式
        public void ToggleMode(string modeName)
        {
            if (_currentModes.TryGetValue(modeName, out _) && _modeOptions.TryGetValue(modeName, out var options))
            {
                // 模式索引在选项列表内循环 (加1后对总数取模)
                _currentModes[modeName] = (_currentModes[modeName] + 1) % options.Count;
                _modeChangedEvents[modeName]?.Invoke(); // 触发已订阅的模式变更事件
                PluginLog.Info($"[LogicManager] 模式组 '{modeName}' 已切换到: {GetCurrentModeString(modeName)}");
            }
        }

        // 获取指定模式组当前激活模式的字符串名称
        public string GetCurrentModeString(string modeName)
        {
            if (_currentModes.TryGetValue(modeName, out var currentIndex) &&
                _modeOptions.TryGetValue(modeName, out var options) &&
                currentIndex >= 0 && currentIndex < options.Count) // 确保索引有效
            {
                return options[currentIndex];
            }
            return string.Empty; // 如果模式组无效或索引越界，返回空字符串
        }

        // 【新增】获取指定模式组当前激活模式的索引
        public int GetCurrentModeIndex(string modeName)
        {
            // 如果模式组存在，则返回当前索引，否则返回-1表示无效
            return _currentModes.TryGetValue(modeName, out var currentIndex) ? currentIndex : -1;
        }

        // 订阅指定模式组的状态变更事件
        public void SubscribeToModeChange(string modeName, Action handler)
        {
            if (string.IsNullOrEmpty(modeName) || handler == null) // 基本校验
                return;
            if (!_modeChangedEvents.ContainsKey(modeName)) // 如果事件字典中尚无此模式组的条目
                _modeChangedEvents[modeName] = null;     // 则初始化为空委托

            _modeChangedEvents[modeName] += handler; // 添加事件处理器
        }

        // 取消订阅指定模式组的状态变更事件
        public void UnsubscribeFromModeChange(string modeName, Action handler)
        {
            if (string.IsNullOrEmpty(modeName) || handler == null) // 基本校验
                return;
            if (_modeChangedEvents.ContainsKey(modeName)) // 如果事件字典中存在此模式组的条目
            {
                _modeChangedEvents[modeName] -= handler; // 移除事件处理器
            }
        }
        #endregion

        #region 配置加载 (完整)

        // 加载所有类型的配置文件
        private void LoadAllConfigs()
        {
            var assembly = Assembly.GetExecutingAssembly(); // 获取当前执行的程序集

            // 1. 加载通用配置 (General_List.json)
            var generalConfigs = this.LoadAndDeserialize<Dictionary<string, List<ButtonConfig>>>(assembly, "Loupedeck.ReaOSCPlugin.General.General_List.json");
            this.ProcessGroupedConfigs(generalConfigs, isFx: false);

            // 2. 加载效果器配置 (Effects_List.json)
            var effectsConfigs = this.LoadAndDeserialize<Dictionary<string, List<ButtonConfig>>>(assembly, "Loupedeck.ReaOSCPlugin.Effects.Effects_List.json");
            this.ProcessGroupedConfigs(effectsConfigs, isFx: true); // 标记为FX配置

            // 3. 加载所有动态文件夹的内容配置 (*_List.json, 但排除 Dynamic_List.json 本身)
            var resourceNames = assembly.GetManifestResourceNames(); // 获取所有嵌入资源的名称
            var dynamicContentResources = resourceNames.Where(r =>
                r.StartsWith("Loupedeck.ReaOSCPlugin.Dynamic.") &&
                r.EndsWith("_List.json") &&
                !r.EndsWith("Dynamic_List.json"));

            foreach (var resourceName in dynamicContentResources)
            {
                var folderContent = this.LoadAndDeserialize<FolderContentConfig>(assembly, resourceName);
                this.ProcessFolderContentConfigs(folderContent);
            }

            // 4. 加载动态文件夹入口列表 (Dynamic_List.json)
            var dynamicFolderEntries = this.LoadAndDeserialize<List<ButtonConfig>>(assembly, "Loupedeck.ReaOSCPlugin.Dynamic.Dynamic_List.json");
            if (dynamicFolderEntries != null)
            {
                // 为动态文件夹入口设置固定的 GroupName
                foreach (var entry in dynamicFolderEntries)
                { entry.GroupName = "ReaOSC Dynamic"; }
                this.RegisterConfigs(dynamicFolderEntries, isFx: false);
            }
        }

        // 从嵌入资源加载并反序列化JSON数据
        private T LoadAndDeserialize<T>(Assembly assembly, string resourceName) where T : class
        {
            try
            {
                using (var stream = assembly.GetManifestResourceStream(resourceName)) // 打开资源流
                {
                    if (stream == null)
                    { PluginLog.Error($"[LogicManager] 无法找到嵌入资源: {resourceName}"); return null; }
                    using (var reader = new StreamReader(stream)) // 读取流
                    { return JsonConvert.DeserializeObject<T>(reader.ReadToEnd()); } // 反序列化JSON
                }
            }
            catch (Exception ex)
            { PluginLog.Error(ex, $"[LogicManager] 读取或解析资源 '{resourceName}' 失败。"); return null; }
        }

        // 处理分组的配置 (例如 General_List.json, Effects_List.json 的结构)
        private void ProcessGroupedConfigs(Dictionary<string, List<ButtonConfig>> groupedConfigs, bool isFx)
        {
            if (groupedConfigs == null)
                return;
            foreach (var group in groupedConfigs) // 遍历每个组 (例如 "Edit", "MIDI")
            {
                // 将组名 (group.Key) 赋值给该组下所有配置项的 GroupName 属性
                var configs = group.Value.Select(config => { config.GroupName = group.Key; return config; }).ToList();
                this.RegisterConfigs(configs, isFx); // 注册这些配置项
            }
        }

        // 处理动态文件夹内容的配置 (例如 Add_Track_List.json)
        private void ProcessFolderContentConfigs(FolderContentConfig folderContent)
        {
            if (folderContent == null)
                return;
            this.RegisterConfigs(folderContent.Buttons, isFx: false); // 注册文件夹内的按钮
            this.RegisterConfigs(folderContent.Dials, isFx: false);   // 注册文件夹内的旋钮
        }

        // 核心的配置注册方法，为每个配置项生成唯一的 actionParameter 并存储
        private void RegisterConfigs(List<ButtonConfig> configs, bool isFx)
        {
            if (configs == null)
                return;
            foreach (var config in configs)
            {
                if (string.IsNullOrEmpty(config.GroupName)) // GroupName 是必需的
                {
                    PluginLog.Warning($"[LogicManager] 配置 '{config.DisplayName}' 缺少 GroupName，已跳过。");
                    continue;
                }

                // 根据 GroupName 和 DisplayName/OscAddress 构建 actionParameter (插件内部的唯一动作标识)
                string groupNameForPath = config.GroupName.Replace(" FX", "").Replace(" ", "/"); // 将 GroupName 转换为路径格式
                string actionParameter;

                if (isFx) // 特殊处理效果器 (FX) 类型的配置
                {
                    actionParameter = $"Add/{groupNameForPath}/{config.DisplayName}";
                }
                else // 处理通用配置
                {
                    // 优先使用 OscAddress (如果提供)，否则使用 DisplayName 作为基础OSC路径部分
                    string baseOscName = String.IsNullOrEmpty(config.OscAddress) ? config.DisplayName.Replace(" ", "/") : config.OscAddress.Replace(" ", "/");
                    actionParameter = $"/{groupNameForPath}/{baseOscName}".Replace("//", "/"); // 替换可能出现的双斜杠

                    // 为不同类型的旋钮动作添加唯一后缀，避免与按钮的 actionParameter 冲突
                    if (config.ActionType == "TickDial" || config.ActionType == "ToggleDial")
                        actionParameter += "/DialAction";
                    if (config.ActionType == "2ModeTickDial")
                        actionParameter += "/2ModeTickDialAction";
                }

                if (this._allConfigs.ContainsKey(actionParameter)) // 如果已存在相同的 actionParameter，则跳过 (避免重复注册)
                    continue;

                this._allConfigs[actionParameter] = config; // 存储配置

                // 初始化状态
                if (config.ActionType == "ToggleButton" || config.ActionType == "ToggleDial")
                {
                    // 对于开关类按钮/旋钮，从 OSCStateManager 获取初始状态
                    this._toggleStates[actionParameter] = OSCStateManager.Instance.GetState(actionParameter) > 0.5f;
                }
                else if (config.ActionType == "2ModeTickDial")
                {
                    this._dialModes[actionParameter] = 0; // 双模式旋钮默认处于模式0
                }
            }
        }
        #endregion

        #region 公共访问与核心逻辑 (完整)

        // 获取所有已加载的配置 (只读字典)
        public IReadOnlyDictionary<string, ButtonConfig> GetAllConfigs() => this._allConfigs;

        // 根据 actionParameter 获取单个配置
        public ButtonConfig GetConfig(string actionParameter) => this._allConfigs.TryGetValue(actionParameter, out var c) ? c : null;

        // 根据 GroupName 和 DisplayName 获取单个配置 (用于动态文件夹入口查找等)
        public ButtonConfig GetConfigByDisplayName(string groupName, string displayName) =>
            this._allConfigs.Values.FirstOrDefault(c =>
                c.GroupName.Equals(groupName, StringComparison.OrdinalIgnoreCase) &&
                c.DisplayName.Equals(displayName, StringComparison.OrdinalIgnoreCase));

        // OSC状态变化事件处理器
        private void OnOSCStateChanged(object sender, OSCStateManager.StateChangedEventArgs e)
        {
            // 如果变化的OSC地址对应一个已注册的ToggleButton或ToggleDial
            if (this._allConfigs.TryGetValue(e.Address, out var config) &&
                (config.ActionType == "ToggleButton" || config.ActionType == "ToggleDial"))
            {
                var newState = e.Value > 0.5f; // 将float值转换为布尔状态
                // 如果状态字典中尚无此地址，或新状态与旧状态不同，则更新
                if (!this._toggleStates.ContainsKey(e.Address) || this._toggleStates[e.Address] != newState)
                {
                    this._toggleStates[e.Address] = newState;
                    // 注意：这里只更新内部状态，UI的更新由具体的按钮/旋钮类通过订阅此事件或模式事件来处理
                }
            }
        }

        // 获取指定 actionParameter 的开关状态
        public bool GetToggleState(string actionParameter) => this._toggleStates.TryGetValue(actionParameter, out var s) && s;

        // 设置指定 actionParameter 的开关状态 (通常由按钮自身逻辑调用)
        public void SetToggleState(string actionParameter, bool state) => this._toggleStates[actionParameter] = state;

        // 获取指定 actionParameter 的双模式旋钮的当前模式
        public int GetDialMode(string actionParameter) => this._dialModes.TryGetValue(actionParameter, out var m) ? m : 0;

        // 处理通用按钮按下逻辑 (由 Dynamic_Folder_Base 调用)
        public void ProcessGeneralButtonPress(ButtonConfig config, string actionParameter)
        {
            if (config == null)
                return;

            float valueToSend = 1f; // 默认为触发值
            if (config.ActionType == "ToggleButton") // 如果是开关按钮
            {
                valueToSend = this.GetToggleState(actionParameter) ? 0f : 1f; // 发送反转后的状态值
                // 注意：ToggleButton 的状态由 General_Button_Base 中的 RunCommand 直接管理并调用 SetToggleState
            }
            // 注意: General_Button_Base 中的 RunCommand 会自己调用 SendOSCMessage 和 SetToggleState,
            // 此处 ProcessGeneralButtonPress 主要是给 Dynamic_Folder_Base 一个统一的调用点，
            // 但实际OSC发送和状态切换应由更具体的类或此方法完成，需审视是否存在重复逻辑。
            // 当前 Dynamic_Folder_Base 的 RunCommand 对于非Dial的按钮，会调用此方法，
            // 而 General_Button_Base 的 RunCommand 直接处理。
            // 为了避免混淆，Dynamic_Folder_Base 中的 ProcessGeneralButtonPress 应该直接调用相应 actionParameter 的 RunCommand,
            // 或者这里的逻辑需要更明确其职责。
            // 【审视点】当前设计下，此方法可能与 General_Button_Base.RunCommand 有逻辑重叠。
            // 若 Dynamic_Folder_Base 期望 Logic_Manager 统一处理OSC发送，则 General_Button_Base.RunCommand 不应再发。
            // 若 General_Button_Base 自行处理，则此方法对于 ToggleButton 的 valueToSend 计算是多余的，因为状态已在 General_Button_Base 中切换。
            // 暂时保留，假设 Dynamic_Folder_Base 中的按钮可能不通过 General_Button_Base 实例。
            ReaOSCPlugin.SendOSCMessage(actionParameter, valueToSend);
        }

        // 处理FX按钮按下逻辑 (由 Effects_Button_Base 调用)
        public void ProcessFxButtonPress(string actionParameter) => ReaOSCPlugin.SendFXMessage(actionParameter, 1); // 发送固定值1表示触发

        // 处理旋钮调整逻辑 (由 General_Dial_Base 和 Dynamic_Folder_Base 调用)
        public void ProcessDialAdjustment(ButtonConfig config, int ticks, string actionParameter, Dictionary<string, DateTime> lastEventTimes)
        {
            if (config == null)
                return;

            // 根据旋钮类型和当前模式确定 OSC 地址
            var address = (config.ActionType == "2ModeTickDial" && this.GetDialMode(actionParameter) == 1)
                ? config.IncreaseOSCAddress_Mode2 // 双模式旋钮的模式2增加地址
                : config.IncreaseOSCAddress;     // 普通或双模式旋钮的模式1增加地址

            var oppositeAddress = (config.ActionType == "2ModeTickDial" && this.GetDialMode(actionParameter) == 1)
                ? config.DecreaseOSCAddress_Mode2 // 双模式旋钮的模式2减少地址
                : config.DecreaseOSCAddress;     // 普通或双模式旋钮的模式1减少地址

            float acceleration = 1.0f; // 默认加速度
            // 如果配置了加速度因子，并且 lastEventTimes 中有记录 (由调用方传入和管理)
            if (config.AccelerationFactor.HasValue && lastEventTimes.TryGetValue(actionParameter, out var lastTime))
            {
                var timeDiff = (float)(DateTime.Now - lastTime).TotalMilliseconds; // 计算与上次事件的时间差
                if (timeDiff < 100) // 如果时间差小于100ms (快速转动)
                {
                    acceleration = config.AccelerationFactor.Value; // 应用加速度因子
                }
            }
            lastEventTimes[actionParameter] = DateTime.Now; // 更新本次事件的时间戳

            string targetAddress = ticks > 0 ? address : oppositeAddress; // 根据调整方向选择地址
            if (!string.IsNullOrEmpty(targetAddress))
            {
                // 发送OSC消息，值为 tick 数乘以加速度 (确保值为正)
                ReaOSCPlugin.SendOSCMessage(targetAddress, Math.Abs(ticks) * acceleration);
            }
        }

        // 处理旋钮按下逻辑 (由 General_Dial_Base 和 Dynamic_Folder_Base 调用)
        // 返回值: bool 表示是否因为此操作导致了需要重绘的模式切换
        public bool ProcessDialPress(ButtonConfig config, string actionParameter)
        {
            if (config.ActionType == "2ModeTickDial") // 如果是双模式旋钮
            {
                // 切换模式 (0 -> 1, 1 -> 0)
                this._dialModes[actionParameter] = (this._dialModes.TryGetValue(actionParameter, out var mode) ? mode : 0) + 1 % 2;
                return true; // 模式已改变，通知调用方可能需要重绘
            }
            if (!string.IsNullOrEmpty(config.ResetOscAddress)) // 如果配置了重置地址
            {
                // 构建重置 OSC 地址，注意去除 actionParameter 可能带的后缀如 "/DialAction"
                string pathPrefix = actionParameter;
                if (actionParameter.Contains("/DialAction"))
                    pathPrefix = actionParameter.Substring(0, actionParameter.LastIndexOf("/DialAction", StringComparison.Ordinal));
                else if (actionParameter.Contains("/2ModeTickDialAction"))
                    pathPrefix = actionParameter.Substring(0, actionParameter.LastIndexOf("/2ModeTickDialAction", StringComparison.Ordinal));

                // 拼接并发送重置消息，确保 ResetOscAddress 的前导 '/' 被正确处理
                ReaOSCPlugin.SendOSCMessage(pathPrefix + "/" + config.ResetOscAddress.TrimStart('/'), 1f);
            }
            return false; // 对于非双模式旋钮的按下或无重置地址的情况，不认为导致了需要重绘的模式切换
        }
        #endregion

        // 释放资源，主要是取消订阅事件
        public void Dispose()
        {
            OSCStateManager.Instance.StateChanged -= this.OnOSCStateChanged;
        }
    }
}