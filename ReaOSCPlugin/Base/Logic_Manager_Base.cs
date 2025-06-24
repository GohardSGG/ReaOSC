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
        public void ToggleMode(String modeName) 
        { 
            if (this._currentModes.TryGetValue(modeName, out _) && this._modeOptions.TryGetValue(modeName, out var options)) 
            { 
                this._currentModes[modeName] = (this._currentModes[modeName] + 1) % options.Count; 
                this._modeChangedEvents[modeName]?.Invoke(); 

                // 【新增的全局刷新逻辑应在此处】
                foreach (var kvp in this._allConfigs)
                {
                    if (kvp.Value != null && kvp.Value.ModeName == modeName)
                    {
                        PluginLog.Info($"[LogicManager|ToggleMode] 模式组 '{modeName}' 已改变，触发依赖项 '{kvp.Key}' (Display: {kvp.Value.DisplayName}) 的状态刷新。");
                        this.CommandStateNeedsRefresh?.Invoke(this, kvp.Key);
                    }
                }
                // 【全局刷新逻辑结束】

                PluginLog.Info($"[LogicManager][Mode] 模式组 '{modeName}' 已切换到: {this.GetCurrentModeString(modeName)}"); 
                this.CommandStateNeedsRefresh?.Invoke(this, this.GetActionParameterForModeController(modeName)); 
            } 
        }
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

            // 【新增】在注册前，为直接在 Content.Buttons 中定义且拥有显式 GroupName 的按钮设置 UseOwnGroupAsRoot
            if (folderContent.Buttons != null)
            {
                foreach (var buttonCfgInContent in folderContent.Buttons)
                {
                    // 检查 GroupName 是否在 JSON 中明确提供，而不是依赖后续的默认值
                    // 这里的 buttonCfgInContent.GroupName 是从 JSON 直接反序列化得到的值
                    if (!String.IsNullOrEmpty(buttonCfgInContent.GroupName))
                    {
                        buttonCfgInContent.UseOwnGroupAsRoot = true;
                        PluginLog.Info($"[LogicManager|ProcessFolderContentConfigs] Button '{buttonCfgInContent.DisplayName}' in folder '{folderDisplayNameAsDefaultGroupName}' has explicit GroupName '{buttonCfgInContent.GroupName}'. Setting UseOwnGroupAsRoot=true.");
                    }
                    // 如果 buttonCfgInContent.GroupName 为空，则其 UseOwnGroupAsRoot 保持默认的 false
                    // RegisterConfigs 稍后可能会为其设置一个默认的 GroupName (folderDisplayNameAsDefaultGroupName)
                }
            }

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
            PluginLog.Info($"[LogicManager|OnOSCStateChanged] Received OSC: Address='{e.Address}', Value='{e.Value}'"); 
            if (this._oscAddressToActionParameterMap.TryGetValue(e.Address, out String mappedActionParameter))
            {
                var config = this.GetConfig(mappedActionParameter);
                if (config == null)
                {
                    PluginLog.Warning($"[LogicManager|OnOSCStateChanged] Matched OSC Address '{e.Address}' to actionParameter '{mappedActionParameter}', but config is null."); 
                    return;
                }
                PluginLog.Info($"[LogicManager|OnOSCStateChanged] Matched OSC Address '{e.Address}' to actionParameter '{mappedActionParameter}' (Config: '{config.DisplayName}', Type: '{config.ActionType}')."); 

                if (config.ActionType == "ToggleButton" || config.ActionType == "ToggleDial")
                {
                    var newState = e.Value > 0.5f;
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
                else if (config.ActionType == "ControlDial")
                {
                    if (this._controlDialConfigs.TryGetValue(mappedActionParameter, out var parsedDialConfig) && 
                        this._controlDialCurrentValues.ContainsKey(mappedActionParameter))
                    {
                        Single incomingFloatValue = e.Value; // OSC值通常是float，无需转换
                        Single validatedValue = incomingFloatValue;
                        Single currentStoredValue = this._controlDialCurrentValues[mappedActionParameter];

                        if (parsedDialConfig.Mode == ControlDialMode.Continuous)
                        {
                            validatedValue = Math.Clamp(incomingFloatValue, parsedDialConfig.MinValue, parsedDialConfig.MaxValue);
                        }
                        else // Discrete Mode
                        {
                            if (!parsedDialConfig.DiscreteValues.Contains(incomingFloatValue))
                            {
                                PluginLog.Warning($"[LogicManager|OnOSCStateChanged] ControlDial '{config.DisplayName}' (action: {mappedActionParameter}) received OSC value {incomingFloatValue} which is not in its discrete list of values. Ignoring update from OSC.");
                                return; // 忽略无效的离散值
                            }
                            // validatedValue 已经是 incomingFloatValue，且已确认在列表中
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
                                if (!String.IsNullOrEmpty(folderPart) && folderPart != "/") pathParts.Add(folderPart);
                                if (!String.IsNullOrEmpty(itemGroupPart) && itemGroupPart != "/") pathParts.Add(itemGroupPart);

                                var sanitizedActionSegmentForPath = SanitizeOscPathSegment(resolvedActionSegment);
                                if (!String.IsNullOrEmpty(sanitizedActionSegmentForPath) && sanitizedActionSegmentForPath != "/") pathParts.Add(sanitizedActionSegmentForPath);

                                if (pathParts.Any())
                                {
                                    oscAddressToSend = "/" + String.Join("/", pathParts.Where(s => !String.IsNullOrEmpty(s))); 
                                    oscAddressToSend = oscAddressToSend.Replace("//", "/").TrimEnd('/');
                                    if (oscAddressToSend == "/") oscAddressToSend = ""; 
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
                    PluginLog.Info($"[ControlDialDebug] Adjustment for: {globalActionParameter}, ticks: {ticks}");
                    if (this._controlDialConfigs.TryGetValue(globalActionParameter, out var controlConfig) &&
                        this._controlDialCurrentValues.TryGetValue(globalActionParameter, out var currentParamValue)) 
                    {
                        PluginLog.Info($"[ControlDialDebug] Config: IsIntBased={controlConfig.IsParameterIntegerBased}, MinVal={controlConfig.MinValue:F3}, MaxVal={controlConfig.MaxValue:F3}, Unit: {controlConfig.DisplayUnitString}, CurrentParam: {currentParamValue:F3}");
                        Single newParamValue = currentParamValue;

                        if (controlConfig.HasUnitConversion && controlConfig.DisplayUnitString.Equals("dB", StringComparison.OrdinalIgnoreCase))
                        {
                            Single currentUnitValue = this.ConvertParameterToUnit(currentParamValue, controlConfig);
                            PluginLog.Info($"[ControlDialDebug][dBMode] Current Param: {currentParamValue:F3} -> Current UnitVal: {currentUnitValue:F1}dB");
                            Single targetUnitValue;
                            if (Single.IsNegativeInfinity(currentUnitValue))
                            {
                                if (ticks > 0) 
                                {
                                    const Single firstStepParamOffset = 0.001f; 
                                    targetUnitValue = this.ConvertParameterToUnit(controlConfig.MinValue + firstStepParamOffset, controlConfig);
                                    if (Single.IsNegativeInfinity(targetUnitValue)) { targetUnitValue = controlConfig.UnitMin; if(Single.IsNegativeInfinity(targetUnitValue)) targetUnitValue = -60.0f; }
                                    if (ticks > 1) { targetUnitValue += (ticks - 1) * 0.1f; }
                                    PluginLog.Info($"[ControlDialDebug][dBMode] From -inf, ticks: {ticks}. Target UnitVal: {targetUnitValue:F1}");
                                }
                                else { targetUnitValue = Single.NegativeInfinity; PluginLog.Info($"[ControlDialDebug][dBMode] At -inf, ticks <= 0. Stays -inf."); }
                            }
                            else 
                            {
                                targetUnitValue = currentUnitValue + (ticks * 0.1f);
                                PluginLog.Info($"[ControlDialDebug][dBMode] Standard step. Current UnitVal: {currentUnitValue:F1}, Ticks: {ticks}, Target UnitVal: {targetUnitValue:F1}");
                            }
                            if (!Single.IsNegativeInfinity(targetUnitValue))
                            {
                                targetUnitValue = Math.Min(targetUnitValue, controlConfig.UnitMax);
                                if (!Single.IsNegativeInfinity(controlConfig.UnitMin)) { targetUnitValue = Math.Max(targetUnitValue, controlConfig.UnitMin); }
                            }
                            newParamValue = this.ConvertUnitToParameter(targetUnitValue, controlConfig);
                            PluginLog.Info($"[ControlDialDebug][dBMode] Target UnitVal: {targetUnitValue:F1}dB -> New Param PreClamp: {newParamValue:F3}");
                        }
                        else if (controlConfig.Mode == ControlDialMode.Continuous)
                        {
                            if (controlConfig.IsParameterIntegerBased) // 整数型参数，直接加减ticks
                            {
                                newParamValue = currentParamValue + ticks;
                                newParamValue = (Single)Math.Round(newParamValue); // 确保逻辑上是整数
                                PluginLog.Info($"[ControlDialDebug][ContinuousInt] New Param PreClamp: {newParamValue:F0}");
                            }
                            else // 浮点型参数 (0.0-1.0)，ticks需要缩放
                            {
                                Single scaledAdjustment = ticks * 0.01f; 
                                newParamValue = currentParamValue + scaledAdjustment;
                                PluginLog.Info($"[ControlDialDebug][ContinuousFloat] ScaledAdj: {scaledAdjustment:F3}, New Param PreClamp: {newParamValue:F3}");
                            }
                        }
                        else // Discrete Mode
                        {
                            if (controlConfig.DiscreteValues != null && controlConfig.DiscreteValues.Any())
                            {
                                const Single tolerance = 0.0001f;
                                Int32 discreteFloatCurrentIndex = -1; 
                                for(var i =0; i < controlConfig.DiscreteValues.Count; i++)
                                {
                                    if(Math.Abs(controlConfig.DiscreteValues[i] - currentParamValue) < tolerance)
                                    {
                                        discreteFloatCurrentIndex = i;
                                        break;
                                    }
                                }
                                if (discreteFloatCurrentIndex == -1) 
                                {
                                    PluginLog.Warning($"[ControlDialDebug][DiscreteFloat] Current param value {currentParamValue:F3} not in discrete list. Resetting index to 0.");
                                    discreteFloatCurrentIndex = 0; 
                                }
                                Int32 count = controlConfig.DiscreteValues.Count;
                                Int32 newIndex = (discreteFloatCurrentIndex + ticks % count + count) % count; 
                                newParamValue = controlConfig.DiscreteValues[newIndex];
                                PluginLog.Info($"[ControlDialDebug][DiscreteFloat] CurrentIdx: {discreteFloatCurrentIndex}, NewIdx: {newIndex}, New Param PreClamp: {newParamValue:F3}");
                            }
                            else
                            {
                                PluginLog.Warning($"[ControlDialDebug][DiscreteFloat] DiscreteValues list empty for {globalActionParameter}. No change.");
                                newParamValue = currentParamValue;
                            }
                        }
                        
                        // 核心：严格范围钳位
                        newParamValue = Math.Clamp(newParamValue, controlConfig.MinValue, controlConfig.MaxValue);
                        PluginLog.Info($"[ControlDialDebug] Final New Param (PostClamp): {newParamValue:F3}, Old Param: {currentParamValue:F3}");

                        if (Math.Abs(newParamValue - currentParamValue) > 0.00001f) 
                        {
                            this._controlDialCurrentValues[globalActionParameter] = newParamValue;
                            String oscAddress = this.GetResolvedOscAddress(config, config.OscAddress ?? config.Title ?? config.DisplayName);
                            if (!String.IsNullOrEmpty(oscAddress) && oscAddress != "/")
                            {
                                Single valueToSend = newParamValue;
                                if(controlConfig.IsParameterIntegerBased && !controlConfig.HasUnitConversion) // 对于纯整数型（非dB），发送四舍五入的整数对应的float
                                {
                                    valueToSend = (Single)Math.Round(newParamValue);
                                }
                                ReaOSCPlugin.SendOSCMessage(oscAddress, valueToSend); 
                                PluginLog.Info($"[LogicManager] ControlDial '{config.DisplayName}' OSC sent to '{oscAddress}' -> {valueToSend:F3} (Original NewParam: {newParamValue:F3})");
                            }
                            this.CommandStateNeedsRefresh?.Invoke(this, globalActionParameter);
                        }
                        else { PluginLog.Info($"[ControlDialDebug] Param value did not change significantly. No OSC sent."); }
                    }
                    else { PluginLog.Warning($"[ControlDialDebug] Config or current value not found for {globalActionParameter}."); }
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
                    this.ProcessDialAdjustment(actionParameter, ticks); // 调用新版，只传 actionParameter 和 ticks
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
                    if (this._controlDialConfigs.TryGetValue(globalActionParameter, out var controlConfigToReset) &&
                        this._controlDialCurrentValues.ContainsKey(globalActionParameter)) 
                    {
                        Single oldValue = this._controlDialCurrentValues[globalActionParameter];
                        Single newParamValueFromDefault = controlConfigToReset.DefaultValue; // DefaultValue已经是Single (0.0-1.0f)

                        if (controlConfigToReset.IsParameterIntegerBased && !controlConfigToReset.HasUnitConversion) 
                        {
                            newParamValueFromDefault = (Single)Math.Round(newParamValueFromDefault);
                        }
                        
                        this._controlDialCurrentValues[globalActionParameter] = newParamValueFromDefault;
                        PluginLog.Info($"[LogicManager|ProcessDialPress] ControlDial '{config.DisplayName}' reset to ParamDefault: {newParamValueFromDefault:F3} (Original Default: {controlConfigToReset.DefaultValue:F3})");
                        
                        String oscAddressReset = this.GetResolvedOscAddress(config, config.OscAddress ?? config.Title ?? config.DisplayName);
                        if (!String.IsNullOrEmpty(oscAddressReset) && oscAddressReset != "/")
                        {   
                            ReaOSCPlugin.SendOSCMessage(oscAddressReset, newParamValueFromDefault); 
                            PluginLog.Info($"[LogicManager] ControlDial '{config.DisplayName}' Reset OSC sent to '{oscAddressReset}' -> {newParamValueFromDefault:F3}");
                        }

                        if (Math.Abs(oldValue - newParamValueFromDefault) > 0.00001f) 
                        { uiShouldRefresh = true; }
                        else { PluginLog.Info($"[LogicManager] ControlDial '{config.DisplayName}' was already at its default value {newParamValueFromDefault:F3}. Press did not change value significantly.");}
                    }
                    break;
            }
            if (uiShouldRefresh) { this.CommandStateNeedsRefresh?.Invoke(this, globalActionParameter); }
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
                if (p_clamped < 0.005f) return -132.0f; if (p_clamped < 0.01f) return -114.0f; if (p_clamped < 0.05f) return -72.0f;
                if (p_clamped < 0.1f) return -54.0f; if (p_clamped < 0.25f) return -30.0f; if (p_clamped < 0.5f) return -11.0f;
                if (p_clamped < P_AT_0DB) return 0.0f; if (p_clamped < 0.8f) return 3.76f; if (p_clamped < 0.85f) return 5.90f;
                if (p_clamped < 0.9f) return 7.99f; if (p_clamped < 0.95f) return 10.00f; if (p_clamped < 0.98f) return 11.20f;
                return dialConfig.UnitMax;
            }
            else { /* non-dB (linear) remains same */ Single pm = dialConfig.MinValue; Single pM = dialConfig.MaxValue; Single um = dialConfig.UnitMin; Single uM = dialConfig.UnitMax; if (Math.Abs(pM - pm) < 0.00001f) return um; Single prop = (p_clamped - pm) / (pM - pm); return um + prop * (uM - um); }
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
                if (unitValue < -132.0f) return paramMin; // Should have been caught by specific < -132 check already
                if (unitValue < -114.0f) return 0.01f;
                // ... (add more fallback clamps based on nearest known point) ...
                if (unitValue > 11.20f) return paramMax; // Should have been caught by >= unitMax check
                return P_AT_0DB; // Default fallback
            }
            else { /* non-dB (linear) remains same */ Single pm = dialConfig.MinValue; Single pM = dialConfig.MaxValue; Single um = dialConfig.UnitMin; Single uM = dialConfig.UnitMax; if (Math.Abs(uM - um) < 0.00001f) return pm; Single prop = (unitValue - um) / (uM - um); Single res = pm + prop * (pM - pm); return Math.Clamp(res, pm, pM); }
        }

        // 获取 ControlDial 在设备上应显示的文本
        public String GetControlDialDisplayText(String actionParameter)
        {
            var config = this.GetConfig(actionParameter);
            if (config == null || config.ActionType != "ControlDial")
            {
                PluginLog.Warning($"[LogicManager|GetControlDialDisplayText] Config not found or not a ControlDial for action: {actionParameter}");
                return actionParameter; 
            }

            if (!this._controlDialConfigs.TryGetValue(actionParameter, out var parsedDialConfig) ||
                !this._controlDialCurrentValues.TryGetValue(actionParameter, out var currentParameterValue)) 
            {
                PluginLog.Warning($"[LogicManager|GetControlDialDisplayText] Parsed config or current value not found for ControlDial: {actionParameter}");
                return config.Title ?? config.DisplayName ?? actionParameter;
            }

            if (parsedDialConfig.HasUnitConversion)
            {
                if (parsedDialConfig.DisplayUnitString.Equals("L&R", StringComparison.OrdinalIgnoreCase))
                {
                    Int32 intValue = (Int32)Math.Round(currentParameterValue);
                    if (intValue == 0)
                    {
                        return "C";
                    }
                    else if (intValue < 0)
                    {
                        return $"L{-intValue}";
                    }
                    else // intValue > 0
                    {
                        return $"R{intValue}";
                    }
                }
                else // Existing logic for other unit types like dB
                { 
                    Single unitValue = this.ConvertParameterToUnit(currentParameterValue, parsedDialConfig); 
                    String valueText;
                    if (Single.IsNegativeInfinity(unitValue))
                    {
                        valueText = "-inf";
                    }
                    else
                    {
                        // 对于带单位的转换，通常保留一位小数比较常见，特别是dB
                        valueText = unitValue.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);
                    }
                    return $"{valueText}{parsedDialConfig.UnitLabel}"; 
                }
            }
            else // 没有单位转换
            {
                if (parsedDialConfig.IsParameterIntegerBased)
                {
                    // 参数是整数型的，四舍五入并显示为整数
                    return ((Int32)Math.Round(currentParameterValue)).ToString();
                }
                else
                {
                    // 参数是浮点型的 (通常0.0-1.0)，格式化以显示几位小数
                    return currentParameterValue.ToString("F3", System.Globalization.CultureInfo.InvariantCulture); 
                }
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
            if (config.Parameter == null || !config.Parameter.Any())
            {
                PluginLog.Warning($"[LogicManager] ControlDial '{config.DisplayName}' (action: {actionParameter}) has no 'Parameter' array defined. Cannot initialize.");
                return;
            }

            var parsedDialConfig = new ControlDialParsedConfig();
            Single initialParameterDefaultValue = 0.0f;
            // 默认 IsParameterIntegerBased 为false，除非所有相关参数都被成功解析为整数且无浮点特征
            var allParamsLookLikeIntegers = true; 

            if (config.Parameter.Count > 0 && config.Parameter[0]?.ToLower() == "true") // 连续模式
            {
                parsedDialConfig.Mode = ControlDialMode.Continuous;
                if (config.Parameter.Count >= 3)
                {
                    String minStr = config.Parameter[1];
                    String maxStr = config.Parameter[2];
                    var minIsInt = Int32.TryParse(minStr, out _) && !minStr.Contains(".") && !minStr.ToLowerInvariant().Contains("e");
                    var maxIsInt = Int32.TryParse(maxStr, out _) && !maxStr.Contains(".") && !maxStr.ToLowerInvariant().Contains("e");
                    allParamsLookLikeIntegers = minIsInt && maxIsInt;

                    if (Single.TryParse(minStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var minFloat) &&
                        Single.TryParse(maxStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var maxFloat))
                    {
                        parsedDialConfig.MinValue = Math.Min(minFloat, maxFloat);
                        parsedDialConfig.MaxValue = Math.Max(minFloat, maxFloat);
                        initialParameterDefaultValue = parsedDialConfig.MinValue; 
                    }
                    else // 解析失败，使用默认浮点范围
                    {
                        PluginLog.Warning($"[LogicManager] ControlDial '{config.DisplayName}' (action: {actionParameter}) continuous mode min/max parameters ('{minStr}', '{maxStr}') are invalid. Using 0.0f-1.0f default.");
                        parsedDialConfig.MinValue = 0.0f;
                        parsedDialConfig.MaxValue = 1.0f;
                        initialParameterDefaultValue = 0.0f;
                        allParamsLookLikeIntegers = false; // 解析失败，不能认为是整数基准
                    }
                }
                else // 参数数量不足，使用默认浮点范围
                {
                    PluginLog.Warning($"[LogicManager] ControlDial '{config.DisplayName}' (action: {actionParameter}) continuous mode parameters missing. Expected [\"true\", \"min_str\", \"max_str\"]. Using 0.0f-1.0f default.");
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
                // 对于离散模式，如果所有值都是整数形式，则整体视为整数基准
                foreach (var paramStr in config.Parameter) 
                {
                    if (Single.TryParse(paramStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var valFloat))
                    {
                        parsedDialConfig.DiscreteValues.Add(valFloat);
                        if (firstDiscreteValue) 
                        { 
                            initialParameterDefaultValue = valFloat; 
                            firstDiscreteValue = false; 
                        }
                        // 更新 allParamsLookLikeIntegers 状态
                        if (!Int32.TryParse(paramStr, out _) || paramStr.Contains(".") || paramStr.ToLowerInvariant().Contains("e"))
                        {
                            allParamsLookLikeIntegers = false;
                        }
                    }
                    else
                    {
                        PluginLog.Warning($"[LogicManager] ControlDial '{config.DisplayName}' (action: {actionParameter}) discrete mode parameter '{paramStr}' is not a valid float. Skipping.");
                        allParamsLookLikeIntegers = false; // 一旦有解析失败，则不视为纯整数基准
                    }
                }
                if (!parsedDialConfig.DiscreteValues.Any())
                {
                    PluginLog.Warning($"[LogicManager] ControlDial '{config.DisplayName}' (action: {actionParameter}) discrete mode has no valid float parameters. Adding 0.0f as default.");
                    parsedDialConfig.DiscreteValues.Add(0.0f);
                    initialParameterDefaultValue = 0.0f;
                    allParamsLookLikeIntegers = false;
                }
            }
            parsedDialConfig.IsParameterIntegerBased = allParamsLookLikeIntegers;

            // 处理 ParameterDefault
            parsedDialConfig.DefaultValue = initialParameterDefaultValue; 
            if (!String.IsNullOrEmpty(config.ParameterDefault))
            {
                String defaultParamStr = config.ParameterDefault;
                var defaultIsInt = Int32.TryParse(defaultParamStr, out _) && !defaultParamStr.Contains(".") && !defaultParamStr.ToLowerInvariant().Contains("e");

                if (Single.TryParse(defaultParamStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var explicitParamDefaultFloat))
                {
                    // 如果 Parameter 数组本身是整数基准的，那么 ParameterDefault 也应被视为整数基准，即使它可以被解析为浮点数
                    // 但如果 ParameterDefault 本身包含小数点，则覆盖 IsParameterIntegerBased 的判断（不常见，但处理边缘情况）
                    if (parsedDialConfig.IsParameterIntegerBased && !defaultIsInt && (defaultParamStr.Contains(".") || defaultParamStr.ToLowerInvariant().Contains("e")))
                    {
                        PluginLog.Warning($"[LogicManager] ControlDial '{config.DisplayName}': Parameters appear integer-based, but ParameterDefault ('{defaultParamStr}') looks like a float. The dial will be treated as float-based due to ParameterDefault.");
                        parsedDialConfig.IsParameterIntegerBased = false;
                    }

                    if (parsedDialConfig.Mode == ControlDialMode.Continuous)
                    {
                        parsedDialConfig.DefaultValue = Math.Clamp(explicitParamDefaultFloat, parsedDialConfig.MinValue, parsedDialConfig.MaxValue);
                    }
                    else // Discrete
                    {
                        const Single tolerance = 0.0001f;
                        var foundInDiscrete = false;
                        foreach(var discreteVal in parsedDialConfig.DiscreteValues)
                        {
                            if (Math.Abs(explicitParamDefaultFloat - discreteVal) < tolerance)
                            {
                                parsedDialConfig.DefaultValue = discreteVal; 
                                foundInDiscrete = true;
                                break;
                            }
                        }
                        if(!foundInDiscrete)
                        {
                             PluginLog.Warning($"[LogicManager] ControlDial '{config.DisplayName}' ParameterDefault '{defaultParamStr}' not found (with tolerance) in discrete values. Using first discrete value or initial calc as default.");
                             // DefaultValue 保持为 initialParameterDefaultValue
                        }
                    }
                }
                else
                {
                    PluginLog.Warning($"[LogicManager] ControlDial '{config.DisplayName}': Could not parse ParameterDefault '{defaultParamStr}'. Using initial default.");
                }
            }
            
            // Unit 和 UnitDefault 的处理 (此部分逻辑不变，但会基于新的 DefaultValue 类型进行操作)
            if (config.Unit != null && config.Unit.Count >= 3)
            {
                parsedDialConfig.DisplayUnitString = config.Unit[0]; // 首先设置 DisplayUnitString

                if (parsedDialConfig.DisplayUnitString.Equals("L&R", StringComparison.OrdinalIgnoreCase))
                {
                    parsedDialConfig.HasUnitConversion = true;
                    parsedDialConfig.UnitLabel = ""; // L&R 的值是自解释的，不需要额外标签

                    // 解析 UnitMin from config.Unit[1] (例如 "L100")
                    if (config.Unit[1] != null && config.Unit[1].ToUpperInvariant().StartsWith("L") &&
                        Single.TryParse(config.Unit[1].Substring(1), NumberStyles.Any, CultureInfo.InvariantCulture, out var lVal))
                    {
                        parsedDialConfig.UnitMin = -lVal;
                    }
                    else
                    {
                        PluginLog.Warning($"[LogicManager] ControlDial '{config.DisplayName}' L&R UnitMin '{config.Unit[1]}' 无效。默认设为 -100f。");
                        parsedDialConfig.UnitMin = -100f; // 回退值
                    }

                    // 解析 UnitMax from config.Unit[2] (例如 "R100")
                    if (config.Unit[2] != null && config.Unit[2].ToUpperInvariant().StartsWith("R") &&
                        Single.TryParse(config.Unit[2].Substring(1), NumberStyles.Any, CultureInfo.InvariantCulture, out var rVal))
                    {
                        parsedDialConfig.UnitMax = rVal;
                    }
                    else
                    {
                        PluginLog.Warning($"[LogicManager] ControlDial '{config.DisplayName}' L&R UnitMax '{config.Unit[2]}' 无效。默认设为 100f。");
                        parsedDialConfig.UnitMax = 100f; // 回退值
                    }
                    PluginLog.Info($"[LogicManager] ControlDial '{config.DisplayName}' (action: {actionParameter}) 配置为 L&R 单位转换。Unit: {parsedDialConfig.DisplayUnitString}, Min: {parsedDialConfig.UnitMin}, Max: {parsedDialConfig.UnitMax}");

                    // 解析 UnitDefault from config.UnitDefault (例如 "C")
                    if (!String.IsNullOrEmpty(config.UnitDefault))
                    {
                        if (config.UnitDefault.ToUpperInvariant() == "C")
                        {
                            parsedDialConfig.ParsedUnitDefault = 0f;
                        }
                        // 也允许数字 "0" 代表 L&R 的中间值
                        else if (Single.TryParse(config.UnitDefault, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsedNumericDef) && parsedNumericDef == 0f)
                        {
                             parsedDialConfig.ParsedUnitDefault = 0f;
                        }
                        else
                        {
                            PluginLog.Warning($"[LogicManager] ControlDial '{config.DisplayName}' L&R UnitDefault '{config.UnitDefault}' 无效。默认设为 0f (Center)。");
                            parsedDialConfig.ParsedUnitDefault = 0f; // 回退到中间值
                        }
                        PluginLog.Info($"[LogicManager] ControlDial '{config.DisplayName}' (action: {actionParameter}) L&R ParsedUnitDefault (单位刻度): {parsedDialConfig.ParsedUnitDefault}.");

                        // 将单位默认值应用到参数刻度的 DefaultValue
                        // 这一步依赖 ConvertUnitToParameter 方法后续的更新以支持 L&R
                        Single newDefaultValue = this.ConvertUnitToParameter(parsedDialConfig.ParsedUnitDefault, parsedDialConfig);
                        PluginLog.Info($"[LogicManager] ControlDial '{config.DisplayName}' (action: {actionParameter}): L&R UnitDefault ('{config.UnitDefault}' -> 单位刻度 {parsedDialConfig.ParsedUnitDefault}) 被转换为参数刻度 DefaultValue。旧参数 DefaultValue: {parsedDialConfig.DefaultValue:F3}, 新参数 DefaultValue: {newDefaultValue:F3}.");
                        parsedDialConfig.DefaultValue = newDefaultValue;
                    }
                    // else: 如果没有提供 UnitDefault，则 DefaultValue 保持基于 Parameter 数组的初始计算值。
                    // 例如 Pan 的 Parameter 为 ["true", "-100", "100"], 初始 DefaultValue 为 -100f。
                    // 如果 UnitDefault 为 "C", ParsedUnitDefault 为 0f, ConvertUnitToParameter(0f, L&R_config) 应返回 0f, 因此 DefaultValue 会被更新为 0f。
                }
                else if (TryParseSingleExtended(config.Unit[1], out var unitMin) &&
                    TryParseSingleExtended(config.Unit[2], out var unitMax))
                {
                    parsedDialConfig.UnitLabel = parsedDialConfig.DisplayUnitString; // 其他类型单位的默认标签
                    parsedDialConfig.HasUnitConversion = true;
                    parsedDialConfig.UnitMin = unitMin;
                    parsedDialConfig.UnitMax = unitMax;
                    PluginLog.Info($"[LogicManager] ControlDial '{config.DisplayName}' (action: {actionParameter}) configured for Unit Conversion. Unit: {parsedDialConfig.DisplayUnitString}, Min: {parsedDialConfig.UnitMin}, Max: {parsedDialConfig.UnitMax}");

                    if (!String.IsNullOrEmpty(config.UnitDefault))
                    {
                        if (TryParseSingleExtended(config.UnitDefault, out var parsedUnitDef))
                        {
                            parsedDialConfig.ParsedUnitDefault = parsedUnitDef;
                            PluginLog.Info($"[LogicManager] ControlDial '{config.DisplayName}' (action: {actionParameter}) ParsedUnitDefault (unit scale): {parsedDialConfig.ParsedUnitDefault}.");
                            if(parsedDialConfig.HasUnitConversion) 
                            {
                                Single newDefaultValue = this.ConvertUnitToParameter(parsedDialConfig.ParsedUnitDefault, parsedDialConfig);
                                PluginLog.Info($"[LogicManager] ControlDial '{config.DisplayName}' (action: {actionParameter}): UnitDefault ('{config.UnitDefault}' -> unit scale {parsedDialConfig.ParsedUnitDefault}) being converted to parameter scale DefaultValue. Old param DefaultValue: {parsedDialConfig.DefaultValue:F3}, New (target) param DefaultValue: {newDefaultValue:F3}.");
                                parsedDialConfig.DefaultValue = newDefaultValue; // DefaultValue (0.0-1.0f) 被单位默认值覆盖
                            }
                            else
                            {
                                PluginLog.Warning($"[LogicManager] ControlDial '{config.DisplayName}' (action: {actionParameter}): UnitDefault was parsed, but HasUnitConversion is false. Cannot apply UnitDefault to parameter scale DefaultValue. Current param scale DefaultValue is {parsedDialConfig.DefaultValue}");
                            }
                        }
                        else
                        {
                            PluginLog.Warning($"[LogicManager] ControlDial '{config.DisplayName}' (action: {actionParameter}) has invalid UnitDefault string: '{config.UnitDefault}'. UnitDefault will not be applied.");
                        }
                    }
                }
                else
                {
                    PluginLog.Warning($"[LogicManager] ControlDial '{config.DisplayName}' (action: {actionParameter}) has invalid Unit Min/Max strings: '{config.Unit[1]}', '{config.Unit[2]}' for unit type '{parsedDialConfig.DisplayUnitString}'. Unit conversion will NOT be enabled.");
                    parsedDialConfig.HasUnitConversion = false; 
                }
            }
            else if (config.Unit != null && config.Unit.Any()) 
            {
                 String expectedFormatLiteral = "[\"Name\", \"Min\", \"Max\"]";
                 PluginLog.Warning($"[LogicManager] ControlDial '{config.DisplayName}' (action: {actionParameter}) 'Unit' array is present but does not have 3 elements. Expected {expectedFormatLiteral}. Unit conversion will NOT be enabled.");
            }

            this._controlDialConfigs[actionParameter] = parsedDialConfig;
            this._controlDialCurrentValues[actionParameter] = parsedDialConfig.DefaultValue; 
            PluginLog.Info($"[LogicManager] ControlDial '{config.DisplayName}' (action: {actionParameter}) FINALIZED registration. Mode: {parsedDialConfig.Mode}, ParamDefault (0-1 scale): {parsedDialConfig.DefaultValue:F3}, HasUnitConversion: {parsedDialConfig.HasUnitConversion}. Current Param Value (0-1 scale): {this._controlDialCurrentValues[actionParameter]:F3}");

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

        /// <summary>
        /// 解析包含 "{mode}" 占位符的文本模板。
        /// </summary>
        /// <param name="itemConfig">按钮或旋钮的配置，包含 ModeName。</param>
        /// <param name="textTemplate">可能包含 "{mode}" 的文本模板。</param>
        /// <returns>已解析的文本，或在无法解析时返回经过处理的模板。</returns>
        public String ResolveTextWithMode(ButtonConfig itemConfig, String textTemplate)
        {
            // 如果配置为空、模板为空或模板中不包含"{mode}"占位符，则直接返回原始模板
            if (itemConfig == null || String.IsNullOrEmpty(textTemplate) || !textTemplate.Contains("{mode}"))
            {
                return textTemplate;
            }

            // 如果配置中没有定义ModeName，则无法解析"{mode}"
            if (String.IsNullOrEmpty(itemConfig.ModeName))
            {
                PluginLog.Warning($"[LogicManager|ResolveTextWithMode] 项目 '{itemConfig.DisplayName}' 的模板 '{textTemplate}' 包含 '{{mode}}' 但其配置中未定义 ModeName。将 '{{mode}}' 视为空字符串。");
                return textTemplate.Replace("{mode}", ""); // 将 {mode} 替换为空字符串
            }

            // 获取当前模式的原始字符串
            var currentModeActual = this.GetCurrentModeString(itemConfig.ModeName);

            // 对模式字符串进行清理，使其适用于OSC路径和可能的显示
            // 注意: SanitizeOscPathSegment 主要用于OSC路径，对于纯显示文本可能需要调整
            var sanitizedMode = SanitizeOscPathSegment(currentModeActual); 

            // 如果清理后的模式字符串为空 (例如，原始模式名为空或清理后变为空)
            // 并且模板中确实需要 "{mode}"
            if (String.IsNullOrEmpty(sanitizedMode) && textTemplate.Contains("{mode}"))
            {
                 PluginLog.Warning($"[LogicManager|ResolveTextWithMode] 项目 '{itemConfig.DisplayName}' (ModeName: '{itemConfig.ModeName}') 的当前模式名 '{currentModeActual}' 解析/清理后为空。模板是 '{textTemplate}'。将 '{{mode}}' 视为空字符串。");
                 return textTemplate.Replace("{mode}", ""); // 将 {mode} 替换为空字符串
            }
            
            // 执行替换
            return textTemplate.Replace("{mode}", sanitizedMode);
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

    }
}