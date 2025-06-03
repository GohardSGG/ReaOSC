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

        private Logic_Manager_Base() { }

        public void Initialize()
        {
            if (this._isInitialized)
                return;
            PluginLog.Info("[LogicManager] 开始初始化...");
            this.LoadAllConfigs();
            OSCStateManager.Instance.StateChanged += this.OnOSCStateChanged;
            this._isInitialized = true;
            PluginLog.Info("[LogicManager] 初始化成功。");
        }

        #region 配置加载与访问

        private void LoadAllConfigs()
        {
            var assembly = Assembly.GetExecutingAssembly();

            // 1. 加载通用配置
            var generalConfigs = this.LoadAndDeserialize<Dictionary<string, List<ButtonConfig>>>(assembly, "Loupedeck.ReaOSCPlugin.General.General_List.json");
            this.ProcessGroupedConfigs(generalConfigs, isFx: false);

            // 2. 加载效果器配置
            var effectsConfigs = this.LoadAndDeserialize<Dictionary<string, List<ButtonConfig>>>(assembly, "Loupedeck.ReaOSCPlugin.Effects.Effects_List.json");
            this.ProcessGroupedConfigs(effectsConfigs, isFx: true);

            // 3. 【修正】加载所有动态文件夹的内容配置
            var resourceNames = assembly.GetManifestResourceNames();
            // 注意：这里我们排除了 Dynamic_List.json，因为它结构不同
            var dynamicContentResources = resourceNames.Where(r => r.StartsWith("Loupedeck.ReaOSCPlugin.Dynamic.") && r.EndsWith("_List.json") && !r.EndsWith("Dynamic_List.json"));

            foreach (var resourceName in dynamicContentResources)
            {
                var folderContent = this.LoadAndDeserialize<FolderContentConfig>(assembly, resourceName);
                if (folderContent != null)
                {
                    this.ProcessFolderContentConfigs(folderContent);
                }
            }

            // 4. 【新增】单独加载并处理动态文件夹入口列表 (Dynamic_List.json)
            var dynamicFolderEntries = this.LoadAndDeserialize<List<ButtonConfig>>(assembly, "Loupedeck.ReaOSCPlugin.Dynamic.Dynamic_List.json");
            if (dynamicFolderEntries != null)
            {
                // 为这些入口按钮统一设置一个组名，以便在UI中查找
                foreach (var entry in dynamicFolderEntries)
                {
                    entry.GroupName = "ReaOSC Dynamic";
                }
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
            catch (Exception ex)
            {
                PluginLog.Error(ex, $"[LogicManager] 读取或解析资源 '{resourceName}' 失败。");
                return null;
            }
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

                    // 为不同类型的旋钮添加唯一后缀，避免路径冲突
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

        public IReadOnlyDictionary<string, ButtonConfig> GetAllConfigs() => this._allConfigs;
        public ButtonConfig GetConfig(string actionParameter) => this._allConfigs.TryGetValue(actionParameter, out var c) ? c : null;
        public ButtonConfig GetConfigByDisplayName(string groupName, string displayName) =>
            this._allConfigs.Values.FirstOrDefault(c => c.GroupName.Equals(groupName, StringComparison.OrdinalIgnoreCase) && c.DisplayName.Equals(displayName, StringComparison.OrdinalIgnoreCase));

        #endregion

        #region OSC 状态与核心逻辑 (这部分代码保持不变)
        // ... (省略未改变的代码) ...
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
        public void ProcessFxButtonPress(string actionParameter) { ReaOSCPlugin.SendFXMessage(actionParameter, 1); }
        public void ProcessDialAdjustment(ButtonConfig config, int ticks, string actionParameter, Dictionary<string, DateTime> lastEventTimes) {/*...代码未变...*/ }
        public bool ProcessDialPress(ButtonConfig config, string actionParameter) {/*...代码未变...*/ return false; }
        #endregion

        public void Dispose() { OSCStateManager.Instance.StateChanged -= this.OnOSCStateChanged; }
    }
}