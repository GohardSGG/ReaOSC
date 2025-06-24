// 文件名: Base/General_Folder_Base.cs
// 描述: 新的统一动态文件夹基类，整合了旧 Dynamic_Folder_Base 和 FX_Folder_Base 的功能。
namespace Loupedeck.ReaOSCPlugin.Base
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks; // 预添加，基于旧代码的使用

    using Newtonsoft.Json.Linq; // 用于处理 JObject _dataSourceJson

    using Loupedeck.ReaOSCPlugin.Helpers; // 为 PluginImage 等辅助类

    public abstract class General_Folder_Base : PluginDynamicFolder, IDisposable
    {
        // --- 来自 FX_Folder_Base (并保留) ---
        private ButtonConfig _entryConfig; // 文件夹入口配置
        private JObject _dataSourceJson;   // 动态列表的数据源 (例如Effect_List.json的内容)

        // 动态列表项管理 (源自 FX_Folder_Base)
        private readonly Dictionary<String, ButtonConfig> _allListItems = new Dictionary<String, ButtonConfig>();
        private List<String> _currentDisplayedItemActionNames = new List<String>();

        // 文件夹旋钮配置 (通用)
        private readonly List<ButtonConfig> _folderDialConfigs = new List<ButtonConfig>();

        // 过滤器管理 (源自 FX_Folder_Base)
        private readonly Dictionary<String, List<String>> _filterOptions = new Dictionary<String, List<String>>();
        private readonly Dictionary<String, String> _currentFilterValues = new Dictionary<String, String>();
        private String _busFilterDialDisplayName = null;
        private List<String> _favoriteItemDisplayNames = new List<String>();

        // 分页管理 (源自 FX_Folder_Base)
        private Int32 _currentPage = 0;
        private Int32 _totalPages = 1;

        // UI反馈辅助 (通用，FX_Folder_Base 中已有 _lastPressTimes)
        // private readonly Dictionary<String, DateTime> _lastPressTimes = new Dictionary<String, DateTime>(); // 将被新的Timer机制取代


        // --- 来自旧 Dynamic_Folder_Base (并引入/整合) ---
        private readonly Boolean _isButtonListDynamic; // 标记按钮列表是静态还是动态
        private List<ButtonConfig> _rawStaticButtonConfigs = new List<ButtonConfig>(); // 【新增】存储原始静态按钮配置

        // 静态按钮管理 (源自旧 Dynamic_Folder_Base)
        private readonly List<String> _localButtonIds = new List<String>();
        private readonly Dictionary<String, String> _localIdToGlobalActionParameter_Buttons = new Dictionary<String, String>();
        private readonly Dictionary<String, ButtonConfig> _localIdToConfig_Buttons = new Dictionary<String, ButtonConfig>();

        // ParameterDial 状态管理 (源自旧 Dynamic_Folder_Base)
        private readonly Dictionary<String, Int32> _parameterDialCurrentIndexes = new Dictionary<String, Int32>();

        // 旧版静态按钮瞬时反馈 (源自旧 Dynamic_Folder_Base - 将被新的Timer机制取代)
        // private readonly Dictionary<String, DateTime> _lastTriggerPressTimes = new Dictionary<String, DateTime>();
        // private readonly Dictionary<String, DateTime> _lastCombineButtonPressTimes = new Dictionary<String, DateTime>();

        // 【新增】统一的瞬时高亮管理机制
        private readonly Dictionary<String, Boolean> _folderItemTemporaryActiveStates = new Dictionary<String, Boolean>();
        private readonly Dictionary<String, System.Timers.Timer> _folderItemResetTimers = new Dictionary<String, System.Timers.Timer>();
        private const int HighlightDurationMilliseconds = 200; // 高亮持续时间

        public General_Folder_Base()
        {
            // 确保 Logic_Manager_Base 已初始化
            Logic_Manager_Base.Instance.Initialize();

            var folderClassName = this.GetType().Name;
            var folderBaseName = folderClassName.Replace("_Dynamic", "").Replace("_", " ");

            this._entryConfig = Logic_Manager_Base.Instance.GetConfigByDisplayName("Dynamic", folderBaseName);
            if (this._entryConfig == null)
            {
                PluginLog.Error($"[{folderBaseName}] Constructor: 未能在 Logic_Manager 中找到文件夹入口 '{folderBaseName}' 的配置项。");
                this.DisplayName = folderBaseName;
                this.GroupName = "Dynamic";
            }
            else
            {
                this.DisplayName = this._entryConfig.DisplayName;
                this.GroupName = this._entryConfig.GroupName ?? "Dynamic";
            }

            var folderContentConfig = Logic_Manager_Base.Instance.GetFolderContent(this.DisplayName);

            if (folderContentConfig != null)
            {
                // 加载旋钮配置
                if (folderContentConfig.Dials != null)
                {
                    foreach (var dialConfig in folderContentConfig.Dials)
                    {
                        this._folderDialConfigs.Add(dialConfig);
                        if (dialConfig.ActionType == "ParameterDial")
                        {
                            String localId = this.GetLocalDialId(dialConfig);
                            if (!String.IsNullOrEmpty(localId) && dialConfig.Parameter != null && dialConfig.Parameter.Any())
                            {
                                this._parameterDialCurrentIndexes[localId] = 0; // 默认选中第一个参数
                            }
                        }
                    }
                }

                // 判断列表类型并初始化，使用 FolderContentConfig 中的预处理信息
                this._isButtonListDynamic = folderContentConfig.IsButtonListDynamic;

                if (this._isButtonListDynamic)
                {
                    this._dataSourceJson = Logic_Manager_Base.Instance.GetFxData(this.DisplayName);
                    if (this._dataSourceJson == null)
                    {
                        PluginLog.Error($"[{this.DisplayName}] Constructor: 动态列表模式但未能从 Logic_Manager 获取数据源。文件夹内容可能为空。");
                    }
                    this.InitializeDynamicListData(); // 调用新方法
                }
                else // 静态按钮列表
                {
                    // 【修改】存储原始静态按钮配置，并传递给 PopulateStaticButtonMappings
                    this._rawStaticButtonConfigs = folderContentConfig.Buttons ?? new List<ButtonConfig>();
                    this.PopulateStaticButtonMappings(this._rawStaticButtonConfigs); 
                    if (this._rawStaticButtonConfigs == null || !this._rawStaticButtonConfigs.Any())
                    {
                        PluginLog.Info($"[{this.DisplayName}] Constructor: 配置为静态按钮列表，但按钮列表为空。");
                    }
                }
            }
            else
            {
                PluginLog.Warning($"[{this.DisplayName}] Constructor: 未能从 Logic_Manager 获取到文件夹内容配置 (FolderContentConfig)。");
                this._isButtonListDynamic = false; // 无内容配置，则不可能是动态列表，也没有静态按钮可加载
            }

            // 订阅事件 (OnCommandStateNeedsRefresh 方法将在后续步骤中添加)
            Logic_Manager_Base.Instance.CommandStateNeedsRefresh += this.OnCommandStateNeedsRefresh;
        }

        // 辅助方法，用于生成旋钮的内部localId (与旧Dynamic_Folder_Base和FX_Folder_Base逻辑类似)
        private String GetLocalDialId(ButtonConfig dialConfig)
        {
            if (dialConfig == null)
            {
                return null;
            }
            var groupName = dialConfig.GroupName ?? this.DisplayName; 
            var displayName = dialConfig.DisplayName ?? ""; 

            if (String.IsNullOrEmpty(displayName) && dialConfig.ActionType != "Placeholder")
            {
                 PluginLog.Warning($"[{this.DisplayName}] GetLocalDialId: 旋钮配置 (类型: {dialConfig.ActionType}) 的 DisplayName 为空。GroupName: {groupName}");
                 return null; 
            }
            
            if (dialConfig.ActionType == "Placeholder" && String.IsNullOrEmpty(displayName))
            {
                 PluginLog.Verbose($"[{this.DisplayName}] GetLocalDialId: Placeholder类型旋钮无DisplayName，不生成特定localId。");
                 return null; 
            }
            return $"{groupName}_{displayName}".Replace(" ", "_");
        }

        // 处理静态按钮列表的映射 (逻辑来自旧 Dynamic_Folder_Base)
        private void PopulateStaticButtonMappings(List<ButtonConfig> staticButtonConfigs)
        {
            this._localButtonIds.Clear();
            this._localIdToGlobalActionParameter_Buttons.Clear();
            this._localIdToConfig_Buttons.Clear();

            if (this._isButtonListDynamic || staticButtonConfigs == null || !staticButtonConfigs.Any())
            {
                return; 
            }

            foreach (var buttonConfigFromJson in staticButtonConfigs) 
            {
                if (buttonConfigFromJson.ActionType == "Placeholder") 
                {
                    continue; 
                }

                var kvp = Logic_Manager_Base.Instance.GetAllConfigs().FirstOrDefault(
                    x => x.Value.DisplayName == buttonConfigFromJson.DisplayName && 
                         x.Value.GroupName == (buttonConfigFromJson.GroupName ?? this.DisplayName) 
                );
                var globalActionParameter = kvp.Key;   
                var loadedConfig = kvp.Value;          

                if (String.IsNullOrEmpty(globalActionParameter) || loadedConfig == null)
                {
                    PluginLog.Warning($"[{this.DisplayName}] PopulateStaticButtonMappings: 未能从Logic_Manager找到按钮的全局配置。按钮DisplayName='{buttonConfigFromJson.DisplayName}', 按钮原始GroupName='{buttonConfigFromJson.GroupName ?? "(未定义，使用文件夹名)"}'。");
                    continue;
                }

                var localId = $"{loadedConfig.GroupName}_{loadedConfig.DisplayName}".Replace(" ", ""); 

                if (this._localIdToConfig_Buttons.ContainsKey(localId))
                {
                    PluginLog.Warning($"[{this.DisplayName}] PopulateStaticButtonMappings: 静态按钮localId '{localId}' 重复。");
                    continue;
                }
                this._localIdToGlobalActionParameter_Buttons[localId] = globalActionParameter;
                this._localIdToConfig_Buttons[localId] = loadedConfig; 
                this._localButtonIds.Add(localId);

                // 【新增】为静态 TriggerButton 和 CombineButton 初始化 Timer
                if (loadedConfig.ActionType == "TriggerButton" || loadedConfig.ActionType == "CombineButton")
                {
                    if (!this._folderItemResetTimers.ContainsKey(localId))
                    {
                        var timer = new System.Timers.Timer(HighlightDurationMilliseconds);
                        timer.AutoReset = false;
                        // 使用 lambda 表达式捕获 localId
                        timer.Elapsed += (sender, e) => HandleFolderItemResetTimerElapsed(sender, e, localId);
                        this._folderItemResetTimers[localId] = timer;
                        this._folderItemTemporaryActiveStates[localId] = false; // 初始化状态
                    }
                }
            }
            PluginLog.Info($"[{this.DisplayName}] PopulateStaticButtonMappings: 处理了 {this._localButtonIds.Count} 个静态按钮。");
        }

        // 【新增】Timer Elapsed 处理方法
        private void HandleFolderItemResetTimerElapsed(Object sender, System.Timers.ElapsedEventArgs e, String itemActionParameter)
        {
            if (this._folderItemTemporaryActiveStates.TryGetValue(itemActionParameter, out bool isActive) && isActive)
            {
                this._folderItemTemporaryActiveStates[itemActionParameter] = false;
                this.CommandImageChanged(itemActionParameter); 
                PluginLog.Verbose($"[{this.DisplayName}] Timer elapsed for '{itemActionParameter}', highglight removed.");
            }
        }

        // 辅助方法，用于从 ButtonConfig (通常是动态列表项) 创建唯一的动作参数 (与 FX_Folder_Base 逻辑一致)
        private String CreateActionParameterForItem(ButtonConfig config)
        {
            if (config == null || String.IsNullOrEmpty(config.GroupName) || String.IsNullOrEmpty(config.DisplayName))
            {
                PluginLog.Warning($"[{this.DisplayName}] CreateActionParameterForItem: 无法为配置不完整的项创建ActionParameter。GroupName='{config?.GroupName}', DisplayName='{config?.DisplayName}'");
                return null; // 或者抛出异常，取决于期望的严格程度
            }
            // 确保路径段的规范化和空格替换
            var groupPart = Logic_Manager_Base.SanitizeOscPathSegment(config.GroupName);
            var namePart = Logic_Manager_Base.SanitizeOscPathSegment(config.DisplayName);
            return $"/{groupPart}/{namePart}".Replace("//", "/"); // 确保不会出现双斜杠
        }

        // 初始化动态列表数据、过滤器和收藏夹 (逻辑来自 FX_Folder_Base)
        private void InitializeDynamicListData()
        {
            PluginLog.Info($"[{this.DisplayName}] 开始初始化动态列表数据、过滤器和收藏夹...");
            if (this._dataSourceJson == null)
            {
                PluginLog.Error($"[{this.DisplayName}] InitializeDynamicListData: _dataSourceJson 为空，无法继续。");
                this.UpdateDisplayedDynamicItemsList(); // 确保列表状态被设置为空或默认
                return;
            }

            // --- 第1步: 加载收藏夹 ---
            this._favoriteItemDisplayNames.Clear();
            if (this._dataSourceJson.TryGetValue("Favorite", out JToken favToken) && favToken is JArray favArray)
            {
                this._favoriteItemDisplayNames = favArray.ToObject<List<String>>() ?? new List<String>();
                PluginLog.Info($"[{this.DisplayName}] 加载到 {this._favoriteItemDisplayNames.Count} 个收藏项。");
            }

            // --- 第2步: 识别并初始化过滤器选项 ---
            this._filterOptions.Clear();
            this._currentFilterValues.Clear();
            this._busFilterDialDisplayName = null;

            // 预先识别 BusFilter
            var filterDialConfigs = this._folderDialConfigs.Where(dc => dc.ActionType == "FilterDial").ToList();
            foreach (var dialConfigPreScan in filterDialConfigs)
            {
                if (!String.IsNullOrEmpty(dialConfigPreScan.BusFilter) && dialConfigPreScan.BusFilter.Equals("Yes", StringComparison.OrdinalIgnoreCase))
                {
                    if (String.IsNullOrEmpty(dialConfigPreScan.DisplayName))
                    {
                        PluginLog.Warning($"[{this.DisplayName}] 一个被标记为BusFilter的旋钮没有DisplayName，已忽略此BusFilter标记。");
                        continue;
                    }
                    if (this._busFilterDialDisplayName == null)
                    {
                        this._busFilterDialDisplayName = dialConfigPreScan.DisplayName;
                        PluginLog.Info($"[{this.DisplayName}] 过滤器 '{this._busFilterDialDisplayName}' 被指定为 BusFilter。");
                    }
                    else
                    {
                        PluginLog.Warning($"[{this.DisplayName}] 发现多个BusFilter定义 ('{this._busFilterDialDisplayName}' 和 '{dialConfigPreScan.DisplayName}'). 将使用第一个 ('{this._busFilterDialDisplayName}')。");
                    }
                    // 找到第一个就跳出，因为只支持一个BusFilter
                    break; 
                }
            }
            
            foreach (var dialConfig in filterDialConfigs)
            {
                var filterName = dialConfig.DisplayName;
                if (String.IsNullOrEmpty(filterName))
                {
                    PluginLog.Warning($"[{this.DisplayName}] 发现一个FilterDial没有DisplayName，已跳过。");
                    continue;
                }

                List<String> options;
                Boolean isThisTheBusFilter = !String.IsNullOrEmpty(this._busFilterDialDisplayName) && filterName == this._busFilterDialDisplayName;

                if (this._dataSourceJson.TryGetValue(filterName, out JToken optionsToken) && optionsToken is JArray optionsArray)
                {
                    options = optionsArray.ToObject<List<String>>() ?? new List<String>();
                }
                else
                {
                    options = new List<String>(); // 数据源中没有此过滤器的选项，从空列表开始
                    PluginLog.Verbose($"[{this.DisplayName}] FilterDial '{filterName}' 在数据源JSON中未找到选项列表，将基于是否BusFilter和收藏夹情况构建选项。");
                }

                if (isThisTheBusFilter)
                {
                    // BusFilter: 不加 "All"
                    // 如果有收藏夹且选项中不含 "Favorite"，则在开头添加 "Favorite"
                    if (this._favoriteItemDisplayNames.Any() && !options.Contains("Favorite"))
                    {
                        options.Insert(0, "Favorite");
                    }
                    this._currentFilterValues[filterName] = options.FirstOrDefault(); // 默认选中第一个 (可能是Favorite或第一个分类)
                }
                else
                {
                    // 次级过滤器: 如果选项中不含 "All"，则在开头添加 "All"
                    if (!options.Contains("All"))
                    {
                        options.Insert(0, "All");
                    }
                    this._currentFilterValues[filterName] = "All"; // 默认选中 "All"
                }
                
                this._filterOptions[filterName] = options;
                PluginLog.Info($"[{this.DisplayName}] 初始化过滤器 '{filterName}', BusFilter: {isThisTheBusFilter}. 选项数量: {options.Count}. 当前值: {this._currentFilterValues[filterName] ?? "无"}");
            }

            // --- 第3步: 解析动态列表项 ---
            this._allListItems.Clear(); // 清理旧的列表项是正确的
            // this._currentDisplayedItemActionNames.Clear(); // 将在 UpdateDisplayedDynamicItemsList 中更新

            // 【移除对以下集合的错误清理】
            // this._folderDialConfigs.Clear();             // !错误
            // this._filterOptions.Clear();                 // !错误 (已在上面处理或不应在此)
            // this._currentFilterValues.Clear();           // !错误 (已在上面处理或不应在此)
            // this._favoriteItemDisplayNames.Clear();      // !错误 (已在上面处理或不应在此)
            
            // 【移除对高亮状态字典的全局清理，这些应在Dispose或更细粒度管理】
            // foreach (var timerEntry in this._folderItemResetTimers) { timerEntry.Value.Dispose(); }
            // this._folderItemResetTimers.Clear();
            // this._folderItemTemporaryActiveStates.Clear();

            var processedTopLevelKeys = new HashSet<String> { "Favorite" }; 
            foreach (var filterNameInDataSource in this._filterOptions.Keys) // 确保在 this._filterOptions 填充后使用
            {
                processedTopLevelKeys.Add(filterNameInDataSource); // 记录已作为过滤器处理的顶层键
            }

            foreach (var property in this._dataSourceJson.Properties())
            {
                var topLevelKey = property.Name; // 这代表数据源中的主分类，例如 "Eventide", "FabFilter"
                if (processedTopLevelKeys.Contains(topLevelKey)) // 如果这个顶层键是过滤器名或"Favorite"，则跳过，因为它们不是列表项的直接来源
                {
                    continue; 
                }
                
                if (property.Value is JArray itemsArray)
                {
                    foreach (var itemToken in itemsArray)
                    {
                        if (itemToken is JObject itemJObject)
                        {
                            try
                            {
                                ButtonConfig itemConfig = itemJObject.ToObject<ButtonConfig>();
                                if (itemConfig == null || String.IsNullOrEmpty(itemConfig.DisplayName))
                                {
                                    PluginLog.Warning($"[{this.DisplayName}] 解析列表项失败或DisplayName为空，在主分类 '{topLevelKey}' 下。项: {itemJObject.ToString(Newtonsoft.Json.Formatting.None)}");
                                    continue;
                                }

                                itemConfig.GroupName = topLevelKey; // 列表项的GroupName是其在数据源JSON中的父级键名

                                if (String.IsNullOrEmpty(itemConfig.ActionType)) // 默认为TriggerButton以用于PluginImage绘制
                                {
                                    itemConfig.ActionType = "TriggerButton"; 
                                }

                                itemConfig.FilterableProperties ??= new Dictionary<String, String>();

                                // 填充FilterableProperties (来自JObject的直接属性，这些属性名对应FilterDial的DisplayName)
                                foreach (var subFilterName in this._filterOptions.Keys)
                                {
                                    if (subFilterName == this._busFilterDialDisplayName) // BusFilter的值通常是GroupName，不作为可过滤属性
                                    {
                                        continue;
                                    }

                                    if (itemJObject.TryGetValue(subFilterName, out JToken propValToken) && propValToken.Type != JTokenType.Null)
                                    {
                                        itemConfig.FilterableProperties[subFilterName] = propValToken.ToString();
                                    }
                                }
                                
                                String actionParameter = this.CreateActionParameterForItem(itemConfig);
                                if (String.IsNullOrEmpty(actionParameter))
                                {
                                    PluginLog.Warning($"[{this.DisplayName}] 为项目 '{itemConfig.DisplayName}' (GroupName '{itemConfig.GroupName}') 生成的actionParameter为空，已跳过。");
                                    continue;
                                }

                                if (this._allListItems.ContainsKey(actionParameter))
                                {
                                    PluginLog.Warning($"[{this.DisplayName}] 发现重复的ActionParameter '{actionParameter}'。将跳过重复项。");
                                    continue;
                                }
                                this._allListItems[actionParameter] = itemConfig;
                                this._currentDisplayedItemActionNames.Add(actionParameter);
                            }
                            catch (Exception ex)
                            {
                                PluginLog.Error(ex, $"[{this.DisplayName}] 解析列表项时出错，在主分类 '{topLevelKey}' 下。项: {itemJObject.ToString(Newtonsoft.Json.Formatting.None)}");
                            }
                        }
                    }
                }
            }
            PluginLog.Info($"[{this.DisplayName}] 完成列表项解析，共加载 {this._allListItems.Count} 个项。");

            // --- 第4步: 初始列表内容更新 ---
            this.UpdateDisplayedDynamicItemsList(); // (将在后续实现)
            PluginLog.Info($"[{this.DisplayName}] 完成InitializeDynamicListData。");
        }

        // 根据当前过滤器设置，更新当前可显示的动态列表项，并处理分页 (逻辑来自 FX_Folder_Base)
        private void UpdateDisplayedDynamicItemsList()
        {
            PluginLog.Info($"[{this.DisplayName}] 开始更新动态显示列表...");
            IEnumerable<ButtonConfig> itemsToDisplay = this._allListItems.Values;

            Boolean favoriteFilterActive = false;
            // 首先处理 BusFilter (如果定义了)
            if (!String.IsNullOrEmpty(this._busFilterDialDisplayName) &&
                this._currentFilterValues.TryGetValue(this._busFilterDialDisplayName, out var busFilterValue)) // busFilterValue 可以为 null
            {
                if (busFilterValue == "Favorite")
                {
                    if (this._favoriteItemDisplayNames.Any())
                    {
                        itemsToDisplay = itemsToDisplay.Where(item => 
                            this._favoriteItemDisplayNames.Contains(item.DisplayName) || // 收藏夹是基于DisplayName匹配
                            this._favoriteItemDisplayNames.Contains(item.GroupName + "/" + item.DisplayName) // 也可能是全路径名
                        );
                        favoriteFilterActive = true;
                        PluginLog.Info($"[{this.DisplayName}] 应用收藏夹过滤器。");
                    }
                    else
                    {
                        itemsToDisplay = Enumerable.Empty<ButtonConfig>(); // 收藏夹为空，则不显示任何内容
                        favoriteFilterActive = true;
                        PluginLog.Info($"[{this.DisplayName}] 收藏夹过滤器激活，但收藏列表为空。");
                    }
                }
                // 【修改】如果 busFilterValue 不是 "Favorite" 且不为null/空 (即选定了具体分类)
                else if (!String.IsNullOrEmpty(busFilterValue)) 
                {
                    // BusFilter 通常作用于列表项的 GroupName
                    itemsToDisplay = itemsToDisplay.Where(item => item.GroupName == busFilterValue);
                    PluginLog.Info($"[{this.DisplayName}] 应用BusFilter '{this._busFilterDialDisplayName}' = '{busFilterValue}' (作用于GroupName)。");
                }
                // 如果 busFilterValue 是 null 或空，则不应用此 BusFilter (相当于以前 "All" 的效果，但 "All" 不再是选项)
            }

            // 然后处理其他次级过滤器 (如果收藏夹过滤器未激活，或者激活了但仍需进一步过滤)
            // 注意：通常如果收藏夹激活，可能不需要再应用次级过滤器，但这取决于具体需求。
            // 为保持与FX_Folder_Base类似逻辑，这里即使收藏夹激活，也继续应用次级过滤器 (除非itemsToDisplay已为空)
            if (itemsToDisplay.Any()) // 仅当还有项目可供过滤时才继续
            {
                foreach (var filterEntry in this._currentFilterValues)
                {
                    var filterName = filterEntry.Key;
                    var selectedValue = filterEntry.Value; // selectedValue 可以为 null

                    // 跳过已经处理过的BusFilter本身，以及当BusFilter选为Favorite时也不再用次级过滤器（通常逻辑）
                    // 【修正】如果收藏夹激活，次级过滤器应该仍然可以独立工作，而不是被跳过
                    if (filterName == this._busFilterDialDisplayName) 
                    {
                        continue;
                    }
                    
                    // 【修改】如果 selectedValue 不为null/空 (即选定了具体分类) 且不为 "All"
                    if (!String.IsNullOrEmpty(selectedValue) && selectedValue != "All")
                    {
                        itemsToDisplay = itemsToDisplay.Where(item =>
                            item.FilterableProperties != null &&
                            item.FilterableProperties.TryGetValue(filterName, out var propVal) &&
                            propVal.Equals(selectedValue, StringComparison.OrdinalIgnoreCase)
                        );
                        PluginLog.Info($"[{this.DisplayName}] 应用次级过滤器 '{filterName}' = '{selectedValue}'。");
                    }
                    // 如果 selectedValue 是 null 或空或 "All"，则不应用此特定过滤器
                }
            }

            this._currentDisplayedItemActionNames = itemsToDisplay.Select(config => this.CreateActionParameterForItem(config)).ToList();
            
            // 计算分页，假设每页12个项 (Loupedeck标准按钮数量)
            Int32 itemsPerPage = 12; 
            this._totalPages = (Int32)Math.Ceiling((Double)this._currentDisplayedItemActionNames.Count / itemsPerPage); 
            if (this._totalPages == 0)
            {
                this._totalPages = 1; // 至少有1页，即使是空的
            }
            
            this._currentPage = Math.Min(this._currentPage, this._totalPages - 1); // 确保当前页不超过总页数
            if (this._currentPage < 0)
            {
                this._currentPage = 0; // 确保当前页不为负
            }

            PluginLog.Info($"[{this.DisplayName}] 动态列表更新完毕。当前显示 {this._currentDisplayedItemActionNames.Count} 项。总页数: {this._totalPages}, 当前页: {this._currentPage + 1}");
        }

        // 占位符，以便Linter通过，后续会完整实现
        public void Dispose()
        {
            // 1. 取消事件订阅
            if (Logic_Manager_Base.Instance != null) // 检查 Logic_Manager_Base 实例是否存在，以防插件卸载顺序问题
            {
                Logic_Manager_Base.Instance.CommandStateNeedsRefresh -= this.OnCommandStateNeedsRefresh;
            }

            // 2. 清理所有集合和字典
            this._entryConfig = null;    // 如果它是可 Disposable 的，则调用其 Dispose
            this._dataSourceJson = null; // JObject 不需要显式Dispose

            this._allListItems.Clear();
            this._currentDisplayedItemActionNames.Clear();
            this._folderDialConfigs.Clear(); 
            this._filterOptions.Clear();
            this._currentFilterValues.Clear();
            this._favoriteItemDisplayNames.Clear();
            
            // this._lastPressTimes.Clear(); // 移除旧的
            
            this._localButtonIds.Clear();
            this._localIdToGlobalActionParameter_Buttons.Clear();
            this._localIdToConfig_Buttons.Clear();
            
            this._parameterDialCurrentIndexes.Clear();
            
            // this._lastTriggerPressTimes.Clear(); // 移除旧的
            // this._lastCombineButtonPressTimes.Clear(); // 移除旧的

            // 【新增】清理新的Timer相关字典
            foreach (var timerEntry in this._folderItemResetTimers)
            {
                timerEntry.Value.Stop();
                timerEntry.Value.Elapsed -= (sender, e) => HandleFolderItemResetTimerElapsed(sender, e, timerEntry.Key); // 尝试移除，但对于lambda可能无效，Dispose是关键
                timerEntry.Value.Dispose();
            }
            this._folderItemResetTimers.Clear();
            this._folderItemTemporaryActiveStates.Clear();

            // 3. 记录日志
            PluginLog.Info($"[{this.DisplayName ?? "Folder"}] Disposed.");
        }

        // --- 核心Loupedeck SDK方法重写 ---

        public override IEnumerable<String> GetButtonPressActionNames()
        {
            if (this._isButtonListDynamic)
            {
                // 对于动态列表，从 _currentDisplayedItemActionNames 获取当前页的项
                // 假设每页12个按钮
                Int32 itemsPerPage = 12; 
                return this._currentDisplayedItemActionNames
                    .Skip(this._currentPage * itemsPerPage)
                    .Take(itemsPerPage)
                    .Select(actionParameter => this.CreateCommandName(actionParameter)); // SDK需要完整的命令名
            }
            else
            {
                // 【修改】对于静态按钮列表，遍历原始配置以支持 Placeholder
                var staticButtonActionNames = new List<String>();
                foreach (var buttonConfig in this._rawStaticButtonConfigs)
                {
                    if (buttonConfig.ActionType == "Placeholder")
                    {
                        staticButtonActionNames.Add(null); // Placeholder 为 null
                    }
                    else
                    {
                        // 对于非 Placeholder 按钮，需要找到它在 PopulateStaticButtonMappings 中生成的 localId
                        // PopulateStaticButtonMappings 应该已经用 GroupName 和 DisplayName 填充了 _localIdToConfig_Buttons
                        // 我们可以反向查找，或者确保 _localButtonIds 的顺序与 _rawStaticButtonConfigs 中非占位符的顺序一致
                        // 为了简单和鲁棒性，我们重新构建 localId 的方式与 PopulateStaticButtonMappings 一致
                        var loadedConfig = this._localIdToConfig_Buttons.Values.FirstOrDefault(
                            cfg => cfg.DisplayName == buttonConfig.DisplayName && 
                                   cfg.GroupName == (buttonConfig.GroupName ?? this.DisplayName)
                        );

                        if (loadedConfig != null)
                        {
                            // PopulateStaticButtonMappings 中 localId 的生成方式是 "{GroupName}{DisplayName}".Replace(" ", "")
                            // 但我们应该从 _localIdToGlobalActionParameter_Buttons 的键中获取正确的 localId，因为那里存储的是准确的
                            var localIdEntry = this._localIdToGlobalActionParameter_Buttons.FirstOrDefault(
                                kvp => kvp.Value == Logic_Manager_Base.Instance.GetAllConfigs().FirstOrDefault(
                                    x => x.Value.DisplayName == buttonConfig.DisplayName && 
                                         x.Value.GroupName == (buttonConfig.GroupName ?? this.DisplayName) &&
                                         x.Value.ActionType == buttonConfig.ActionType //确保ActionType也匹配，以防同名不同类型
                                ).Key
                            );

                            if (!String.IsNullOrEmpty(localIdEntry.Key))
                            {
                                staticButtonActionNames.Add(this.CreateCommandName(localIdEntry.Key));
                            }
                            else
                            {
                                PluginLog.Warning($"[{this.DisplayName}] GetButtonPressActionNames: 未能为静态按钮 '{buttonConfig.DisplayName}' (Group: '{buttonConfig.GroupName ?? this.DisplayName}') 找到有效的 localId。将视为空白。");
                                staticButtonActionNames.Add(null); // 如果找不到，也视为空白
                            }
                        }
                        else
                        {
                            PluginLog.Warning($"[{this.DisplayName}] GetButtonPressActionNames: 未能从_localIdToConfig_Buttons中为静态按钮 '{buttonConfig.DisplayName}' (Group: '{buttonConfig.GroupName ?? this.DisplayName}') 找到加载的配置。将视为空白。");
                            staticButtonActionNames.Add(null); // 如果找不到配置，视为空白
                        }
                    }
                }
                return staticButtonActionNames;
            }
        }


        public override IEnumerable<String> GetEncoderRotateActionNames()
        {
            var rotateActionNames = new String[6]; // 设备通常有6个旋钮

            for (Int32 i = 0; i < 6; i++)
            {
                if (i < this._folderDialConfigs.Count)
                {
                    var dialConfig = this._folderDialConfigs[i];
                    if (dialConfig.ActionType == "Placeholder")
                    {
                        rotateActionNames[i] = null; 
                    }
                    else
                    {
                        var localId = this.GetLocalDialId(dialConfig);
                        if (!String.IsNullOrEmpty(localId)) 
                        {
                            rotateActionNames[i] = this.CreateAdjustmentName(localId); 
                        }
                        else 
                        { 
                            rotateActionNames[i] = null; // 如果无法生成有效localId，也视为空
                        }
                    }
                }
                else
                {
                    rotateActionNames[i] = null; // 超出JSON定义数量的槽位也为空
                }
            }
            PluginLog.Info($"[{this.DisplayName}] GetEncoderRotateActionNames: [{String.Join(", ", rotateActionNames.Select(s => s ?? "null"))}]");
            return rotateActionNames;
        }

        public override IEnumerable<String> GetEncoderPressActionNames(DeviceType deviceType)
        {
            var pressActionNames = new String[6]; // 设备通常有6个旋钮

            for (Int32 i = 0; i < 6; i++)
            {
                if (i < this._folderDialConfigs.Count)
                {
                    var dialConfig = this._folderDialConfigs[i];
                    if (dialConfig.ActionType == "Placeholder") 
                    {
                        pressActionNames[i] = null; 
                    }
                    // "Back" 导航旋钮的按下是一个特殊的文件夹命令
                    else if (dialConfig.ActionType == "NavigationDial" && dialConfig.DisplayName == "Back") 
                    {
                        var localId = this.GetLocalDialId(dialConfig);
                        if (!String.IsNullOrEmpty(localId)) 
                        {
                            // 对于文件夹导航命令，如 "Back"，通常使用 base.CreateCommandName
                            // 或者如果希望它通过本类的 RunCommand 处理，也可以用 this.CreateCommandName
                            // 旧 Dynamic_Folder_Base 和 FX_Folder_Base 在此处都使用了 base.CreateCommandName
                            pressActionNames[i] = base.CreateCommandName(localId); 
                        }
                        else { pressActionNames[i] = null; }
                    }
                    // 其他旋钮类型，如 ToggleDial, 2ModeTickDial，如果其按下动作需要作为独立命令注册
                    // 根据之前的讨论，这些的按下逻辑将主要在 RunCommand 中通过 Logic_Manager_Base.ProcessDialPress 处理
                    // 因此，它们通常不需要在这里返回一个 encoder press action name，除非它们的按下也完全由 RunCommand(actionParameter) 驱动
                    // 例如，如果 ToggleDial 按下要发送一个特定的、与旋转无关的OSC消息，且该消息由 RunCommand(localId) 处理
                    else if (dialConfig.ActionType == "ToggleDial" || 
                             dialConfig.ActionType == "2ModeTickDial" ||
                             dialConfig.ActionType == "ControlDial")
                    {
                        // 如果这些类型的旋钮按下确实需要一个由 RunCommand(localId) 处理的独立命令：
                        var localId = this.GetLocalDialId(dialConfig);
                        if (!String.IsNullOrEmpty(localId))
                        {
                            pressActionNames[i] = this.CreateCommandName(localId); // 使用 this.CreateCommandName
                        }
                        else { pressActionNames[i] = null; }
                    }
                    // 其他类型旋钮 (如 FilterDial, PageDial, ParameterDial, TickDial) 默认按下无独立命令注册在此处
                    else 
                    {
                        pressActionNames[i] = null; 
                    }
                }
                else
                {
                    pressActionNames[i] = null; // 超出JSON定义数量的槽位也为空
                }
            }
            PluginLog.Info($"[{this.DisplayName}] GetEncoderPressActionNames: [{String.Join(", ", pressActionNames.Select(s => s ?? "null"))}]");
            return pressActionNames;
        }

        public override PluginDynamicFolderNavigation GetNavigationArea(DeviceType _) =>
            PluginDynamicFolderNavigation.None; // 插件将通过自定义的"Back"旋钮等方式处理导航

        public override void ApplyAdjustment(String actionParameter, Int32 ticks)
        {
            // SDK 传入的 actionParameter 通常是 CreateAdjustmentName(localId) 中的 localId
            var localDialId = actionParameter; 

            var dialConfig = this._folderDialConfigs.FirstOrDefault(dc => this.GetLocalDialId(dc) == localDialId);

            if (dialConfig == null || dialConfig.ActionType == "Placeholder")
            {
                // PluginLog.Warning($"[{this.DisplayName}] ApplyAdjustment: 未找到配置或为Placeholder: '{localDialId}'.");
                return; 
            }
            
            Boolean listChanged = false;       // 标记按钮/列表项是否因调整而改变 (用于 ButtonActionNamesChanged)
            Boolean valueChanged = false;      // 标记旋钮本身显示的值是否改变 (用于 AdjustmentValueChanged)
            Boolean pageCountChanged = false;  // 标记总页数是否因过滤器调整而改变

            switch (dialConfig.ActionType)
            {
                case "FilterDial":
                    var filterName = dialConfig.DisplayName;
                    if (this._filterOptions.TryGetValue(filterName, out var options) && options.Any())
                    {
                        var currentSelectedValue = this._currentFilterValues.ContainsKey(filterName) ? this._currentFilterValues[filterName] : options[0];
                        var currentIndex = options.IndexOf(currentSelectedValue);
                        if (currentIndex == -1)
                        {
                            currentIndex = 0;
                        }

                        var newIndex = (currentIndex + ticks + options.Count) % options.Count;
                        if (newIndex < 0)
                        {
                            newIndex += options.Count; // 确保正索引
                        }
                        this._currentFilterValues[filterName] = options[newIndex];
                        
                        this._currentPage = 0; // 过滤器改变，重置到第一页
                        this.UpdateDisplayedDynamicItemsList(); // 这会更新 _totalPages
                        
                        listChanged = true;    // 列表内容已改变
                        valueChanged = true;   // 此旋钮显示的值已改变
                        pageCountChanged = true; // 总页数可能已改变
                        PluginLog.Info($"[{this.DisplayName}] FilterDial '{filterName}' 值变为: {this._currentFilterValues[filterName] ?? "无"}");
                    }
                    break;
                
                case "PageDial":
                    var newPage = this._currentPage + ticks;
                    if (newPage >= 0 && newPage < this._totalPages)
                    {
                        this._currentPage = newPage;
                        listChanged = true;  // 列表内容已改变 (翻页了)
                        valueChanged = true; // 此旋钮显示的值已改变
                        PluginLog.Info($"[{this.DisplayName}] PageDial: 当前页变为 {this._currentPage + 1}");
                    }
                    break;
                
                case "NavigationDial":
                    if (dialConfig.DisplayName == "Back")
                    {
                        PluginLog.Info($"[{this.DisplayName}] NavigationDial 'Back' 旋转。关闭文件夹。");
                        this.Close();
                        return; // 关闭后不应再更新UI
                    }
                    break;

                // 对于以下类型，我们将逻辑委托给 Logic_Manager_Base
                // 注意：需要确保 Logic_Manager_Base.ProcessDialAdjustment 能够通过 localDialId (或其对应的全局参数) 正确处理
                case "ParameterDial":
                case "ToggleDial":
                case "2ModeTickDial":
                case "TickDial":
                case "ControlDial": // 【新增】处理ControlDial旋转
                    // 首先，我们需要从 localDialId 找到对应的全局 actionParameter，因为 Logic_Manager 使用全局参数作为键
                    // 这个查找逻辑可能比较复杂，因为 localDialId 是文件夹内部的，而 Logic_Manager 的键是全局唯一的。
                    // 一个简单的（但不完全健壮的）假设是 localDialId 就是全局参数的一部分，或者需要拼接。
                    // 更好的方法是在 PopulateStaticButtonMappings 或 InitializeDynamicListData (如果旋钮也被视为一种全局可配置项时) 
                    // 就建立 localDialId 到 globalActionParameter 的映射。但旋钮通常不通过这种方式注册到 Logic_Manager 的 _allConfigs。
                    // 另一种方式是，Logic_Manager.ProcessDialAdjustment 接受 ButtonConfig 对象。

                    // 暂时采用一种简化的查找方式：尝试基于 localDialId 和当前文件夹的 DisplayName (作为GroupName) 去 Logic_Manager 查找对应的配置项
                    // 这假设 Logic_Manager 中的旋钮配置键格式为 "/FolderName_DialName/DialAction" 或类似
                    // String potentialGlobalKey = $"/{this.DisplayName.Replace(" ", "_")}_{dialConfig.DisplayName.Replace(" ", "_")}/DialAction"; // 这是一个猜测
                    // 更好的方法是，如果这些旋钮的逻辑要由LogicManager处理，它们应该在LogicManager中有对应的注册条目和全局Key
                    // 旧Dynamic_Folder_Base的ParameterDial是在本地ApplyAdjustment中处理的
                    // 旧General_Dial_Base是调用Logic_Manager.ProcessDialAdjustment(config, ticks, actionParameter, this._lastEventTimes);
                    // 其中actionParameter已经是全局的。
                    
                    // 现在的localDialId就是ApplyAdjustment的直接参数actionParameter
                    // 我们需要一个方法从这个文件夹内的localDialId找到LogicManager的全局Key
                    // 这个转换可能需要在 Logic_Manager_Base.GetAllConfigs() 中查找，匹配 GroupName 和 DisplayName，以及 ActionType
                    var globalParamKvp = Logic_Manager_Base.Instance.GetAllConfigs().FirstOrDefault(kvp => 
                        (kvp.Value.GroupName == this.DisplayName || kvp.Value.GroupName == dialConfig.GroupName) && // GroupName可能是在JSON中明确指定的，或默认为文件夹名
                        kvp.Value.DisplayName == dialConfig.DisplayName && 
                        kvp.Value.ActionType == dialConfig.ActionType
                    );

                    if (!EqualityComparer<KeyValuePair<String, ButtonConfig>>.Default.Equals(globalParamKvp, default))
                    {
                        String globalActionParameter = globalParamKvp.Key;
                        Logic_Manager_Base.Instance.ProcessDialAdjustment(globalActionParameter, ticks);
                        // Logic_Manager_Base.ProcessDialAdjustment 内部应负责调用 CommandStateNeedsRefresh
                        // 这会触发 OnCommandStateNeedsRefresh, 进而调用 AdjustmentValueChanged
                        // 所以这里可能不需要直接调用 this.AdjustmentValueChanged(actionParameter);
                        // 但如果Logic_Manager不保证刷新本旋钮，则需要调用。ToggleDial等状态变化会触发。ParameterDial和ControlDial的值变化需要本地刷新。
                        if(dialConfig.ActionType == "ParameterDial" || dialConfig.ActionType == "ControlDial")
                        {
                            valueChanged = true;
                        }

                    }
                    else
                    {
                        // 如果在Logic_Manager中找不到对应的全局配置，对于ParameterDial，可以尝试在本地处理
                        if (dialConfig.ActionType == "ParameterDial" && dialConfig.Parameter != null && dialConfig.Parameter.Any())
                        {
                            if (!this._parameterDialCurrentIndexes.TryGetValue(localDialId, out var currentIndex))
                            {
                                currentIndex = 0; // 理论上构造函数已初始化
                            }
                            Int32 newParamIndex = (currentIndex + ticks + dialConfig.Parameter.Count) % dialConfig.Parameter.Count;
                            if (newParamIndex < 0)
                            {
                                newParamIndex += dialConfig.Parameter.Count;
                            }


                            this._parameterDialCurrentIndexes[localDialId] = newParamIndex;
                            valueChanged = true;
                            PluginLog.Info($"[{this.DisplayName}] ParameterDial '{dialConfig.DisplayName}' (本地处理) 值变为索引 {newParamIndex}: '{dialConfig.Parameter[newParamIndex]}'.");
                            // 如果ParameterDial的值改变需要更新关联的ParameterButton，Logic_Manager会通过CommandStateNeedsRefresh处理
                        }
                        // ControlDial 的核心逻辑完全在 Logic_Manager_Base 中，如果找不到全局配置，则不应在本地处理
                        else if (dialConfig.ActionType != "ControlDial") 
                        {
                            PluginLog.Warning($"[{this.DisplayName}] ApplyAdjustment: 未能在Logic_Manager中找到旋钮 '{dialConfig.DisplayName}' (Type: {dialConfig.ActionType}) 的全局配置，且无法本地处理。");
                        }
                    }
                    break;

                default:
                    PluginLog.Warning($"[{this.DisplayName}] ApplyAdjustment: 未处理的 ActionType '{dialConfig.ActionType}' for dial '{dialConfig.DisplayName}'.");
                    break;
            }

            if(listChanged)
            {
                this.ButtonActionNamesChanged(); 
            }
            if(valueChanged) // 如果旋钮的显示值改变了
            {
                this.AdjustmentValueChanged(actionParameter); 
            }
            if(pageCountChanged) // 如果总页数改变了，通知PageDial更新其显示
            {
                var pageDialConfig = this._folderDialConfigs.FirstOrDefault(dc => dc.ActionType == "PageDial");
                if(pageDialConfig != null)
                {
                    var pageDialLocalId = this.GetLocalDialId(pageDialConfig);
                    if(!String.IsNullOrEmpty(pageDialLocalId))
                    {
                        this.AdjustmentValueChanged(pageDialLocalId);
                    }
                }
            }
        }

        public override void RunCommand(String actionParameter)
        {
            String commandLocalIdToLookup = actionParameter; // 对于动态列表项，这将是其全局actionParameter
            const String commandPrefix = "plugin:command:"; 
            if (actionParameter.StartsWith(commandPrefix))
            {
                commandLocalIdToLookup = actionParameter.Substring(commandPrefix.Length);
            }
            PluginLog.Info($"[{this.DisplayName}] RunCommand looking for key: '{commandLocalIdToLookup}' (Original: '{actionParameter}')");

            // 1. 检查是否为旋钮按下 (这部分逻辑已相对独立，暂不修改其高亮，除非有特别需求)
            var dialConfigPressed = this._folderDialConfigs.FirstOrDefault(dc => this.GetLocalDialId(dc) == commandLocalIdToLookup);
            if (dialConfigPressed != null)
            {
                // ... (旋钮按下逻辑保持不变) ...
                if (dialConfigPressed.ActionType == "Placeholder") 
                { 
                    PluginLog.Info($"[{this.DisplayName}] Placeholder旋钮 '{dialConfigPressed.DisplayName}' 被按下，无操作。");
                    return; 
                }

                if (dialConfigPressed.ActionType == "NavigationDial" && dialConfigPressed.DisplayName == "Back")
                {
                    PluginLog.Info($"[{this.DisplayName}] 'Back' NavigationDial (localId: '{commandLocalIdToLookup}') 被按下。关闭文件夹。");
                    this.Close();
                    return;
                }
                
                if (dialConfigPressed.ActionType == "ToggleDial" || 
                    dialConfigPressed.ActionType == "2ModeTickDial" ||
                    dialConfigPressed.ActionType == "ControlDial")
                {
                    var globalParamKvp = Logic_Manager_Base.Instance.GetAllConfigs().FirstOrDefault(kvp => 
                        (kvp.Value.GroupName == this.DisplayName || kvp.Value.GroupName == dialConfigPressed.GroupName) && 
                        kvp.Value.DisplayName == dialConfigPressed.DisplayName && 
                        kvp.Value.ActionType == dialConfigPressed.ActionType
                    );
                    if (!EqualityComparer<KeyValuePair<String, ButtonConfig>>.Default.Equals(globalParamKvp, default))
                    {
                        String globalDialActionParameter = globalParamKvp.Key;
                        if (Logic_Manager_Base.Instance.ProcessDialPress(globalDialActionParameter))
                        {
                            this.AdjustmentValueChanged(this.CreateAdjustmentName(commandLocalIdToLookup)); 
                        }
                        PluginLog.Info($"[{this.DisplayName}] 已处理旋钮 '{dialConfigPressed.DisplayName}' (Type: {dialConfigPressed.ActionType}) 的按下事件，通过Logic_Manager。全球参数: {globalDialActionParameter}");
                        return; 
                    }
                    else
                    { 
                        PluginLog.Warning($"[{this.DisplayName}] 未能在Logic_Manager中找到旋钮 '{dialConfigPressed.DisplayName}' (Type: {dialConfigPressed.ActionType}) 的全局配置以处理按下事件。Local ID: {commandLocalIdToLookup}");
                    }
                }
            }

            // 2. 检查是否为按钮 (静态或动态列表项)
            ButtonConfig pressedButtonConfig = null;
            String buttonKeyForState = commandLocalIdToLookup; // 对于动态项，这是全局参数；对于静态项，这是localId

            if (this._isButtonListDynamic)
            {
                this._allListItems.TryGetValue(commandLocalIdToLookup, out pressedButtonConfig);
            }
            else 
            {
                this._localIdToConfig_Buttons.TryGetValue(commandLocalIdToLookup, out pressedButtonConfig);
                // 对于静态按钮，buttonKeyForState (即localId) 是正确的用于Timer和状态字典的键
            }

            if (pressedButtonConfig != null)
            {
                // 【新增】处理 NavigationButton，应优先于其他按钮类型判断
                if (pressedButtonConfig.ActionType == "NavigationButton")
                {
                    // 约定：导航按钮通过其 DisplayName, Title, 或 OscAddress 字段指定为 "Back" 来触发返回
                    bool isBackButton = (pressedButtonConfig.DisplayName == "Back") || 
                                      (String.IsNullOrEmpty(pressedButtonConfig.DisplayName) && pressedButtonConfig.Title == "Back") || 
                                      (!String.IsNullOrEmpty(pressedButtonConfig.OscAddress) && pressedButtonConfig.OscAddress == "Back");

                    if (isBackButton)
                    {
                        PluginLog.Info($"[{this.DisplayName}] NavigationButton 'Back' (Key: '{commandLocalIdToLookup}') pressed. Closing folder.");
                        this.Close();
                        return; // 操作完成，直接返回
                    }
                    else
                    {
                        PluginLog.Warning($"[{this.DisplayName}] NavigationButton '{pressedButtonConfig.DisplayName}' (Key: '{commandLocalIdToLookup}') pressed, but its target is not recognized as 'Back'. No action taken.");
                        // 即使不是Back，也触发一下UI刷新，以防有高亮等效果
                        this.CommandImageChanged(buttonKeyForState);
                        return;
                    }
                }

                String globalButtonParamForLogicManager = commandLocalIdToLookup; // 默认动态项的key就是全局key
                if (!this._isButtonListDynamic) // 如果是静态按钮，需要从localId获取全局参数
                {
                    if (!this._localIdToGlobalActionParameter_Buttons.TryGetValue(commandLocalIdToLookup, out globalButtonParamForLogicManager))
                    {
                        PluginLog.Warning($"[{this.DisplayName}] RunCommand: 未找到静态按钮 localId '{commandLocalIdToLookup}' 对应的 globalActionParameter。");
                        return;
                    }
                }

                // 调用 Logic_Manager 处理核心动作和OSC发送
                Logic_Manager_Base.Instance.ProcessUserAction(globalButtonParamForLogicManager, this.DisplayName, pressedButtonConfig);

                // 【修改】统一使用新的 Timer 高亮机制
                if (pressedButtonConfig.ActionType == "TriggerButton" || pressedButtonConfig.ActionType == "CombineButton")
                {
                    this._folderItemTemporaryActiveStates[buttonKeyForState] = true;
                    if (!this._folderItemResetTimers.TryGetValue(buttonKeyForState, out var timer))
                    {
                        // 为动态列表项按需创建 Timer
                        if (this._isButtonListDynamic) 
                        {
                            timer = new System.Timers.Timer(HighlightDurationMilliseconds);
                            timer.AutoReset = false;
                            timer.Elapsed += (s, e) => HandleFolderItemResetTimerElapsed(s, e, buttonKeyForState); // buttonKeyForState 是动态项的全局 actionParameter
                            this._folderItemResetTimers[buttonKeyForState] = timer;
                            PluginLog.Verbose($"[{this.DisplayName}] On-demand timer created for dynamic item '{buttonKeyForState}'.");
                        }
                        else
                        {
                             // 静态按钮的Timer应该在PopulateStaticButtonMappings中已创建，理论上不应到这里
                             PluginLog.Warning($"[{this.DisplayName}] Timer not found for static button '{buttonKeyForState}' in RunCommand. This should have been initialized.");
                        }
                    }
                    
                    timer?.Stop(); // ?.确保timer不为null
                    timer?.Start();
                    
                    this.CommandImageChanged(buttonKeyForState); // 请求重绘此按钮
                    PluginLog.Info($"[{this.DisplayName}] Button '{pressedButtonConfig.DisplayName}' (key: '{buttonKeyForState}', type: {pressedButtonConfig.ActionType}) pressed, highlight activated.");
                }
                else if (pressedButtonConfig.ActionType == "ToggleButton" || pressedButtonConfig.ActionType == "ParameterButton")
                {
                    // ToggleButton 和 ParameterButton 的状态由 Logic_Manager 通过 CommandStateNeedsRefresh 触发刷新
                    // 如果其按下也需要瞬时视觉反馈（即使状态没变），可以考虑也使用上面的Timer机制，
                    // 但通常它们的视觉变化是状态驱动的，CommandStateNeedsRefresh 应该足够。
                    // 为保持与 General_Button_Base 的一致性（它不为Toggle提供瞬时反馈），此处仅依赖 CommandStateNeedsRefresh
                    // this.CommandImageChanged(buttonKeyForState); // 可选：如果希望按下立即有反馈，即使是状态驱动按钮
                }
                return;
            }
            
            if(dialConfigPressed == null) 
            {
                 PluginLog.Warning($"[{this.DisplayName}] RunCommand: 未找到与 key '{commandLocalIdToLookup}' 匹配的旋钮或按钮配置。");
            }
        }

        public override Boolean ProcessButtonEvent2(String actionParameter, DeviceButtonEvent2 buttonEvent)
        {
            // 此方法可以处理更底层的按钮事件。
            // 主要确保 "Back" 导航按钮的按下事件能可靠地关闭文件夹。

            if (buttonEvent.EventType == DeviceButtonEventType.Press)
            {
                String commandLocalIdToLookup = actionParameter;
                const String commandPrefix = "plugin:command:"; 
                if (actionParameter.StartsWith(commandPrefix))
                {
                    commandLocalIdToLookup = actionParameter.Substring(commandPrefix.Length);
                }

                // 检查是否为 "Back" 导航旋钮的 localId
                var dialConfigPressed = this._folderDialConfigs.FirstOrDefault(dc => 
                    dc.ActionType == "NavigationDial" && 
                    dc.DisplayName == "Back" && 
                    this.GetLocalDialId(dc) == commandLocalIdToLookup
                );

                if (dialConfigPressed != null)
                {
                    PluginLog.Info($"[{this.DisplayName}] ProcessButtonEvent2: 'Back' NavigationDial (localId: '{commandLocalIdToLookup}') 被按下。关闭文件夹。");
                    this.Close();
                    return true; // 事件已处理，不再向上传递
                }
            }
            return base.ProcessButtonEvent2(actionParameter, buttonEvent); // 其他情况调用基类实现
        }

        // --- III. UI 绘制方法 ---
        public override BitmapImage GetCommandImage(String actionParameter, PluginImageSize imageSize)
        {
            String localId = actionParameter; 
            const String commandPrefix = "plugin:command:"; 
            if (actionParameter.StartsWith(commandPrefix))
            {
                localId = actionParameter.Substring(commandPrefix.Length);
            }

            if (localId.Equals(NavigateUpActionName))
            {
                // NavigateUpActionName 的图像绘制保持不变
                using(var bb = new BitmapBuilder(imageSize))
                {
                    bb.Clear(BitmapColor.Black);
                    // 使用 PluginImage.GetButtonFontSize 以保持一致性，如果需要的话
                    // 或者直接使用一个固定的、合适的字体大小
                    bb.DrawText("Up", BitmapColor.White, GetButtonFontSize("Up")); 
                    return bb.ToImage();
                }
            }

            ButtonConfig configToDraw = null;
            String globalParamForState = null; 
            Boolean isStaticButton = false;
            Logic_Manager_Base logicManager = Logic_Manager_Base.Instance; // 获取 Logic_Manager 实例

            if (this._isButtonListDynamic)
            {
                if (!this._allListItems.TryGetValue(localId, out configToDraw))
                {
                    // 动态列表项未找到，绘制默认错误图像
                    return PluginImage.DrawElement(imageSize, null, "Itm?", isActive:true, preferIconOnlyForDial: false);
                }
            }
            else 
            {
                isStaticButton = true;
                if (!this._localIdToConfig_Buttons.TryGetValue(localId, out configToDraw) || 
                    !this._localIdToGlobalActionParameter_Buttons.TryGetValue(localId, out globalParamForState))
                {
                    // 静态按钮配置未找到，绘制默认错误图像
                    return PluginImage.DrawElement(imageSize, null, "Btn?", isActive:true, preferIconOnlyForDial: false);
                }
            }
            if (configToDraw == null)
            {
                 // 理论上不会到这里，因为上面已经处理了未找到的情况
                return PluginImage.DrawElement(imageSize, null, "Err", isActive:true, preferIconOnlyForDial: false);
            }

            BitmapImage loadedIcon = PluginImage.TryLoadIcon(configToDraw, this.DisplayName);
            String preliminaryTitle = null; 
            Boolean isActive = false;
            Int32 currentModeForDrawing = 0; 
            String preliminaryAuxText = configToDraw.Text; // 原始辅助文本模板

            // 确定初步标题
            if (configToDraw.ActionType == "ParameterButton" && isStaticButton)
            {
                // ParameterButton的标题由DetermineParameterButtonTitle特殊处理，其内部可能也需要{mode}解析（如果其回退标题含{mode}）
                preliminaryTitle = this.DetermineParameterButtonTitle(configToDraw); 
            }
            else
            {
                preliminaryTitle = configToDraw.Title ?? configToDraw.DisplayName;
            }

            // 使用 Logic_Manager 解析标题和辅助文本中的 {mode}
            String mainTitleToDraw = logicManager.ResolveTextWithMode(configToDraw, preliminaryTitle);
            String auxTextToDraw = logicManager.ResolveTextWithMode(configToDraw, preliminaryAuxText);

            // 【修改】确定激活状态，统一使用新的 _folderItemTemporaryActiveStates 机制
            if (configToDraw.ActionType == "TriggerButton" || configToDraw.ActionType == "CombineButton")
            {
                // localId 对于动态列表项是其全局 actionParameter，对于静态按钮是其 localId
                // 这与 _folderItemTemporaryActiveStates 和 _folderItemResetTimers 中使用的键一致
                isActive = this._folderItemTemporaryActiveStates.TryGetValue(localId, out var tempState) && tempState;
            }
            else if (configToDraw.ActionType == "ToggleButton")
            { 
                if (isStaticButton && !String.IsNullOrEmpty(globalParamForState))
                {
                    isActive = logicManager.GetToggleState(globalParamForState);
                }
                else if (!isStaticButton) 
                {
                    isActive = logicManager.GetToggleState(localId); 
                }
            }
            // ParameterButton 的 isActive 状态通常不通过这种瞬时高亮管理，而是由其参数源决定，或固定显示
            
            return PluginImage.DrawElement(imageSize, configToDraw, mainTitleToDraw, null, isActive, currentModeForDrawing, loadedIcon, false, auxTextToDraw, false );
        }

        public override BitmapImage GetAdjustmentImage(String actionParameter, PluginImageSize imageSize)
        {
            if (String.IsNullOrEmpty(actionParameter))
            {
                // 空 actionParameter，返回全黑图像 (保持不变)
                using (var bb = new BitmapBuilder(imageSize))
                {
                    bb.Clear(BitmapColor.Black);
                    return bb.ToImage();
                }
            }
            var localDialId = actionParameter; 
            var dialConfig = this._folderDialConfigs.FirstOrDefault(dc => this.GetLocalDialId(dc) == localDialId);
            if (dialConfig == null || dialConfig.ActionType == "Placeholder")
            {
                // 未找到配置或为占位符，返回全黑图像 (保持不变)
                using (var bb = new BitmapBuilder(imageSize))
                {
                    bb.Clear(BitmapColor.Black);
                    return bb.ToImage();
                }
            }
            
            Logic_Manager_Base logicManager = Logic_Manager_Base.Instance; // 获取 Logic_Manager 实例
            BitmapImage loadedIcon = PluginImage.TryLoadIcon(dialConfig, this.DisplayName);
            
            // 初步确定标题 (未解析 {mode})
            String preliminaryTitle = dialConfig.ShowTitle?.Equals("No", StringComparison.OrdinalIgnoreCase) == true ? null : (dialConfig.Title ?? dialConfig.DisplayName);
            // 初步确定辅助文本 (未解析 {mode})
            String preliminaryAuxText = dialConfig.Text;

            // 使用 ResolveTextWithMode 解析标题和辅助文本
            String mainTitleToDraw = logicManager.ResolveTextWithMode(dialConfig, preliminaryTitle);
            String auxTextToDraw = logicManager.ResolveTextWithMode(dialConfig, preliminaryAuxText);
            
            String valueTextToDisplay = null; 
            Boolean isActiveStatus = false; 
            Int32 currentModeStatus = 0;    
            String globalParamForState = this.GetGlobalParamForDialState(dialConfig); // 此方法获取全局参数，用于从LogicManager获取状态

            switch (dialConfig.ActionType)
            {
                case "FilterDial":
                    valueTextToDisplay = this._currentFilterValues.TryGetValue(dialConfig.DisplayName, out var val) ? val : "N/A";
                    break;
                case "PageDial":
                    valueTextToDisplay = $"{this._currentPage + 1} / {this._totalPages}";
                    break;
                case "ParameterDial":
                    valueTextToDisplay = this.DetermineParameterDialValue(dialConfig, localDialId);
                    break;
                case "ToggleDial": 
                    if(globalParamForState != null)
                    {
                        isActiveStatus = logicManager.GetToggleState(globalParamForState); 
                    }
                    break;
                case "2ModeTickDial":
                    if(globalParamForState != null)
                    {
                        currentModeStatus = logicManager.GetDialMode(globalParamForState);
                        // 对于 2ModeTickDial，如果其 ModeName 与 dialConfig.ModeName 匹配 (理论上应该如此如果它受模式控制)
                        // 它的标题可能在 ResolveTextWithMode 时已经根据当前模式切换了 (如果 Title 或 Title_Mode2 含 {mode})
                        // 这里 mainTitleToDraw 已处理过 ResolveTextWithMode，但如果其 Title/Title_Mode2 本身也应该根据外部模式组改变，
                        // 且 ResolveTextWithMode(dialConfig,...) 使用了 dialConfig.ModeName，那么这里可能需要重新确定 preliminaryTitle
                        // 以便在 PluginImage.DrawElement 中，dialConfig.Title_Mode2 能被正确使用。
                        // 不过，PluginImage.DrawElement 内部对2ModeTickDial的标题有自己的处理逻辑（基于currentModeStatus从dialConfig取）
                        // 所以这里 mainTitleToDraw 的准备主要是为了那些 config.Title 本身就包含 {mode} 的情况。
                    }
                    break;
                case "ControlDial": 
                    if (!String.IsNullOrEmpty(globalParamForState)) 
                    {
                        valueTextToDisplay = logicManager.GetControlDialValue(globalParamForState).ToString();
                    }
                    else
                    {
                        PluginLog.Warning($"[{this.DisplayName}|GetAdjustmentImage] ControlDial '{dialConfig.DisplayName}' (localId: {localDialId}) 无法获取 globalParamForState 以查询值。");
                        valueTextToDisplay = "ERR"; 
                    }
                    break;
            }
            
            return PluginImage.DrawElement(imageSize, dialConfig, mainTitleToDraw, valueTextToDisplay, isActiveStatus, currentModeStatus, loadedIcon, false, auxTextToDraw, true);
        }

        public override BitmapImage GetButtonImage(PluginImageSize imageSize) // 文件夹入口按钮
        {
            if (this._entryConfig == null) 
            {
                using (var bb = new BitmapBuilder(imageSize))
                {
                    bb.Clear(BitmapColor.Black);
                    bb.DrawText(!String.IsNullOrEmpty(this.DisplayName) ? this.DisplayName : "Folder", BitmapColor.White, GetButtonFontSize(this.DisplayName));
                    return bb.ToImage();
                }
            }
            BitmapImage loadedEntryIcon = PluginImage.TryLoadIcon(this._entryConfig, this.DisplayName);
            // 文件夹入口是按钮，preferIconOnlyForDial: false
            // actualAuxText 由 DrawElement 根据 config.Text 处理
            return PluginImage.DrawElement(imageSize, this._entryConfig, null, null, false, 0, loadedEntryIcon, false, null, false);
        }

        private String DetermineParameterButtonTitle(ButtonConfig paramButtonConfig)
        {
            var sourceDialGlobalParam = this.FindSourceDialGlobalActionParameter(paramButtonConfig, paramButtonConfig.ParameterSourceDial);
            return !String.IsNullOrEmpty(sourceDialGlobalParam)
                ? (Logic_Manager_Base.Instance.GetParameterDialSelectedTitle(sourceDialGlobalParam) ?? paramButtonConfig.Title ?? paramButtonConfig.DisplayName)
                : $"Err:{paramButtonConfig.ParameterSourceDial?.Substring(0, Math.Min(paramButtonConfig.ParameterSourceDial?.Length ?? 0, 4))}";
        }
        private String DetermineParameterDialValue(ButtonConfig dialCfg, String localDialId)
        {
            if (this._parameterDialCurrentIndexes.TryGetValue(localDialId, out var currentIndex) && 
                dialCfg.Parameter != null && currentIndex >= 0 && currentIndex < dialCfg.Parameter.Count)
            { 
                return dialCfg.Parameter[currentIndex]; 
            }
            return dialCfg.Parameter?.FirstOrDefault() ?? "N/A"; 
        }
        private String GetGlobalParamForDialState(ButtonConfig dialCfg)
        {
             var kvp = Logic_Manager_Base.Instance.GetAllConfigs().FirstOrDefault(c => 
                (c.Value.GroupName == this.DisplayName || c.Value.GroupName == dialCfg.GroupName) && 
                c.Value.DisplayName == dialCfg.DisplayName && 
                c.Value.ActionType == dialCfg.ActionType );
            return !EqualityComparer<KeyValuePair<String, ButtonConfig>>.Default.Equals(kvp, default) ? kvp.Key : null;
        }

        // 辅助方法，用于根据标题长度自动获取按钮的字体大小 (如果PluginImage需要，但它内部有自己的)
        // 此方法可以从旧 Dynamic_Folder_Base 迁移，但 PluginImage.GetButtonFontSize 更通用
        private static Int32 GetButtonFontSize(String title) 
        {
            if (String.IsNullOrEmpty(title))
            {
                return 23;
            }
            var len = title.Length; 
            return len switch { 1 => 38, 2 => 33, 3 => 31, 4 => 26, 5 => 23, 6 => 22, 7 => 20, 8 => 18, _ => 16 }; 
        }

        // --- IV. 辅助方法与事件处理 ---
        // (GetLocalDialId, CreateActionParameterForItem, FindSourceDialGlobalActionParameter 已实现)

        private void OnCommandStateNeedsRefresh(Object sender, String globalActionParameterThatChanged)
        {
            if (String.IsNullOrEmpty(globalActionParameterThatChanged))
            {
                return;
            }

            // 检查是否为本文件夹管理的静态按钮相关的状态变化
            var staticButtonEntry = this._localIdToGlobalActionParameter_Buttons.FirstOrDefault(kvp => kvp.Value == globalActionParameterThatChanged);
            if (!EqualityComparer<KeyValuePair<String, String>>.Default.Equals(staticButtonEntry, default))
            {
                var localButtonId = staticButtonEntry.Key;
                if (this._localIdToConfig_Buttons.TryGetValue(localButtonId, out var buttonConfig))
                {
                    PluginLog.Info($"[{this.DisplayName}] OnCommandStateNeedsRefresh: 静态按钮 '{buttonConfig.DisplayName}' (localId: {localButtonId}, global: {globalActionParameterThatChanged}) 状态改变，触发UI刷新。");
                    this.ButtonActionNamesChanged(); // 通知SDK按钮可能需要重绘
                    this.CommandImageChanged(localButtonId); // 【修改】明确通知此特定按钮的图像已改变

                    // 如果是ParameterButton，其源ParameterDial改变时，它需要刷新
                    // 这个检查也可以放在下面ParameterDial改变的部分，但这里更直接
                    if (buttonConfig.ActionType == "ParameterButton")
                    {
                        // 通常ParameterButton的刷新已由ButtonActionNamesChanged()覆盖
                        // 无需额外操作，除非有特定逻辑
                    }
                    return; // 处理完毕
                }
            }

            // 检查是否为本文件夹管理的旋钮相关的状态变化
            // 这需要遍历 _folderDialConfigs 并确定哪个旋钮（如果有的话）对应于 globalActionParameterThatChanged
            foreach (var dialConfig in this._folderDialConfigs)
            {
                // 尝试找到此 dialConfig 在 Logic_Manager 中的全局表示
                var globalDialKvp = Logic_Manager_Base.Instance.GetAllConfigs().FirstOrDefault(kvp => 
                    (kvp.Value.GroupName == this.DisplayName || kvp.Value.GroupName == dialConfig.GroupName) && 
                    kvp.Value.DisplayName == dialConfig.DisplayName && 
                    kvp.Value.ActionType == dialConfig.ActionType
                );

                if (!EqualityComparer<KeyValuePair<String, ButtonConfig>>.Default.Equals(globalDialKvp, default) && globalDialKvp.Key == globalActionParameterThatChanged)
                {
                    var localDialId = this.GetLocalDialId(dialConfig);
                    if (!String.IsNullOrEmpty(localDialId))
                    {
                        PluginLog.Info($"[{this.DisplayName}] OnCommandStateNeedsRefresh: 旋钮 '{dialConfig.DisplayName}' (localId: {localDialId}, global: {globalActionParameterThatChanged}) 状态改变，触发UI刷新。");
                        this.AdjustmentValueChanged(localDialId); // 【修改】使用原始 localDialId 通知旋钮图像更新

                        // 如果是ParameterDial改变，需要检查是否有链接的ParameterButton在此文件夹内并刷新它们
                        if (dialConfig.ActionType == "ParameterDial")
                        {
                            Boolean linkedButtonNeedsRefresh = false;
                            foreach (var btnEntryKvp in this._localIdToConfig_Buttons) // 仅检查此文件夹内的静态按钮
                            {
                                var otherLocalConfig = btnEntryKvp.Value;
                                if (otherLocalConfig.ActionType == "ParameterButton" &&
                                    otherLocalConfig.ParameterSourceDial == dialConfig.DisplayName &&
                                    (otherLocalConfig.GroupName == dialConfig.GroupName || otherLocalConfig.GroupName == this.DisplayName) // GroupName匹配逻辑
                                   )
                                {
                                    PluginLog.Info($"[{this.DisplayName}] OnCommandStateNeedsRefresh: ParameterDial '{dialConfig.DisplayName}' 改变，将刷新关联的静态ParameterButton '{otherLocalConfig.DisplayName}'.");
                                    linkedButtonNeedsRefresh = true;
                                    break; 
                                }
                            }
                            // 如果是动态列表文件夹，ParameterButton可能在 _allListItems 中，需要不同的检查逻辑（暂未实现，因为动态列表项通常不是ParameterButton）
                            
                            if(linkedButtonNeedsRefresh) 
                            {
                                this.ButtonActionNamesChanged();
                            }
                        }
                        return; // 处理完毕
                    }
                }
            }
            // PluginLog.Verbose($"[{this.DisplayName}] OnCommandStateNeedsRefresh: globalActionParameter '{globalActionParameterThatChanged}' 与当前文件夹管理的任何已知项不直接匹配。");
        }

        public override String GetCommandDisplayName(String actionParameter, PluginImageSize imageSize)
        {
            String localId = actionParameter;
            const String commandPrefix = "plugin:command:";
            if (actionParameter.StartsWith(commandPrefix))
            {
                localId = actionParameter.Substring(commandPrefix.Length);
            }

            if (localId.Equals(NavigateUpActionName) || localId == base.CreateCommandName("back"))
            {
                return "Back"; // 为向上导航/返回按钮提供显示名称
            }
            
            // 对于占位符类型的 encoder press action (如果 GetEncoderPressActionNames 生成了它们)
            // 旧 Dynamic_Folder_Base 中有类似处理，但新版 GetEncoderPressActionNames 中对 Placeholder 返回 null
            // 为保险起见，如果 actionParameter 仍然可能是这种形式，则返回空字符串
            if (localId.StartsWith("placeholder_d_encoder_press_"))
            {
                return ""; 
            }

            ButtonConfig config = null;
            if (this._isButtonListDynamic)
            {
                // 对于动态列表项，通常依赖图像，但如果需要，可以返回标题
                if (this._allListItems.TryGetValue(localId, out var itemConfig))
                {
                    config = itemConfig;
                }
            }
            else
            {
                // 对于静态按钮
                if (this._localIdToConfig_Buttons.TryGetValue(localId, out var staticConfig))
                {
                    config = staticConfig;
                }
            }

            if (config != null)
            {
                return config.Title ?? config.DisplayName; // 优先用Title，其次用DisplayName
            }

            PluginLog.Verbose($"[{this.DisplayName}|GetCommandDisplayName] 未找到配置 for localId '{localId}', 返回null.");
            return null; // 默认或未找到配置则返回null
        }

        public override String GetAdjustmentDisplayName(String actionParameter, PluginImageSize imageSize) => null; // 旋钮名称直接绘制在图像上

        public override String GetAdjustmentValue(String actionParameter) => null; // 旋钮值直接绘制在图像上

        // 从旧 Dynamic_Folder_Base 迁移，用于 ParameterButton 查找其数据源 ParameterDial
        private String FindSourceDialGlobalActionParameter(ButtonConfig parameterButtonConfig, String sourceDialDisplayNameFromButtonConfig)
        {
            if (parameterButtonConfig == null || String.IsNullOrEmpty(parameterButtonConfig.GroupName) || String.IsNullOrEmpty(sourceDialDisplayNameFromButtonConfig))
            {
                PluginLog.Warning($"[{this.DisplayName}] FindSourceDialGlobal: 无效的参数。ParameterButtonConfig 或其 GroupName 为空，或 sourceDialDisplayName 为空。");
                return null;
            }

            var allConfigs = Logic_Manager_Base.Instance.GetAllConfigs();
            var foundDialEntry = allConfigs.FirstOrDefault(kvp => 
                kvp.Value.DisplayName == sourceDialDisplayNameFromButtonConfig &&
                kvp.Value.GroupName == parameterButtonConfig.GroupName && 
                kvp.Value.ActionType == "ParameterDial");

            if (!EqualityComparer<KeyValuePair<String, ButtonConfig>>.Default.Equals(foundDialEntry, default) && !String.IsNullOrEmpty(foundDialEntry.Key))
            {
                return foundDialEntry.Key;
            }
            
            PluginLog.Warning($"[{this.DisplayName}] FindSourceDialGlobal: ParameterButton '{parameterButtonConfig.DisplayName}' (JSON Group: '{parameterButtonConfig.GroupName}') 未能找到源 ParameterDial，其 DisplayName 为 '{sourceDialDisplayNameFromButtonConfig}'.");
            return null;
        }
    }
} 