namespace Loupedeck.ReaOSCPlugin.Base
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Reflection;

    using Newtonsoft.Json;

    /// <summary>
    /// ReaOSC 插件的中央逻辑管理器。
    /// 负责：配置加载、状态存储、核心OSC逻辑。
    /// </summary>
    public class Logic_Manager_Base : IDisposable
    {
        // --- 单例模式 ---
        private static readonly Lazy<Logic_Manager_Base> _instance = new Lazy<Logic_Manager_Base>(() => new Logic_Manager_Base());
        public static Logic_Manager_Base Instance => _instance.Value;

        // --- 数据存储 ---
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
            var generalConfigs = this.LoadConfigsFromResource("Loupedeck.ReaOSCPlugin.General.General_List.json");
            this.ProcessLoadedConfigs(generalConfigs, isFx: false);

            var effectsConfigs = this.LoadConfigsFromResource("Loupedeck.ReaOSCPlugin.Effects.Effects_List.json");
            this.ProcessLoadedConfigs(effectsConfigs, isFx: true);
        }

        private Dictionary<string, List<ButtonConfig>> LoadConfigsFromResource(string resourceName)
        {
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                using (var stream = assembly.GetManifestResourceStream(resourceName))
                {
                    if (stream == null)
                    { PluginLog.Error($"[LogicManager] 无法找到嵌入资源: {resourceName}"); return null; }
                    using (var reader = new StreamReader(stream))
                    {
                        return JsonConvert.DeserializeObject<Dictionary<string, List<ButtonConfig>>>(reader.ReadToEnd());
                    }
                }
            }
            catch (Exception ex)
            {
                PluginLog.Error(ex, $"[LogicManager] 读取或解析资源 '{resourceName}' 失败。");
                return null;
            }
        }

        private void ProcessLoadedConfigs(Dictionary<string, List<ButtonConfig>> groupedConfigs, bool isFx = false)
        {
            if (groupedConfigs == null)
                return;

            foreach (var group in groupedConfigs)
            {
                var groupNameFromJson = group.Key;
                string groupNameForPath = groupNameFromJson.Replace(" FX", "").Replace(" ", "/");

                foreach (var config in group.Value)
                {
                    config.GroupName = groupNameFromJson;
                    string actionParameter;

                    if (isFx)
                    {
                        actionParameter = $"Add/{groupNameForPath}/{config.DisplayName}";
                    }
                    else
                    {
                        string baseOscName = String.IsNullOrEmpty(config.OscAddress) ? config.DisplayName.Replace(" ", "/") : config.OscAddress.Replace(" ", "/");
                        actionParameter = $"/{groupNameForPath}/{baseOscName}".Replace("//", "/");
                        if (config.ActionType == "TickDial")
                            actionParameter += "/TickDialAction";
                        if (config.ActionType == "2ModeTickDial")
                            actionParameter += "/2ModeTickDialAction";
                    }

                    if (this._allConfigs.ContainsKey(actionParameter))
                        continue;

                    this._allConfigs[actionParameter] = config;
                    if (config.ActionType == "ToggleButton")
                    {
                        this._toggleStates[actionParameter] = OSCStateManager.Instance.GetState(actionParameter) > 0.5f;
                    }
                    else if (config.ActionType == "2ModeTickDial")
                    {
                        this._dialModes[actionParameter] = 0;
                    }
                }
            }
        }

        public IReadOnlyDictionary<string, ButtonConfig> GetAllConfigs() => this._allConfigs;
        public ButtonConfig GetConfig(string actionParameter) => this._allConfigs.TryGetValue(actionParameter, out var c) ? c : null;

        #endregion

        #region OSC 状态与核心逻辑

        private void OnOSCStateChanged(object sender, OSCStateManager.StateChangedEventArgs e)
        {
            if (this._allConfigs.TryGetValue(e.Address, out var config) && config.ActionType == "ToggleButton")
            {
                var newState = e.Value > 0.5f;
                if (this._toggleStates.TryGetValue(e.Address, out var oldState) && oldState != newState)
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

        public void ProcessFxButtonPress(string actionParameter)
        {
            ReaOSCPlugin.SendFXMessage(actionParameter, 1);
        }

        public void ProcessDialAdjustment(ButtonConfig config, int ticks, string actionParameter, Dictionary<string, DateTime> lastEventTimes)
        {
            if (ticks == 0 || config == null)
                return;

            string groupNameForPath = config.GroupName.Replace(" FX", "").Replace(" ", "/");

            if (config.ActionType == "TickDial" || config.ActionType == "2ModeTickDial")
            {
                var currentMode = this.GetDialMode(actionParameter);

                string oscAddress = "";
                if (currentMode == 1)
                {
                    oscAddress = ticks > 0 ? config.IncreaseOSCAddress_Mode2 : config.DecreaseOSCAddress_Mode2;
                }
                else
                {
                    oscAddress = ticks > 0 ? config.IncreaseOSCAddress : config.DecreaseOSCAddress;
                }

                if (string.IsNullOrEmpty(oscAddress))
                {
                    var modeTitle = currentMode == 1 ? config.Title_Mode2 : config.Title;
                    if (string.IsNullOrEmpty(modeTitle))
                        modeTitle = config.DisplayName;
                    var actionSuffix = ticks > 0 ? "Up" : "Down";
                    oscAddress = $"/{groupNameForPath}/{modeTitle.Replace(" ", "/")}/{actionSuffix}".Replace("//", "/");
                }

                if (!string.IsNullOrEmpty(oscAddress))
                {
                    var acceleration = config.AccelerationFactor ?? 1.0f;
                    var lastEventTime = lastEventTimes.TryGetValue(actionParameter, out var time) ? time : DateTime.MinValue;
                    var now = DateTime.Now;
                    lastEventTimes[actionParameter] = now;

                    var elapsed = now - lastEventTime;
                    if (lastEventTime == DateTime.MinValue)
                        elapsed = TimeSpan.FromSeconds(1);

                    double speedFactor = 1.0 / Math.Max(elapsed.TotalSeconds, 0.02);
                    int baseCount = Math.Abs(ticks);
                    int totalCount = (int)(baseCount * acceleration * speedFactor);
                    totalCount = Math.Clamp(totalCount, 1, 10);

                    for (int i = 0; i < totalCount; i++)
                    {
                        ReaOSCPlugin.SendOSCMessage(oscAddress, 1f);
                    }
                }
            }
            else if (config.ActionType == "ToggleDial")
            {
                string baseOscName = string.IsNullOrEmpty(config.OscAddress) ? config.DisplayName : config.OscAddress;
                var addr = $"/{groupNameForPath}/{baseOscName.Replace(" ", "/")}".Replace("//", "/");
                var isActive = this.GetToggleState(actionParameter);

                float newValue = -1f;
                if (ticks > 0 && !isActive)
                    newValue = 1f;
                else if (ticks < 0 && isActive)
                    newValue = 0f;

                if (newValue >= 0f)
                    ReaOSCPlugin.SendOSCMessage(addr, newValue);
            }
        }

        public bool ProcessDialPress(ButtonConfig config, string actionParameter)
        {
            if (config == null)
                return false;

            if (config.ActionType == "2ModeTickDial")
            {
                this._dialModes[actionParameter] = 1 - this.GetDialMode(actionParameter);
                return true; // 返回true表示状态已改变，需要重绘
            }

            string groupNameForPath = config.GroupName.Replace(" FX", "").Replace(" ", "/");
            string baseOscNameForReset = config.DisplayName.Replace(" ", "/");
            string effectiveResetNamePart = string.IsNullOrEmpty(config.ResetOscAddress) ? "Reset" : config.ResetOscAddress;

            if (!string.IsNullOrEmpty(effectiveResetNamePart))
            {
                var resetAddr = $"/{groupNameForPath}/{baseOscNameForReset}/{effectiveResetNamePart.Replace(" ", "/")}".Replace("//", "/");
                ReaOSCPlugin.SendOSCMessage(resetAddr, 1f);
            }
            return false; // Reset操作通常不需要重绘
        }

        #endregion

        public void Dispose()
        {
            OSCStateManager.Instance.StateChanged -= this.OnOSCStateChanged;
        }
    }
}