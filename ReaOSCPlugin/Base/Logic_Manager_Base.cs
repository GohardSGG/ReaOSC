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
        public String Description { get; set; }

        // --- OSC 相关 ---
        public String OscAddress { get; set; } // 可选。如果提供，优先作为此控件贡献给CombineButton的路径片段 (经过处理后)

        // --- 模式相关 (主要用于 SelectModeButton 及其控制的按钮) ---
        public String ModeName { get; set; }
        public List<String> Modes { get; set; } // For SelectModeButton: 定义可选模式
        public List<String> Titles { get; set; } // For ParameterDial: 定义可选参数值; For SelectModeButton & controlled buttons: 定义不同模式下的显示标题
        public List<String> OscAddresses { get; set; } // For SelectModeButton & controlled buttons: 定义不同模式下的OSC地址

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
        public Int32 MinValue { get; set; } // 仅用于 Continuous 模式
        public Int32 MaxValue { get; set; } // 仅用于 Continuous 模式
        public List<Int32> DiscreteValues { get; set; } // 仅用于 Discrete 模式
        public Int32 DefaultValue { get; set; }
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
        
        private Boolean _isInitialized = false;
        private String _customConfigBasePath;

        private readonly Dictionary<String, List<String>> _modeOptions = new Dictionary<String, List<String>>();
        private readonly Dictionary<String, Int32> _currentModes = new Dictionary<String, Int32>();
        private readonly Dictionary<String, Action> _modeChangedEvents = new Dictionary<String, Action>();

        private readonly Dictionary<String, String> _oscAddressToActionParameterMap = new Dictionary<String, String>();
        public event EventHandler<String> CommandStateNeedsRefresh;

        private readonly Dictionary<String, Int32> _parameterDialSelectedIndexes = new Dictionary<String, Int32>();

        // 【新增】用于 ControlDial 状态存储
        private readonly Dictionary<String, Int32> _controlDialCurrentValues = new Dictionary<String, Int32>();
        private readonly Dictionary<String, ControlDialParsedConfig> _controlDialConfigs = new Dictionary<String, ControlDialParsedConfig>();

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
        public void RegisterModeGroup(ButtonConfig config) { var modeName = config.DisplayName; var modes = config.Modes; if (String.IsNullOrEmpty(modeName) || modes == null || modes.Count == 0) { return; } if (!this._modeOptions.ContainsKey(modeName)) { this._modeOptions[modeName] = modes; this._currentModes[modeName] = 0; this._modeChangedEvents[modeName] = null; PluginLog.Info($"[LogicManager][Mode] 模式组 '{modeName}' 已注册，包含模式: {String.Join(", ", modes)}"); } }
        public void ToggleMode(String modeName) { if (this._currentModes.TryGetValue(modeName, out _) && this._modeOptions.TryGetValue(modeName, out var options)) { this._currentModes[modeName] = (this._currentModes[modeName] + 1) % options.Count; this._modeChangedEvents[modeName]?.Invoke(); PluginLog.Info($"[LogicManager][Mode] 模式组 '{modeName}' 已切换到: {this.GetCurrentModeString(modeName)}"); this.CommandStateNeedsRefresh?.Invoke(this, this.GetActionParameterForModeController(modeName)); } }
        private String GetActionParameterForModeController(String modeName) => this._allConfigs.FirstOrDefault(kvp => kvp.Value.ActionType == "SelectModeButton" && kvp.Value.DisplayName == modeName).Key;
        public String GetCurrentModeString(String modeName) { if (this._currentModes.TryGetValue(modeName, out var currentIndex) && this._modeOptions.TryGetValue(modeName, out var options) && currentIndex >= 0 && currentIndex < options.Count) { return options[currentIndex]; } return String.Empty; }
        public Int32 GetCurrentModeIndex(String modeName) => this._currentModes.TryGetValue(modeName, out var currentIndex) ? currentIndex : -1;

        public void SubscribeToModeChange(String modeName, Action handler) { if (String.IsNullOrEmpty(modeName) || handler == null) { return; } if (!this._modeChangedEvents.ContainsKey(modeName)) { this._modeChangedEvents[modeName] = null; } this._modeChangedEvents[modeName] += handler; }
        public void UnsubscribeFromModeChange(String modeName, Action handler) { if (String.IsNullOrEmpty(modeName) || handler == null) { return; } if (this._modeChangedEvents.ContainsKey(modeName)) { this._modeChangedEvents[modeName] -= handler; } }
        #endregion

        #region 配置加载
        private void LoadAllConfigs()
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            // 根据您的要求，使用 AppData 下的特定目录作为自定义配置的根目录
            this._customConfigBasePath = Path.Combine(localAppData, "Loupedeck", "Plugins", "ReaOSC");

            PluginLog.Info($"[LogicManager] 使用自定义配置的基础路径: '{this._customConfigBasePath}'");

            // 为每个配置文件调用新的加载方法，该方法会自动处理外部覆盖和内部回退
            var generalConfigs = this.LoadConfigFile<Dictionary<String, List<ButtonConfig>>>("General/General_List.json");
            this.ProcessGroupedConfigs(generalConfigs);
            
            var dynamicFolderDefs = this.LoadConfigFile<List<ButtonConfig>>("Dynamic/Dynamic_List.json");
            this.ProcessDynamicFolderDefs(dynamicFolderDefs);
        }
        
        private T LoadConfigFile<T>(String relativePath) where T : class
        {
            // 将路径分隔符统一为当前系统的格式
            var platformRelativePath = relativePath.Replace('/', Path.DirectorySeparatorChar);
            var customFilePath = Path.Combine(this._customConfigBasePath, platformRelativePath);

            // 1. 优先尝试从外部自定义路径加载
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

            // 2. 如果外部文件加载失败或不存在，则回退到加载内嵌资源
            // 将相对路径转换为资源名称, e.g., "General/General_List.json" -> "Loupedeck.ReaOSCPlugin.General.General_List.json"
            var resourceName = $"Loupedeck.ReaOSCPlugin.{relativePath.Replace('/', '.')}";
            return this.LoadAndDeserialize<T>(Assembly.GetExecutingAssembly(), resourceName);
        }

        // 辅助方法，用于处理从 Dynamic_List.json 或自定义文件中加载的文件夹定义
        private void ProcessDynamicFolderDefs(List<ButtonConfig> dynamicFolderDefs)
        {
            if (dynamicFolderDefs == null) { return; }
            
            var folderEntriesToRegister = new List<ButtonConfig>();
            foreach (var folderDef in dynamicFolderDefs) // folderDef is a ButtonConfig for the folder entry itself
            {
                // Ensure the folder entry itself has a GroupName, typically "Dynamic"
                if (String.IsNullOrEmpty(folderDef.GroupName))
                {
                    folderDef.GroupName = "Dynamic"; 
                }

                if (folderDef.Content != null) // Content 是 JObject
                {
                    FolderContentConfig actualFolderContent = new FolderContentConfig();
                    var contentJObject = folderDef.Content; 

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
                            // 【新增】如果Buttons是"List"，则加载对应的外部数据源到_fxDataCache
                            var fileNamePartList = folderDef.DisplayName.Replace(" ", "_");
                            var fxListNameFromContent = $"Dynamic/{fileNamePartList}_List.json";
                            var fxDataFromContent = this.LoadConfigFile<Newtonsoft.Json.Linq.JObject>(fxListNameFromContent);
                            if (fxDataFromContent != null)
                            {
                                this._fxDataCache[folderDef.DisplayName] = fxDataFromContent;
                                PluginLog.Info($"[LogicManager] For folder '{folderDef.DisplayName}' (Buttons: \"List\"), successfully loaded data source '{fxListNameFromContent}' into _fxDataCache.");
                            }
                            else
                            {
                                PluginLog.Warning($"[LogicManager] For folder '{folderDef.DisplayName}' (Buttons: \"List\"), FAILED to load data source '{fxListNameFromContent}'. FX_Folder_Base instances may be empty if they rely on this.");
                            }
                        }
                        else
                        {
                            PluginLog.Warning($"[LogicManager] ProcessDynamicFolderDefs for folder '{folderDef.DisplayName}': 'Content.Buttons' is of an unexpected type: {buttonsToken.Type}. Assuming no static buttons and not a dynamic list from external file for FX_Folder_Base.");
                            actualFolderContent.IsButtonListDynamic = false; 
                        }
                    }
                    else
                    { // Buttons property missing in Content
                        PluginLog.Info($"[LogicManager] ProcessDynamicFolderDefs for folder '{folderDef.DisplayName}': 'Content.Buttons' is missing. Assuming no static buttons.");
                        actualFolderContent.IsButtonListDynamic = false; 
                    }

                    var dialsToken = contentJObject["Dials"];
                    if (dialsToken != null && dialsToken.Type == Newtonsoft.Json.Linq.JTokenType.Array)
                    {
                        actualFolderContent.Dials = dialsToken.ToObject<List<ButtonConfig>>() ?? new List<ButtonConfig>();
                    }
                    
                    this._folderContents[folderDef.DisplayName] = actualFolderContent;
                    this.ProcessFolderContentConfigs(actualFolderContent, folderDef.DisplayName); 
                }
                else // folderDef.Content == null (或者在JSON中完全不存在此字段)
                {
                    // 这种情况下，我们假设它是一个纯粹由外部 *_List.json 文件驱动的文件夹 (类似旧的FX_Folder_Base行为)
                    var fileNamePart = folderDef.DisplayName.Replace(" ", "_");
                    var fxListName = $"Dynamic/{fileNamePart}_List.json";
                    var fxData = this.LoadConfigFile<Newtonsoft.Json.Linq.JObject>(fxListName);
                    if (fxData != null)
                    {
                        this._fxDataCache[folderDef.DisplayName] = fxData;
                        PluginLog.Info($"[LogicManager] For folder '{folderDef.DisplayName}' (No Content field), successfully loaded data source '{fxListName}' into _fxDataCache.");

                        // 【重要】即使没有Content字段，如果这是一个FX_Folder_Base类型的文件夹，它也可能有在Dynamic_List.json中定义的旋钮。
                        // 我们需要一种方法来让FX_Folder_Base获取这些旋钮。 
                        // 一个选择是，如果folderDef.Content为null，我们也创建一个空的FolderContentConfig，
                        // 并尝试从folderDef本身（它是一个ButtonConfig）提取可能的Dials定义。
                        // 然而，Dynamic_List.json的当前结构是Dials在Content内部。 
                        // 所以，如果一个FX类型文件夹想要在Dynamic_List.json中定义旋钮，它必须有一个Content对象，即使Buttons是"List"。
                        // 因此，这个else块主要适用于那些完全没有在Dynamic_List.json中定义任何按钮或旋钮，纯粹依赖外部文件的旧式文件夹。
                        // 对于我们的目标（Effect, Instrument等），它们有Content.Dials，所以会走上面的if分支。
                    }
                    else
                    {
                        PluginLog.Warning($"[LogicManager] For folder '{folderDef.DisplayName}' (No Content field), FAILED to load data source '{fxListName}'.");
                    }
                }
                
                // Prepare the folder entry itself for registration (without its JObject Content)
                var folderEntryForRegistration = new ButtonConfig {
                    DisplayName = folderDef.DisplayName,
                    Title = folderDef.Title,
                    TitleColor = folderDef.TitleColor,
                    GroupName = folderDef.GroupName, // Should be "Dynamic"
                    ActionType = folderDef.ActionType,
                    Description = folderDef.Description,
                    Text = folderDef.Text,
                    TextColor = folderDef.TextColor,
                    TextSize = folderDef.TextSize,
                    TextX = folderDef.TextX,
                    TextY = folderDef.TextY,
                    TextWidth = folderDef.TextWidth,
                    TextHeight = folderDef.TextHeight,
                    BackgroundColor = folderDef.BackgroundColor,
                    ButtonImage = folderDef.ButtonImage,
                    PreserveBrandOrderInJson = folderDef.PreserveBrandOrderInJson
                    // Do NOT copy Content (JObject) here
                };
                folderEntriesToRegister.Add(folderEntryForRegistration);
            }
            // Register the folder entries themselves (not their internal content)
            this.RegisterConfigs(folderEntriesToRegister, isDynamicFolderEntry: true, defaultGroupName: "Dynamic");
        }

        private T LoadAndDeserialize<T>(Assembly assembly, String resourceName) where T : class { try { using (var stream = assembly.GetManifestResourceStream(resourceName)) { if (stream == null) { PluginLog.Info($"[LogicManager] 内嵌资源 '{resourceName}' 未找到或加载失败。"); return null; } using (var reader = new StreamReader(stream)) { return JsonConvert.DeserializeObject<T>(reader.ReadToEnd()); } } } catch (Exception ex) { PluginLog.Error(ex, $"[LogicManager] 读取或解析内嵌资源 '{resourceName}' 失败。"); return null; } }
        private void ProcessGroupedConfigs(Dictionary<String, List<ButtonConfig>> groupedConfigs) { if (groupedConfigs == null) { return; } foreach (var group in groupedConfigs) { var configs = group.Value.Select(config => { config.GroupName = group.Key; return config; }).ToList(); this.RegisterConfigs(configs); } }
        private void ProcessFolderContentConfigs(FolderContentConfig folderContent, String folderDisplayNameAsDefaultGroupName) 
        { 
            if (folderContent == null) { return; } 
            // Pass the folder's DisplayName as the default GroupName for its buttons and dials
            this.RegisterConfigs(folderContent.Buttons, isDynamicFolderEntry: false, defaultGroupName: folderDisplayNameAsDefaultGroupName); 
            this.RegisterConfigs(folderContent.Dials, isDynamicFolderEntry: false, defaultGroupName: folderDisplayNameAsDefaultGroupName); 
        }

        private void RegisterConfigs(List<ButtonConfig> configs, Boolean isDynamicFolderEntry = false, String defaultGroupName = null)
        {
            if (configs == null)
            {
                return;
            }
            foreach (var config in configs)
            {
                // 【新增】Assign default GroupName if current one is empty and a default is provided
                if (String.IsNullOrEmpty(config.GroupName) && !String.IsNullOrEmpty(defaultGroupName))
                {
                    config.GroupName = defaultGroupName;
                }

                if (String.IsNullOrEmpty(config.GroupName))
                { 
                    // If still no GroupName, log and skip (unless it's a folder entry that uses DisplayName as key)
                    if (!isDynamicFolderEntry) // Dynamic folder entries use DisplayName as key, GroupName "Dynamic" is set earlier
                    {
                        PluginLog.Warning($"[LogicManager] RegisterConfigs: 配置 '{config.DisplayName}' 缺少 GroupName，已跳过。"); 
                        continue; 
                    }
                }
                String actionParameter;
                if (isDynamicFolderEntry)
                { actionParameter = config.DisplayName; }
                else
                {
                    String groupNameForPath = SanitizeOscPathSegment(config.GroupName);
                    String displayNameForPath = SanitizeOscPathSegment(config.DisplayName);
                    actionParameter = $"/{groupNameForPath}/{displayNameForPath}";
                    if (config.ActionType != null && config.ActionType.Contains("Dial"))
                    {
                        actionParameter += "/DialAction";
                    }
                }
                actionParameter = actionParameter.Replace("//", "/").TrimEnd('/');
                if (this._allConfigs.ContainsKey(actionParameter))
                { continue; }
                this._allConfigs[actionParameter] = config;

                if (config.ActionType == "ToggleButton" || config.ActionType == "ToggleDial")
                { 
                    this._toggleStates[actionParameter] = false; 
                    PluginLog.Info($"[LogicManager|RegisterConfigs] Initialized toggle state for '{actionParameter}' to false."); // 保留原有日志或调整
                }
                else if (config.ActionType == "2ModeTickDial")
                { this._dialModes[actionParameter] = 0; }
                else if (config.ActionType == "ParameterDial")
                { this._parameterDialSelectedIndexes[actionParameter] = 0; }
                else if (config.ActionType == "ControlDial")
                {
                    this.ParseAndStoreControlDialConfig(config, actionParameter);
                }

                String effectiveOscAddressForStateListener = null;
                if (!String.IsNullOrEmpty(config.OscAddress))
                {
                    // 【修正】确保用于监听的地址是基于JSON GroupName（如果OscAddress是相对的）
                    // 或者如果OscAddress是绝对的，则直接使用。
                    // DetermineOscAddressForAction 会处理这个问题。
                    effectiveOscAddressForStateListener = this.DetermineOscAddressForAction(config, config.GroupName, config.OscAddress);
                }
                else if (config.ActionType == "ToggleButton" || config.ActionType == "ToggleDial")
                {
                    effectiveOscAddressForStateListener = this.DetermineOscAddressForAction(config, config.GroupName); // 使用JSON GroupName
                }

                if (!String.IsNullOrEmpty(effectiveOscAddressForStateListener) && effectiveOscAddressForStateListener != "/")
                {
                    if (config.ActionType == "ToggleButton" || config.ActionType == "ToggleDial")
                    {
                        this._oscAddressToActionParameterMap[effectiveOscAddressForStateListener] = actionParameter;
                        PluginLog.Info($"[LogicManager|RegisterConfigs] Mapping OSC listen address \'{effectiveOscAddressForStateListener}\' to actionParameter \'{actionParameter}\' for ToggleButton/Dial \'{config.DisplayName}\' (JSON Group: \'{config.GroupName}\')."); // 新增日志记录监听地址和actionParameter
                        // 初始化状态时也考虑当前OSC设备的状态
                        // 【修正】确保 GetState 的调用不会因为 OSCStateManager 未初始化而异常 (虽然不太可能，但保险起见)
                        var initialStateFromDevice = OSCStateManager.Instance?.GetState(effectiveOscAddressForStateListener) > 0.5f;
                        if (this._toggleStates.TryGetValue(actionParameter, out var currentState) && currentState != initialStateFromDevice)
                        {
                            this._toggleStates[actionParameter] = initialStateFromDevice;
                            PluginLog.Info($"[LogicManager|RegisterConfigs] Initial toggle state for '{actionParameter}' (from device) set to {initialStateFromDevice}. Overwrote previous default.");
                        }
                        else if (!this._toggleStates.ContainsKey(actionParameter)) // 避免覆盖上面已设置的false，除非来自设备
                        {
                             this._toggleStates[actionParameter] = initialStateFromDevice;
                             PluginLog.Info($"[LogicManager|RegisterConfigs] Initial toggle state for '{actionParameter}' (from device) set to {initialStateFromDevice}.");
                        }
                    }
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
            if (groupName == "Dynamic")
            { return this._allConfigs.TryGetValue(displayName, out var c) && c.GroupName == groupName ? c : null; }

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
            PluginLog.Info($"[LogicManager|OnOSCStateChanged] Received OSC: Address='{e.Address}', Value='{e.Value}'"); // 新增日志：记录收到的OSC消息
            if (this._oscAddressToActionParameterMap.TryGetValue(e.Address, out String mappedActionParameter))
            {
                var config = this.GetConfig(mappedActionParameter);
                if (config == null)
                {
                    PluginLog.Warning($"[LogicManager|OnOSCStateChanged] Matched OSC Address '{e.Address}' to actionParameter '{mappedActionParameter}', but config is null."); // 完善日志
                    return;
                }
                PluginLog.Info($"[LogicManager|OnOSCStateChanged] Matched OSC Address '{e.Address}' to actionParameter '{mappedActionParameter}' (Config: '{config.DisplayName}', Type: '{config.ActionType}')."); // 完善日志

                if (config.ActionType == "ToggleButton" || config.ActionType == "ToggleDial")
                {
                    var newState = e.Value > 0.5f;
                    if (!this._toggleStates.ContainsKey(mappedActionParameter) || this._toggleStates[mappedActionParameter] != newState)
                    {
                        this._toggleStates[mappedActionParameter] = newState;
                        PluginLog.Info($"[LogicManager|OnOSCStateChanged] State for '{mappedActionParameter}' ('{config.DisplayName}') changed to {newState}. Invoking CommandStateNeedsRefresh."); // 完善日志
                        this.CommandStateNeedsRefresh?.Invoke(this, mappedActionParameter);
                    }
                    else
                    {
                        PluginLog.Verbose($"[LogicManager|OnOSCStateChanged] State for '{mappedActionParameter}' ('{config.DisplayName}') already {newState}. No change needed."); // 新增日志：状态未变
                    }
                }
                // 【新增】处理ControlDial的外部状态更新
                else if (config.ActionType == "ControlDial")
                {
                    if (this._controlDialConfigs.TryGetValue(mappedActionParameter, out var parsedDialConfig) && 
                        this._controlDialCurrentValues.ContainsKey(mappedActionParameter))
                    {
                        Int32 incomingIntValue = (Int32)e.Value; // OSC值通常是float，转为int (截断)
                        Int32 validatedValue = incomingIntValue;
                        Int32 currentStoredValue = this._controlDialCurrentValues[mappedActionParameter];

                        if (parsedDialConfig.Mode == ControlDialMode.Continuous)
                        {
                            validatedValue = Math.Clamp(incomingIntValue, parsedDialConfig.MinValue, parsedDialConfig.MaxValue);
                        }
                        else // Discrete Mode
                        {
                            if (!parsedDialConfig.DiscreteValues.Contains(incomingIntValue))
                            {
                                PluginLog.Warning($"[LogicManager|OnOSCStateChanged] ControlDial '{config.DisplayName}' (action: {mappedActionParameter}) received OSC value {incomingIntValue} which is not in its discrete list of values. Ignoring update from OSC.");
                                return; // 忽略无效的离散值
                            }
                            // validatedValue 已经是 incomingIntValue，且已确认在列表中
                        }

                        if (validatedValue != currentStoredValue)
                        {
                            this._controlDialCurrentValues[mappedActionParameter] = validatedValue;
                            PluginLog.Info($"[LogicManager|OnOSCStateChanged] ControlDial '{config.DisplayName}' (action: {mappedActionParameter}) state updated via OSC from {currentStoredValue} to {validatedValue}. Address: {e.Address}");
                            this.CommandStateNeedsRefresh?.Invoke(this, mappedActionParameter);
                        }
                        else
                        {
                            PluginLog.Verbose($"[LogicManager|OnOSCStateChanged] ControlDial '{config.DisplayName}' (action: {mappedActionParameter}) received OSC value {validatedValue} which matches current state. No change needed from OSC.");
                        }
                    }
                    else
                    {
                        PluginLog.Warning($"[LogicManager|OnOSCStateChanged] ControlDial '{config.DisplayName}' (action: {mappedActionParameter}) received OSC for address '{e.Address}', but its internal config or current value was not found.");
                    }
                }
            }
            else
            {
                PluginLog.Info($"[LogicManager|OnOSCStateChanged] OSC Address '{e.Address}' NOT found in _oscAddressToActionParameterMap."); // 新增日志：未匹配到地址
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
        public Int32 GetControlDialValue(String actionParameter)
        {
            return this._controlDialCurrentValues.TryGetValue(actionParameter, out var val) ? val : 0;
        }

        public Boolean ProcessUserAction(String actionParameter, String dynamicFolderDisplayName = null, ButtonConfig itemConfig = null)
        {
            // 优先使用传入的 itemConfig，如果为null，则尝试从 _allConfigs 中获取
            var config = itemConfig ?? this.GetConfig(actionParameter);
            
            if (config == null)
            {
                PluginLog.Warning($"[LogicManager] ProcessUserAction: 未找到配置 for '{actionParameter}', 且未提供 itemConfig。");
                return false;
            }

            Boolean needsUiRefresh = false;
            String oscAddressToSend = null;
            Single oscValueToSend = 1.0f; // Single for float
            Boolean sendOsc = false;

            switch (config.ActionType)
            {
                case "ToggleButton":
                    // 对于ToggleButton，其 actionParameter 应该是全局注册的 Key
                    this.SetToggleState(actionParameter, !this.GetToggleState(actionParameter)); 
                    oscAddressToSend = this.DetermineOscAddressForAction(config, config.GroupName); // 使用按钮自己的GroupName
                    oscValueToSend = this.GetToggleState(actionParameter) ? 1.0f : 0.0f;
                    sendOsc = true;
                    break;

                case "TriggerButton":
                    if (itemConfig != null && !String.IsNullOrEmpty(dynamicFolderDisplayName)) // 表明是来自文件夹的动态列表项
                    {
                        // 构建特定格式的OSC地址: /FolderName/Add/TopGroup/ItemName
                        var folderNamePart = SanitizeOscPathSegment(dynamicFolderDisplayName);
                        var topGroupPart = SanitizeOscPathSegment(itemConfig.GroupName); // itemConfig.GroupName 是数据源JSON中的父级键
                        var itemNamePart = SanitizeOscPathSegment(itemConfig.DisplayName);
                        oscAddressToSend = $"/{folderNamePart}/Add/{topGroupPart}/{itemNamePart}".Replace("//", "/");
                    }
                    else // 普通的、已注册的 TriggerButton
                    {
                        oscAddressToSend = this.DetermineOscAddressForAction(config, config.GroupName); // 使用按钮自己的GroupName
                    }
                    oscValueToSend = 1.0f;
                    sendOsc = true;
                    this.CommandStateNeedsRefresh?.Invoke(this, actionParameter); // TriggerButton按下通常也刷新一下，用于瞬时反馈
                    break;

                case "CombineButton":
                    this.ProcessCombineButtonAction(config, dynamicFolderDisplayName); // dynamicFolderDisplayName 仅用于日志或上下文
                    this.CommandStateNeedsRefresh?.Invoke(this, actionParameter);
                    break;

                case "SelectModeButton":
                    this.ToggleMode(config.DisplayName); // SelectModeButton的DisplayName是ModeGroup的名称
                    // ToggleMode 内部会调用 CommandStateNeedsRefresh
                    needsUiRefresh = true; // SelectModeButton按下通常需要刷新依赖它的按钮
                    break;
                // ControlDial 的按下操作由 ProcessDialPress 处理，不应在此处处理旋转逻辑
                default:
                    PluginLog.Warning($"[LogicManager] ProcessUserAction: 未处理的 ActionType '{config.ActionType}' for '{config.DisplayName}' (actionParameter: '{actionParameter}')");
                    break;
            }

            if (sendOsc)
            {
                if (!String.IsNullOrEmpty(oscAddressToSend) && oscAddressToSend != "/")
                {
                    ReaOSCPlugin.SendOSCMessage(oscAddressToSend, oscValueToSend);
                    PluginLog.Info($"[LogicManager] ProcessUserAction: OSC已发送 '{oscAddressToSend}' -> {oscValueToSend} (源: '{config.DisplayName}', ActionType: {config.ActionType})");
                }
                else
                {
                    PluginLog.Warning($"[LogicManager] ProcessUserAction: 无法为 '{config.DisplayName}' (ActionType: {config.ActionType}) 确定有效的OSC地址。");
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
            if (config == null)
            { PluginLog.Warning($"[LogicManager] ProcessDialAdjustment (New): Config not found for '{globalActionParameter}'"); return; }

            switch (config.ActionType)
            {
                case "ParameterDial":
                    if (config.Titles == null || config.Titles.Count == 0)
                    {
                        break;
                    }
                    var currentIndex = this.GetParameterDialSelectedIndex(globalActionParameter);
                    currentIndex += ticks;
                    if (currentIndex >= config.Titles.Count)
                    {
                        currentIndex = 0;
                    }
                    else if (currentIndex < 0)
                    {
                        currentIndex = config.Titles.Count - 1;
                    }
                    this._parameterDialSelectedIndexes[globalActionParameter] = currentIndex;
                    this.CommandStateNeedsRefresh?.Invoke(this, globalActionParameter);
                    this.NotifyLinkedParameterButtons(config.DisplayName, config.GroupName); // GroupName是JSON GroupName
                    break;
                // 【修正OSC地址规则】确保TickDial, 2ModeTickDial, ToggleDial的OSC地址基于JSON GroupName
                case "TickDial":
                case "2ModeTickDial":
                    // 这些类型通常在旧版ProcessDialAdjustment中处理OSC，
                    // 但如果新路径也可能调整它们，需要在这里发送OSC
                    // 调用ProcessLegacyDialAdjustmentInternal以复用其OSC发送逻辑，它已使用JSON GroupName
                    this.ProcessLegacyDialAdjustmentInternal(config, ticks, globalActionParameter, null); // lastEventTimes 可能需要传递或处理
                    this.CommandStateNeedsRefresh?.Invoke(this, globalActionParameter); // 确保刷新
                    break;
                case "ToggleDial":
                    this.ProcessLegacyToggleDialAdjustmentInternal(config, ticks, globalActionParameter);
                    this.CommandStateNeedsRefresh?.Invoke(this, globalActionParameter); // 确保刷新
                    break;
                case "ControlDial":
                    if (this._controlDialConfigs.TryGetValue(globalActionParameter, out var controlConfig) &&
                        this._controlDialCurrentValues.TryGetValue(globalActionParameter, out var currentValue))
                    {
                        Int32 newValue = currentValue;
                        if (controlConfig.Mode == ControlDialMode.Continuous)
                        {
                            newValue = Math.Clamp(currentValue + ticks, controlConfig.MinValue, controlConfig.MaxValue);
                        }
                        else // Discrete Mode
                        {
                            if (controlConfig.DiscreteValues != null && controlConfig.DiscreteValues.Any())
                            {
                                var discreteModeCurrentIndex = controlConfig.DiscreteValues.IndexOf(currentValue); // 变量重命名
                                if (discreteModeCurrentIndex == -1) // Should not happen if initialized correctly
                                {
                                    discreteModeCurrentIndex = 0;
                                }
                                var newIndex = (discreteModeCurrentIndex + ticks % controlConfig.DiscreteValues.Count + controlConfig.DiscreteValues.Count) % controlConfig.DiscreteValues.Count;
                                newValue = controlConfig.DiscreteValues[newIndex];
                            }
                        }

                        if (newValue != currentValue)
                        {
                            this._controlDialCurrentValues[globalActionParameter] = newValue;
                            // OSC地址构建: 优先用config.OscAddress, 否则用 /<GroupName>/<Title>
                            // 注意：config.Title 是预期的显示标题，比 config.DisplayName 更适合用于OSC路径 (如果OscAddress未定义)
                            String oscAddress = this.DetermineOscAddressForAction(config, config.GroupName, config.OscAddress ?? config.Title);
                            if (!String.IsNullOrEmpty(oscAddress) && oscAddress != "/")
                            {
                                ReaOSCPlugin.SendOSCMessage(oscAddress, newValue); // 发送整数值
                                PluginLog.Info($"[LogicManager] ControlDial '{config.DisplayName}' OSC sent to '{oscAddress}' -> {newValue}");
                            }
                            this.CommandStateNeedsRefresh?.Invoke(this, globalActionParameter);
                        }
                    }
                    break;
                default:
                    PluginLog.Warning($"[LogicManager] ProcessDialAdjustment (New): 未处理的 ActionType '{config.ActionType}' for '{globalActionParameter}'");
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
                    this.ProcessDialAdjustment(actionParameter, ticks);
                    break;
                case "ControlDial":
                    if (this._controlDialConfigs.TryGetValue(actionParameter, out var controlConfigToReset))
                    {
                        this._controlDialCurrentValues[actionParameter] = controlConfigToReset.DefaultValue;
                        String oscAddressReset = this.DetermineOscAddressForAction(config, config.GroupName, config.OscAddress ?? config.Title);
                        if (!String.IsNullOrEmpty(oscAddressReset) && oscAddressReset != "/")
                        {
                            ReaOSCPlugin.SendOSCMessage(oscAddressReset, controlConfigToReset.DefaultValue); // 发送默认整数值
                            PluginLog.Info($"[LogicManager] ControlDial '{config.DisplayName}' Reset OSC sent to '{oscAddressReset}' -> {controlConfigToReset.DefaultValue}");
                        }
                    }
                    break;
                default:
                    PluginLog.Warning($"[LogicManager] ProcessDialAdjustment (Legacy): 未处理的 ActionType '{config.ActionType}' for '{actionParameter}'");
                    break;
            }
        }

        public Boolean ProcessDialPress(String globalActionParameter)
        {
            var config = this.GetConfig(globalActionParameter);
            if (config == null)
            { PluginLog.Warning($"[LogicManager] ProcessDialPress (New): Config not found for '{globalActionParameter}'"); return false; }

            Boolean uiShouldRefresh = false;
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
                    if (!String.IsNullOrEmpty(config.ResetOscAddress))
                    { this.SendResetOscForDial(config, globalActionParameter); }
                    break;
                case "TickDial":
                case "ToggleDial":
                    if (!String.IsNullOrEmpty(config.ResetOscAddress))
                    { this.SendResetOscForDial(config, globalActionParameter); }
                    break;
                case "ControlDial":
                    if (this._controlDialConfigs.TryGetValue(globalActionParameter, out var controlConfigToReset) &&
                        this._controlDialCurrentValues.ContainsKey(globalActionParameter)) // 【修改】确保当前值也存在，以便比较
                    {
                        var oldValue = this._controlDialCurrentValues[globalActionParameter]; // 获取旧值用于比较
                        var newValue = controlConfigToReset.DefaultValue;
                        this._controlDialCurrentValues[globalActionParameter] = newValue;
                        
                        String oscAddressReset = this.DetermineOscAddressForAction(config, config.GroupName, config.OscAddress ?? config.Title);
                        if (!String.IsNullOrEmpty(oscAddressReset) && oscAddressReset != "/")
                        {
                            ReaOSCPlugin.SendOSCMessage(oscAddressReset, newValue); // 发送默认整数值
                            PluginLog.Info($"[LogicManager] ControlDial '{config.DisplayName}' Reset OSC sent to '{oscAddressReset}' -> {newValue}");
                        }

                        if (oldValue != newValue) // 只有当值确实因为重置而改变时才标记UI刷新
                        {
                            uiShouldRefresh = true; 
                        }
                        else
                        {
                             PluginLog.Info($"[LogicManager] ControlDial '{config.DisplayName}' was already at its default value {newValue}. Press did not change value, UI refresh relies on external OSC feedback if any.");
                        }
                    }
                    break;
            }
            // 【修改】将 CommandStateNeedsRefresh 的调用移到 switch 之后，并基于 uiShouldRefresh
            if (uiShouldRefresh) 
            {
                this.CommandStateNeedsRefresh?.Invoke(this, globalActionParameter);
            }
            return uiShouldRefresh; // 返回最终的刷新状态
        }

        private void SendResetOscForDial(ButtonConfig config, String actionParameterKey)
        {
            // 【新增辅助方法】用于发送旋钮按下的Reset OSC消息，确保使用JSON GroupName
            String fullResetAddress = this.DetermineOscAddressForAction(config, config.GroupName, config.ResetOscAddress);
            if (!String.IsNullOrEmpty(fullResetAddress) && fullResetAddress != "/")
            { ReaOSCPlugin.SendOSCMessage(fullResetAddress, 1f); }
            else
            { PluginLog.Warning($"[LogicManager] Dial Reset '{actionParameterKey}' (JSON GroupName '{config.GroupName}') 生成的 OSC 地址无效。"); }
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
                return this.ProcessDialPress(actionParameter);
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
        public static String SanitizeOscAddress(String address) { if (String.IsNullOrEmpty(address)) { return "/"; } var sanitized = address.Replace(" ", "_"); sanitized = Regex.Replace(sanitized, @"[^\w_/-]", ""); if (!sanitized.StartsWith("/")) { sanitized = "/" + sanitized; } return sanitized.Replace("//", "/"); }

        // 【修正OSC地址规则】DetermineOscAddressForAction的第二个参数明确为jsonGroupNameForOsc
        private String DetermineOscAddressForAction(ButtonConfig config, String jsonGroupNameForOsc, String explicitOscAddressField = null)
        {
            String basePath = SanitizeOscPathSegment(jsonGroupNameForOsc);
            String actionPath;

            if (!String.IsNullOrEmpty(explicitOscAddressField))
            { actionPath = explicitOscAddressField; } // explicitOscAddressField 可能已经是完整路径或片段
            else if (!String.IsNullOrEmpty(config.OscAddress))
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

            if (String.IsNullOrEmpty(basePath) || basePath == "/") // 如果 basePath 为空或仅为根
            {
                // 如果 actionPath 是 DisplayName (例如 ControlDial 且 OscAddress 未定义)，我们要确保它不被 SanitizeOscPathSegment 进一步处理 (因为它已经是合适的片段)
                // 但如果 actionPath 本身包含斜杠 (例如用户定义的 OscAddress)，则 SanitizeOscPathSegment 可能仍然需要。
                // 这里的逻辑是：如果原始actionPath (未经过SanitizeOscPathSegment的) 包含斜杠，则它可能已经是相对路径或绝对路径的一部分
                if (actionPath.Contains("/")) 
                {
                    return $"/{sanitizedActionPath}".Replace("//", "/");
                }
                else // 如果原始actionPath不含斜杠，则它是纯粹的名称，应该直接附加
                {
                     return $"/{actionPath}".Replace("//", "/"); // 使用原始 actionPath
                }
            }
            // 如果 basePath 存在，且 actionPath 不以斜杠开头
            if (actionPath.Contains("/")) // 如果 actionPath 包含斜杠，它可能是多段路径
            {
                 return $"/{basePath}/{sanitizedActionPath}".Replace("//", "/");
            }
            else // 如果 actionPath 不含斜杠，它是纯粹的名称
            {
                 return $"/{basePath}/{actionPath}".Replace("//", "/"); // 使用原始 actionPath
            }
        }
        #endregion

        #region 旧的旋钮处理逻辑 (内部实现) - 这些方法已经使用config.GroupName，符合规则
        private void ProcessLegacyDialAdjustmentInternal(ButtonConfig config, Int32 ticks, String actionParameter, Dictionary<String, DateTime> lastEventTimes)
        {
            String groupNameForPath = SanitizeOscPathSegment(config.GroupName); // 使用JSON GroupName
            String displayNameForPath = SanitizeOscPathSegment(config.DisplayName);

            String jsonIncreaseAddress, jsonDecreaseAddress;
            if (config.ActionType == "2ModeTickDial" && this.GetDialMode(actionParameter) == 1)
            { jsonIncreaseAddress = config.IncreaseOSCAddress_Mode2; jsonDecreaseAddress = config.DecreaseOSCAddress_Mode2; }
            else
            { jsonIncreaseAddress = config.IncreaseOSCAddress; jsonDecreaseAddress = config.DecreaseOSCAddress; }

            String finalIncreaseOscAddress, finalDecreaseOscAddress;
            if (!String.IsNullOrEmpty(jsonIncreaseAddress))
            { finalIncreaseOscAddress = this.DetermineOscAddressForAction(config, config.GroupName, jsonIncreaseAddress); } // 使用JSON GroupName
            else
            { finalIncreaseOscAddress = $"/{groupNameForPath}/{displayNameForPath}/Right".Replace("//", "/"); }
            if (!String.IsNullOrEmpty(jsonDecreaseAddress))
            { finalDecreaseOscAddress = this.DetermineOscAddressForAction(config, config.GroupName, jsonDecreaseAddress); } // 使用JSON GroupName
            else
            { finalDecreaseOscAddress = $"/{groupNameForPath}/{displayNameForPath}/Left".Replace("//", "/"); }

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
            PluginLog.Info("[LogicManager] Disposed.");
        }

        // 【新增】辅助方法：解析并存储 ControlDial 的配置
        private void ParseAndStoreControlDialConfig(ButtonConfig config, String actionParameter)
        {
            if (config.Parameter == null || !config.Parameter.Any())
            {
                PluginLog.Warning($"[LogicManager] ControlDial '{config.DisplayName}' (action: {actionParameter}) has no 'Parameter' array defined. Cannot initialize.");
                return;
            }

            var parsedDialConfig = new ControlDialParsedConfig();
            Int32 defaultValueFromParam = 0; // 临时默认值，如果ParameterDefault未提供

            if (config.Parameter.Count > 0 && config.Parameter[0]?.ToLower() == "true")
            {
                // 连续模式
                parsedDialConfig.Mode = ControlDialMode.Continuous;
                if (config.Parameter.Count >= 3 &&
                    Int32.TryParse(config.Parameter[1], out var min) &&
                    Int32.TryParse(config.Parameter[2], out var max))
                {
                    parsedDialConfig.MinValue = Math.Min(min, max);
                    parsedDialConfig.MaxValue = Math.Max(min, max);
                    defaultValueFromParam = parsedDialConfig.MinValue; // 连续模式下，若无ParameterDefault，则默认值为最小值
                }
                else
                {
                    PluginLog.Warning($"[LogicManager] ControlDial '{config.DisplayName}' (action: {actionParameter}) continuous mode parameters are invalid. Expected true, min_int, max_int. Using 0-100 default.");
                    parsedDialConfig.MinValue = 0;
                    parsedDialConfig.MaxValue = 100;
                    defaultValueFromParam = 0;
                }
            }
            else
            {
                // 离散模式
                parsedDialConfig.Mode = ControlDialMode.Discrete;
                parsedDialConfig.DiscreteValues = new List<Int32>();
                foreach (var paramStr in config.Parameter)
                {
                    if (Int32.TryParse(paramStr, out var val))
                    {
                        parsedDialConfig.DiscreteValues.Add(val);
                    }
                    else
                    {
                        PluginLog.Warning($"[LogicManager] ControlDial '{config.DisplayName}' (action: {actionParameter}) discrete mode parameter '{paramStr}' is not a valid integer. Skipping.");
                    }
                }
                if (!parsedDialConfig.DiscreteValues.Any())
                {
                    PluginLog.Warning($"[LogicManager] ControlDial '{config.DisplayName}' (action: {actionParameter}) discrete mode has no valid integer parameters. Adding 0 as default.");
                    parsedDialConfig.DiscreteValues.Add(0);
                }
                defaultValueFromParam = parsedDialConfig.DiscreteValues[0]; // 离散模式下，若无ParameterDefault，则默认值为列表第一个值
            }

            // 处理 ParameterDefault
            parsedDialConfig.DefaultValue = defaultValueFromParam; // 先用从Parameter数组推断的
            if (!String.IsNullOrEmpty(config.ParameterDefault) && Int32.TryParse(config.ParameterDefault, out var explicitDefault))
            {
                if (parsedDialConfig.Mode == ControlDialMode.Continuous)
                {
                    parsedDialConfig.DefaultValue = Math.Clamp(explicitDefault, parsedDialConfig.MinValue, parsedDialConfig.MaxValue);
                }
                else // Discrete
                {
                    if (parsedDialConfig.DiscreteValues.Contains(explicitDefault))
                    {
                        parsedDialConfig.DefaultValue = explicitDefault;
                    }
                    else
                    {
                         PluginLog.Warning($"[LogicManager] ControlDial '{config.DisplayName}' (action: {actionParameter}) ParameterDefault '{explicitDefault}' not found in discrete values. Using first discrete value as default.");
                        // DefaultValue 保持为 defaultValueFromParam (即列表第一个)
                    }
                }
            }

            this._controlDialConfigs[actionParameter] = parsedDialConfig;
            this._controlDialCurrentValues[actionParameter] = parsedDialConfig.DefaultValue;
            PluginLog.Info($"[LogicManager] ControlDial '{config.DisplayName}' (action: {actionParameter}) initialized. Mode: {parsedDialConfig.Mode}, Default: {parsedDialConfig.DefaultValue}. Current: {this._controlDialCurrentValues[actionParameter]}");

            // 【新增】为ControlDial注册OSC地址以进行监听
            // OSC地址构建: 优先用config.OscAddress, 否则用 /<GroupName>/<Title>
            String oscAddressForListening = this.DetermineOscAddressForAction(config, config.GroupName, config.OscAddress ?? config.Title);
            if (!String.IsNullOrEmpty(oscAddressForListening) && oscAddressForListening != "/")
            {
                if (!this._oscAddressToActionParameterMap.ContainsKey(oscAddressForListening))
                {
                    this._oscAddressToActionParameterMap[oscAddressForListening] = actionParameter;
                    PluginLog.Info($"[LogicManager|ParseAndStoreControlDialConfig] ControlDial '{config.DisplayName}' (action: {actionParameter}) is now listening on OSC address '{oscAddressForListening}' for external state changes.");
                }
                else if (this._oscAddressToActionParameterMap[oscAddressForListening] != actionParameter)
                {
                    // 如果地址已被映射到其他action，这是一个配置冲突
                    PluginLog.Error($"[LogicManager|ParseAndStoreControlDialConfig] OSC Address Conflict: Address '{oscAddressForListening}' for ControlDial '{config.DisplayName}' (action: {actionParameter}) is already mapped to another action '{this._oscAddressToActionParameterMap[oscAddressForListening]}'. External state changes for this ControlDial might not be received correctly.");
                }
                // 如果地址已被映射到相同的action (例如，插件重载或重复注册)，则无需操作，保持现有映射
            }
            else
            {
                PluginLog.Warning($"[LogicManager|ParseAndStoreControlDialConfig] ControlDial '{config.DisplayName}' (action: {actionParameter}) could not determine a valid OSC address for listening to external state changes. It will not be updated by incoming OSC messages.");
            }
        }
    }
}