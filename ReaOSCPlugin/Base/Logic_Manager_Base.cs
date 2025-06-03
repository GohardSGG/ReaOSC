// 文件名: Base/Logic_Manager_Base.cs
namespace Loupedeck.ReaOSCPlugin.Base
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;

    using Newtonsoft.Json;

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
        private bool _isInitialized = false;

        private readonly Dictionary<string, List<string>> _modeOptions = new Dictionary<string, List<string>>();
        private readonly Dictionary<string, int> _currentModes = new Dictionary<string, int>();
        private readonly Dictionary<string, Action> _modeChangedEvents = new Dictionary<string, Action>();

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

        #region 模式管理 (完整)
        public void RegisterModeGroup(ButtonConfig config)
        {
            var modeName = config.DisplayName;
            var modes = config.Modes;
            if (string.IsNullOrEmpty(modeName) || modes == null || modes.Count == 0)
                return;
            if (!_modeOptions.ContainsKey(modeName))
            {
                _modeOptions[modeName] = modes;
                _currentModes[modeName] = 0;
                _modeChangedEvents[modeName] = null;
                PluginLog.Info($"[LogicManager] 模式组 '{modeName}' 已注册，包含模式: {string.Join(", ", modes)}");
            }
        }

        public void ToggleMode(string modeName)
        {
            if (_currentModes.TryGetValue(modeName, out _) && _modeOptions.TryGetValue(modeName, out var options))
            {
                _currentModes[modeName] = (_currentModes[modeName] + 1) % options.Count;
                _modeChangedEvents[modeName]?.Invoke();
                PluginLog.Info($"[LogicManager] 模式组 '{modeName}' 已切换到: {GetCurrentModeString(modeName)}");
            }
        }

        public string GetCurrentModeString(string modeName)
        {
            if (_currentModes.TryGetValue(modeName, out var currentIndex) && _modeOptions.TryGetValue(modeName, out var options) && currentIndex >= 0 && currentIndex < options.Count)
            {
                return options[currentIndex];
            }
            return string.Empty;
        }

        public int GetCurrentModeIndex(string modeName)
        {
            return _currentModes.TryGetValue(modeName, out var currentIndex) ? currentIndex : -1;
        }

        public void SubscribeToModeChange(string modeName, Action handler)
        {
            if (string.IsNullOrEmpty(modeName) || handler == null)
                return;
            if (!_modeChangedEvents.ContainsKey(modeName))
                _modeChangedEvents[modeName] = null;
            _modeChangedEvents[modeName] += handler;
        }

        public void UnsubscribeFromModeChange(string modeName, Action handler)
        {
            if (string.IsNullOrEmpty(modeName) || handler == null)
                return;
            if (_modeChangedEvents.ContainsKey(modeName))
            {
                _modeChangedEvents[modeName] -= handler;
            }
        }
        #endregion

        #region 配置加载 (完整)
        private void LoadAllConfigs()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var generalConfigs = this.LoadAndDeserialize<Dictionary<string, List<ButtonConfig>>>(assembly, "Loupedeck.ReaOSCPlugin.General.General_List.json");
            this.ProcessGroupedConfigs(generalConfigs, isFx: false);
            var effectsConfigs = this.LoadAndDeserialize<Dictionary<string, List<ButtonConfig>>>(assembly, "Loupedeck.ReaOSCPlugin.Effects.Effects_List.json");
            this.ProcessGroupedConfigs(effectsConfigs, isFx: true);
            var resourceNames = assembly.GetManifestResourceNames();
            var dynamicContentResources = resourceNames.Where(r => r.StartsWith("Loupedeck.ReaOSCPlugin.Dynamic.") && r.EndsWith("_List.json") && !r.EndsWith("Dynamic_List.json"));
            foreach (var resourceName in dynamicContentResources)
            {
                var folderContent = this.LoadAndDeserialize<FolderContentConfig>(assembly, resourceName);
                this.ProcessFolderContentConfigs(folderContent);
            }
            var dynamicFolderEntries = this.LoadAndDeserialize<List<ButtonConfig>>(assembly, "Loupedeck.ReaOSCPlugin.Dynamic.Dynamic_List.json");
            if (dynamicFolderEntries != null)
            {
                foreach (var entry in dynamicFolderEntries)
                { entry.GroupName = "ReaOSC Dynamic"; }
                this.RegisterConfigs(dynamicFolderEntries, isFx: false);
            }
        }

        private T LoadAndDeserialize<T>(Assembly assembly, string resourceName) where T : class
        {
            try
            {
                using (var stream = assembly.GetManifestResourceStream(resourceName))
                {
                    if (stream == null)
                    { PluginLog.Error($"[LogicManager] 无法找到嵌入资源: {resourceName}"); return null; }
                    using (var reader = new StreamReader(stream))
                    { return JsonConvert.DeserializeObject<T>(reader.ReadToEnd()); }
                }
            }
            catch (Exception ex) { PluginLog.Error(ex, $"[LogicManager] 读取或解析资源 '{resourceName}' 失败。"); return null; }
        }

        private void ProcessGroupedConfigs(Dictionary<string, List<ButtonConfig>> groupedConfigs, bool isFx)
        {
            if (groupedConfigs == null)
                return;
            foreach (var group in groupedConfigs)
            {
                var configs = group.Value.Select(config => { config.GroupName = group.Key; return config; }).ToList();
                this.RegisterConfigs(configs, isFx);
            }
        }

        private void ProcessFolderContentConfigs(FolderContentConfig folderContent)
        {
            if (folderContent == null)
                return;
            this.RegisterConfigs(folderContent.Buttons, isFx: false);
            this.RegisterConfigs(folderContent.Dials, isFx: false);
        }

        private void RegisterConfigs(List<ButtonConfig> configs, bool isFx)
        {
            if (configs == null)
                return;
            foreach (var config in configs)
            {
                if (string.IsNullOrEmpty(config.GroupName))
                {
                    PluginLog.Warning($"[LogicManager] 配置 '{config.DisplayName}' 缺少 GroupName，已跳过。");
                    continue;
                }
                string groupNameForPath = config.GroupName.Replace(" FX", "").Replace(" ", "/");
                string actionParameter;
                if (isFx)
                {
                    actionParameter = $"Add/{groupNameForPath}/{config.DisplayName}";
                }
                else
                {
                    string baseOscName = String.IsNullOrEmpty(config.OscAddress) ? config.DisplayName.Replace(" ", "/") : config.OscAddress.Replace(" ", "/");
                    actionParameter = $"/{groupNameForPath}/{baseOscName}".Replace("//", "/");
                    if (config.ActionType == "TickDial" || config.ActionType == "ToggleDial")
                        actionParameter += "/DialAction";
                    if (config.ActionType == "2ModeTickDial")
                        actionParameter += "/2ModeTickDialAction";
                }
                if (this._allConfigs.ContainsKey(actionParameter))
                    continue;

                this._allConfigs[actionParameter] = config;
                if (config.ActionType == "ToggleButton" || config.ActionType == "ToggleDial")
                {
                    this._toggleStates[actionParameter] = OSCStateManager.Instance.GetState(actionParameter) > 0.5f;
                }
                else if (config.ActionType == "2ModeTickDial")
                {
                    this._dialModes[actionParameter] = 0;
                }
            }
        }
        #endregion

        #region 公共访问与核心逻辑 (完整)
        public IReadOnlyDictionary<string, ButtonConfig> GetAllConfigs() => this._allConfigs;
        public ButtonConfig GetConfig(string actionParameter) => this._allConfigs.TryGetValue(actionParameter, out var c) ? c : null;
        public ButtonConfig GetConfigByDisplayName(string groupName, string displayName) => this._allConfigs.Values.FirstOrDefault(c => c.GroupName.Equals(groupName, StringComparison.OrdinalIgnoreCase) && c.DisplayName.Equals(displayName, StringComparison.OrdinalIgnoreCase));

        private void OnOSCStateChanged(object sender, OSCStateManager.StateChangedEventArgs e)
        {
            if (this._allConfigs.TryGetValue(e.Address, out var config) && (config.ActionType == "ToggleButton" || config.ActionType == "ToggleDial"))
            {
                var newState = e.Value > 0.5f;
                if (!this._toggleStates.ContainsKey(e.Address) || this._toggleStates[e.Address] != newState)
                {
                    this._toggleStates[e.Address] = newState;
                }
            }
        }

        public bool GetToggleState(string actionParameter) => this._toggleStates.TryGetValue(actionParameter, out var s) && s;
        public void SetToggleState(string actionParameter, bool state) => this._toggleStates[actionParameter] = state;
        public int GetDialMode(string actionParameter) => this._dialModes.TryGetValue(actionParameter, out var m) ? m : 0;

        public void ProcessGeneralButtonPress(ButtonConfig config, string actionParameter)
        {
            if (config == null)
                return;
            float valueToSend = 1f;
            if (config.ActionType == "ToggleButton")
            {
                valueToSend = this.GetToggleState(actionParameter) ? 0f : 1f;
            }
            ReaOSCPlugin.SendOSCMessage(actionParameter, valueToSend);
        }

        public void ProcessFxButtonPress(string actionParameter) => ReaOSCPlugin.SendFXMessage(actionParameter, 1);

        public void ProcessDialAdjustment(ButtonConfig config, int ticks, string actionParameter, Dictionary<string, DateTime> lastEventTimes)
        {
            if (config == null)
                return;

            var address = config.ActionType == "2ModeTickDial" && this.GetDialMode(actionParameter) == 1
                ? config.IncreaseOSCAddress_Mode2
                : config.IncreaseOSCAddress;
            var oppositeAddress = config.ActionType == "2ModeTickDial" && this.GetDialMode(actionParameter) == 1
                ? config.DecreaseOSCAddress_Mode2
                : config.DecreaseOSCAddress;

            float acceleration = 1.0f;
            if (config.AccelerationFactor.HasValue && lastEventTimes.TryGetValue(actionParameter, out var lastTime))
            {
                var timeDiff = (float)(DateTime.Now - lastTime).TotalMilliseconds;
                if (timeDiff < 100)
                {
                    acceleration = config.AccelerationFactor.Value;
                }
            }
            lastEventTimes[actionParameter] = DateTime.Now;

            string targetAddress = ticks > 0 ? address : oppositeAddress;
            if (!string.IsNullOrEmpty(targetAddress))
            {
                ReaOSCPlugin.SendOSCMessage(targetAddress, Math.Abs(ticks) * acceleration);
            }
        }

        public bool ProcessDialPress(ButtonConfig config, string actionParameter)
        {
            if (config.ActionType == "2ModeTickDial")
            {
                this._dialModes[actionParameter] = (this._dialModes.TryGetValue(actionParameter, out var mode) ? mode : 0) + 1 % 2;
                return true;
            }
            if (!string.IsNullOrEmpty(config.ResetOscAddress))
            {
                string pathPrefix = actionParameter.Contains("/DialAction") ? actionParameter.Substring(0, actionParameter.LastIndexOf('/')) : actionParameter;
                ReaOSCPlugin.SendOSCMessage(pathPrefix + "/" + config.ResetOscAddress.TrimStart('/'), 1f);
            }
            return false;
        }
        #endregion

        public void Dispose()
        {
            OSCStateManager.Instance.StateChanged -= this.OnOSCStateChanged;
        }
    }
}