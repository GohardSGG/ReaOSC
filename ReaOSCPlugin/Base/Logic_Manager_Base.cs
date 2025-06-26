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
    using System.Globalization; // 【新增】导入 System.Globalization 命名空间

    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq; //确保JToken的引用

    public class ButtonConfig
    {
        // === 通用配置 ===
        public String DisplayName { get; set; }
        public String Title { get; set; } // 主要用于UI显示，尤其当DisplayName不适合直接显示时
        public String TitleColor { get; set; } // 通用标题颜色
        public String GroupName { get; set; }
        public String ActionType { get; set; }
        // 可能的值: "TriggerButton", "ToggleButton", "TickDial", "ToggleDial", "2ModeTickDial", "SelectModeButton",
        // 【新增】 "ParameterDial", "ParameterButton", "CombineButton", "FilterDial", "PageDial", "PlaceholderDial", "NavigationDial", "ControlDial"
        // 【新增】 "ModeControlDial"
        public String Description { get; set; }

        // --- OSC 相关 ---
        public String OscAddress { get; set; } // 可选。如果提供，优先作为此控件贡献给CombineButton的路径片段 (经过处理后)

        // --- 模式相关 (主要用于 SelectModeButton 及其控制的按钮) ---
        public String ModeName { get; set; }
        public List<String> Modes { get; set; } // For SelectModeButton: 定义可选模式
        public List<String> Titles { get; set; } // For ParameterDial: 定义可选参数值; For SelectModeButton & controlled buttons: 定义不同模式下的显示标题
        public List<String> OscAddresses { get; set; } // For SelectModeButton & controlled buttons: 定义不同模式下的OSC地址
        
        // 【新增】专门用于 ModeControlDial 的多模式列表属性
        public List<List<String>> ParametersByMode { get; set; } 
        public List<List<String>> UnitsByMode { get; set; }
        public List<String> UnitDefaultsByMode { get; set; } // 注意：这个对应的是之前单数的 UnitDefault，但现在是每个模式一个
        // public List<String> OscAddressesForListenersByMode { get; set; } // 【已移除】不再需要用户在JSON中为每个内部模式定义监听地址
        public List<String> ParameterDefaultsByMode { get; set; } // 【修正】确保与JSON中的 ParameterDefaultsByMode (有s) 匹配

        // === 按钮相关 ===
        // --- ToggleButton 和 ToggleDial 特有 (也可能被ParameterDial用于不同状态的显示) ---
        public String ActiveColor { get; set; } // ToggleButton ON 状态背景色, ParameterDial 激活状态背景色 (如果适用)
        public String ActiveTextColor { get; set; } // ToggleButton ON 状态文字颜色
        public String DeactiveTextColor { get; set; } // ToggleButton OFF 状态文字颜色
        public String DeactiveColor { get; set; } // ToggleButton OFF 状态背景色

        public String ButtonImage { get; set; } // (目前主要用于 General_Button_Base, 动态文件夹内按钮较少直接用图片)

        // === 旋钮相关 (TickDial, ToggleDial, 2ModeTickDial) ===
        public String IncreaseOSCAddress { get; set; } // 主要用于 TickDial
        public String DecreaseOSCAddress { get; set; } // 主要用于 TickDial
        public Single? AccelerationFactor { get; set; } // 主要用于 TickDial,Single对应float
        public String ResetOscAddress { get; set; } // 主要用于 TickDial, ToggleDial 按下时的 OSC

        // --- 次要文本 (可用于所有类型按钮/旋钮的额外小字显示) ---
        public String Text { get; set; }
        public String TextColor { get; set; }
        public Int32? TextSize { get; set; }
        public Int32? TextX { get; set; }
        public Int32? TextY { get; set; }
        public Int32? TextWidth { get; set; }
        public Int32? TextHeight { get; set; }

        // === 2ModeTickDial 特有配置 ===
        public String Title_Mode2 { get; set; }
        public String TitleColor_Mode2 { get; set; }
        public String IncreaseOSCAddress_Mode2 { get; set; }
        public String DecreaseOSCAddress_Mode2 { get; set; }
        public String BackgroundColor { get; set; } // 旋钮模式1的背景色 (或通用背景色)
        public String BackgroundColor_Mode2 { get; set; } // 旋钮模式2的背景色

        // === 【新增】ParameterDial 特有 ===
        // Titles 字段已存在，将被 ParameterDial 用作参数值列表
        public String ShowParameterInDial { get; set; } // "Yes" 或 "No". "Yes" 则旋钮UI显示当前选中的Title, "No"则显示固定的Title/DisplayName

        // === 【新增】ParameterButton 特有 ===
        public String ParameterSourceDial { get; set; } // 指向要显示其参数的 ParameterDial 的 DisplayName

        // === 【新增】CombineButton 特有 ===
        // BaseOscPrefix 将由文件夹 GroupName 动态决定，不需要在此配置
        public List<String> ParameterOrder { get; set; } // 定义 CombineButton 收集参数的顺序，值为参与控件的 DisplayName

        // === 【新增】ToggleButton (当参与 CombineButton 时) ===
        // PathSegmentIfOn 也不再需要，ToggleButton ON 时贡献 DisplayName 或 OscAddress (处理后)
        
        // === 【修改】动态文件夹内容定义 ===
        public JObject Content { get; set; } // 改为 JObject 以便灵活解析

        // 【新增】用于 FX_Folder_Base，控制品牌/类别是否按JSON文件中的顺序排序
        public Boolean PreserveBrandOrderInJson { get; set; } = false;

        // === 【新增】用于动态列表项的过滤属性 ===
        public Dictionary<String, String> FilterableProperties { get; set; } = new Dictionary<String, String>();

        // === 【新增】用于 FilterDial，标记是否为主过滤器 ===
        public String BusFilter { get; set; } // JSON中可以是 "Yes" 或其他，代码中判断 "Yes" (忽略大小写)

        // === 【新增】用于旋钮，控制是否显示标题 ===
        public String ShowTitle { get; set; } // JSON中可以是 "Yes" 或 "No"

        // === 【新增】ParameterDial 特有的参数列表 ===
        // === 【修改】也用于 ControlDial 的参数列表 ===
        public List<String> Parameter { get; set; } // 用于存储ParameterDial的参数选项列表，或ControlDial的配置

        // === 【新增】用于 ControlDial 指定默认值 ===
        public String ParameterDefault { get; set; }

        // === 【新增】用于 ControlDial 的单位转换 ===
        public List<String> Unit { get; set; } // 例如: ["dB", "-inf", "+12"]
        public String UnitDefault { get; set; } // 例如: "0" (代表单位刻度下的0dB)

        // 【新增】标志位，指示当此按钮在动态文件夹内且定义了GroupName时，是否应将其GroupName作为OSC地址的根
        public Boolean UseOwnGroupAsRoot { get; set; } = false;

        // === 【新增】用于捕获JSON中未明确定义的其他属性 ===
        // Newtonsoft.Json.JsonExtensionDataAttribute 标签可以自动捕获额外数据到字典
        // 但更推荐的方式是如果明确知道有哪些额外属性，就定义它们，或者在解析时手动处理。
        // 为简单起见，如果BusFilter是唯一关心的，上面的BusFilter字段就够了。
        // 如果需要更通用的，可以取消注释下面的代码，并确保 Logic_Manager_Base 中使用适当的 JsonSerializerSettings
        // [Newtonsoft.Json.JsonExtensionData]
        // public Dictionary<string, JToken> AdditionalProperties { get; set; } = new Dictionary<string, JToken>();
    }

    public class FolderContentConfig
    {
        public List<ButtonConfig> Buttons { get; set; } = new List<ButtonConfig>();
        public List<ButtonConfig> Dials { get; set; } = new List<ButtonConfig>();
        public Boolean IsButtonListDynamic { get; set; } // 【新增】标记按钮列表是否为动态加载
    }

    // 【新增】用于存储ControlDial解析后的配置
    internal enum ControlDialMode { Continuous, Discrete }
    internal class ControlDialParsedConfig
    {
        public ControlDialMode Mode { get; set; }
        public Single MinValue { get; set; } // 参数刻度, e.g., 0.0f (通常代表0%)
        public Single MaxValue { get; set; } // 参数刻度, e.g., 1.0f (通常代表100%)
        public List<Single> DiscreteValues { get; set; } // 参数刻度 (离散模式下的可选浮点值, 0.0f-1.0f)
        public Single DefaultValue { get; set; } // 参数刻度的默认值 (e.g., 0.0f-1.0f之间)
        public Boolean IsParameterIntegerBased { get; set; } = false; // 新增：标记参数是否基于整数

        // --- 【新增】单位转换相关字段 ---
        public Boolean HasUnitConversion { get; set; } = false; // 是否启用单位转换
        public String DisplayUnitString { get; set; }      // 单位类型字符串 (e.g., "dB", "LR")
        public String UnitLabel { get; set; }              // 显示在数值后的单位标签 (e.g., "dB")
        public Single UnitMin { get; set; }                // 单位的最小值 (e.g., float.NegativeInfinity for "-inf")
        public Single UnitMax { get; set; }                // 单位的最大值 (e.g., 12f for "+12")
        public Single ParsedUnitDefault { get; set; }      // 解析后的 UnitDefault 值 (单位刻度, e.g., 0f for "0dB")
    }

    public class Logic_Manager_Base : IDisposable
    {
        private static readonly Lazy<Logic_Manager_Base> _instance = new Lazy<Logic_Manager_Base>(() => new Logic_Manager_Base());
        public static Logic_Manager_Base Instance => _instance.Value;

        private readonly Dictionary<String, ButtonConfig> _allConfigs = new Dictionary<String, ButtonConfig>();
        private readonly Dictionary<String, Boolean> _toggleStates = new Dictionary<String, Boolean>();
        private readonly Dictionary<String, Int32> _dialModes = new Dictionary<String, Int32>();
        
        // 【新增】用于存储动态文件夹内容的字典
        private readonly Dictionary<String, FolderContentConfig> _folderContents = new Dictionary<String, FolderContentConfig>();
        private readonly Dictionary<String, Newtonsoft.Json.Linq.JObject> _fxDataCache = new Dictionary<String, Newtonsoft.Json.Linq.JObject>();
        
        // 【新增】用于动态标题功能的数据结构
        // 键: 唯一的标题字段标识符 (e.g., actionParameter#Title, actionParameter#Title_Mode2)
        // 值: 当前的模板字符串 (可能来自JSON的静态值，或来自OSC的动态值)
        private readonly Dictionary<String, String> _dynamicTitleSourceStrings = new Dictionary<String, String>();

        // 键: 唯一的标题字段标识符
        // 值: 如果该标题字段来自OSC，则这里存储其OSC地址
        private readonly Dictionary<String, String> _titleFieldToOscAddressMapping = new Dictionary<String, String>();
        // 【新增】反向映射：从OSC地址到监听该地址的动态标题唯一键列表
        private readonly Dictionary<String, List<String>> _oscAddressToDynamicTitleKeys = new Dictionary<String, List<String>>();

        private Boolean _isInitialized = false;
        private String _customConfigBasePath;

        private readonly Dictionary<String, List<String>> _modeOptions = new Dictionary<String, List<String>>();
        private readonly Dictionary<String, Int32> _currentModes = new Dictionary<String, Int32>();
        private readonly Dictionary<String, Action> _modeChangedEvents = new Dictionary<String, Action>();

        private readonly Dictionary<String, String> _oscAddressToActionParameterMap = new Dictionary<String, String>();
        public event EventHandler<String> CommandStateNeedsRefresh;

        private readonly Dictionary<String, Int32> _parameterDialSelectedIndexes = new Dictionary<String, Int32>();

        // 【修改】用于 ControlDial 状态存储，值类型改为 Single
        private readonly Dictionary<String, Single> _controlDialCurrentValues = new Dictionary<String, Single>();
        private readonly Dictionary<String, ControlDialParsedConfig> _controlDialConfigs = new Dictionary<String, ControlDialParsedConfig>();

        // 【新增】ModeControlDial 相关数据结构
        private readonly Dictionary<String, List<ControlDialParsedConfig>> _modeControlDialParsedConfigs = new Dictionary<String, List<ControlDialParsedConfig>>();
        private readonly Dictionary<String, List<String>> _modeControlDialOscListenerAddresses = new Dictionary<String, List<String>>();
        private readonly Dictionary<String, List<Single>> _modeControlDialCurrentValuesByMode = new Dictionary<String, List<Single>>();
        private readonly Dictionary<String, List<Single>> _modeControlDialInitialParameterDefaultsByMode = new Dictionary<String, List<Single>>();

        private Logic_Manager_Base() { }

        public void Initialize()
        {
            if (this._isInitialized)
            {
                return;
            }
            PluginLog.Info("[LogicManager] 开始初始化...");
            this.LoadAllConfigs();
            OSCStateManager.Instance.StateChanged += this.OnOSCStateChanged;
            this._isInitialized = true;
            PluginLog.Info($"[LogicManager] 初始化成功。加载了 {this._allConfigs.Count} 个配置项。");
        }

        #region 模式管理
        public void RegisterModeGroup(ButtonConfig config) { var modeName = config.DisplayName; var modes = config.Modes; if (String.IsNullOrEmpty(modeName) || modes == null || modes.Count == 0) { return; } if (!this._modeOptions.ContainsKey(modeName)) { this._modeOptions[modeName] = modes; this._currentModes[modeName] = 0; this._modeChangedEvents[modeName] = null; PluginLog.Info($"[LogicManager][Mode] 模式组 '{modeName}' 已注册，包含模式: {String.Join(", ", modes)}。"); } }
        public void ToggleMode(String modeName) 
        { 
            if (this._currentModes.TryGetValue(modeName, out _) && this._modeOptions.TryGetValue(modeName, out var options))
            { 
                var oldModeIndex = this._currentModes[modeName];
                this._currentModes[modeName] = (this._currentModes[modeName] + 1) % options.Count; 
                var newModeIndex = this._currentModes[modeName];
                PluginLog.Info($"[LogicManager][Mode] 模式组 '{modeName}' 从索引 {oldModeIndex} ('{options[oldModeIndex]}') 切换到索引 {newModeIndex} ('{options[newModeIndex]}').");

                this._modeChangedEvents[modeName]?.Invoke(); 

                // 刷新所有受此模式组影响的控件 (包括按钮和ModeControlDial等)
                foreach (var kvp in this._allConfigs)
                {
                    if (kvp.Value != null && kvp.Value.ModeName == modeName)
                    {
                        PluginLog.Info($"[LogicManager|ToggleMode] 模式组 '{modeName}' 已改变，触发依赖项 '{kvp.Key}' (Display: {kvp.Value.DisplayName}, Type: {kvp.Value.ActionType}) 的状态刷新。");
                        this.CommandStateNeedsRefresh?.Invoke(this, kvp.Key); 
                    }
                }
                // SelectModeButton 自身也需要刷新，以显示新的当前模式名
                var modeControllerActionParam = this.GetActionParameterForModeController(modeName);
                if (!String.IsNullOrEmpty(modeControllerActionParam))
                {
                     this.CommandStateNeedsRefresh?.Invoke(this, modeControllerActionParam); 
                }
            } 
            else
            {
                PluginLog.Warning($"[LogicManager|ToggleMode] 尝试切换未注册或无效的模式组: '{modeName}'");
            }
        }
        private String GetActionParameterForModeController(String modeName) => this._allConfigs.FirstOrDefault(kvp => kvp.Value.ActionType == "SelectModeButton" && kvp.Value.DisplayName == modeName).Key;
        public String GetCurrentModeString(String modeName) 
        {
            PluginLog.Info($"[LogicManager|GetCurrentModeString] Requesting current mode for ModeName: '{modeName ?? "null"}'");
            if (this._currentModes.TryGetValue(modeName, out var currentIndex) && 
                this._modeOptions.TryGetValue(modeName, out var options) && 
                currentIndex >= 0 && currentIndex < options.Count) 
            {
                var modeStr = options[currentIndex];
                PluginLog.Info($"[LogicManager|GetCurrentModeString] Found. ModeName: '{modeName}', Index: {currentIndex}, ModeString: '{modeStr}'");
                return modeStr; 
            }
            PluginLog.Warning($"[LogicManager|GetCurrentModeString] ModeName '{modeName ?? "null"}' not found or index/options invalid. CurrentModes has key: {this._currentModes.ContainsKey(modeName)}, ModeOptions has key: {this._modeOptions.ContainsKey(modeName)}");
            if(this._modeOptions.TryGetValue(modeName, out var availableOptions))
            {
                PluginLog.Warning($"[LogicManager|GetCurrentModeString] Available options for ModeName '{modeName}': [{string.Join(", ", availableOptions)}]");
            }
            return String.Empty; 
        }
        public Int32 GetCurrentModeIndex(String modeName) => this._currentModes.TryGetValue(modeName, out var currentIndex) ? currentIndex : -1;

        public void SubscribeToModeChange(String modeName, Action handler) { if (String.IsNullOrEmpty(modeName) || handler == null) { return; } if (!this._modeChangedEvents.ContainsKey(modeName)) { this._modeChangedEvents[modeName] = null; } this._modeChangedEvents[modeName] += handler; }
        public void UnsubscribeFromModeChange(String modeName, Action handler) { if (String.IsNullOrEmpty(modeName) || handler == null) { return; } if (this._modeChangedEvents.ContainsKey(modeName)) { this._modeChangedEvents[modeName] -= handler; } }
        #endregion

        #region 配置加载
        private void LoadAllConfigs()
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            this._customConfigBasePath = Path.Combine(localAppData, "Loupedeck", "Plugins", "ReaOSC");
            PluginLog.Info($"[LogicManager] 使用自定义配置的基础路径: '{this._customConfigBasePath}'");

            // 清理旧的动态标题映射，以防重载 (通常Initialize只调用一次，但安全起见)
            this._dynamicTitleSourceStrings.Clear();
            this._titleFieldToOscAddressMapping.Clear();

            // 加载 General_List.json
            var generalConfigs = this.LoadConfigFile<Dictionary<String, List<ButtonConfig>>>("General/General_List.json");
            this.ProcessGroupedConfigs(generalConfigs, isDynamicFolderContent: false);
            
            // 加载 Dynamic_List.json (文件夹定义)
            var dynamicFolderDefsObject = this.LoadConfigFile<JObject>("Dynamic/Dynamic_List.json");
            this.ProcessDynamicFolderDefs(dynamicFolderDefsObject);
        }
        
        private T LoadConfigFile<T>(String relativePath) where T : class
        {
            var platformRelativePath = relativePath.Replace('/', Path.DirectorySeparatorChar);
            var customFilePath = Path.Combine(this._customConfigBasePath, platformRelativePath);
            if (File.Exists(customFilePath))
            {
                try
                {
                    var jsonContent = File.ReadAllText(customFilePath);
                    PluginLog.Info($"[LogicManager] 正在加载自定义配置文件: '{customFilePath}'");
                    return JsonConvert.DeserializeObject<T>(jsonContent);
                }
                catch (Exception ex)
                {
                    PluginLog.Error(ex, $"[LogicManager] 读取或解析自定义配置文件 '{customFilePath}' 失败。将回退到内置版本。");
                }
            }
            var resourceName = $"Loupedeck.ReaOSCPlugin.{relativePath.Replace('/', '.')}";
            return this.LoadAndDeserialize<T>(Assembly.GetExecutingAssembly(), resourceName);
        }

        private T LoadAndDeserialize<T>(Assembly assembly, String resourceName) where T : class 
        { 
            try 
            { 
                using (var stream = assembly.GetManifestResourceStream(resourceName)) 
                { 
                    if (stream == null) 
                    { 
                        PluginLog.Info($"[LogicManager] 内嵌资源 '{resourceName}' 未找到或加载失败。"); 
                        return null; 
                    } 
                    using (var reader = new StreamReader(stream)) 
                    { 
                        return JsonConvert.DeserializeObject<T>(reader.ReadToEnd()); 
                    } 
                } 
            } 
            catch (Exception ex) 
            { 
                PluginLog.Error(ex, $"[LogicManager] 读取或解析内嵌资源 '{resourceName}' 失败。"); 
                return null; 
            } 
        }

        private void ProcessGroupedConfigs(Dictionary<String, List<ButtonConfig>> groupedConfigs, bool isDynamicFolderContent, String folderDisplayNameForContext = null)
        { 
            if (groupedConfigs == null) { return; } 
            foreach (var group in groupedConfigs) 
            { 
                var configsInGroup = group.Value.Select(config => 
                { 
                    config.GroupName = group.Key; // 组名来自字典的键
                    return config; 
                }).ToList(); 
                // 对于非文件夹内容（例如 General_List.json），isDynamicFolderEntry 总是 false，defaultGroupName 应该是 group.Key
                // 对于文件夹内容，isDynamicFolderEntry 也是 false（因为处理的是文件夹内的控件，而非文件夹入口本身），defaultGroupName 应该是 folderDisplayNameForContext
                this.RegisterConfigs(configsInGroup, isDynamicFolderEntry: false, defaultGroupName: isDynamicFolderContent ? folderDisplayNameForContext : group.Key);
            } 
        }

        private void ProcessDynamicFolderDefs(JObject dynamicFolderDefsObject)
        {
            if (dynamicFolderDefsObject == null) 
            {
                PluginLog.Warning("[LogicManager] ProcessDynamicFolderDefs: dynamicFolderDefsObject 为空，不处理动态文件夹定义。");
                return;
            }
            
            var folderEntriesToRegister = new List<ButtonConfig>();

            foreach (var property in dynamicFolderDefsObject.Properties())
            {
                var folderDisplayNameFromKey = property.Name; 
                JObject folderDefJson = property.Value as JObject;
                if (folderDefJson == null) { PluginLog.Warning($"[LogicManager|ProcessDynamicFolderDefs] Folder '{folderDisplayNameFromKey}' has non-object value, skipping."); continue; }
                ButtonConfig folderDef = folderDefJson.ToObject<ButtonConfig>();
                if (folderDef == null) { PluginLog.Warning($"[LogicManager|ProcessDynamicFolderDefs] Could not deserialize folder '{folderDisplayNameFromKey}' to ButtonConfig, skipping."); continue; }
                
                // 【修正的诊断日志】
                if (folderDef.ActionType == "ModeControlDial")
                {
                    PluginLog.Info($"[LogicManager|ProcessDynamicFolderDefs|DIAG] ModeControlDial '{folderDef.DisplayName ?? "(null DisplayName)"}' deserialized state: " +
                                   $"ParametersByMode is null: {folderDef.ParametersByMode == null}, Count: {folderDef.ParametersByMode?.Count ?? -1}. " +
                                   $"UnitsByMode is null: {folderDef.UnitsByMode == null}, Count: {folderDef.UnitsByMode?.Count ?? -1}. " +
                                   $"UnitDefaultsByMode is null: {folderDef.UnitDefaultsByMode == null}, Count: {folderDef.UnitDefaultsByMode?.Count ?? -1}. " +
                                   // OscAddressesForListenersByMode 已移除, 不再诊断
                                   $"ParameterDefaultsByMode is null: {folderDef.ParameterDefaultsByMode == null}, Count: {folderDef.ParameterDefaultsByMode?.Count ?? -1}.");
                }
                
                folderDef.DisplayName = folderDisplayNameFromKey; 
                folderDef.GroupName = "Dynamic"; 

                JObject contentJObject = folderDef.Content; 
                if (contentJObject != null) 
                {
                    FolderContentConfig actualFolderContent = new FolderContentConfig();
                    var buttonsToken = contentJObject["Buttons"];
                    if (buttonsToken != null)
                    {
                        if (buttonsToken.Type == Newtonsoft.Json.Linq.JTokenType.Array)
                        {
                            actualFolderContent.Buttons = buttonsToken.ToObject<List<ButtonConfig>>() ?? new List<ButtonConfig>();
                            actualFolderContent.IsButtonListDynamic = false;
                        }
                        else if (buttonsToken.Type == Newtonsoft.Json.Linq.JTokenType.String && buttonsToken.ToString() == "List")
                        {
                            actualFolderContent.IsButtonListDynamic = true;
                            var fxListNameFromContent = $"Dynamic/{folderDef.DisplayName.Replace(" ", "_")}_List.json";
                            var fxDataFromContent = this.LoadConfigFile<Newtonsoft.Json.Linq.JObject>(fxListNameFromContent);
                            if (fxDataFromContent != null) { this._fxDataCache[folderDef.DisplayName] = fxDataFromContent; }
                            else { PluginLog.Warning($"[LogicManager] For folder '{folderDef.DisplayName}', FAILED to load data source '{fxListNameFromContent}'."); }
                        }
                    }
                    var dialsToken = contentJObject["Dials"];
                    if (dialsToken != null && dialsToken.Type == Newtonsoft.Json.Linq.JTokenType.Array)
                    {
                        actualFolderContent.Dials = dialsToken.ToObject<List<ButtonConfig>>() ?? new List<ButtonConfig>();
                    }
                    this._folderContents[folderDef.DisplayName] = actualFolderContent; 
                    this.ProcessFolderContentConfigs(actualFolderContent, folderDef.DisplayName); 
                }
                else 
                {
                    var fxListName = $"Dynamic/{folderDef.DisplayName.Replace(" ", "_")}_List.json";
                    var fxData = this.LoadConfigFile<Newtonsoft.Json.Linq.JObject>(fxListName);
                    if (fxData != null)
                    {
                        this._fxDataCache[folderDef.DisplayName] = fxData;
                        this._folderContents[folderDef.DisplayName] = new FolderContentConfig { IsButtonListDynamic = true };
                    }
                    else { this._folderContents[folderDef.DisplayName] = new FolderContentConfig { IsButtonListDynamic = false }; }
                }
                
                var folderEntryForRegistration = new ButtonConfig { /* ... copy relevant fields from folderDef ... */ 
                    DisplayName = folderDef.DisplayName, Title = folderDef.Title, TitleColor = folderDef.TitleColor,
                    GroupName = folderDef.GroupName, ActionType = folderDef.ActionType, Description = folderDef.Description,
                    Text = folderDef.Text, TextColor = folderDef.TextColor, TextSize = folderDef.TextSize,
                    TextX = folderDef.TextX, TextY = folderDef.TextY, TextWidth = folderDef.TextWidth, TextHeight = folderDef.TextHeight,
                    BackgroundColor = folderDef.BackgroundColor, ButtonImage = folderDef.ButtonImage,
                    PreserveBrandOrderInJson = folderDef.PreserveBrandOrderInJson
                };
                folderEntriesToRegister.Add(folderEntryForRegistration);
            }
            this.RegisterConfigs(folderEntriesToRegister, isDynamicFolderEntry: true, defaultGroupName: "Dynamic");
        }
        
        private void ProcessFolderContentConfigs(FolderContentConfig folderContent, String folderDisplayNameAsDefaultGroupName) 
        { 
            if (folderContent == null) { return; } 
            if (folderContent.Buttons != null)
            {
                foreach (var buttonCfgInContent in folderContent.Buttons)
                {
                    if (!String.IsNullOrEmpty(buttonCfgInContent.GroupName)) { buttonCfgInContent.UseOwnGroupAsRoot = true; }
                }
                this.RegisterConfigs(folderContent.Buttons, isDynamicFolderEntry: false, defaultGroupName: folderDisplayNameAsDefaultGroupName);
            }
            if (folderContent.Dials != null)
            {
                 this.RegisterConfigs(folderContent.Dials, isDynamicFolderEntry: false, defaultGroupName: folderDisplayNameAsDefaultGroupName);
            }
        }

        // Helper to generate action parameter consistently
        private String GenerateActionParameter(ButtonConfig config, bool isDynamicFolderEntry, string defaultGroupNameForContext)
        {
            string effectiveGroupName = String.IsNullOrEmpty(config.GroupName) ? defaultGroupNameForContext : config.GroupName;

            if (isDynamicFolderEntry) 
            {
                // For dynamic folder entries, ActionParameter is just its DisplayName.
                // Their GroupName is "Dynamic" (set in ProcessDynamicFolderDefs).
                return config.DisplayName; 
            }

            if (String.IsNullOrEmpty(effectiveGroupName))
            { 
                PluginLog.Warning($"[LogicManager|GenerateActionParameter] Config '{config.DisplayName ?? "(No DisplayName)"}' (Type: {config.ActionType ?? "N/A"}) has no effective GroupName. Cannot generate action parameter."); 
                return null; 
            }

            String groupNameForPath = SanitizeOscPathSegment(effectiveGroupName);
            String displayNameForPath = SanitizeOscPathSegment(config.DisplayName);
            String parameter = $"/{groupNameForPath}/{displayNameForPath}";
            
            // IMPORTANT: Ensure ModeControlDial also gets /DialAction suffix for consistency if other dials do.
            if (config.ActionType != null && (config.ActionType.Contains("Dial") /* || config.ActionType == "ModeControlDial" */) )
            {
                parameter += "/DialAction";
            }
            parameter = parameter.Replace("//", "/").TrimEnd('/');
            return String.IsNullOrEmpty(parameter) || parameter == "/" ? null : parameter;
        }

        private void RegisterConfigs(List<ButtonConfig> configs, Boolean isDynamicFolderEntry = false, String defaultGroupName = null)
        {
            if (configs == null || !configs.Any()) { return; }

            // Stage 1: Register all SelectModeButtons to populate _modeOptions
            foreach (var config in configs)
            {
                if (config.ActionType == "SelectModeButton")
                {
                    // Ensure GroupName is assigned before generating actionParameter for _allConfigs key
                    if (String.IsNullOrEmpty(config.GroupName) && !String.IsNullOrEmpty(defaultGroupName))
                    {
                        config.GroupName = defaultGroupName;
                    }
                    String actionParameter = this.GenerateActionParameter(config, isDynamicFolderEntry, defaultGroupName);
                    if (String.IsNullOrEmpty(actionParameter)) 
                    {
                        PluginLog.Warning($"[LogicManager|RegisterConfigs Stage1] Could not generate actionParameter for SelectModeButton '{config.DisplayName}'. Skipping its _allConfigs registration.");
                        // Still attempt to register mode group if DisplayName and Modes are valid
                        if (!String.IsNullOrEmpty(config.DisplayName) && config.Modes != null && config.Modes.Any())
                        {
                             this.RegisterModeGroup(config);
                        }
                        continue;
                    }

                    if (!this._allConfigs.ContainsKey(actionParameter))
                    {
                        this._allConfigs[actionParameter] = config;
                        PluginLog.Info($"[LogicManager|RegisterConfigs Stage1] Added SelectModeButton '{config.DisplayName}' (ActionParam: '{actionParameter}') to _allConfigs.");
                    }
                    else if (this._allConfigs[actionParameter].ActionType != "SelectModeButton")
                    {
                        PluginLog.Warning($"[LogicManager|RegisterConfigs Stage1] ActionParameter '{actionParameter}' for SelectModeButton '{config.DisplayName}' already exists in _allConfigs with a different ActionType ('{this._allConfigs[actionParameter].ActionType}'). Overwriting with SelectModeButton.");
                        this._allConfigs[actionParameter] = config; // Overwrite if different type, assuming SelectModeButton definition is more critical here.
                    }
                    this.RegisterModeGroup(config);
                }
            }

            // Stage 2: Register all other types of configurations
            foreach (var config in configs)
            {
                if (config.ActionType == "SelectModeButton")
                {
                    continue; // Already processed
                }

                if (String.IsNullOrEmpty(config.GroupName) && !String.IsNullOrEmpty(defaultGroupName))
                {
                    config.GroupName = defaultGroupName;
                }
                
                String actionParameter = this.GenerateActionParameter(config, isDynamicFolderEntry, defaultGroupName);

                if (String.IsNullOrEmpty(actionParameter))
                { 
                    PluginLog.Warning($"[LogicManager|RegisterConfigs Stage2] Could not generate actionParameter for '{config.DisplayName}' (Type: {config.ActionType}). Skipping registration.");
                    continue; 
                }

                // 【新增】在将配置存入 _allConfigs 之前，处理其动态标题字段
                this.RegisterDynamicTitleFields(config, actionParameter);

                if (this._allConfigs.ContainsKey(actionParameter))
                { 
                    // If it was already added as a SelectModeButton in Stage 1, that's an error in config (same name for different types)
                    // Or, if this list of configs is somehow processed multiple times leading to true duplicates.
                    if (this._allConfigs[actionParameter].ActionType != config.ActionType) {
                        PluginLog.Error($"[LogicManager|RegisterConfigs Stage2] ActionParameter '{actionParameter}' for '{config.DisplayName}' (Type: {config.ActionType}) was already registered with a different type ('{this._allConfigs[actionParameter].ActionType}'). This indicates a configuration error. Skipping this item.");
                    }
                    // If same actionParameter and same type, assume it's a duplicate pass and skip silently or log verbose.
                    // PluginLog.Verbose($"[LogicManager|RegisterConfigs Stage2] ActionParameter '{actionParameter}' for '{config.DisplayName}' (Type: {config.ActionType}) already processed. Skipping duplicate.");
                    continue; 
                }
                
                this._allConfigs[actionParameter] = config;
                PluginLog.Info($"[LogicManager|RegisterConfigs Stage2] Registered '{config.ActionType}': '{config.DisplayName}' with ActionParameter: '{actionParameter}' (GroupName: '{config.GroupName ?? "N/A"}')");

                // Specific initialization based on ActionType
                if (config.ActionType == "ToggleButton" || config.ActionType == "ToggleDial")
                { 
                    this._toggleStates[actionParameter] = false; 
                    PluginLog.Verbose($"[LogicManager|RegisterConfigs Stage2] Initialized toggle state for '{actionParameter}' to false."); 
                    String listenAddrToggle = this.DetermineOscAddressForAction(config, config.GroupName, config.OscAddress); // Use JSON GroupName
                    if (!String.IsNullOrEmpty(listenAddrToggle) && listenAddrToggle != "/")
                    {
                        this._oscAddressToActionParameterMap[listenAddrToggle] = actionParameter;
                        var initialState = OSCStateManager.Instance?.GetState(listenAddrToggle) > 0.5f;
                        this._toggleStates[actionParameter] = initialState;
                        PluginLog.Info($"[LogicManager|RegisterConfigs Stage2] Mapped OSC listen '{listenAddrToggle}' to '{actionParameter}' for '{config.DisplayName}'. Initial state from OSC: {initialState}");
                    }
                }
                else if (config.ActionType == "2ModeTickDial")
                { this._dialModes[actionParameter] = 0; }
                else if (config.ActionType == "ParameterDial")
                { this._parameterDialSelectedIndexes[actionParameter] = 0; }
                else if (config.ActionType == "ControlDial")
                {
                    this.ParseAndStoreControlDialConfig(config, actionParameter); // This now calls the core segment parser
                }
                else if (config.ActionType == "ModeControlDial") 
                {
                    this.ParseAndStoreModeControlDialConfig(config, actionParameter); // This also calls the core segment parser for its internal modes
                }
            }
        }
        #endregion

        #region 公共访问与核心逻辑

        public IReadOnlyDictionary<String, ButtonConfig> GetAllConfigs() => this._allConfigs;
        public ButtonConfig GetConfig(String actionParameter) => this._allConfigs.TryGetValue(actionParameter, out var c) ? c : null;
        
        // 【新增】公共方法，用于按文件夹名称获取其内容
        public FolderContentConfig GetFolderContent(String folderName) => this._folderContents.TryGetValue(folderName, out var c) ? c : null;
        
        public Newtonsoft.Json.Linq.JObject GetFxData(String fxFolderName) => this._fxDataCache.TryGetValue(fxFolderName, out var data) ? data : null;

        public ButtonConfig GetConfigByDisplayName(String groupName, String displayName)
        {
            if (groupName == "Dynamic") // 对于动态文件夹入口的查找
            { 
                // 文件夹入口的 actionParameter 直接是其 DisplayName
                // 同时检查 GroupName 是否为 "Dynamic" 以增加准确性
                return this._allConfigs.TryGetValue(displayName, out var c) && c != null && c.GroupName == groupName ? c : null; 
            }

            // 构建基于JSON GroupName的actionParameter进行精确查找
            String actionParameterKey = $"/{SanitizeOscPathSegment(groupName)}/{SanitizeOscPathSegment(displayName)}";
            String actionParameterKeyDial = actionParameterKey + "/DialAction";

            if (this._allConfigs.TryGetValue(actionParameterKey, out var configButton))
            {
                return configButton;
            }
            if (this._allConfigs.TryGetValue(actionParameterKeyDial, out var configDial))
            {
                return configDial;
            }

            // Fallback: 遍历查找 (通常不应依赖此)
            return this._allConfigs.Values.FirstOrDefault(c => c.GroupName == groupName && c.DisplayName == displayName);
        }

        private void OnOSCStateChanged(Object sender, OSCStateManager.StateChangedEventArgs e)
        {
            PluginLog.Info($"[LogicManager|OnOSCStateChanged] Received OSC: Address='{e.Address}', Value='{(e.IsString ? e.StringValue : e.Value.ToString(System.Globalization.CultureInfo.InvariantCulture))}' IsString={e.IsString}"); 

            // --- 新增：提前退出逻辑 ---
            // 使用 IsAddressRelevant 方法进行统一判断
            if (!this.IsAddressRelevant(e.Address))
            {
                PluginLog.Verbose($"[LogicManager|OnOSCStateChanged] OSC Address '{e.Address}' is not relevant for dynamic titles or actions. Skipping further processing.");
                return;
            }
            // --- 结束：提前退出逻辑 ---

            bool isRelevantForDynamicTitle = this._oscAddressToDynamicTitleKeys.ContainsKey(e.Address);
            // bool isRelevantForAction = this._oscAddressToActionParameterMap.ContainsKey(e.Address); // 已在IsAddressRelevant中检查

            // 【修改】处理动态标题更新 - 使用新的反向映射字典
            if (isRelevantForDynamicTitle && this._oscAddressToDynamicTitleKeys.TryGetValue(e.Address, out var uniqueKeysToUpdateList))
            {
                string newTemplate = e.IsString ? e.StringValue : e.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
                foreach (string uniqueKey in uniqueKeysToUpdateList)
                {
                    // 检查值是否真的改变了，避免不必要的更新和日志
                    if (!_dynamicTitleSourceStrings.TryGetValue(uniqueKey, out var oldTemplate) || oldTemplate != newTemplate)
                    {
                        _dynamicTitleSourceStrings[uniqueKey] = newTemplate;
                        PluginLog.Info($"[LogicManager|OnOSCStateChanged] Dynamic title for key '{uniqueKey}' (OSC Address: '{e.Address}') updated to template: '{newTemplate}'");
                        
                        string baseActionParameter = ExtractBaseActionParameterFromUniqueKey(uniqueKey);
                        if (!String.IsNullOrEmpty(baseActionParameter))
                        {
                            this.CommandStateNeedsRefresh?.Invoke(this, baseActionParameter);
                            PluginLog.Verbose($"[LogicManager|OnOSCStateChanged] Triggered CommandStateNeedsRefresh for baseActionParameter '{baseActionParameter}' due to dynamic title update for key '{uniqueKey}'.");
                        }
                        else
                        {
                            PluginLog.Warning($"[LogicManager|OnOSCStateChanged] Could not extract baseActionParameter from uniqueKey '{uniqueKey}' for dynamic title update.");
                        }
                    }
                    else
                    {
                        PluginLog.Verbose($"[LogicManager|OnOSCStateChanged] Dynamic title for key '{uniqueKey}' (OSC Address: '{e.Address}') received same template value ('{newTemplate}'). No state change or refresh triggered for this key.");
                    }
                }
            }
            // --- 结束：处理动态标题更新 ---

            // 后续的功能性状态更新逻辑 (例如 ToggleButton, ControlDial)
            // 仅当地址与功能性操作相关时执行
            if (this._oscAddressToActionParameterMap.TryGetValue(e.Address, out String mappedActionParameter)) // isRelevantForAction 隐含在此查找中
            {
                var config = this.GetConfig(mappedActionParameter);
                if (config == null && mappedActionParameter.Contains("#")) // 可能是 ModeControlDial 的内部模式
                {
                    var parts = mappedActionParameter.Split('#');
                    if (parts.Length == 2)
                    {
                        config = this.GetConfig(parts[0]); // 获取 ModeControlDial 的顶层配置
                    }
                }

                if (config == null)
                {
                    PluginLog.Warning($"[LogicManager|OnOSCStateChanged] Matched OSC Address '{e.Address}' to mappedKey '{mappedActionParameter}', but config is null."); 
                    return;
                }
                PluginLog.Info($"[LogicManager|OnOSCStateChanged] Matched OSC Address '{e.Address}' to mappedKey '{mappedActionParameter}' (Config: '{config.DisplayName}', Type: '{config.ActionType}')."); 

                if (config.ActionType == "ToggleButton" || config.ActionType == "ToggleDial")
                {
                    var newState = e.Value > 0.5f;
                    // mappedActionParameter 就是 ToggleButton/Dial 的全局 actionParameter
                    if (!this._toggleStates.ContainsKey(mappedActionParameter) || this._toggleStates[mappedActionParameter] != newState) 
                    {
                        this._toggleStates[mappedActionParameter] = newState;
                        PluginLog.Info($"[LogicManager|OnOSCStateChanged] State for '{mappedActionParameter}' ('{config.DisplayName}') changed to {newState}. Invoking CommandStateNeedsRefresh."); 
                        this.CommandStateNeedsRefresh?.Invoke(this, mappedActionParameter);
                    }
                    else
                    {
                        PluginLog.Verbose($"[LogicManager|OnOSCStateChanged] State for '{mappedActionParameter}' ('{config.DisplayName}') already {newState}. No change needed."); 
                    }
                }
                else if (config.ActionType == "ControlDial") // 普通 ControlDial
                {
                    if (this._controlDialConfigs.TryGetValue(mappedActionParameter, out var parsedDialConfig) && 
                        this._controlDialCurrentValues.ContainsKey(mappedActionParameter))
                    {
                        Single incomingFloatValue = e.Value; 
                        Single validatedValue = incomingFloatValue;
                        Single currentStoredValue = this._controlDialCurrentValues[mappedActionParameter];

                        if (parsedDialConfig.Mode == ControlDialMode.Continuous)
                        {
                            validatedValue = Math.Clamp(incomingFloatValue, parsedDialConfig.MinValue, parsedDialConfig.MaxValue);
                        }
                        else // Discrete Mode
                        {
                            // 对于离散值，通常期望精确匹配。如果OSC传入的值不在列表中，则忽略。
                            const Single tolerance = 0.0001f; // 浮点数比较容差
                            if (!parsedDialConfig.DiscreteValues.Any(dv => Math.Abs(dv - incomingFloatValue) < tolerance))
                            {
                                PluginLog.Warning($"[LogicManager|OnOSCStateChanged] ControlDial '{config.DisplayName}' (action: {mappedActionParameter}) received OSC value {incomingFloatValue} which is not in its discrete list of values (with tolerance). Ignoring update from OSC.");
                                return; 
                            }
                            // 如果找到了，使用列表中的精确值以避免浮点误差累积
                            validatedValue = parsedDialConfig.DiscreteValues.First(dv => Math.Abs(dv - incomingFloatValue) < tolerance);
                        }

                        if (Math.Abs(validatedValue - currentStoredValue) > 0.00001f) // 只有当值确实改变时才更新和通知
                        {
                            this._controlDialCurrentValues[mappedActionParameter] = validatedValue;
                            PluginLog.Info($"[LogicManager|OnOSCStateChanged] ControlDial '{config.DisplayName}' (action: {mappedActionParameter}) state updated via OSC from {currentStoredValue:F3} to {validatedValue:F3}. Address: {e.Address}");
                            this.CommandStateNeedsRefresh?.Invoke(this, mappedActionParameter);
                        }
                        else
                        {
                            PluginLog.Verbose($"[LogicManager|OnOSCStateChanged] ControlDial '{config.DisplayName}' (action: {mappedActionParameter}) received OSC value {validatedValue:F3} which matches current state. No change needed from OSC.");
                        }
                    }
                    else
                    {
                        PluginLog.Warning($"[LogicManager|OnOSCStateChanged] ControlDial '{config.DisplayName}' (action: {mappedActionParameter}) received OSC for address '{e.Address}', but its internal config or current value was not found.");
                    }
                }
                else if (config.ActionType == "ModeControlDial") // 【新增】处理 ModeControlDial
                {
                    var parts = mappedActionParameter.Split('#'); // mappedActionParameter is compoundKey actionParam#internalIdx
                    if (parts.Length == 2 && Int32.TryParse(parts[1], out var internalModeIdx))
                    {
                        String modeDialActionParam = parts[0];
                        // 顶层 ModeControlDial 的配置 (config 变量已经是它了)
                        if (this._modeControlDialParsedConfigs.TryGetValue(modeDialActionParam, out var internalConfigsList) &&
                            internalModeIdx >= 0 && internalModeIdx < internalConfigsList.Count &&
                            this._modeControlDialCurrentValuesByMode.TryGetValue(modeDialActionParam, out var internalValuesList) &&
                            internalModeIdx < internalValuesList.Count)
                        {
                            ControlDialParsedConfig targetInternalConfig = internalConfigsList[internalModeIdx];
                            Single incomingOscValue = e.Value; // 外部OSC值是参数刻度值
                            Single validatedParamValueForStorage = incomingOscValue; 

                            if (targetInternalConfig.Mode == ControlDialMode.Continuous)
                            {
                                validatedParamValueForStorage = Math.Clamp(incomingOscValue, targetInternalConfig.MinValue, targetInternalConfig.MaxValue);
                            }
                            else // Discrete Mode
                            {
                                const Single tolerance = 0.0001f;
                                if (!targetInternalConfig.DiscreteValues.Any(dv => Math.Abs(dv - incomingOscValue) < tolerance))
                                {
                                    PluginLog.Warning($"[LogicManager|OnOSCStateChanged] ModeControlDial '{config.DisplayName}' (action: {modeDialActionParam}, internalMode: {internalModeIdx}) received OSC value {incomingOscValue} which is not in its discrete list (with tolerance). Ignoring.");
                                    return;
                                }
                                validatedParamValueForStorage = targetInternalConfig.DiscreteValues.First(dv => Math.Abs(dv - incomingOscValue) < tolerance);
                            }

                            if (Math.Abs(internalValuesList[internalModeIdx] - validatedParamValueForStorage) > 0.00001f)
                            {
                                internalValuesList[internalModeIdx] = validatedParamValueForStorage;
                                PluginLog.Info($"[LogicManager|OnOSCStateChanged] ModeControlDial '{config.DisplayName}' (action: {modeDialActionParam}, internalMode: {internalModeIdx}, ExtMode: {this.GetCurrentModeString(config.ModeName)}) value updated to {validatedParamValueForStorage:F3} via OSC from {e.Address}.");

                                Int32 activeExternalModeIndex = this.GetCurrentModeIndex(config.ModeName);
                                if (activeExternalModeIndex == internalModeIdx)
                                {
                                    this.CommandStateNeedsRefresh?.Invoke(this, modeDialActionParam); // 使用 ModeControlDial 的顶层 actionParameter 刷新
                                    PluginLog.Info($"[LogicManager|OnOSCStateChanged] ModeControlDial '{config.DisplayName}' (action: {modeDialActionParam}): Active internal mode ({internalModeIdx}) updated. Triggering UI refresh.");
                                }
                                else
                                {
                                    PluginLog.Info($"[LogicManager|OnOSCStateChanged] ModeControlDial '{config.DisplayName}' (action: {modeDialActionParam}): Internal mode ({internalModeIdx}) updated, but it is not the currently active external mode ({activeExternalModeIndex}). UI not refreshed immediately.");
                                }
                            }
                        }
                        else
                        {
                             PluginLog.Warning($"[LogicManager|OnOSCStateChanged] ModeControlDial '{config.DisplayName}': Could not find parsed internal config or values list for compound key '{mappedActionParameter}'. ActionParam: {modeDialActionParam}, InternalIdx: {internalModeIdx}");
                        }
                    }
                    else
                    {
                        PluginLog.Warning($"[LogicManager|OnOSCStateChanged] ModeControlDial '{config.DisplayName}': Could not parse compound key '{mappedActionParameter}' from OSC map for an incoming message. This internal mode might not have been registered for listening, or the compound key format is unexpected.");
                    }
                }
            }
            else if (!isRelevantForDynamicTitle) // 仅当它也不是动态标题地址时，才记录"未在功能映射中找到"
            {
                PluginLog.Info($"[LogicManager|OnOSCStateChanged] OSC Address '{e.Address}' NOT found in _oscAddressToActionParameterMap (and was not a dynamic title)."); 
            }
        }

        public Boolean GetToggleState(String actionParameter) => this._toggleStates.TryGetValue(actionParameter, out var s) && s;
        public void SetToggleState(String actionParameter, Boolean state)
        {
            this._toggleStates[actionParameter] = state;
            this.CommandStateNeedsRefresh?.Invoke(this, actionParameter); // 【修正】确保手动设置状态也刷新UI
        }
        public Int32 GetDialMode(String actionParameter) => this._dialModes.TryGetValue(actionParameter, out var m) ? m : 0;
        public Int32 GetParameterDialSelectedIndex(String actionParameter) => this._parameterDialSelectedIndexes.TryGetValue(actionParameter, out var index) ? index : 0;
        public String GetParameterDialSelectedTitle(String actionParameter)
        {
            var config = this.GetConfig(actionParameter);
            if (config?.ActionType == "ParameterDial" && config.Titles != null && config.Titles.Count > 0)
            {
                var index = this.GetParameterDialSelectedIndex(actionParameter);
                if (index >= 0 && index < config.Titles.Count)
                { return config.Titles[index]; }
            }
            return null;
        }

        // 【新增】获取 ControlDial 当前值
        public Single GetControlDialValue(String actionParameter)
        {
            return this._controlDialCurrentValues.TryGetValue(actionParameter, out var val) ? val : 0f;
        }

        public Boolean ProcessUserAction(String actionParameter, String dynamicFolderDisplayName = null, ButtonConfig itemConfig = null)
        {
            var config = itemConfig ?? this.GetConfig(actionParameter);
            
            if (config == null)
            {
                PluginLog.Warning($"[LogicManager] ProcessUserAction: 未找到配置 for '{actionParameter}', 且未提供 itemConfig。");
                return false;
            }

            Boolean needsUiRefresh = false;
            String oscAddressToSend = null;
            Single oscValueToSend = 1.0f; 
            Boolean sendOsc = false;
            // String finalOscSegmentForAction = null; //不再需要，GetResolvedOscAddress会处理

            switch (config.ActionType)
            {
                case "ToggleButton":
                    this.SetToggleState(actionParameter, !this.GetToggleState(actionParameter)); 
                    oscValueToSend = this.GetToggleState(actionParameter) ? 1.0f : 0.0f;
                    sendOsc = true;

                    String toggleButtonOscTemplate = null;
                    if (!String.IsNullOrEmpty(config.ModeName) && config.OscAddresses != null && config.OscAddresses.Any())
                    {
                        var currentModeIndex = this.GetCurrentModeIndex(config.ModeName);
                        if (currentModeIndex >= 0 && currentModeIndex < config.OscAddresses.Count && !String.IsNullOrEmpty(config.OscAddresses[currentModeIndex]))
                        {
                            toggleButtonOscTemplate = config.OscAddresses[currentModeIndex];
                            PluginLog.Info($"[LogicManager|ProcessUserAction] ToggleButton '{config.DisplayName}' with ModeName '{config.ModeName}', using template from OscAddresses[{currentModeIndex}]: '{toggleButtonOscTemplate}'");
                        }
                        else
                        {
                            toggleButtonOscTemplate = config.OscAddress; // 回退到通用 OscAddress
                            PluginLog.Info($"[LogicManager|ProcessUserAction] ToggleButton '{config.DisplayName}' with ModeName '{config.ModeName}', mode index invalid or OscAddresses template empty. Falling back to config.OscAddress: '{toggleButtonOscTemplate ?? "null"}'");
                        }
                    }
                    else
                    {
                        toggleButtonOscTemplate = config.OscAddress; // 普通ToggleButton或无有效模式特定地址
                         PluginLog.Info($"[LogicManager|ProcessUserAction] ToggleButton '{config.DisplayName}' (no ModeName or no OscAddresses). Using config.OscAddress: '{toggleButtonOscTemplate ?? "null"}'");
                    }
                    
                    oscAddressToSend = this.GetResolvedOscAddress(config, toggleButtonOscTemplate);
                    PluginLog.Info($"[LogicManager|ProcessUserAction] ToggleButton '{config.DisplayName}', resolved OSC address: '{oscAddressToSend}'");
                    break;

                case "TriggerButton":
                    if (itemConfig != null && !String.IsNullOrEmpty(dynamicFolderDisplayName)) // 动态文件夹列表项的特殊处理
                    {
                        // 确定动作特定部分的模板，新优先级：OscAddress > DisplayName > Title
                        var actionSegmentTemplate = itemConfig.OscAddress;
                        if (String.IsNullOrEmpty(actionSegmentTemplate))
                        {
                            actionSegmentTemplate = itemConfig.DisplayName; 
                        }
                        if (String.IsNullOrEmpty(actionSegmentTemplate))
                        {
                            actionSegmentTemplate = itemConfig.Title; 
                        }

                        if (itemConfig.UseOwnGroupAsRoot && !String.IsNullOrEmpty(itemConfig.GroupName))
                        {
                            // 情况1: 按钮有自己的 GroupName 且应作为根 (通常是直接在 Content.Buttons 中定义的)
                            // GetResolvedOscAddress 将使用 itemConfig.GroupName 作为 basePart
                            oscAddressToSend = this.GetResolvedOscAddress(itemConfig, actionSegmentTemplate);
                            PluginLog.Info($"[LogicManager|ProcessUserAction] Dynamic TriggerButton '{itemConfig.DisplayName}' (Folder: '{dynamicFolderDisplayName}') uses OwnGroup '{itemConfig.GroupName}' as root. Template: '{actionSegmentTemplate ?? "null"}'. Resolved OSC: '{oscAddressToSend}'");
                        }
                        else
                        {
                            // 情况2: 按钮 GroupName 是派生的或不作为根 (通常是 _List.json 加载的项，或 Content.Buttons 中无 GroupName 的项)
                            var folderPart = FormatTopLevelOscPathSegment(dynamicFolderDisplayName);
                            var itemGroupPart = SanitizeOscPathSegment(itemConfig.GroupName); // 按钮自己的 GroupName (可能是派生的或默认的)
                            var resolvedActionSegment = ResolveTextWithMode(itemConfig, actionSegmentTemplate); // 解析模板中的{mode}

                            if (!String.IsNullOrEmpty(resolvedActionSegment) && resolvedActionSegment.StartsWith("/"))
                            {
                                oscAddressToSend = SanitizeOscAddress(resolvedActionSegment);
                                PluginLog.Info($"[LogicManager|ProcessUserAction] Dynamic TriggerButton '{itemConfig.DisplayName}' (Folder: '{dynamicFolderDisplayName}', ItemGroup: '{itemConfig.GroupName ?? "null"}') used ABSOLUTE path from its template: '{oscAddressToSend}'");
                            }
                            else
                            {
                                List<String> pathParts = new List<String>();
                                if (!String.IsNullOrEmpty(folderPart) && folderPart != "/")
                                {
                                    pathParts.Add(folderPart);
                                }


                                if (!String.IsNullOrEmpty(itemGroupPart) && itemGroupPart != "/")
                                {
                                    pathParts.Add(itemGroupPart);
                                }


                                var sanitizedActionSegmentForPath = SanitizeOscPathSegment(resolvedActionSegment);
                                if (!String.IsNullOrEmpty(sanitizedActionSegmentForPath) && sanitizedActionSegmentForPath != "/")
                                {
                                    pathParts.Add(sanitizedActionSegmentForPath);
                                }


                                if (pathParts.Any())
                                {
                                    oscAddressToSend = "/" + String.Join("/", pathParts.Where(s => !String.IsNullOrEmpty(s))); 
                                    oscAddressToSend = oscAddressToSend.Replace("//", "/").TrimEnd('/');
                                    if (oscAddressToSend == "/")
                                    {
                                        oscAddressToSend = "";
                                    }

                                }
                                else { oscAddressToSend = ""; }
                                PluginLog.Info($"[LogicManager|ProcessUserAction] Dynamic TriggerButton '{itemConfig.DisplayName}' (Folder: '{dynamicFolderDisplayName}', ItemGroup: '{itemConfig.GroupName ?? "null"}'). PathParts: [{String.Join(", ", pathParts)}]. Constructed OSC: '{oscAddressToSend}'");
                            }
                        }
                        
                        if (String.IsNullOrEmpty(oscAddressToSend)) 
                        {
                            PluginLog.Warning($"[LogicManager|ProcessUserAction] Generated OSC address for dynamic item '{itemConfig.DisplayName}' in folder '{dynamicFolderDisplayName}' was effectively empty or invalid. OSC not sent.");
                        }
                    }
                    else // 普通 TriggerButton (非动态列表项)
                    {
                        var triggerButtonOscTemplate = config.OscAddress;
                        oscAddressToSend = this.GetResolvedOscAddress(config, triggerButtonOscTemplate); 
                        PluginLog.Info($"[LogicManager|ProcessUserAction] Static TriggerButton '{config.DisplayName}', resolved OSC address: '{oscAddressToSend}'");
                    }
                    oscValueToSend = 1.0f;
                    sendOsc = true;
                    this.CommandStateNeedsRefresh?.Invoke(this, actionParameter); 
                    break;

                case "CombineButton":
                    this.ProcessCombineButtonAction(config, dynamicFolderDisplayName); 
                    this.CommandStateNeedsRefresh?.Invoke(this, actionParameter);
                    sendOsc = false; 
                    break;

                case "SelectModeButton":
                    this.ToggleMode(config.DisplayName); 
                    needsUiRefresh = true; 
                    sendOsc = false; 
                    break;
                default:
                    PluginLog.Warning($"[LogicManager] ProcessUserAction: 未处理的 ActionType '{config.ActionType}' for '{config.DisplayName}' (actionParameter: '{actionParameter}')");
                    break;
            }

            PluginLog.Info($"[LogicManager] ProcessUserAction: PRE-SEND CHECK: Config='{config.DisplayName ?? "N/A"}', ActionType='{config.ActionType ?? "N/A"}', GroupName='{config.GroupName ?? "N/A"}', ModeName='{(String.IsNullOrEmpty(config.ModeName) ? "N/A" : config.ModeName)}', CurrentMode='{(String.IsNullOrEmpty(config.ModeName) ? "N/A" : GetCurrentModeString(config.ModeName))}', InitialExplicitSegment='{(oscAddressToSend ?? "null")}', DeterminedOscAddress='{(oscAddressToSend ?? "Not set or N/A for this ActionType")}', Value='{oscValueToSend}'");

            if (sendOsc) 
            {
                if (!String.IsNullOrEmpty(oscAddressToSend) && oscAddressToSend != "/" && !oscAddressToSend.Contains("{mode}"))
                {
                    ReaOSCPlugin.SendOSCMessage(oscAddressToSend, oscValueToSend); 
                    PluginLog.Info($"[LogicManager] ProcessUserAction: OSC已发送 '{oscAddressToSend}' -> {oscValueToSend} (源: '{config.DisplayName ?? "N/A"}', ActionType: {config.ActionType ?? "N/A"})");
                }
                else
                {
                    PluginLog.Warning($"[LogicManager] ProcessUserAction: 无效或未解析的OSC地址，未发送。Config='{config.DisplayName ?? "N/A"}', ActionType='{config.ActionType ?? "N/A"}', Calculated OSC Address='{oscAddressToSend ?? "null"}'. Review mode configuration and OSC address templates.");
                }
            }
            return needsUiRefresh;
        }

        public void ProcessGeneralButtonPress(ButtonConfig config, String actionParameterKey)
        {
            if (config == null)
            {
                config = this.GetConfig(actionParameterKey); // 如果传入的是key而非config对象
                if (config == null)
                {
                    PluginLog.Error($"[LogicManager] ProcessGeneralButtonPress: Config not found for key '{actionParameterKey}'.");
                    return;
                }
            }

            Single valueToSend = 1f; // Single for float
            if (config.ActionType == "ToggleButton")
            {
                this.SetToggleState(actionParameterKey, !this.GetToggleState(actionParameterKey)); // 使用actionParameterKey作为状态键
                valueToSend = this.GetToggleState(actionParameterKey) ? 1f : 0f;
            }
            // 【修正OSC地址规则】使用控件自身的JSON GroupName
            String oscAddressToSend = this.DetermineOscAddressForAction(config, config.GroupName);
            if (!String.IsNullOrEmpty(oscAddressToSend) && oscAddressToSend != "/")
            { ReaOSCPlugin.SendOSCMessage(oscAddressToSend, valueToSend); }
            else
            { PluginLog.Warning($"[LogicManager] GeneralButtonPress '{actionParameterKey}' (JSON GroupName '{config.GroupName}') 无法确定有效OSC地址。"); }
        }
        public void ProcessFxButtonPress(String actionParameter) => ReaOSCPlugin.SendFXMessage(actionParameter, 1);


        public void ProcessDialAdjustment(String globalActionParameter, Int32 ticks)
        {
            var config = this.GetConfig(globalActionParameter);
            if (config == null) { PluginLog.Warning($"[LogicManager] ProcessDialAdjustment (New): Config not found for '{globalActionParameter}'"); return; }

            switch (config.ActionType)
            {
                case "ParameterDial": 
                    // ParameterDial 的配置信息在其自身的 config 对象中
                    if (config.Titles != null && config.Titles.Any()) 
                    {
                        var currentIndex = this.GetParameterDialSelectedIndex(globalActionParameter);
                        currentIndex += ticks;
                        if (currentIndex >= config.Titles.Count) { currentIndex = 0; }
                        else if (currentIndex < 0) { currentIndex = config.Titles.Count - 1; }
                        this._parameterDialSelectedIndexes[globalActionParameter] = currentIndex;
                        this.CommandStateNeedsRefresh?.Invoke(this, globalActionParameter);
                        this.NotifyLinkedParameterButtons(config.DisplayName, config.GroupName); 
                    } 
                    break;
                case "TickDial": 
                case "2ModeTickDial":
                    this.ProcessLegacyDialAdjustmentInternal(config, ticks, globalActionParameter, null); 
                    this.CommandStateNeedsRefresh?.Invoke(this, globalActionParameter); 
                    break;
                case "ToggleDial": 
                    this.ProcessLegacyToggleDialAdjustmentInternal(config, ticks, globalActionParameter);
                    this.CommandStateNeedsRefresh?.Invoke(this, globalActionParameter); 
                    break;
                case "ControlDial":
                case "ModeControlDial": // 【新增】ModeControlDial 路由到新的处理逻辑
                    PluginLog.Info($"[LogicManager|ProcessDialAdjustment] Routing '{config.ActionType}' '{globalActionParameter}' to specific handler.");
                    this.ProcessAdvancedDialAdjustment(globalActionParameter, ticks); // 新的方法处理 ControlDial 和 ModeControlDial
                            this.CommandStateNeedsRefresh?.Invoke(this, globalActionParameter);
                    break;
                default:
                    PluginLog.Warning($"[LogicManager] ProcessDialAdjustment (New): Unhandled ActionType '{config.ActionType}' for '{globalActionParameter}'");
                    break;
            }
        }

        public void ProcessDialAdjustment(ButtonConfig config, Int32 ticks, String actionParameter, Dictionary<String, DateTime> lastEventTimes)
        {
            if (config == null)
            { config = this.GetConfig(actionParameter); if (config == null) { PluginLog.Error($"[LogicManager] ProcessDialAdjustment (Legacy): Config is null and not found for '{actionParameter}'."); return; } }

            switch (config.ActionType)
            {
                case "TickDial":
                case "2ModeTickDial":
                    this.ProcessLegacyDialAdjustmentInternal(config, ticks, actionParameter, lastEventTimes);
                    break;
                case "ToggleDial":
                    this.ProcessLegacyToggleDialAdjustmentInternal(config, ticks, actionParameter);
                    break;
                case "ParameterDial":
                    this.ProcessDialAdjustment(actionParameter, ticks); // 调用新版，只传 actionParameter 和 ticks
                    break;
                // 【新增】确保 ControlDial 被路由到新版 ProcessDialAdjustment
                case "ControlDial": 
                case "ModeControlDial": // 【新增】ModeControlDial 路由到新逻辑
                    this.ProcessDialAdjustment(actionParameter, ticks); 
                    break;
                default:
                    PluginLog.Warning($"[LogicManager] ProcessDialAdjustment (Legacy): 未处理的 ActionType '{config.ActionType}' for '{actionParameter}'");
                    break;
            }
        }

        public Boolean ProcessDialPress(String globalActionParameter)
        {
            var config = this.GetConfig(globalActionParameter);
            if (config == null) { PluginLog.Warning($"[LogicManager] ProcessDialPress (New): Config not found for '{globalActionParameter}'"); return false; }

            Boolean uiShouldRefresh = false;
            switch (config.ActionType)
            {
                case "ParameterDial":
                    // ParameterDial 按下通常无操作或由其自身逻辑处理
                    break; 
                case "2ModeTickDial":
                    this._dialModes[globalActionParameter] = (this.GetDialMode(globalActionParameter) + 1) % 2;
                    uiShouldRefresh = true;
                    // 2ModeTickDial 的 ResetOSCAddress 发送逻辑在 SendResetOscForDial 中处理
                    if (!String.IsNullOrEmpty(config.ResetOscAddress)) { this.SendResetOscForDial(config, globalActionParameter); }
                    break;
                case "TickDial":
                case "ToggleDial":
                    if (!String.IsNullOrEmpty(config.ResetOscAddress)) { this.SendResetOscForDial(config, globalActionParameter); }
                    break;
                case "ControlDial":
                case "ModeControlDial": // 【新增】ModeControlDial 路由到新逻辑
                    this.ProcessAdvancedDialPress(globalActionParameter); // 新的方法处理 ControlDial 和 ModeControlDial 按下
                    // ProcessAdvancedDialPress 内部会处理UI刷新
                    break;
                default:
                    PluginLog.Warning($"[LogicManager] ProcessDialPress (Legacy): 未处理的 ActionType '{config.ActionType}' for '{globalActionParameter}'");
                    break;
                        }
            if (uiShouldRefresh && config.ActionType != "ControlDial" && config.ActionType != "ModeControlDial") 
            { 
                this.CommandStateNeedsRefresh?.Invoke(this, globalActionParameter); 
            }
            return uiShouldRefresh; 
        }

        private void SendResetOscForDial(ButtonConfig config, String actionParameterKey)
        {
            // 【修改】使用新的 GetResolvedOscAddress 方法
            String fullResetAddress = this.GetResolvedOscAddress(config, config.ResetOscAddress);
            if (!String.IsNullOrEmpty(fullResetAddress) && fullResetAddress != "/") // 确保地址有效且不是根
            { 
                ReaOSCPlugin.SendOSCMessage(fullResetAddress, 1f); 
            }
            else
            { 
                PluginLog.Warning($"[LogicManager|SendResetOscForDial] Dial Reset for '{config.DisplayName}' (action: {actionParameterKey}, JSON Group: '{config.GroupName}') using template '{config.ResetOscAddress ?? "(null - will use DisplayName/Title)"}' generated an invalid OSC address: '{fullResetAddress ?? "null"}'. OSC not sent."); 
            }
            this.CommandStateNeedsRefresh?.Invoke(this, actionParameterKey);
        }


        public Boolean ProcessDialPress(ButtonConfig config, String actionParameter)
        {
            if (config == null)
            { config = this.GetConfig(actionParameter); if (config == null) { PluginLog.Error($"[LogicManager] ProcessDialPress (Legacy): Config is null and not found for '{actionParameter}'."); return false; } }

            Boolean modeChanged = false;
            if (config.ActionType == "2ModeTickDial")
            {
                this._dialModes[actionParameter] = (this.GetDialMode(actionParameter) + 1) % 2;
                modeChanged = true;
                this.CommandStateNeedsRefresh?.Invoke(this, actionParameter);
            }
            // 【修正OSC地址规则】使用辅助方法或直接确保 DetermineOscAddressForAction 使用 config.GroupName
            if (!String.IsNullOrEmpty(config.ResetOscAddress) &&
                (config.ActionType == "TickDial" || config.ActionType == "ToggleDial" || config.ActionType == "2ModeTickDial"))
            {
                this.SendResetOscForDial(config, actionParameter);
            }
            else if (config.ActionType == "ParameterDial")
            {
                return this.ProcessDialPress(actionParameter);
            }
            else if (config.ActionType == "ControlDial") // 【新增】适配旧版 ProcessDialPress
            {
                return this.ProcessDialPress(actionParameter); // 这会调用到上面的新版 ProcessDialPress
            }
            else if (config.ActionType == "ModeControlDial") // 【新增】
            {
                return this.ProcessDialPress(actionParameter); // 这会调用到上面的新版 ProcessDialPress
            }
            return modeChanged;
        }

        private void ProcessCombineButtonAction(ButtonConfig combineButtonConfig, String dynamicFolderDisplayNameForLog) // dynamicFolderDisplayNameForLog 用于日志，不再用于OSC路径
        {
            if (combineButtonConfig.ParameterOrder == null || combineButtonConfig.ParameterOrder.Count == 0)
            { PluginLog.Warning($"[LogicManager] CombineButton '{combineButtonConfig.DisplayName}' (JSON Group: '{combineButtonConfig.GroupName}') has no ParameterOrder. Folder context: '{dynamicFolderDisplayNameForLog}'."); return; }

            // 【修正OSC地址规则】最终OSC地址的基路径使用CombineButton的JSON GroupName
            String oscMessageBasePath = SanitizeOscPathSegment(combineButtonConfig.GroupName);
            List<String> pathSegmentsForOsc = new List<String> { oscMessageBasePath };

            // 【修正依赖控件查找规则】依赖控件的actionParameter也是基于其JSON GroupName注册的。
            // CombineButton与其依赖控件在同一个_List.json中，共享相同的JSON GroupName。
            String lookupKeyBasePathForDependents = SanitizeOscPathSegment(combineButtonConfig.GroupName);

            foreach (var targetDisplayNameInOrder in combineButtonConfig.ParameterOrder)
            {
                String controlActionParameterKey = $"/{lookupKeyBasePathForDependents}/{SanitizeOscPathSegment(targetDisplayNameInOrder)}";
                String controlActionParameterKeyDial = controlActionParameterKey + "/DialAction";

                ButtonConfig targetConfig = this.GetConfig(controlActionParameterKey) ?? this.GetConfig(controlActionParameterKeyDial);

                if (targetConfig == null)
                {
                    // 【修正Fallback的比较】应该用 combineButtonConfig.GroupName (即 lookupKeyBasePathForDependents)
                    targetConfig = this._allConfigs.Values.FirstOrDefault(c => c.GroupName == combineButtonConfig.GroupName && c.DisplayName == targetDisplayNameInOrder);
                    if (targetConfig == null)
                    {
                        PluginLog.Error($"[LogicManager] CombineButton '{combineButtonConfig.DisplayName}': Cannot find dependent control '{targetDisplayNameInOrder}' using key pattern '/{lookupKeyBasePathForDependents}/{SanitizeOscPathSegment(targetDisplayNameInOrder)}' or by direct Group/Display match. Folder context: '{dynamicFolderDisplayNameForLog}'.");
                        continue;
                    }
                }

                String originalSegmentValue = null;
                Boolean addSegment = false;
                // targetGlobalActionParam 就是我们用来成功找到 targetConfig 的键
                String targetGlobalActionParam = (this.GetConfig(controlActionParameterKey) != null) ? controlActionParameterKey : controlActionParameterKeyDial;
                // 如果是通过 fallback 找到的，需要重新确定其 globalActionParameter
                if (this.GetConfig(controlActionParameterKey) == null && this.GetConfig(controlActionParameterKeyDial) == null && targetConfig != null)
                {
                    targetGlobalActionParam = this.GetActionParameterFromConfig(targetConfig, targetConfig.GroupName); // 使用它自己的JSON GroupName
                }


                if (targetConfig.ActionType == "ParameterDial")
                {
                    originalSegmentValue = this.GetParameterDialSelectedTitle(targetGlobalActionParam);
                    addSegment = !String.IsNullOrEmpty(originalSegmentValue);
                }
                else if (targetConfig.ActionType == "ToggleButton")
                {
                    Boolean isToggleOn = this.GetToggleState(targetGlobalActionParam);
                    if (isToggleOn)
                    {
                        // ToggleButton贡献给CombineButton的路径片段：优先用OscAddress字段，否则用DisplayName
                        originalSegmentValue = !String.IsNullOrEmpty(targetConfig.OscAddress) ? targetConfig.OscAddress : targetConfig.DisplayName;
                        addSegment = true;
                    }
                }

                if (addSegment && !String.IsNullOrEmpty(originalSegmentValue))
                {
                    pathSegmentsForOsc.Add(SanitizeOscPathSegment(originalSegmentValue));
                }
            }

            if (pathSegmentsForOsc.Count > (String.IsNullOrEmpty(oscMessageBasePath) || oscMessageBasePath == "/" ? 0 : 1))
            {
                String finalOscAddress = "/" + String.Join("/", pathSegmentsForOsc);
                finalOscAddress = finalOscAddress.Replace("//", "/");
                if (finalOscAddress == "/" && pathSegmentsForOsc.Count == 1 && (String.IsNullOrEmpty(pathSegmentsForOsc[0]) || pathSegmentsForOsc[0] == "/"))
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
        private String GetActionParameterFromConfig(ButtonConfig config, String jsonGroupName)
        {
            String groupPart = SanitizeOscPathSegment(jsonGroupName); // 参数重命名为jsonGroupName
            String namePart = SanitizeOscPathSegment(config.DisplayName);
            String key = $"/{groupPart}/{namePart}";
            if (config.ActionType != null && config.ActionType.Contains("Dial"))
            {
                key += "/DialAction";
            }
            return key.Replace("//", "/");
        }

        private void NotifyLinkedParameterButtons(String sourceDialDisplayName, String sourceDialJsonGroupName) // 参数改为sourceDialJsonGroupName
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
        public static String SanitizeOscPathSegment(String segment) { if (String.IsNullOrEmpty(segment)) { return ""; } var sanitized = segment.Replace(" ", "_"); sanitized = Regex.Replace(sanitized, @"[^\w_/-]", ""); return sanitized.Trim('/'); }
        
        // 【新增】专门处理第一级 OSC 路径片段的方法
        // 例如 "My Awesome Folder" -> "My/Awesome/Folder"
        public static String FormatTopLevelOscPathSegment(String segment)
        {
            if (String.IsNullOrEmpty(segment)) { return ""; }
            var parts = segment.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            // 对每个词块应用标准 SanitizeOscPathSegment 清理
            // SanitizeOscPathSegment 会处理掉部分内的空格（虽然这里通常不应该有，因为已经按空格分割了）和特殊字符
            var cleanedParts = parts.Select(p => SanitizeOscPathSegment(p.Trim())) 
                                    .Where(p => !String.IsNullOrEmpty(p));
            return String.Join("/", cleanedParts);
        }
        
        public static String SanitizeOscAddress(String address) { 
            if (String.IsNullOrEmpty(address)) { return "/"; } 
            // 对于一个完整的、可能是多段的地址，其内部的原始空格（如果存在且未被片段处理替换掉）应转为下划线
            var sanitized = address.Replace(" ", "_"); 
            sanitized = Regex.Replace(sanitized, @"[^\w_/-]", ""); 
            if (!sanitized.StartsWith("/")) 
            { 
                sanitized = "/" + sanitized; 
            } 
            var final = sanitized.Replace("//", "/").TrimEnd('/');
            return String.IsNullOrEmpty(final) ? "/" : final; // 确保空字符串结果也返回 "/"
        }

        // 【修正OSC地址规则】DetermineOscAddressForAction的第二个参数明确为jsonGroupNameForOsc
        private String DetermineOscAddressForAction(ButtonConfig config, String jsonGroupNameForOsc, String explicitOscAddressField = null)
        {
            // 1. 处理 OSC 地址的"根"或"前缀"部分, 这通常来自 jsonGroupNameForOsc (文件夹名或控件组名)
            //    根据新规则，这一部分如果包含空格，空格会变成路径分隔符 '/'
            String basePart = FormatTopLevelOscPathSegment(jsonGroupNameForOsc);

            // 2. 处理 OSC 地址的"具体动作"或"名称"部分
            String actionSpecificSource = explicitOscAddressField ?? config.OscAddress ?? config.Title ?? config.DisplayName;

            if (String.IsNullOrEmpty(actionSpecificSource)) 
            {
                // 如果没有任何可用的次级路径源，则仅使用 basePart (如果存在)
                return String.IsNullOrEmpty(basePart) ? "/" : $"/{basePart}".Replace("//", "/").TrimEnd('/');
            }

            // 如果 actionSpecificSource 是绝对路径 (以 "/" 开头)，则它覆盖 basePart
            if (actionSpecificSource.StartsWith("/"))
            {
                // 对绝对路径进行标准清理 (所有内部空格变下划线)
                return SanitizeOscAddress(actionSpecificSource); 
            }

            // 如果 actionSpecificSource 是相对路径片段，对其进行标准清理 (内部空格变下划线)
            String actionSpecificPart = SanitizeOscPathSegment(actionSpecificSource);

            // 3. 组合路径
            if (String.IsNullOrEmpty(basePart) || basePart == "/") // 如果没有有效 basePath
            {
                var finalAddressNoBase = $"/{actionSpecificPart}".Replace("//", "/").TrimEnd('/');
                return String.IsNullOrEmpty(finalAddressNoBase) ? "/" : finalAddressNoBase;
            }
            else
            {
                var finalAddressWithBase = $"/{basePart}/{actionSpecificPart}".Replace("//", "/").TrimEnd('/');
                return String.IsNullOrEmpty(finalAddressWithBase) ? "/" : finalAddressWithBase;
            }
        }
        #endregion

        #region 旧的旋钮处理逻辑 (内部实现) - 这些方法已经使用config.GroupName，符合规则
        private void ProcessLegacyDialAdjustmentInternal(ButtonConfig config, Int32 ticks, String actionParameter, Dictionary<String, DateTime> lastEventTimes)
        {
            String groupNameForPath = SanitizeOscPathSegment(config.GroupName); // 确保只声明一次
            String displayNameForPath = SanitizeOscPathSegment(config.DisplayName); // 确保只声明一次

            String jsonIncreaseAddress, jsonDecreaseAddress;
            if (config.ActionType == "2ModeTickDial" && this.GetDialMode(actionParameter) == 1)
            { 
                jsonIncreaseAddress = config.IncreaseOSCAddress_Mode2; 
                jsonDecreaseAddress = config.DecreaseOSCAddress_Mode2; 
            }
            else
            { 
                jsonIncreaseAddress = config.IncreaseOSCAddress; 
                jsonDecreaseAddress = config.DecreaseOSCAddress; 
            }

            // 【修改】使用 GetResolvedOscAddress，并优化回退逻辑
            String finalIncreaseOscAddress = this.GetResolvedOscAddress(config, jsonIncreaseAddress);
            String finalDecreaseOscAddress = this.GetResolvedOscAddress(config, jsonDecreaseAddress);
            
            // 如果 GetResolvedOscAddress 返回无效地址 (通常是"/") 并且原始模板 (jsonIncreaseAddress) 本身就是空的，
            // 说明 GetResolvedOscAddress 内部的回退（基于DisplayName/Title）也失败了，此时才使用更硬编码的默认路径。
            if ((String.IsNullOrEmpty(finalIncreaseOscAddress) || finalIncreaseOscAddress == "/") && String.IsNullOrEmpty(jsonIncreaseAddress))
            {
                 finalIncreaseOscAddress = $"/{groupNameForPath}/{displayNameForPath}/Right".Replace("//", "/");
                 PluginLog.Warning($"[LogicManager|ProcessLegacyDialAdjustmentInternal] Increase address for '{config.DisplayName}' (template was null/empty) resolved to default: '{finalIncreaseOscAddress}'");
            }
            else if (String.IsNullOrEmpty(finalIncreaseOscAddress) || finalIncreaseOscAddress == "/")
            {
                PluginLog.Warning($"[LogicManager|ProcessLegacyDialAdjustmentInternal] Increase address for '{config.DisplayName}' (template: '{jsonIncreaseAddress ?? "null"}') was resolved to an invalid address: '{finalIncreaseOscAddress}'. OSC might not be sent correctly if this path is used.");
                // 保留 GetResolvedOscAddress 的结果，即使它是 "/"，后续发送逻辑会处理
            }

            if ((String.IsNullOrEmpty(finalDecreaseOscAddress) || finalDecreaseOscAddress == "/") && String.IsNullOrEmpty(jsonDecreaseAddress))
            {
                 finalDecreaseOscAddress = $"/{groupNameForPath}/{displayNameForPath}/Left".Replace("//", "/");
                 PluginLog.Warning($"[LogicManager|ProcessLegacyDialAdjustmentInternal] Decrease address for '{config.DisplayName}' (template was null/empty) resolved to default: '{finalDecreaseOscAddress}'");
            }
            else if (String.IsNullOrEmpty(finalDecreaseOscAddress) || finalDecreaseOscAddress == "/")
            {
                PluginLog.Warning($"[LogicManager|ProcessLegacyDialAdjustmentInternal] Decrease address for '{config.DisplayName}' (template: '{jsonDecreaseAddress ?? "null"}') was resolved to an invalid address: '{finalDecreaseOscAddress}'. OSC might not be sent correctly if this path is used.");
            }

            Single acceleration = 1.0f; // Single for float
            if (lastEventTimes != null && config.AccelerationFactor.HasValue && lastEventTimes.TryGetValue(actionParameter, out var lastTime))
            { var timeDiff = (Single)(DateTime.Now - lastTime).TotalMilliseconds; if (timeDiff < 100) { acceleration = config.AccelerationFactor.Value; } }

            String targetAddress = ticks > 0 ? finalIncreaseOscAddress : finalDecreaseOscAddress;
            if (!String.IsNullOrEmpty(targetAddress) && targetAddress != "/")
            { ReaOSCPlugin.SendOSCMessage(targetAddress, Math.Abs(ticks) * acceleration); }
            else
            { PluginLog.Warning($"[LogicManager] LegacyDial '{actionParameter}' 最终 OSC 地址无效: '{targetAddress}'"); }
        }

        private void ProcessLegacyToggleDialAdjustmentInternal(ButtonConfig config, Int32 ticks, String actionParameter)
        {
            String toggleDialAddress = this.DetermineOscAddressForAction(config, config.GroupName); // 使用JSON GroupName
            if (String.IsNullOrEmpty(toggleDialAddress) || toggleDialAddress == "/")
            { PluginLog.Warning($"[LogicManager] LegacyToggleDial '{actionParameter}' 生成的 OSC 地址无效。"); return; }
            Single valueToSend = (ticks > 0) ? 1.0f : 0.0f; // Single for float
            ReaOSCPlugin.SendOSCMessage(toggleDialAddress, valueToSend);
        }
        #endregion

        public void Dispose()
        {
            OSCStateManager.Instance.StateChanged -= this.OnOSCStateChanged;
            if (this.CommandStateNeedsRefresh != null)
            { foreach (Delegate d in this.CommandStateNeedsRefresh.GetInvocationList()) { this.CommandStateNeedsRefresh -= (EventHandler<String>)d; } }
            this._allConfigs.Clear();
            this._toggleStates.Clear();
            this._dialModes.Clear();
            this._parameterDialSelectedIndexes.Clear();
            this._oscAddressToActionParameterMap.Clear();
            this._modeOptions.Clear();
            this._currentModes.Clear();
            this._modeChangedEvents.Clear();
            this._isInitialized = false;

            // 【新增】清理动态标题相关字典
            this._dynamicTitleSourceStrings.Clear();
            this._titleFieldToOscAddressMapping.Clear();

            PluginLog.Info("[LogicManager] Disposed.");
        }

        #region ControlDial Unit Conversion and Display Helpers

        // 移除旧的多项式常量 C1_POLY 至 C5_POLY 和 P_POLY_0DB_TARGET_PARAM
        // private const Single C1_POLY = 1.373f; ... 等
        // 定义新的有效最小dB，用于从p=0开始的第一个插值区间
        private const Single EFFECTIVE_MIN_DB_FOR_0_TO_0005_INTERPOLATION = -144.0f; 
        // 0dB对应的参数值，仍作为一个重要的参考点和校准点使用
        private const Single P_AT_0DB = 0.716f;

        private Single ConvertParameterToUnit(Single parameterValue, ControlDialParsedConfig dialConfig) 
        {
            if (!dialConfig.HasUnitConversion) { return parameterValue; }

            Single p_clamped = Math.Clamp(parameterValue, dialConfig.MinValue, dialConfig.MaxValue);

            if (dialConfig.DisplayUnitString.Equals("dB", StringComparison.OrdinalIgnoreCase))
            {
                const Single epsilon = 0.00001f; // 比较浮点数的容差

                // 精确匹配校准点（优先处理）
                if (p_clamped <= dialConfig.MinValue + epsilon) { return Single.NegativeInfinity; }
                if (Math.Abs(p_clamped - 0.005f) < epsilon) { return -132.0f; }
                if (Math.Abs(p_clamped - 0.01f)  < epsilon) { return -114.0f; }
                if (Math.Abs(p_clamped - 0.05f)  < epsilon) { return -72.0f; }
                if (Math.Abs(p_clamped - 0.1f)   < epsilon) { return -54.0f; }
                if (Math.Abs(p_clamped - 0.25f)  < epsilon) { return -30.0f; }
                if (Math.Abs(p_clamped - 0.5f)   < epsilon) { return -11.0f; }
                if (Math.Abs(p_clamped - P_AT_0DB) < epsilon) { return 0.0f; }   // 0.716f
                if (Math.Abs(p_clamped - 0.8f)   < epsilon) { return 3.76f; }
                if (Math.Abs(p_clamped - 0.85f)  < epsilon) { return 5.90f; }
                if (Math.Abs(p_clamped - 0.9f)   < epsilon) { return 7.99f; }
                if (Math.Abs(p_clamped - 0.95f)  < epsilon) { return 10.00f; }
                if (Math.Abs(p_clamped - 0.98f)  < epsilon) { return 11.20f; }
                if (p_clamped >= dialConfig.MaxValue - epsilon) { return dialConfig.UnitMax; } // 通常是 +12dB @ p=1.0f

                // 分段线性插值
                if (p_clamped > dialConfig.MinValue && p_clamped < 0.005f) { Single p = (p_clamped - dialConfig.MinValue) / (0.005f - dialConfig.MinValue); return EFFECTIVE_MIN_DB_FOR_0_TO_0005_INTERPOLATION + p * (-132.0f - EFFECTIVE_MIN_DB_FOR_0_TO_0005_INTERPOLATION); }
                if (p_clamped > 0.005f && p_clamped < 0.01f)  { Single p = (p_clamped - 0.005f) / (0.01f - 0.005f); return -132.0f + p * (-114.0f - (-132.0f)); }
                if (p_clamped > 0.01f  && p_clamped < 0.05f)  { Single p = (p_clamped - 0.01f) / (0.05f - 0.01f); return -114.0f + p * (-72.0f - (-114.0f)); }
                if (p_clamped > 0.05f  && p_clamped < 0.1f)   { Single p = (p_clamped - 0.05f) / (0.1f - 0.05f); return -72.0f + p * (-54.0f - (-72.0f)); }
                if (p_clamped > 0.1f   && p_clamped < 0.25f)  { Single p = (p_clamped - 0.1f) / (0.25f - 0.1f); return -54.0f + p * (-30.0f - (-54.0f)); }
                if (p_clamped > 0.25f  && p_clamped < 0.5f)   { Single p = (p_clamped - 0.25f) / (0.5f - 0.25f); return -30.0f + p * (-11.0f - (-30.0f)); }
                if (p_clamped > 0.5f   && p_clamped < P_AT_0DB) { Single p = (p_clamped - 0.5f) / (P_AT_0DB - 0.5f); return -11.0f + p * (0.0f - (-11.0f)); }
                if (p_clamped > P_AT_0DB && p_clamped < 0.8f)   { Single p = (p_clamped - P_AT_0DB) / (0.8f - P_AT_0DB); return 0.0f + p * (3.76f - 0.0f); }
                if (p_clamped > 0.8f   && p_clamped < 0.85f)  { Single p = (p_clamped - 0.8f) / (0.85f - 0.8f); return 3.76f + p * (5.90f - 3.76f); }
                if (p_clamped > 0.85f  && p_clamped < 0.9f)   { Single p = (p_clamped - 0.85f) / (0.9f - 0.85f); return 5.90f + p * (7.99f - 5.90f); }
                if (p_clamped > 0.9f   && p_clamped < 0.95f)  { Single p = (p_clamped - 0.9f) / (0.95f - 0.9f); return 7.99f + p * (10.00f - 7.99f); }
                if (p_clamped > 0.95f  && p_clamped < 0.98f)  { Single p = (p_clamped - 0.95f) / (0.98f - 0.95f); return 10.00f + p * (11.20f - 10.00f); }
                if (p_clamped > 0.98f  && p_clamped < dialConfig.MaxValue) { Single p = (p_clamped - 0.98f) / (dialConfig.MaxValue - 0.98f); return 11.20f + p * (dialConfig.UnitMax - 11.20f); }

                PluginLog.Warning($"[ConvertParameterToUnit][dB] Parameter {p_clamped:F5} did not fit any interpolation segment. Returning closest boundary.");
                // Fallback clamping to nearest known point if somehow not caught by exact matches or segments
                if (p_clamped < 0.005f)
                {
                    return -132.0f;
                }

                if (p_clamped < 0.01f)
                {
                    return -114.0f;
                }

                if (p_clamped < 0.05f)
                {
                    return -72.0f;
                }

                if (p_clamped < 0.1f)
                {
                    return -54.0f;
                }

                if (p_clamped < 0.25f)
                {
                    return -30.0f;
                }

                if (p_clamped < 0.5f)
                {
                    return -11.0f;
                }


                if (p_clamped < P_AT_0DB)
                {
                    return 0.0f;
                }

                if (p_clamped < 0.8f)
                {
                    return 3.76f;
                }

                if (p_clamped < 0.85f)
                {
                    return 5.90f;
                }


                if (p_clamped < 0.9f)
                {
                    return 7.99f;
                }

                if (p_clamped < 0.95f)
                {
                    return 10.00f;
                }

                if (p_clamped < 0.98f)
                {
                    return 11.20f;
                }


                return dialConfig.UnitMax;
            }
            else { /* non-dB (linear) remains same */ Single pm = dialConfig.MinValue; Single pM = dialConfig.MaxValue; Single um = dialConfig.UnitMin; Single uM = dialConfig.UnitMax; if (Math.Abs(pM - pm) < 0.00001f) { return um; } Single prop = (p_clamped - pm) / (pM - pm); return um + prop * (uM - um); }
        }

        private Single ConvertUnitToParameter(Single unitValue, ControlDialParsedConfig dialConfig) 
        {
            if (!dialConfig.HasUnitConversion) { return unitValue; }

            Single paramMin = dialConfig.MinValue;
            Single paramMax = dialConfig.MaxValue;
            Single unitMax = dialConfig.UnitMax; 

            if (dialConfig.DisplayUnitString.Equals("dB", StringComparison.OrdinalIgnoreCase))
            {
                const Single dbEpsilon = 0.001f; 

                if (Single.IsNegativeInfinity(unitValue)) { return paramMin; }
                if (unitValue >= unitMax - dbEpsilon) { return paramMax; }
                if (Math.Abs(unitValue - 11.20f) < dbEpsilon) { return 0.98f; }
                if (Math.Abs(unitValue - 10.00f) < dbEpsilon) { return 0.95f; }
                if (Math.Abs(unitValue - 7.99f)  < dbEpsilon) { return 0.9f; }
                if (Math.Abs(unitValue - 5.90f)  < dbEpsilon) { return 0.85f; }
                if (Math.Abs(unitValue - 3.76f)  < dbEpsilon) { return 0.8f; }
                if (Math.Abs(unitValue - 0.0f)   < dbEpsilon) { return P_AT_0DB; }
                if (Math.Abs(unitValue - (-11.0f)) < dbEpsilon) { return 0.5f; }
                if (Math.Abs(unitValue - (-30.0f)) < dbEpsilon) { return 0.25f; }
                if (Math.Abs(unitValue - (-54.0f)) < dbEpsilon) { return 0.1f; }
                if (Math.Abs(unitValue - (-72.0f)) < dbEpsilon) { return 0.05f; }
                if (Math.Abs(unitValue - (-114.0f))< dbEpsilon) { return 0.01f; }
                if (Math.Abs(unitValue - (-132.0f))< dbEpsilon) { return 0.005f; }
                // If unitValue is less than -132dB but not -inf, map to param range [0, 0.005]
                if (unitValue < -132.0f) { Single p = (unitValue - EFFECTIVE_MIN_DB_FOR_0_TO_0005_INTERPOLATION) / (-132.0f - EFFECTIVE_MIN_DB_FOR_0_TO_0005_INTERPOLATION); return paramMin + p * (0.005f - paramMin); }

                // 분할 선형 보간
                if (unitValue > -132.0f && unitValue < -114.0f) { Single t = (unitValue - (-132.0f)) / (-114.0f - (-132.0f)); return 0.005f + t * (0.01f - 0.005f); }
                if (unitValue > -114.0f && unitValue < -72.0f)  { Single t = (unitValue - (-114.0f)) / (-72.0f - (-114.0f)); return 0.01f + t * (0.05f - 0.01f); }
                if (unitValue > -72.0f  && unitValue < -54.0f)  { Single t = (unitValue - (-72.0f)) / (-54.0f - (-72.0f)); return 0.05f + t * (0.1f - 0.05f); }
                if (unitValue > -54.0f  && unitValue < -30.0f)  { Single t = (unitValue - (-54.0f)) / (-30.0f - (-54.0f)); return 0.1f + t * (0.25f - 0.1f); }
                if (unitValue > -30.0f  && unitValue < -11.0f)  { Single t = (unitValue - (-30.0f)) / (-11.0f - (-30.0f)); return 0.25f + t * (0.5f - 0.25f); }
                if (unitValue > -11.0f  && unitValue < 0.0f)    { Single t = (unitValue - (-11.0f)) / (0.0f - (-11.0f)); return 0.5f + t * (P_AT_0DB - 0.5f); }
                if (unitValue > 0.0f    && unitValue < 3.76f)   { Single t = (unitValue - 0.0f) / (3.76f - 0.0f); return P_AT_0DB + t * (0.8f - P_AT_0DB); }
                if (unitValue > 3.76f   && unitValue < 5.90f)   { Single t = (unitValue - 3.76f) / (5.90f - 3.76f); return 0.8f + t * (0.85f - 0.8f); }
                if (unitValue > 5.90f   && unitValue < 7.99f)   { Single t = (unitValue - 5.90f) / (7.99f - 5.90f); return 0.85f + t * (0.9f - 0.85f); }
                if (unitValue > 7.99f   && unitValue < 10.00f)  { Single t = (unitValue - 7.99f) / (10.00f - 7.99f); return 0.9f + t * (0.95f - 0.9f); }
                if (unitValue > 10.00f  && unitValue < 11.20f)  { Single t = (unitValue - 10.00f) / (11.20f - 10.00f); return 0.95f + t * (0.98f - 0.95f); }
                if (unitValue > 11.20f  && unitValue < unitMax)   { Single t = (unitValue - 11.20f) / (unitMax - 11.20f); return 0.98f + t * (paramMax - 0.98f); }
                
                PluginLog.Warning($"[ConvertUnitToParameter][dB] Unit value {unitValue:F2} did not fit any interpolation segment. Clamping to parameter bounds.");
                if (unitValue < -132.0f)
                {
                    return paramMin; // Should have been caught by specific < -132 check already
                }


                if (unitValue < -114.0f)
                {
                    return 0.01f;
                }
                // ... (add more fallback clamps based on nearest known point) ...

                if (unitValue > 11.20f)
                {
                    return paramMax; // Should have been caught by >= unitMax check
                }


                return P_AT_0DB; // Default fallback
            }
            else { /* non-dB (linear) remains same */ Single pm = dialConfig.MinValue; Single pM = dialConfig.MaxValue; Single um = dialConfig.UnitMin; Single uM = dialConfig.UnitMax; if (Math.Abs(uM - um) < 0.00001f) { return pm; } Single prop = (unitValue - um) / (uM - um); Single res = pm + prop * (pM - pm); return Math.Clamp(res, pm, pM); }
        }

        // 获取 ControlDial 在设备上应显示的文本
        public String GetControlDialDisplayText(String actionParameter)
        {
            var config = this.GetConfig(actionParameter);
            if (config == null) { return actionParameter; /* or some error string */ }

            if (config.ActionType == "ControlDial")
            {
            if (!this._controlDialConfigs.TryGetValue(actionParameter, out var parsedDialConfig) ||
                !this._controlDialCurrentValues.TryGetValue(actionParameter, out var currentParameterValue)) 
            {
                PluginLog.Warning($"[LogicManager|GetControlDialDisplayText] Parsed config or current value not found for ControlDial: {actionParameter}");
                return config.Title ?? config.DisplayName ?? actionParameter;
            }
                return FormatDisplayTextInternal(currentParameterValue, parsedDialConfig);
            }
            else if (config.ActionType == "ModeControlDial")
            {
                if (TryGetCurrentModeControlDialContext(actionParameter, out var activeInternalConfig, out var currentInternalValue, out _))
                {
                    return FormatDisplayTextInternal(currentInternalValue, activeInternalConfig);
                    }
                else
                {
                    PluginLog.Warning($"[LogicManager|GetControlDialDisplayText] Could not get current mode context for ModeControlDial: {actionParameter}");
                    return config.Title ?? config.DisplayName ?? actionParameter; // Fallback to top-level title
                    }
                }
            else
            {
                 PluginLog.Warning($"[LogicManager|GetControlDialDisplayText] Called for non-ControlDial/ModeControlDial type: {config.ActionType}");
                 return config.Title ?? config.DisplayName ?? actionParameter;
            }
        }

        // 【新增】内部辅助方法，用于格式化 ControlDial 和 ModeControlDial 的显示文本
        private String FormatDisplayTextInternal(Single parameterValue, ControlDialParsedConfig parsedDialConfig)
        {
            if (parsedDialConfig.HasUnitConversion)
            {
                if (parsedDialConfig.DisplayUnitString.Equals("L&R", StringComparison.OrdinalIgnoreCase))
                {
                    Int32 intValue = (Int32)Math.Round(parameterValue);
                    if (intValue == 0) { return "C"; }
                    else if (intValue < 0) { return $"L{-intValue}"; }
                    else { return $"R{intValue}"; }
                    }
                    else
                    {
                    Single unitValue = this.ConvertParameterToUnit(parameterValue, parsedDialConfig); 
                    String valueText;
                    if (Single.IsNegativeInfinity(unitValue)) { valueText = "-inf"; }
                    else { valueText = unitValue.ToString("F1", System.Globalization.CultureInfo.InvariantCulture); }
                    return $"{valueText}{parsedDialConfig.UnitLabel}"; 
                }
            }
            else 
            {
                if (parsedDialConfig.IsParameterIntegerBased)
                { return ((Int32)Math.Round(parameterValue)).ToString(); }
                else
                { return parameterValue.ToString("F3", System.Globalization.CultureInfo.InvariantCulture); }
            }
        }

        #endregion

        // 【新增】辅助方法：尝试将字符串（包括 \"inf\", \"-inf\" 等）解析为 Single (float)
        private static Boolean TryParseSingleExtended(String strVal, out Single result)
        {
            result = 0f;
            if (String.IsNullOrEmpty(strVal))
            {
                return false;
            }

            String lowerStrVal = strVal.ToLowerInvariant();

            if (lowerStrVal == "inf" || lowerStrVal == "+inf")
            {
                result = Single.PositiveInfinity;
                return true;
            }
            if (lowerStrVal == "-inf")
            {
                result = Single.NegativeInfinity;
                return true;
            }
            return Single.TryParse(strVal, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out result);
        }

        // 【新增】辅助方法：解析并存储 ControlDial 的配置
        private void ParseAndStoreControlDialConfig(ButtonConfig config, String actionParameter)
        {
            ControlDialParsedConfig parsedConfig = this.ParseSingleControlDialConfigSegment(config, actionParameter);

            if (parsedConfig == null)
            {
                PluginLog.Error($"[LogicManager|ParseAndStoreControlDialConfig] Failed to parse ControlDial '{config.DisplayName}' (action: {actionParameter}). It will not be available.");
                return;
            }

            this._controlDialConfigs[actionParameter] = parsedConfig;
            this._controlDialCurrentValues[actionParameter] = parsedConfig.DefaultValue; 
            PluginLog.Info($"[LogicManager|ParseAndStoreControlDialConfig] ControlDial '{config.DisplayName}' (action: {actionParameter}) FINALIZED registration. Mode: {parsedConfig.Mode}, ParamDefault (0-1 scale): {parsedConfig.DefaultValue:F3}, HasUnitConversion: {parsedConfig.HasUnitConversion}. Current Param Value (0-1 scale): {this._controlDialCurrentValues[actionParameter]:F3}");

            String oscAddressForListening = this.DetermineOscAddressForAction(config, config.GroupName, config.OscAddress ?? config.Title ?? config.DisplayName);
            if (!String.IsNullOrEmpty(oscAddressForListening) && oscAddressForListening != "/")
            {
                if (!this._oscAddressToActionParameterMap.ContainsKey(oscAddressForListening))
                {
                    this._oscAddressToActionParameterMap[oscAddressForListening] = actionParameter;
                    PluginLog.Info($"[LogicManager|ParseAndStoreControlDialConfig] ControlDial '{config.DisplayName}' (action: {actionParameter}) is now listening on OSC address '{oscAddressForListening}' for external state changes.");
                }
                else if (this._oscAddressToActionParameterMap[oscAddressForListening] != actionParameter)
                {
                    PluginLog.Error($"[LogicManager|ParseAndStoreControlDialConfig] OSC Address Conflict: Address '{oscAddressForListening}' for ControlDial '{config.DisplayName}' (action: {actionParameter}) is already mapped to another action '{this._oscAddressToActionParameterMap[oscAddressForListening]}'. External state changes for this ControlDial might not be received correctly.");
                }
            }
            else
            {
                PluginLog.Warning($"[LogicManager|ParseAndStoreControlDialConfig] ControlDial '{config.DisplayName}' (action: {actionParameter}) could not determine a valid OSC address for listening. It will not be updated by incoming OSC messages if address was '{oscAddressForListening ?? "null"}'.");
            }
        }

        // 【新增】辅助方法：解析并存储 ModeControlDial 的配置
        private void ParseAndStoreModeControlDialConfig(ButtonConfig config, String actionParameter)
        {
            PluginLog.Info($"[LogicManager|ParseAndStoreModeControlDialConfig] Parsing ModeControlDial '{config.DisplayName}' (ActionParameter: {actionParameter}, ModeName: '{config.ModeName}')");

            if (String.IsNullOrEmpty(config.ModeName) || !this._modeOptions.TryGetValue(config.ModeName, out var externalModes) || !externalModes.Any())
            {
                PluginLog.Error($"[LogicManager|ParseAndStoreModeControlDialConfig] ModeControlDial '{config.DisplayName}' (action: {actionParameter}) has invalid or unregistered ModeName '{config.ModeName ?? "null"}'. Skipping registration.");
                return;
            }
            int expectedModeCount = externalModes.Count;

            // 严格校验列表长度
            bool isValid = true;
            if (config.ParametersByMode == null || config.ParametersByMode.Count != expectedModeCount) { PluginLog.Error($"[LogicManager|ParseAndStoreModeControlDialConfig] '{config.DisplayName}': ParametersByMode count mismatch. Expected {expectedModeCount}, Got {config.ParametersByMode?.Count ?? 0}."); isValid = false; }
            if (config.UnitsByMode == null || config.UnitsByMode.Count != expectedModeCount) { PluginLog.Error($"[LogicManager|ParseAndStoreModeControlDialConfig] '{config.DisplayName}': UnitsByMode count mismatch. Expected {expectedModeCount}, Got {config.UnitsByMode?.Count ?? 0}."); isValid = false; }
            if (config.UnitDefaultsByMode == null || config.UnitDefaultsByMode.Count != expectedModeCount) { PluginLog.Error($"[LogicManager|ParseAndStoreModeControlDialConfig] '{config.DisplayName}': UnitDefaultsByMode count mismatch. Expected {expectedModeCount}, Got {config.UnitDefaultsByMode?.Count ?? 0}."); isValid = false; }
            if (config.ParameterDefaultsByMode == null || config.ParameterDefaultsByMode.Count != expectedModeCount) { PluginLog.Error($"[LogicManager|ParseAndStoreModeControlDialConfig] '{config.DisplayName}': ParameterDefaultsByMode count mismatch. Expected {expectedModeCount}, Got {config.ParameterDefaultsByMode?.Count ?? 0}."); isValid = false; }

            if (!isValid)
            {
                PluginLog.Error($"[LogicManager|ParseAndStoreModeControlDialConfig] ModeControlDial '{config.DisplayName}' (action: {actionParameter}) failed validation due to mismatched list counts. Skipping registration.");
                return;
            }

            // 初始化存储结构
            this._modeControlDialParsedConfigs[actionParameter] = new List<ControlDialParsedConfig>(expectedModeCount);
            this._modeControlDialOscListenerAddresses[actionParameter] = new List<String>(expectedModeCount);
            this._modeControlDialCurrentValuesByMode[actionParameter] = new List<Single>(expectedModeCount);
            this._modeControlDialInitialParameterDefaultsByMode[actionParameter] = new List<Single>(expectedModeCount);

            for (int i = 0; i < expectedModeCount; i++)
            {
                var internalSegmentConfig = new ButtonConfig
                {
                    DisplayName = $"{config.DisplayName}_Mode{i}", 
                    GroupName = config.GroupName, 
                    ActionType = "ControlDial", 
                    Parameter = config.ParametersByMode[i],
                    Unit = config.UnitsByMode[i],
                    UnitDefault = config.UnitDefaultsByMode[i],
                    ParameterDefault = config.ParameterDefaultsByMode[i]
                };

                ControlDialParsedConfig parsedInternalConfig = this.ParseSingleControlDialConfigSegment(internalSegmentConfig, $"{actionParameter}_InternalMode{i} (ExtMode: {externalModes[i]})");

                if (parsedInternalConfig == null)
                {
                    PluginLog.Error($"[LogicManager|ParseAndStoreModeControlDialConfig] ModeControlDial '{config.DisplayName}', internal mode {i} (External: '{externalModes[i]}'): Failed to parse segment. Skipping registration of this entire ModeControlDial.");
                    this._modeControlDialParsedConfigs.Remove(actionParameter);
                    this._modeControlDialOscListenerAddresses.Remove(actionParameter); // 清理已部分初始化的列表
                    this._modeControlDialCurrentValuesByMode.Remove(actionParameter);
                    this._modeControlDialInitialParameterDefaultsByMode.Remove(actionParameter);
                    return; 
                }
                
                this._modeControlDialParsedConfigs[actionParameter].Add(parsedInternalConfig);
                
                // 【修改】自动生成并注册监听地址
                String externalModeName = externalModes[i];
                String sanitizedModeName = SanitizeOscPathSegment(externalModeName);
                String baseOscPathForListener = DetermineBaseOscPathForModeControlDialSend(config, actionParameter); // 使用发送基础路径作为监听基础
                String generatedListenerAddress = $"{baseOscPathForListener}/{sanitizedModeName}/feedback".Replace("//", "/").TrimEnd('/');
                if (String.IsNullOrEmpty(generatedListenerAddress) || generatedListenerAddress == "/")
                {
                    PluginLog.Warning($"[LogicManager|ParseAndStoreModeControlDialConfig] ModeControlDial '{config.DisplayName}', internal mode {i} (Ext: '{externalModes[i]}'): Generated listener OSC address was empty or invalid ('{generatedListenerAddress ?? "null"}'). This mode will not receive external OSC updates via this auto-generated path.");
                    this._modeControlDialOscListenerAddresses[actionParameter].Add(null); 
                }
                else
                {
                    this._modeControlDialOscListenerAddresses[actionParameter].Add(generatedListenerAddress);
                    String compoundKey = $"{actionParameter}#{i}";
                    if (!this._oscAddressToActionParameterMap.ContainsKey(generatedListenerAddress))
                    {
                        this._oscAddressToActionParameterMap[generatedListenerAddress] = compoundKey;
                        PluginLog.Info($"[LogicManager|ParseAndStoreModeControlDialConfig] ModeControlDial '{config.DisplayName}', internal mode {i} (Ext: '{externalModes[i]}'): Auto-generated listener on '{generatedListenerAddress}' (mapped to '{compoundKey}')");
                    }
                    else if (this._oscAddressToActionParameterMap[generatedListenerAddress] != compoundKey)
                    {
                         PluginLog.Error($"[LogicManager|ParseAndStoreModeControlDialConfig] ModeControlDial '{config.DisplayName}', internal mode {i} (Ext: '{externalModes[i]}'): OSC Address Conflict for auto-generated listener. Address '{generatedListenerAddress}' is already mapped to '{this._oscAddressToActionParameterMap[generatedListenerAddress]}'.");
                    }
                }
                
                this._modeControlDialInitialParameterDefaultsByMode[actionParameter].Add(parsedInternalConfig.DefaultValue);
                this._modeControlDialCurrentValuesByMode[actionParameter].Add(parsedInternalConfig.DefaultValue); // 初始化当前值
                PluginLog.Info($"[LogicManager|ParseAndStoreModeControlDialConfig] ModeControlDial '{config.DisplayName}', internal mode {i} (Ext: '{externalModes[i]}') parsed. ParamDefault: {parsedInternalConfig.DefaultValue:F3}");
            }
            PluginLog.Info($"[LogicManager|ParseAndStoreModeControlDialConfig] Successfully parsed and stored ModeControlDial '{config.DisplayName}' (action: {actionParameter}) with {expectedModeCount} internal modes.");

            // 【关键新增】将 ModeControlDial 的顶层配置注册到 _allConfigs
            // 确保后续 GetConfig(actionParameter) 能正确取回此 ModeControlDial 的顶层定义
            if (!this._allConfigs.ContainsKey(actionParameter))
            {
                this._allConfigs[actionParameter] = config; // 存储原始传入的 config
                PluginLog.Info($"[LogicManager|ParseAndStoreModeControlDialConfig] Registered top-level ModeControlDial config for '{actionParameter}' into _allConfigs.");
            }
            else
            {
                // 通常不应该发生，因为 actionParameter 应该是唯一的
                // 但是，如果在 RegisterConfigs 主循环中，一个非 ModeControlDial 的项恰好生成了相同的 actionParameter，
                // 并且先于这个 ModeControlDial 被添加到 _allConfigs，那么这里可能会出现日志。
                // 这暗示着 actionParameter 的生成策略可能需要进一步审视以保证全局唯一性，
                // 或者接受后注册的同名项（如果类型不同）会覆盖前者（取决于 _allConfigs 的设计意图）。
                // 目前，如果发生这种情况，我们仅记录警告，顶层的 _allConfigs 仍将保留先注册的那个。
                // 但 _modeControlDialParsedConfigs 等专用字典已经填充了这个 actionParameter 的 ModeControlDial 数据。
                PluginLog.Warning($"[LogicManager|ParseAndStoreModeControlDialConfig] ActionParameter '{actionParameter}' for ModeControlDial '{config.DisplayName}' was ALREADY in _allConfigs (possibly from a non-ModeControlDial item or duplicate definition). The _allConfigs entry was NOT updated, but ModeControlDial-specific dictionaries ARE populated.");
            }
        }

        // 【重构】核心解析逻辑，用于单个ControlDial或ModeControlDial的内部模式
        private ControlDialParsedConfig ParseSingleControlDialConfigSegment(ButtonConfig segmentConfig, String loggingContext)
        {
            PluginLog.Info($"[LogicManager|ParseSingleControlDialConfigSegment] START ({loggingContext}) - DisplayName: '{segmentConfig.DisplayName}', ActionType: '{segmentConfig.ActionType}'");

            if (segmentConfig.Parameter == null || !segmentConfig.Parameter.Any())
            {
                PluginLog.Warning($"[LogicManager|ParseSingleControlDialConfigSegment] ({loggingContext}) 'Parameter' array is missing or empty. Cannot parse. Returning null.");
                return null;
            }

            var parsedDialConfig = new ControlDialParsedConfig();
            Single initialParameterDefaultValue = 0.0f;
            var allParamsLookLikeIntegers = true; 

            if (segmentConfig.Parameter.Count > 0 && segmentConfig.Parameter[0]?.ToLower() == "true") // 连续模式
            {
                parsedDialConfig.Mode = ControlDialMode.Continuous;
                PluginLog.Verbose($"[LogicManager|ParseSingleControlDialConfigSegment] ({loggingContext}) Mode: Continuous");
                if (segmentConfig.Parameter.Count >= 3)
                {
                    String minStr = segmentConfig.Parameter[1];
                    String maxStr = segmentConfig.Parameter[2];
                    var minIsInt = Int32.TryParse(minStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out _) && !minStr.Contains(".") && !minStr.ToLowerInvariant().Contains("e");
                    var maxIsInt = Int32.TryParse(maxStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out _) && !maxStr.Contains(".") && !maxStr.ToLowerInvariant().Contains("e");
                    allParamsLookLikeIntegers = minIsInt && maxIsInt;
                    PluginLog.Verbose($"[LogicManager|ParseSingleControlDialConfigSegment] ({loggingContext}) MinStr: '{minStr}' (IsInt: {minIsInt}), MaxStr: '{maxStr}' (IsInt: {maxIsInt}). AllParamsLookLikeIntegers: {allParamsLookLikeIntegers}");

                    if (Single.TryParse(minStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var minFloat) &&
                        Single.TryParse(maxStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var maxFloat))
                    {
                        parsedDialConfig.MinValue = Math.Min(minFloat, maxFloat);
                        parsedDialConfig.MaxValue = Math.Max(minFloat, maxFloat);
                        initialParameterDefaultValue = parsedDialConfig.MinValue; 
                        PluginLog.Verbose($"[LogicManager|ParseSingleControlDialConfigSegment] ({loggingContext}) Parsed MinValue: {parsedDialConfig.MinValue:F3}, MaxValue: {parsedDialConfig.MaxValue:F3}, InitialDefault: {initialParameterDefaultValue:F3}");
                    }
                    else 
                    {
                        PluginLog.Warning($"[LogicManager|ParseSingleControlDialConfigSegment] ({loggingContext}) Continuous mode min/max parameters ('{minStr}', '{maxStr}') are invalid. Using 0.0f-1.0f default.");
                        parsedDialConfig.MinValue = 0.0f;
                        parsedDialConfig.MaxValue = 1.0f;
                        initialParameterDefaultValue = 0.0f;
                        allParamsLookLikeIntegers = false; 
                    }
                }
                else 
                {
                    PluginLog.Warning($"[LogicManager|ParseSingleControlDialConfigSegment] ({loggingContext}) Continuous mode parameters missing. Expected [\"true\", \"min_str\", \"max_str\"]. Using 0.0f-1.0f default.");
                    parsedDialConfig.MinValue = 0.0f;
                    parsedDialConfig.MaxValue = 1.0f;
                    initialParameterDefaultValue = 0.0f;
                    allParamsLookLikeIntegers = false;
                }
            }
            else // 离散模式
            {
                parsedDialConfig.Mode = ControlDialMode.Discrete;
                parsedDialConfig.DiscreteValues = new List<Single>();
                var firstDiscreteValue = true;
                PluginLog.Verbose($"[LogicManager|ParseSingleControlDialConfigSegment] ({loggingContext}) Mode: Discrete. Processing {segmentConfig.Parameter.Count} parameters.");
                foreach (var paramStr in segmentConfig.Parameter) 
                {
                    if (Single.TryParse(paramStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var valFloat))
                    {
                        parsedDialConfig.DiscreteValues.Add(valFloat);
                        PluginLog.Verbose($"[LogicManager|ParseSingleControlDialConfigSegment] ({loggingContext}) Added discrete value: {valFloat:F3}");
                        if (firstDiscreteValue) 
                        { 
                            initialParameterDefaultValue = valFloat; 
                            firstDiscreteValue = false; 
                        }
                        if (!Int32.TryParse(paramStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out _) || paramStr.Contains(".") || paramStr.ToLowerInvariant().Contains("e"))
                        {
                            allParamsLookLikeIntegers = false;
                        }
                    }
                    else
                    {
                        PluginLog.Warning($"[LogicManager|ParseSingleControlDialConfigSegment] ({loggingContext}) Discrete mode parameter '{paramStr}' is not a valid float. Skipping.");
                        allParamsLookLikeIntegers = false; 
                    }
                }
                if (!parsedDialConfig.DiscreteValues.Any())
                {
                    PluginLog.Warning($"[LogicManager|ParseSingleControlDialConfigSegment] ({loggingContext}) Discrete mode has no valid float parameters. Adding 0.0f as default. Returning null.");
                    // return null; // 之前这里会返回null，现在改为添加默认值并继续，但标记为非整数
                    parsedDialConfig.DiscreteValues.Add(0.0f);
                    initialParameterDefaultValue = 0.0f;
                    allParamsLookLikeIntegers = false; // 如果没有有效离散值，则肯定不是纯整数
                }
                PluginLog.Verbose($"[LogicManager|ParseSingleControlDialConfigSegment] ({loggingContext}) Discrete values processed. AllParamsLookLikeIntegers: {allParamsLookLikeIntegers}, InitialDefault: {initialParameterDefaultValue:F3}");
            }
            parsedDialConfig.IsParameterIntegerBased = allParamsLookLikeIntegers;
            parsedDialConfig.DefaultValue = initialParameterDefaultValue; 
            PluginLog.Verbose($"[LogicManager|ParseSingleControlDialConfigSegment] ({loggingContext}) Initial IsParameterIntegerBased: {parsedDialConfig.IsParameterIntegerBased}, DefaultValue (param scale): {parsedDialConfig.DefaultValue:F3}");

            // 优先应用 UnitDefault (如果存在且有效), 这可能会覆盖上面基于 ParameterDefault 或初始推断的 DefaultValue
            if (segmentConfig.Unit != null && segmentConfig.Unit.Any() && !String.IsNullOrEmpty(segmentConfig.Unit[0]))
            {
                parsedDialConfig.DisplayUnitString = segmentConfig.Unit[0];
                parsedDialConfig.UnitLabel = parsedDialConfig.DisplayUnitString.Equals("L&R", StringComparison.OrdinalIgnoreCase) ? "" : parsedDialConfig.DisplayUnitString;
                parsedDialConfig.HasUnitConversion = true;
                PluginLog.Verbose($"[LogicManager|ParseSingleControlDialConfigSegment] ({loggingContext}) Unit detected: '{parsedDialConfig.DisplayUnitString}'. HasUnitConversion set to true.");

                if (segmentConfig.Unit.Count >= 3)
                {
                    if (TryParseSingleExtended(segmentConfig.Unit[1], out var unitMinNumeric) && TryParseSingleExtended(segmentConfig.Unit[2], out var unitMaxNumeric))
                    { 
                        parsedDialConfig.UnitMin = unitMinNumeric; 
                        parsedDialConfig.UnitMax = unitMaxNumeric; 
                        PluginLog.Verbose($"[LogicManager|ParseSingleControlDialConfigSegment] ({loggingContext}) Parsed UnitMin: {parsedDialConfig.UnitMin:F1}, UnitMax: {parsedDialConfig.UnitMax:F1}");
                    }
                    else 
                    { 
                        PluginLog.Warning($"[LogicManager|ParseSingleControlDialConfigSegment] ({loggingContext}) Unit Min/Max strings ('{segmentConfig.Unit[1]}', '{segmentConfig.Unit[2]}') are invalid or not standard numbers. Defaulting UnitMin/Max to 0f.");
                        parsedDialConfig.UnitMin = 0f; 
                        parsedDialConfig.UnitMax = 0f; 
                    }
                }
                else 
                { 
                    PluginLog.Warning($"[LogicManager|ParseSingleControlDialConfigSegment] ({loggingContext}) 'Unit' array has < 3 elements. Expected [Name, MinStr, MaxStr]. Defaulting UnitMin/Max to 0f.");
                    parsedDialConfig.UnitMin = 0f; 
                    parsedDialConfig.UnitMax = 0f;
                }

                if (!String.IsNullOrEmpty(segmentConfig.UnitDefault))
                {
                    Single newDefaultValueFromUnit = parsedDialConfig.DefaultValue; // Start with current default
                    bool unitDefaultWasSuccessfullyApplied = false;
                    PluginLog.Verbose($"[LogicManager|ParseSingleControlDialConfigSegment] ({loggingContext}) Attempting to apply UnitDefault: '{segmentConfig.UnitDefault}'");

                    if (parsedDialConfig.DisplayUnitString.Equals("L&R", StringComparison.OrdinalIgnoreCase))
                    {
                        string ud = segmentConfig.UnitDefault.ToUpperInvariant();
                        if (ud == "C") { newDefaultValueFromUnit = 0.0f; unitDefaultWasSuccessfullyApplied = true; }
                        else if (ud.StartsWith("L") && Single.TryParse(ud.Substring(1), NumberStyles.Any, CultureInfo.InvariantCulture, out var lVal)) { newDefaultValueFromUnit = -lVal; unitDefaultWasSuccessfullyApplied = true; }
                        else if (ud.StartsWith("R") && Single.TryParse(ud.Substring(1), NumberStyles.Any, CultureInfo.InvariantCulture, out var rVal)) { newDefaultValueFromUnit = rVal; unitDefaultWasSuccessfullyApplied = true; }
                        else { PluginLog.Warning($"[LogicManager|ParseSingleControlDialConfigSegment] ({loggingContext}) (L&R) Unrecognised UnitDefault: '{segmentConfig.UnitDefault}'"); }
                    }
                    else if (TryParseSingleExtended(segmentConfig.UnitDefault, out var parsedNumericUnitDefault))
                    {
                        parsedDialConfig.ParsedUnitDefault = parsedNumericUnitDefault; // Store the unit-scale default
                        newDefaultValueFromUnit = this.ConvertUnitToParameter(parsedDialConfig.ParsedUnitDefault, parsedDialConfig); // Convert to parameter-scale
                        unitDefaultWasSuccessfullyApplied = true;
                        PluginLog.Verbose($"[LogicManager|ParseSingleControlDialConfigSegment] ({loggingContext}) UnitDefault '{segmentConfig.UnitDefault}' (parsed as {parsedNumericUnitDefault:F1}) converted to param scale: {newDefaultValueFromUnit:F3}");
                    }
                    else { PluginLog.Warning($"[LogicManager|ParseSingleControlDialConfigSegment] ({loggingContext}) Invalid UnitDefault string: '{segmentConfig.UnitDefault}' for unit type '{parsedDialConfig.DisplayUnitString}'."); }
                    
                    if(unitDefaultWasSuccessfullyApplied)
                    {
                        // Clamp the new default to the parameter's own Min/Max (which are 0-1 or int range)
                        if (parsedDialConfig.IsParameterIntegerBased) { newDefaultValueFromUnit = (Single)Math.Round(newDefaultValueFromUnit); }
                        parsedDialConfig.DefaultValue = Math.Clamp(newDefaultValueFromUnit, parsedDialConfig.MinValue, parsedDialConfig.MaxValue);
                        PluginLog.Info($"[LogicManager|ParseSingleControlDialConfigSegment] ({loggingContext}) UnitDefault ('{segmentConfig.UnitDefault}') successfully applied. Final ParamDefault (param scale): {parsedDialConfig.DefaultValue:F3}");
                    }
                    else
                    {
                        PluginLog.Warning($"[LogicManager|ParseSingleControlDialConfigSegment] ({loggingContext}) UnitDefault ('{segmentConfig.UnitDefault}') could not be applied. DefaultValue remains: {parsedDialConfig.DefaultValue:F3} (from ParameterDefault or initial inference).");
                    }
                }
                else
                {
                     PluginLog.Verbose($"[LogicManager|ParseSingleControlDialConfigSegment] ({loggingContext}) Unit defined, but UnitDefault is empty. DefaultValue remains: {parsedDialConfig.DefaultValue:F3} (from ParameterDefault or initial inference).");
                }
            }
            // 如果没有有效的 Unit 定义，或者 UnitDefault 未能覆盖，则检查 ParameterDefault
            else if (!String.IsNullOrEmpty(segmentConfig.ParameterDefault)) 
            {
                PluginLog.Verbose($"[LogicManager|ParseSingleControlDialConfigSegment] ({loggingContext}) No Unit or no UnitDefault applied. Attempting to apply ParameterDefault: '{segmentConfig.ParameterDefault}'");
                String defaultParamStr = segmentConfig.ParameterDefault;
                bool defaultIsIntStyled = Int32.TryParse(defaultParamStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out _) && !defaultParamStr.Contains(".") && !defaultParamStr.ToLowerInvariant().Contains("e");
                
                if (Single.TryParse(defaultParamStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var explicitParamDefaultFloat))
                {
                    if (parsedDialConfig.IsParameterIntegerBased && !defaultIsIntStyled && (defaultParamStr.Contains(".") || defaultParamStr.ToLowerInvariant().Contains("e")))
                    {
                        PluginLog.Warning($"[LogicManager|ParseSingleControlDialConfigSegment] ({loggingContext}) Parameters appear integer-based, but ParameterDefault ('{defaultParamStr}') looks like a float. Using float value.");
                        // Potentially change IsParameterIntegerBased if ParameterDefault strongly indicates float for a previously assumed int range.
                        // For now, we just use the float value, IsParameterIntegerBased would have been set by min/max.
                    }

                    Single newDefaultFromParamDefault = explicitParamDefaultFloat;
                    if (parsedDialConfig.IsParameterIntegerBased) { newDefaultFromParamDefault = (Single)Math.Round(newDefaultFromParamDefault); }
                    
                    if (parsedDialConfig.Mode == ControlDialMode.Continuous)
                    {
                        parsedDialConfig.DefaultValue = Math.Clamp(newDefaultFromParamDefault, parsedDialConfig.MinValue, parsedDialConfig.MaxValue);
                    }
                    else // Discrete
                    {
                        const Single tolerance = 0.0001f;
                        bool foundInDiscrete = parsedDialConfig.DiscreteValues?.Any(dv => Math.Abs(newDefaultFromParamDefault - dv) < tolerance) ?? false;
                        if (foundInDiscrete)
                        {
                            parsedDialConfig.DefaultValue = parsedDialConfig.DiscreteValues.First(dv => Math.Abs(newDefaultFromParamDefault - dv) < tolerance);
                        }
                        else
                        {
                             PluginLog.Warning($"[LogicManager|ParseSingleControlDialConfigSegment] ({loggingContext}) ParameterDefault '{defaultParamStr}' (parsed as {newDefaultFromParamDefault:F3}) not found in discrete values. Using initial default '{initialParameterDefaultValue:F3}'.");
                             parsedDialConfig.DefaultValue = initialParameterDefaultValue; // Revert to initial if not found in discrete
                        }
                    }
                    PluginLog.Info($"[LogicManager|ParseSingleControlDialConfigSegment] ({loggingContext}) ParameterDefault ('{segmentConfig.ParameterDefault}') applied. Final ParamDefault (param scale): {parsedDialConfig.DefaultValue:F3}");
                }
                else 
                { 
                    PluginLog.Warning($"[LogicManager|ParseSingleControlDialConfigSegment] ({loggingContext}) Could not parse ParameterDefault '{defaultParamStr}'. DefaultValue remains: {parsedDialConfig.DefaultValue:F3} (from initial inference).");
                }
            }
            else
            {
                 PluginLog.Verbose($"[LogicManager|ParseSingleControlDialConfigSegment] ({loggingContext}) No UnitDefault and no ParameterDefault provided. DefaultValue remains: {parsedDialConfig.DefaultValue:F3} (from initial inference based on Parameter array).");
            }

            PluginLog.Info($"[LogicManager|ParseSingleControlDialConfigSegment] END ({loggingContext}) - Final Parsed Config: Mode={parsedDialConfig.Mode}, MinV={parsedDialConfig.MinValue:F3}, MaxV={parsedDialConfig.MaxValue:F3}, DefaultV={parsedDialConfig.DefaultValue:F3}, IsInt={parsedDialConfig.IsParameterIntegerBased}, HasUnit={parsedDialConfig.HasUnitConversion}, UnitStr='{parsedDialConfig.DisplayUnitString ?? "N/A"}', UnitMin={parsedDialConfig.UnitMin:F1}, UnitMax={parsedDialConfig.UnitMax:F1}, UnitDefP={parsedDialConfig.ParsedUnitDefault:F1}");
            return parsedDialConfig;
        }


        /// <summary>
        /// 解析包含 "{mode}" 占位符的文本模板。
        /// </summary>
        /// <param name="itemConfig">按钮或旋钮的配置，包含 ModeName。</param>
        /// <param name="textTemplate">可能包含 "{mode}" 的文本模板。</param>
        /// <returns>已解析的文本，或在无法解析时返回经过处理的模板。</returns>
        public String ResolveTextWithMode(ButtonConfig itemConfig, String textTemplate)
        {
            PluginLog.Info($"[LogicManager|ResolveTextWithMode] BEGIN Resolving. Item: '{itemConfig?.DisplayName ?? "N/A"}', Item's ModeName property: '{itemConfig?.ModeName ?? "N/A"}', Template: '{textTemplate ?? "N/A"}'");

            // 如果配置为空、模板为空或模板中不包含"{mode}"占位符 (忽略大小写)，则直接返回原始模板
            if (itemConfig == null || String.IsNullOrEmpty(textTemplate) || !Regex.IsMatch(textTemplate, "{mode}", RegexOptions.IgnoreCase))
            {
                PluginLog.Info($"[LogicManager|ResolveTextWithMode] END Resolving (no change or invalid input): Template directly returned: '{textTemplate}'");
                return textTemplate;
            }

            // 如果配置中没有定义ModeName，则无法解析"{mode}"
            if (String.IsNullOrEmpty(itemConfig.ModeName))
            {
                PluginLog.Warning($"[LogicManager|ResolveTextWithMode] Item '{itemConfig.DisplayName}' has empty ModeName. Cannot resolve '{{mode}}' in template '{textTemplate}'. Replacing with empty string (ignore case).");
                var resultNoModeName = Regex.Replace(textTemplate, "{mode}", "", RegexOptions.IgnoreCase);
                PluginLog.Info($"[LogicManager|ResolveTextWithMode] END Resolving (ModeName empty): Result: '{resultNoModeName}'");
                return resultNoModeName;
            }

            // 获取当前模式的原始字符串
            var currentModeActual = this.GetCurrentModeString(itemConfig.ModeName);
            PluginLog.Info($"[LogicManager|ResolveTextWithMode] For ModeName '{itemConfig.ModeName}', GetCurrentModeString returned: '{currentModeActual ?? "null_or_empty"}'");

            // 对模式字符串进行清理，使其适用于OSC路径和可能的显示
            var sanitizedMode = SanitizeOscPathSegment(currentModeActual); 
            PluginLog.Info($"[LogicManager|ResolveTextWithMode] Sanitized mode string for ModeName '{itemConfig.ModeName}' is: '{sanitizedMode ?? "null_or_empty"}' (from actual: '{currentModeActual ?? "null_or_empty"}').");
            
            if (String.IsNullOrEmpty(sanitizedMode)) 
            {
                 PluginLog.Warning($"[LogicManager|ResolveTextWithMode] Item '{itemConfig.DisplayName}' (ModeName: '{itemConfig.ModeName}') current mode ('{currentModeActual}') sanitized to empty. '{{mode}}' in template '{textTemplate}' will be replaced with empty string.");
            }
            
            // 执行替换
            var finalResult = Regex.Replace(textTemplate, "{mode}", sanitizedMode ?? "", RegexOptions.IgnoreCase); // 使用 sanitizedMode ?? "" 确保不传 null
            PluginLog.Info($"[LogicManager|ResolveTextWithMode] END Resolving. For Item '{itemConfig.DisplayName}', ModeName '{itemConfig.ModeName}', SanitizedMode '{sanitizedMode ?? ""}', Final Result: '{finalResult}'");
            return finalResult;
        }

        /// <summary>
        /// 获取经过 {mode} 解析和 GroupName 前缀处理的最终 OSC 地址。
        /// </summary>
        /// <param name="itemConfig">按钮或旋钮的配置。</param>
        /// <param name="oscAddressTemplateFromSource">原始的 OSC 地址模板，可能包含 {mode}。</param>
        /// <returns>最终的 OSC 地址字符串。</returns>
        public String GetResolvedOscAddress(ButtonConfig itemConfig, String oscAddressTemplateFromSource)
        {
            if (itemConfig == null)
            {
                PluginLog.Error("[LogicManager|GetResolvedOscAddress] itemConfig 为空，无法解析 OSC 地址。");
                return "/"; // 返回一个安全的默认值
            }

            // 1. 首先尝试解析传入的 oscAddressTemplateFromSource
            String resolvedSegment = ResolveTextWithMode(itemConfig, oscAddressTemplateFromSource);

            // 2. 如果原始模板解析后为空，则尝试使用 itemConfig.Title 作为模板
            if (String.IsNullOrEmpty(resolvedSegment) && !String.IsNullOrEmpty(itemConfig.Title))
            {
                PluginLog.Verbose($"[LogicManager|GetResolvedOscAddress] Original template for '{itemConfig.DisplayName}' was empty or resolved to empty. Trying Title: '{itemConfig.Title}'");
                resolvedSegment = ResolveTextWithMode(itemConfig, itemConfig.Title);
            }

            // 3. 如果 Title 解析后仍为空，则尝试使用 itemConfig.DisplayName 作为模板
            if (String.IsNullOrEmpty(resolvedSegment) && !String.IsNullOrEmpty(itemConfig.DisplayName))
            {
                PluginLog.Verbose($"[LogicManager|GetResolvedOscAddress] Title for '{itemConfig.DisplayName}' also resolved to empty. Trying DisplayName: '{itemConfig.DisplayName}'");
                resolvedSegment = ResolveTextWithMode(itemConfig, itemConfig.DisplayName);
            }

            // 4. 应用 GroupName 前缀逻辑
            var groupNameForPath = itemConfig.GroupName;
            var basePart = FormatTopLevelOscPathSegment(groupNameForPath);

            // 如果最终的 resolvedSegment 仍然为空 (意味着所有来源都无效)
            if (String.IsNullOrEmpty(resolvedSegment))
            {
                PluginLog.Warning($"[LogicManager|GetResolvedOscAddress] All potential sources (template, Title, DisplayName) for '{itemConfig.DisplayName}' resolved to an empty segment. Base part is '{basePart ?? "null"}'.");
                if (String.IsNullOrEmpty(basePart) || basePart == "/")
                {
                    return "/"; // GroupName 也无效，返回根
                }
                var onlyBaseAddress = $"/{basePart}".Replace("//", "/").TrimEnd('/');
                return String.IsNullOrEmpty(onlyBaseAddress) ? "/" : onlyBaseAddress; // 只返回 GroupName 部分
            }

            // 如果 resolvedSegment 是绝对路径 (以 "/" 开头)
            if (resolvedSegment.StartsWith("/"))
            {
                return SanitizeOscAddress(resolvedSegment);
            }

            // 否则，它是一个相对路径段，需要与 basePart (GroupName) 组合
            var actionSpecificPart = SanitizeOscPathSegment(resolvedSegment); 

            if (String.IsNullOrEmpty(basePart) || basePart == "/") // 如果没有有效的 GroupName
            {
                var finalAddressNoBase = $"/{actionSpecificPart}".Replace("//", "/").TrimEnd('/');
                return String.IsNullOrEmpty(finalAddressNoBase) || finalAddressNoBase == "/" ? "/" : finalAddressNoBase;
            }
            else // 有 GroupName，组合它们
            {   
                var finalAddressWithBase = $"/{basePart}/{actionSpecificPart}".Replace("//", "/").TrimEnd('/');
                return String.IsNullOrEmpty(finalAddressWithBase) || finalAddressWithBase == "/" ? "/" : finalAddressWithBase;
            }
        }

        // 用于构建全局唯一的 Toggle 状态键
        private String GetToggleStateKey(String actionParameter)
        {
            return $"{actionParameter}_ToggleState";
        }

        // 【新增】辅助方法：尝试获取 ModeControlDial 的当前内部模式上下文
        private Boolean TryGetCurrentModeControlDialContext(String modeControlDialActionParam, 
                                                        out ControlDialParsedConfig activeInternalConfig, 
                                                        out Single currentInternalValue, 
                                                        out Int32 activeExternalModeIndex)
        {
            activeInternalConfig = null;
            currentInternalValue = 0f;
            activeExternalModeIndex = -1;

            var topLevelConfig = this.GetConfig(modeControlDialActionParam);
            if (topLevelConfig == null)
            {
                PluginLog.Error($"[LogicManager|TryGetCurrentModeControlDialContext] Top-level config not found for actionParameter '{modeControlDialActionParam}'.");
                return false;
            }

            if (topLevelConfig.ActionType != "ModeControlDial")
            {
                PluginLog.Error($"[LogicManager|TryGetCurrentModeControlDialContext] Action '{modeControlDialActionParam}' (DisplayName: '{topLevelConfig.DisplayName}') is not of type ModeControlDial. Actual type: '{topLevelConfig.ActionType}'.");
                return false;
            }
            
            if (String.IsNullOrEmpty(topLevelConfig.ModeName))
            {
                PluginLog.Error($"[LogicManager|TryGetCurrentModeControlDialContext] ModeControlDial '{modeControlDialActionParam}' (DisplayName: '{topLevelConfig.DisplayName}') has an empty or null ModeName property.");
                return false;
            }

            activeExternalModeIndex = this.GetCurrentModeIndex(topLevelConfig.ModeName);
            if (activeExternalModeIndex == -1)
            {
                PluginLog.Error($"[LogicManager|TryGetCurrentModeControlDialContext] ModeControlDial '{modeControlDialActionParam}' (DisplayName: '{topLevelConfig.DisplayName}'): Could not determine a valid active external mode index for ModeName '{topLevelConfig.ModeName}'. GetCurrentModeIndex returned -1.");
                return false;
            }

            if (!this._modeControlDialParsedConfigs.TryGetValue(modeControlDialActionParam, out var internalConfigsList))
            {
                PluginLog.Error($"[LogicManager|TryGetCurrentModeControlDialContext] ModeControlDial '{modeControlDialActionParam}' (DisplayName: '{topLevelConfig.DisplayName}'): No parsed internal configs list found in _modeControlDialParsedConfigs dictionary.");
                return false;
            }

            if (activeExternalModeIndex < 0 || activeExternalModeIndex >= internalConfigsList.Count)
            {
                PluginLog.Error($"[LogicManager|TryGetCurrentModeControlDialContext] ModeControlDial '{modeControlDialActionParam}' (DisplayName: '{topLevelConfig.DisplayName}'): Active external mode index {activeExternalModeIndex} is out of bounds for the parsed internal configs list (Count: {internalConfigsList.Count}).");
                return false;
            }
            activeInternalConfig = internalConfigsList[activeExternalModeIndex];
            if (activeInternalConfig == null) // 双重检查，以防列表中存了null
            {
                PluginLog.Error($"[LogicManager|TryGetCurrentModeControlDialContext] ModeControlDial '{modeControlDialActionParam}' (DisplayName: '{topLevelConfig.DisplayName}'): Parsed internal config at index {activeExternalModeIndex} is null.");
                return false;
            }

            if (!this._modeControlDialCurrentValuesByMode.TryGetValue(modeControlDialActionParam, out var internalValuesList))
            {
                PluginLog.Error($"[LogicManager|TryGetCurrentModeControlDialContext] ModeControlDial '{modeControlDialActionParam}' (DisplayName: '{topLevelConfig.DisplayName}'): No list of current internal values found in _modeControlDialCurrentValuesByMode dictionary.");
                return false;
            }

            if (activeExternalModeIndex < 0 || activeExternalModeIndex >= internalValuesList.Count)
            {
                PluginLog.Error($"[LogicManager|TryGetCurrentModeControlDialContext] ModeControlDial '{modeControlDialActionParam}' (DisplayName: '{topLevelConfig.DisplayName}'): Active external mode index {activeExternalModeIndex} is out of bounds for the current internal values list (Count: {internalValuesList.Count}).");
                return false;
            }
            currentInternalValue = internalValuesList[activeExternalModeIndex];
            
            PluginLog.Verbose($"[LogicManager|TryGetCurrentModeControlDialContext] Successfully retrieved context for ModeControlDial '{modeControlDialActionParam}' (DisplayName: '{topLevelConfig.DisplayName}'): ActiveExternalModeIndex={activeExternalModeIndex}, InternalConfig.Mode={activeInternalConfig.Mode}, CurrentInternalValue={currentInternalValue:F3}");
            return true;
        }

        // 【新增】统一处理 ControlDial 和 ModeControlDial 调整的私有方法
        private void ProcessAdvancedDialAdjustment(String globalActionParameter, Int32 ticks)
        {
            var topConfig = this.GetConfig(globalActionParameter);
            if (topConfig == null) 
            {
                PluginLog.Warning($"[LogicManager|ProcessAdvancedDialAdjustment] Top-level config not found for '{globalActionParameter}'.");
                return; 
            }

            Single newParamValueToStore; // 参数刻度值 (0-1 or int)
            Single valueForOsc;        // 发送到OSC的值 (参数刻度值)
            String oscAddressToSend;
            ControlDialParsedConfig effectiveConfig; // 当前有效的内部或独立配置
            // Int32 currentActiveModeIndexForMCD = -1; // 在ModeControlDial分支中局部定义即可

            if (topConfig.ActionType == "ControlDial")
            {
                if (!this._controlDialConfigs.TryGetValue(globalActionParameter, out effectiveConfig) ||
                    !this._controlDialCurrentValues.TryGetValue(globalActionParameter, out var currentParamValue))
                {
                    PluginLog.Warning($"[LogicManager|ProcessAdvancedDialAdjustment] ControlDial '{globalActionParameter}' (DisplayName: '{topConfig.DisplayName}'): Config or current value not found.");
                    return;
                }
                newParamValueToStore = CalculateAdjustedParamValue(currentParamValue, ticks, effectiveConfig);
                this._controlDialCurrentValues[globalActionParameter] = newParamValueToStore;
                oscAddressToSend = this.GetResolvedOscAddress(topConfig, topConfig.OscAddress ?? topConfig.Title ?? topConfig.DisplayName);
            }
            else if (topConfig.ActionType == "ModeControlDial")
            {
                if (!TryGetCurrentModeControlDialContext(globalActionParameter, out effectiveConfig, out var currentInternalValue, out var activeExtModeIdx))
                {
                    PluginLog.Warning($"[LogicManager|ProcessAdvancedDialAdjustment] ModeControlDial '{globalActionParameter}' (DisplayName: '{topConfig.DisplayName}'): Could not get current mode context.");
                    return;
                }
                newParamValueToStore = CalculateAdjustedParamValue(currentInternalValue, ticks, effectiveConfig);
                this._modeControlDialCurrentValuesByMode[globalActionParameter][activeExtModeIdx] = newParamValueToStore;
                
                // OSC地址构造
                String baseOscPathSend = DetermineBaseOscPathForModeControlDialSend(topConfig, globalActionParameter);
                String currentModeStrSend = SanitizeOscPathSegment(this.GetCurrentModeString(topConfig.ModeName));
                if (!String.IsNullOrEmpty(topConfig.OscAddress)) // 如果顶层OscAddress字段存在
                {
                    oscAddressToSend = ResolveTextWithMode(topConfig, topConfig.OscAddress); // {mode} 会被替换
                    if (!oscAddressToSend.StartsWith("/")) // 如果是相对路径，则拼接
                    {
                        oscAddressToSend = $"{baseOscPathSend}/{oscAddressToSend}".Replace("//", "/");
                    }
                }
                else // 顶层OscAddress字段不存在或为空，则使用 基础路径/模式名
                {
                    oscAddressToSend = $"{baseOscPathSend}/{currentModeStrSend}".Replace("//", "/");
                }
                oscAddressToSend = oscAddressToSend.TrimEnd('/');
                if (String.IsNullOrEmpty(oscAddressToSend)) { oscAddressToSend = "/"; } // 避免完全空地址
            }
            else
            {
                PluginLog.Warning($"[LogicManager|ProcessAdvancedDialAdjustment] Called with unhandled ActionType: {topConfig.ActionType} for '{globalActionParameter}'.");
                return;
            }

            // 确定发送到OSC的值 (根据您的反馈，是参数刻度值，但需注意整数情况)
            valueForOsc = newParamValueToStore;
            if (effectiveConfig.IsParameterIntegerBased && !effectiveConfig.HasUnitConversion) // 如果是纯整数型参数（无单位转换，意味着不应是dB等需要浮点表示的）
            {
                valueForOsc = (Single)Math.Round(newParamValueToStore); // 发送四舍五入后的整数对应的浮点数
            }
            // 对于有单位转换的 (如dB) 或非整数基准的浮点参数，直接发送参数刻度值 (newParamValueToStore)
            
            if (!String.IsNullOrEmpty(oscAddressToSend) && oscAddressToSend != "/")
            {
                ReaOSCPlugin.SendOSCMessage(oscAddressToSend, valueForOsc);
                PluginLog.Info($"[LogicManager|ProcessAdvancedDialAdjustment] '{topConfig.ActionType}' '{topConfig.DisplayName}' OSC sent to '{oscAddressToSend}' -> {valueForOsc:F3} (Stored ParamValue: {newParamValueToStore:F3})");
            }
            else { PluginLog.Warning($"[LogicManager|ProcessAdvancedDialAdjustment] '{topConfig.ActionType}' '{topConfig.DisplayName}': Invalid OSC Address '{oscAddressToSend ?? "null"}' for sending."); }
        }

        // 【新增】计算调整后的参数值的辅助方法
        private Single CalculateAdjustedParamValue(Single currentParamValue, Int32 ticks, ControlDialParsedConfig dialConfig)
        {
            Single newParamValue = currentParamValue;
            if (dialConfig.HasUnitConversion && dialConfig.DisplayUnitString.Equals("dB", StringComparison.OrdinalIgnoreCase))
            {
                Single currentUnitValue = this.ConvertParameterToUnit(currentParamValue, dialConfig);
                Single targetUnitValue;
                if (Single.IsNegativeInfinity(currentUnitValue))
                {
                    if (ticks > 0) 
                    { 
                        const Single firstStepParamOffset = 0.0001f; // 比0.005f小，确保能跳出-inf
                        var firstParam = dialConfig.MinValue + firstStepParamOffset;
                        if (firstParam >= 0.005f) { firstParam = 0.0049f; } // 确保在-inf到-132dB区间
                        targetUnitValue = this.ConvertParameterToUnit(firstParam, dialConfig);
                        if (Single.IsNegativeInfinity(targetUnitValue)) { targetUnitValue = EFFECTIVE_MIN_DB_FOR_0_TO_0005_INTERPOLATION; } // 回退到定义的有效最小值
                        if (ticks > 1) { targetUnitValue += (ticks - 1) * 0.1f; } // dB模式下，ticks通常代表0.1dB步进
                    }
                    else { targetUnitValue = Single.NegativeInfinity; }
                }
                else 
                { 
                    targetUnitValue = currentUnitValue + (ticks * 0.1f); 
                }
                if (!Single.IsNegativeInfinity(targetUnitValue))
                {
                    targetUnitValue = Math.Min(targetUnitValue, dialConfig.UnitMax);
                    if (!Single.IsNegativeInfinity(dialConfig.UnitMin)) { targetUnitValue = Math.Max(targetUnitValue, dialConfig.UnitMin); }
                }
                newParamValue = this.ConvertUnitToParameter(targetUnitValue, dialConfig);
            }
            else if (dialConfig.Mode == ControlDialMode.Continuous)
            {
                if (dialConfig.IsParameterIntegerBased)
                {
                    newParamValue = currentParamValue + ticks;
                    newParamValue = (Single)Math.Round(newParamValue); 
                }
                else 
                { 
                    Single scaledAdjustment = ticks * 0.01f; 
                    newParamValue = currentParamValue + scaledAdjustment;
                }
            }
            else // Discrete Mode
            {
                if (dialConfig.DiscreteValues != null && dialConfig.DiscreteValues.Any())
                {
                    const Single tolerance = 0.0001f;
                    Int32 currentIndex = dialConfig.DiscreteValues.FindIndex(dv => Math.Abs(dv - currentParamValue) < tolerance);
                    if (currentIndex == -1) { currentIndex = 0; /* Default to first if not found */ }
                    Int32 count = dialConfig.DiscreteValues.Count;
                    Int32 newIndex = (currentIndex + ticks % count + count) % count; 
                    newParamValue = dialConfig.DiscreteValues[newIndex];
                }
            }
            return Math.Clamp(newParamValue, dialConfig.MinValue, dialConfig.MaxValue);
        }

        // 【新增】统一处理 ControlDial 和 ModeControlDial 按下的私有方法
        private void ProcessAdvancedDialPress(String globalActionParameter)
        {
            var topConfig = this.GetConfig(globalActionParameter);
            if (topConfig == null) 
            {
                PluginLog.Warning($"[LogicManager|ProcessAdvancedDialPress] Top-level config not found for '{globalActionParameter}'.");
                return; 
            }

            Single valueToStoreAndSend; // 参数刻度值 (0-1 or int)
            String oscAddressToSend;
            ControlDialParsedConfig effectiveConfig; 

            if (topConfig.ActionType == "ControlDial")
            {
                if (!this._controlDialConfigs.TryGetValue(globalActionParameter, out effectiveConfig))
                {
                    PluginLog.Warning($"[LogicManager|ProcessAdvancedDialPress] ControlDial '{globalActionParameter}' (DisplayName: '{topConfig.DisplayName}'): Config not found.");
                    return;
                }
                valueToStoreAndSend = effectiveConfig.DefaultValue; // DefaultValue已经是参数刻度
                if (effectiveConfig.IsParameterIntegerBased && !effectiveConfig.HasUnitConversion) // 纯整数型
                {
                    valueToStoreAndSend = (Single)Math.Round(valueToStoreAndSend);
                }
                this._controlDialCurrentValues[globalActionParameter] = valueToStoreAndSend;
                oscAddressToSend = this.GetResolvedOscAddress(topConfig, topConfig.OscAddress ?? topConfig.Title ?? topConfig.DisplayName);
            }
            else if (topConfig.ActionType == "ModeControlDial")
            {
                if (!this._modeControlDialInitialParameterDefaultsByMode.TryGetValue(globalActionParameter, out var initialDefaultsList) ||
                    !TryGetCurrentModeControlDialContext(globalActionParameter, out effectiveConfig, out _, out var activeExtModeIdx) ||
                    activeExtModeIdx < 0 || activeExtModeIdx >= initialDefaultsList.Count)
                {
                    PluginLog.Warning($"[LogicManager|ProcessAdvancedDialPress] ModeControlDial '{globalActionParameter}' (DisplayName: '{topConfig.DisplayName}'): Could not get context or initial defaults.");
                    return;
                }
                valueToStoreAndSend = initialDefaultsList[activeExtModeIdx]; // 获取此内部模式的初始参数默认值
                 if (effectiveConfig.IsParameterIntegerBased && !effectiveConfig.HasUnitConversion) // 纯整数型
                {
                    valueToStoreAndSend = (Single)Math.Round(valueToStoreAndSend);
                }
                this._modeControlDialCurrentValuesByMode[globalActionParameter][activeExtModeIdx] = valueToStoreAndSend;
                
                // OSC地址构造 (与ProcessAdvancedDialAdjustment中逻辑类似)
                String baseOscPathPress = DetermineBaseOscPathForModeControlDialSend(topConfig, globalActionParameter);
                String currentModeStrPress = SanitizeOscPathSegment(this.GetCurrentModeString(topConfig.ModeName));
                if (!String.IsNullOrEmpty(topConfig.OscAddress))
                {
                    oscAddressToSend = ResolveTextWithMode(topConfig, topConfig.OscAddress);
                    if (!oscAddressToSend.StartsWith("/"))
                    {
                        oscAddressToSend = $"{baseOscPathPress}/{oscAddressToSend}".Replace("//", "/");
                    }
                }
                else
                {
                    oscAddressToSend = $"{baseOscPathPress}/{currentModeStrPress}".Replace("//", "/");
                }
                oscAddressToSend = oscAddressToSend.TrimEnd('/');
                if (String.IsNullOrEmpty(oscAddressToSend)) { oscAddressToSend = "/"; }
            }
            else
            {
                PluginLog.Warning($"[LogicManager|ProcessAdvancedDialPress] Called with unhandled ActionType: {topConfig.ActionType} for '{globalActionParameter}'.");
                return;
            }

            if (!String.IsNullOrEmpty(oscAddressToSend) && oscAddressToSend != "/")
            {
                ReaOSCPlugin.SendOSCMessage(oscAddressToSend, valueToStoreAndSend);
                PluginLog.Info($"[LogicManager|ProcessAdvancedDialPress] '{topConfig.ActionType}' '{topConfig.DisplayName}' Reset OSC sent to '{oscAddressToSend}' -> {valueToStoreAndSend:F3}");
            }
            else { PluginLog.Warning($"[LogicManager|ProcessAdvancedDialPress] '{topConfig.ActionType}' '{topConfig.DisplayName}': Invalid OSC Address '{oscAddressToSend ?? "null"}' for sending on press."); }
            
            this.CommandStateNeedsRefresh?.Invoke(this, globalActionParameter); // 确保按下也刷新UI
        }

        // 【新增】辅助方法，用于确定ModeControlDial发送OSC消息的基础路径
        private string DetermineBaseOscPathForModeControlDialSend(ButtonConfig modeControlDialConfig, string modeControlDialActionParam)
        {
            // 优先使用顶层OscAddress字段，如果它不包含{mode}占位符，说明它可能是一个固定的基础路径
            if (!String.IsNullOrEmpty(modeControlDialConfig.OscAddress) && !modeControlDialConfig.OscAddress.Contains("{mode}"))
            {
                return SanitizeOscAddress(modeControlDialConfig.OscAddress); // 清理并返回它作为基础
            }
            // 否则，从actionParameter中提取基础部分 (去掉 /DialAction 后缀)
            const string dialActionSuffix = "/DialAction";
            if (modeControlDialActionParam.EndsWith(dialActionSuffix))
            {
                return modeControlDialActionParam.Substring(0, modeControlDialActionParam.Length - dialActionSuffix.Length);
            }
            return modeControlDialActionParam; // 如果没有后缀，则直接使用actionParameter
        }

        // 【新增】辅助方法：生成唯一的标题字段键
        private string GetUniqueTitleFieldKey(string baseActionParameter, string fieldName, int subIndex = -1)
        {
            if (string.IsNullOrEmpty(baseActionParameter)) return fieldName + (fieldName == "Titles_Element" && subIndex >= 0 ? $"#{subIndex}" : ""); // 容错处理
            
            // 仅当 fieldName 是为数组类型（如 Titles_Element）设计的，才附加 subIndex
            if (fieldName == "Titles_Element")
            {
                return $"{baseActionParameter}#{fieldName}{(subIndex >= 0 ? $"#{subIndex}" : "")}";
            }
            // 对于 "Title" 和 "Title_Mode2" 等非数组型字段，subIndex 不应作为键的一部分。
            return $"{baseActionParameter}#{fieldName}"; 
        }

        // 【新增】辅助方法：从唯一键中提取基础的actionParameter
        private string ExtractBaseActionParameterFromUniqueKey(string uniqueKey)
        {
            if (string.IsNullOrEmpty(uniqueKey)) return null;
            var parts = uniqueKey.Split('#');
            return parts.Length > 0 ? parts[0] : null;
        }
        
        // 【新增】辅助方法：注册控件的动态标题字段
        private void RegisterDynamicTitleFields(ButtonConfig config, string actionParameter)
        {
            if (config == null || string.IsNullOrEmpty(actionParameter)) return;

            // 辅助方法，用于注册单个标题字段及其可能的OSC地址
            Action<string, string, int> registerFieldLogic = (fieldName, fieldValue, fieldSubIndex) =>
            {
                if (string.IsNullOrEmpty(fieldValue)) return;

                string uniqueKey = GetUniqueTitleFieldKey(actionParameter, fieldName, fieldSubIndex);
                if (fieldValue.StartsWith("/")) // 如果字段值是一个OSC地址
                {
                    // 仍然保留_titleFieldToOscAddressMapping，以防其他地方用到或用于调试
                    _titleFieldToOscAddressMapping[uniqueKey] = fieldValue; 
                    _dynamicTitleSourceStrings[uniqueKey] = fieldValue; // 初始占位符设为OSC地址本身

                    // 填充新的反向映射字典 _oscAddressToDynamicTitleKeys
                    if (!this._oscAddressToDynamicTitleKeys.ContainsKey(fieldValue))
                    {
                        this._oscAddressToDynamicTitleKeys[fieldValue] = new List<String>();
                    }
                    // 避免重复添加同一个uniqueKey到列表 (虽然在正常注册流程中不太可能发生)
                    if (!this._oscAddressToDynamicTitleKeys[fieldValue].Contains(uniqueKey))
                    {
                        this._oscAddressToDynamicTitleKeys[fieldValue].Add(uniqueKey);
                    }
                    PluginLog.Verbose($"[LogicManager|RegisterDynamicTitleFields] Registered dynamic title for Key: '{uniqueKey}', OSC Address: '{fieldValue}' (ActionParam: '{actionParameter}')");
                }
                else // 字段值是静态模板字符串
                {
                    _dynamicTitleSourceStrings[uniqueKey] = fieldValue ?? string.Empty;
                }
            };

            // 处理 config.Title
            registerFieldLogic("Title", config.Title, -1);

            // 处理 config.Title_Mode2 (主要用于 ActionType "2ModeTickDial"等)
            if (!string.IsNullOrEmpty(config.Title_Mode2)) 
            {
                registerFieldLogic("Title_Mode2", config.Title_Mode2, -1);
            }

            // 处理 config.Titles 数组 (如果存在且有元素)
            if (config.Titles != null && config.Titles.Any())
            {
                for (int i = 0; i < config.Titles.Count; i++)
                {
                    string currentTitleElement = config.Titles[i];
                    // registerFieldLogic 内部会检查 currentTitleElement 是否为空
                    registerFieldLogic("Titles_Element", currentTitleElement, i);
                }
            }
             // 注意: ModeControlDial 的内部模式标题可能需要更复杂的处理，
             // 因为它的标题通常不是直接在顶层ButtonConfig的Title/Titles字段中，
             // 而是可能在 ParametersByMode/UnitsByMode 相关的配置中隐含或显式定义。
             // 当前的 RegisterDynamicTitleFields 主要处理顶层 ButtonConfig 的直接标题属性。
             // 如果 ModeControlDial 的每个模式的"显示名称"也需要动态化，
             // 则需要在 ParseAndStoreModeControlDialConfig 中，为每个内部模式的标题部分调用类似的注册逻辑。
        }

        // 【新增】获取标题模板的方法
        public String GetCurrentTitleTemplate(String baseActionParameter, String titleFieldName, ButtonConfig configForContext, int subIndex = -1)
        {
            if (string.IsNullOrEmpty(baseActionParameter) && configForContext == null)
            {
                PluginLog.Warning($"[LogicManager|GetCurrentTitleTemplate] baseActionParameter and configForContext are both null/empty. Cannot determine title template for field '{titleFieldName}'.");
                return $"Err: No Context";
            }
            
            // 如果 baseActionParameter 为空但 configForContext 不为空，尝试从 configForContext 构建临时的 actionParameter (用于特殊情况或调试)
            // 这通常不应发生，调用方应确保 baseActionParameter 有效。
            string effectiveActionParameter = baseActionParameter;
            if (string.IsNullOrEmpty(effectiveActionParameter) && configForContext != null && !string.IsNullOrEmpty(configForContext.DisplayName))
            {
                 // 尝试生成一个临时的、可能不完全准确的 actionParameter，仅用于查找。
                 // 更好的做法是调用方总是提供有效的 baseActionParameter。
                effectiveActionParameter = $"Fallback_{configForContext.GroupName ?? "NoGroup"}_{configForContext.DisplayName}";
                 PluginLog.Warning($"[LogicManager|GetCurrentTitleTemplate] baseActionParameter was empty. Using a fallback effectiveActionParameter: '{effectiveActionParameter}' for field '{titleFieldName}'. This might not be reliable.");
            }
            if (string.IsNullOrEmpty(effectiveActionParameter)) // 再次检查
            {
                PluginLog.Error($"[LogicManager|GetCurrentTitleTemplate] EffectiveActionParameter is still null or empty. Returning error for field '{titleFieldName}'.");
                return $"Err: No Param";
            }


            string uniqueKey = GetUniqueTitleFieldKey(effectiveActionParameter, titleFieldName, subIndex);

            if (_dynamicTitleSourceStrings.TryGetValue(uniqueKey, out var template))
            {
                PluginLog.Verbose($"[LogicManager|GetCurrentTitleTemplate] Found template for key '{uniqueKey}': '{template}'");
                return template;
            }

            // Fallback: 如果模板在_dynamicTitleSourceStrings中未找到
            PluginLog.Warning($"[LogicManager|GetCurrentTitleTemplate] Template not found in _dynamicTitleSourceStrings for key '{uniqueKey}'. Using fallback logic for field '{titleFieldName}'.");
            if (configForContext != null) {
                switch (titleFieldName)
                {
                    case "Title": return configForContext.Title ?? string.Empty;
                    case "Title_Mode2": return configForContext.Title_Mode2 ?? string.Empty;
                    case "Titles_Element":
                        return (configForContext.Titles != null && subIndex >= 0 && subIndex < configForContext.Titles.Count) ?
                               configForContext.Titles[subIndex] ?? string.Empty : string.Empty;
                    default:
                         PluginLog.Warning($"[LogicManager|GetCurrentTitleTemplate] Unknown titleFieldName '{titleFieldName}' for fallback logic. Key was '{uniqueKey}'. Config DisplayName: '{configForContext?.DisplayName}'");
                         break;
                }
            }
            return $"ErrTpl:{uniqueKey.Substring(Math.Max(0,uniqueKey.Length-15))}"; // 返回部分键名以帮助调试
        }

        // 【新增】公共辅助方法：判断地址是否与插件相关
        public bool IsAddressRelevant(string address)
        {
            if (string.IsNullOrEmpty(address)) return false;
            // 检查地址是否在动态标题的OSC地址映射中，或者在常规操作的OSC地址映射中
            return this._oscAddressToDynamicTitleKeys.ContainsKey(address) || 
                   this._oscAddressToActionParameterMap.ContainsKey(address);
        }

    }
}