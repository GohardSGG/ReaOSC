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
        private readonly Dictionary<string, ButtonConfig> _allListItems = new Dictionary<string, ButtonConfig>();
        private List<string> _currentDisplayedItemActionNames = new List<string>();

        // 文件夹旋钮配置 (通用)
        private readonly List<ButtonConfig> _folderDialConfigs = new List<ButtonConfig>();

        // 过滤器管理 (源自 FX_Folder_Base)
        private readonly Dictionary<string, List<string>> _filterOptions = new Dictionary<string, List<string>>();
        private readonly Dictionary<string, string> _currentFilterValues = new Dictionary<string, string>();
        private string _busFilterDialDisplayName = null;
        private List<string> _favoriteItemDisplayNames = new List<string>();

        // 分页管理 (源自 FX_Folder_Base)
        private int _currentPage = 0;
        private int _totalPages = 1;

        // UI反馈辅助 (通用，FX_Folder_Base 中已有 _lastPressTimes)
        private readonly Dictionary<string, DateTime> _lastPressTimes = new Dictionary<string, DateTime>();


        // --- 来自旧 Dynamic_Folder_Base (并引入/整合) ---
        private bool _isButtonListDynamic; // 标记按钮列表是静态还是动态

        // 静态按钮管理 (源自旧 Dynamic_Folder_Base)
        private readonly List<string> _localButtonIds = new List<string>();
        private readonly Dictionary<string, string> _localIdToGlobalActionParameter_Buttons = new Dictionary<string, string>();
        private readonly Dictionary<string, ButtonConfig> _localIdToConfig_Buttons = new Dictionary<string, ButtonConfig>();

        // ParameterDial 状态管理 (源自旧 Dynamic_Folder_Base)
        private readonly Dictionary<string, int> _parameterDialCurrentIndexes = new Dictionary<string, int>();

        // 旧版静态按钮瞬时反馈 (源自旧 Dynamic_Folder_Base - 待评估是否合并到 _lastPressTimes)
        private readonly Dictionary<string, DateTime> _lastTriggerPressTimes = new Dictionary<string, DateTime>();
        private readonly Dictionary<string, DateTime> _lastCombineButtonPressTimes = new Dictionary<string, DateTime>();

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
                            string localId = this.GetLocalDialId(dialConfig);
                            if (!string.IsNullOrEmpty(localId) && dialConfig.Parameter != null && dialConfig.Parameter.Any())
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
                    this.PopulateStaticButtonMappings(folderContentConfig.Buttons); 
                    if (folderContentConfig.Buttons == null || !folderContentConfig.Buttons.Any())
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
        private string GetLocalDialId(ButtonConfig dialConfig)
        {
            if (dialConfig == null) return null;
            var groupName = dialConfig.GroupName ?? this.DisplayName; 
            var displayName = dialConfig.DisplayName ?? ""; 

            if (string.IsNullOrEmpty(displayName) && dialConfig.ActionType != "Placeholder")
            {
                 PluginLog.Warning($"[{this.DisplayName}] GetLocalDialId: 旋钮配置 (类型: {dialConfig.ActionType}) 的 DisplayName 为空。GroupName: {groupName}");
                 return null; 
            }
            
            if (dialConfig.ActionType == "Placeholder" && string.IsNullOrEmpty(displayName))
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
                // 如果是动态列表模式或没有静态按钮，则不处理
                return; 
            }

            foreach (var buttonConfigFromJson in staticButtonConfigs) 
            {
                // Placeholder按钮类型也应该被跳过 (如果未来按钮也支持Placeholder的话)
                if (buttonConfigFromJson.ActionType == "Placeholder") { continue; }

                // 注意: 静态按钮的 GroupName 和 DisplayName 来自其在 folderContentConfig.Buttons 中的定义
                // 我们需要用这些信息去 Logic_Manager_Base 中查找匹配的已注册全局配置
                var kvp = Logic_Manager_Base.Instance.GetAllConfigs().FirstOrDefault(
                    x => x.Value.DisplayName == buttonConfigFromJson.DisplayName && 
                         x.Value.GroupName == (buttonConfigFromJson.GroupName ?? this.DisplayName) // 如果按钮没定义GroupName，则假定其GroupName为文件夹的DisplayName
                );
                var globalActionParameter = kvp.Key;   
                var loadedConfig = kvp.Value; // 这是从Logic_Manager加载的完整配置，可能包含更多信息或已被处理过         

                if (string.IsNullOrEmpty(globalActionParameter) || loadedConfig == null)
                {
                    PluginLog.Warning($"[{this.DisplayName}] PopulateStaticButtonMappings: 未能从Logic_Manager找到按钮的全局配置。按钮DisplayName='{buttonConfigFromJson.DisplayName}', 按钮原始GroupName='{buttonConfigFromJson.GroupName ?? "(未定义，使用文件夹名)"}'。");
                    continue;
                }

                // 使用加载的配置来生成localId，确保与Logic_Manager中注册的一致性 (虽然通常localId是内部的)
                // 或者，更简单地，可以直接使用 buttonConfigFromJson 中的信息，因为这些按钮是此文件夹"拥有"的
                // 旧 Dynamic_Folder_Base 使用的是 loadedConfig.GroupName 和 loadedConfig.DisplayName
                var localId = $"{loadedConfig.GroupName}_{loadedConfig.DisplayName}".Replace(" ", ""); // 注意旧版移除了空格

                if (this._localIdToConfig_Buttons.ContainsKey(localId))
                {
                    PluginLog.Warning($"[{this.DisplayName}] PopulateStaticButtonMappings: 静态按钮localId '{localId}' 重复。");
                    continue;
                }
                this._localIdToGlobalActionParameter_Buttons[localId] = globalActionParameter;
                this._localIdToConfig_Buttons[localId] = loadedConfig; // 存储从Logic_Manager加载的配置
                this._localButtonIds.Add(localId);
            }
            PluginLog.Info($"[{this.DisplayName}] PopulateStaticButtonMappings: 处理了 {this._localButtonIds.Count} 个静态按钮。");
        }

        // 辅助方法，用于从 ButtonConfig (通常是动态列表项) 创建唯一的动作参数 (与 FX_Folder_Base 逻辑一致)
        private string CreateActionParameterForItem(ButtonConfig config)
        {
            if (config == null || string.IsNullOrEmpty(config.GroupName) || string.IsNullOrEmpty(config.DisplayName))
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
                this._favoriteItemDisplayNames = favArray.ToObject<List<string>>() ?? new List<string>();
                PluginLog.Info($"[{this.DisplayName}] 加载到 {this._favoriteItemDisplayNames.Count} 个收藏项。");
            }

            // --- 第2步: 识别并初始化过滤器选项 ---
            this._filterOptions.Clear();
            this._currentFilterValues.Clear();
            this._busFilterDialDisplayName = null;

            // 过滤器选项的来源是 _folderDialConfigs 中 ActionType 为 "FilterDial" 的旋钮
            // 以及它们在 _dataSourceJson 中对应的数组
            foreach (var dialConfig in this._folderDialConfigs.Where(dc => dc.ActionType == "FilterDial"))
            {
                var filterName = dialConfig.DisplayName;
                if (string.IsNullOrEmpty(filterName))
                {
                    PluginLog.Warning($"[{this.DisplayName}] 发现一个FilterDial没有DisplayName，已跳过。");
                    continue;
                }

                if (this._dataSourceJson.TryGetValue(filterName, out JToken optionsToken) && optionsToken is JArray optionsArray)
                {
                    var options = optionsArray.ToObject<List<string>>() ?? new List<string>();
                    
                    if (!options.Contains("All")) { options.Insert(0, "All"); }

                    bool isBusFilter = !string.IsNullOrEmpty(dialConfig.BusFilter) && dialConfig.BusFilter.Equals("Yes", StringComparison.OrdinalIgnoreCase);

                    if (this._favoriteItemDisplayNames.Any() && !options.Contains("Favorite"))
                    {
                        bool shouldAddFavorite = isBusFilter || 
                                                 (this._busFilterDialDisplayName == null && 
                                                  this._folderDialConfigs.Where(dc => dc.ActionType == "FilterDial").FirstOrDefault() == dialConfig);
                        if (shouldAddFavorite) 
                        { 
                            options.Insert(1, "Favorite"); // "All" 之后
                        } 
                    }
                    
                    this._filterOptions[filterName] = options;
                    this._currentFilterValues[filterName] = "All"; 
                    PluginLog.Info($"[{this.DisplayName}] 初始化过滤器 '{filterName}', 选项数量: {options.Count}. 当前值: All");

                    if (isBusFilter)
                    {
                        if (this._busFilterDialDisplayName == null)
                        {
                            this._busFilterDialDisplayName = filterName;
                            PluginLog.Info($"[{this.DisplayName}] 过滤器 '{filterName}' 被指定为 BusFilter。");
                        }
                        else
                        {
                             PluginLog.Warning($"[{this.DisplayName}] 发现多个BusFilter定义 ('{this._busFilterDialDisplayName}' 和 '{filterName}'). 将使用第一个。");
                        }
                    }
                }
                else
                {
                    PluginLog.Warning($"[{this.DisplayName}] FilterDial '{filterName}' 在数据源中未找到对应的选项列表。将使用默认'All'选项。");
                    this._filterOptions[filterName] = new List<string> { "All" };
                    this._currentFilterValues[filterName] = "All";
                }
            }

            // --- 第3步: 解析动态列表项 ---
            this._allListItems.Clear();
            var processedTopLevelKeys = new HashSet<string> { "Favorite" }; // "All" 不是顶层键，是选项值
            foreach (var filterNameInDataSource in this._filterOptions.Keys)
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
                                if (itemConfig == null || string.IsNullOrEmpty(itemConfig.DisplayName))
                                {
                                    PluginLog.Warning($"[{this.DisplayName}] 解析列表项失败或DisplayName为空，在主分类 '{topLevelKey}' 下。项: {itemJObject.ToString(Newtonsoft.Json.Formatting.None)}");
                                    continue;
                                }

                                itemConfig.GroupName = topLevelKey; // 列表项的GroupName是其在数据源JSON中的父级键名

                                if (string.IsNullOrEmpty(itemConfig.ActionType)) // 默认为TriggerButton以用于PluginImage绘制
                                {
                                    itemConfig.ActionType = "TriggerButton"; 
                                }

                                if (itemConfig.FilterableProperties == null) 
                                {
                                    itemConfig.FilterableProperties = new Dictionary<string, string>();
                                }

                                // 填充FilterableProperties (来自JObject的直接属性，这些属性名对应FilterDial的DisplayName)
                                foreach (var subFilterName in this._filterOptions.Keys)
                                {
                                    if (subFilterName == this._busFilterDialDisplayName) // BusFilter的值通常是GroupName，不作为可过滤属性
                                        continue;

                                    if (itemJObject.TryGetValue(subFilterName, out JToken propValToken) && propValToken.Type != JTokenType.Null)
                                    {
                                        itemConfig.FilterableProperties[subFilterName] = propValToken.ToString();
                                    }
                                }
                                
                                string actionParameter = this.CreateActionParameterForItem(itemConfig);
                                if (string.IsNullOrEmpty(actionParameter))
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
                            }
                            catch (Exception ex)
                            {
                                PluginLog.Error(ex, $"[{this.DisplayName}] 解析列表项时出错，在主分类 '{topLevelKey}' 下。项: {itemJObject.ToString(Newtonsoft.Json.Formatting.None)}");
                            }
                        }
                    }
                }
            }
            PluginLog.Info($"[{this.DisplayName}] 完成列表项解析，共加载 {_allListItems.Count} 个项。");

            // --- 第4步: 初始列表内容更新 ---
            this.UpdateDisplayedDynamicItemsList(); // (将在后续实现)
            PluginLog.Info($"[{this.DisplayName}] 完成InitializeDynamicListData。");
        }

        // 根据当前过滤器设置，更新当前可显示的动态列表项，并处理分页 (逻辑来自 FX_Folder_Base)
        private void UpdateDisplayedDynamicItemsList()
        {
            PluginLog.Info($"[{this.DisplayName}] 开始更新动态显示列表...");
            IEnumerable<ButtonConfig> itemsToDisplay = this._allListItems.Values;

            bool favoriteFilterActive = false;
            // 首先处理 BusFilter (如果定义了)
            if (!string.IsNullOrEmpty(this._busFilterDialDisplayName) &&
                this._currentFilterValues.TryGetValue(this._busFilterDialDisplayName, out var busFilterValue))
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
                else if (busFilterValue != "All") // 如果BusFilter不是"All"也不是"Favorite"
                {
                    // BusFilter 通常作用于列表项的 GroupName
                    itemsToDisplay = itemsToDisplay.Where(item => item.GroupName == busFilterValue);
                    PluginLog.Info($"[{this.DisplayName}] 应用BusFilter '{this._busFilterDialDisplayName}' = '{busFilterValue}' (作用于GroupName)。");
                }
            }

            // 然后处理其他次级过滤器 (如果收藏夹过滤器未激活，或者激活了但仍需进一步过滤)
            // 注意：通常如果收藏夹激活，可能不需要再应用次级过滤器，但这取决于具体需求。
            // 为保持与FX_Folder_Base类似逻辑，这里即使收藏夹激活，也继续应用次级过滤器 (除非itemsToDisplay已为空)
            if (itemsToDisplay.Any()) // 仅当还有项目可供过滤时才继续
            {
                foreach (var filterEntry in this._currentFilterValues)
                {
                    var filterName = filterEntry.Key;
                    var selectedValue = filterEntry.Value;

                    // 跳过已经处理过的BusFilter本身，以及当BusFilter选为Favorite时也不再用次级过滤器（通常逻辑）
                    if (filterName == this._busFilterDialDisplayName || (favoriteFilterActive && filterName != this._busFilterDialDisplayName) ) 
                        continue;
                    
                    if (selectedValue != "All")
                    {
                        itemsToDisplay = itemsToDisplay.Where(item =>
                            item.FilterableProperties != null &&
                            item.FilterableProperties.TryGetValue(filterName, out var propVal) &&
                            propVal.Equals(selectedValue, StringComparison.OrdinalIgnoreCase)
                        );
                        PluginLog.Info($"[{this.DisplayName}] 应用次级过滤器 '{filterName}' = '{selectedValue}'。");
                    }
                }
            }

            this._currentDisplayedItemActionNames = itemsToDisplay.Select(config => this.CreateActionParameterForItem(config)).ToList();
            
            // 计算分页，假设每页12个项 (Loupedeck标准按钮数量)
            int itemsPerPage = 12; 
            this._totalPages = (int)Math.Ceiling((double)this._currentDisplayedItemActionNames.Count / itemsPerPage); 
            if (this._totalPages == 0) this._totalPages = 1; // 至少有1页，即使是空的
            
            this._currentPage = Math.Min(this._currentPage, this._totalPages - 1); // 确保当前页不超过总页数
            if (this._currentPage < 0) this._currentPage = 0; // 确保当前页不为负

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
            
            this._lastPressTimes.Clear();
            
            this._localButtonIds.Clear();
            this._localIdToGlobalActionParameter_Buttons.Clear();
            this._localIdToConfig_Buttons.Clear();
            
            this._parameterDialCurrentIndexes.Clear();
            
            this._lastTriggerPressTimes.Clear();
            this._lastCombineButtonPressTimes.Clear();

            // 3. 记录日志
            PluginLog.Info($"[{this.DisplayName ?? "Folder"}] Disposed.");
        }

        // --- 核心Loupedeck SDK方法重写 ---

        public override IEnumerable<string> GetButtonPressActionNames()
        {
            if (this._isButtonListDynamic)
            {
                // 对于动态列表，从 _currentDisplayedItemActionNames 获取当前页的项
                // 假设每页12个按钮
                int itemsPerPage = 12; 
                return this._currentDisplayedItemActionNames
                    .Skip(this._currentPage * itemsPerPage)
                    .Take(itemsPerPage)
                    .Select(actionParameter => this.CreateCommandName(actionParameter)); // SDK需要完整的命令名
            }
            else
            {
                // 对于静态按钮列表，返回 _localButtonIds 中的所有项
                return this._localButtonIds.Select(id => this.CreateCommandName(id)).ToList();
            }
        }

        public override IEnumerable<string> GetEncoderRotateActionNames()
        {
            var rotateActionNames = new string[6]; // 设备通常有6个旋钮

            for (int i = 0; i < 6; i++)
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
                        if (!string.IsNullOrEmpty(localId)) 
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
            PluginLog.Info($"[{this.DisplayName}] GetEncoderRotateActionNames: [{string.Join(", ", rotateActionNames.Select(s => s ?? "null"))}]");
            return rotateActionNames;
        }

        public override IEnumerable<string> GetEncoderPressActionNames(DeviceType deviceType)
        {
            var pressActionNames = new string[6]; // 设备通常有6个旋钮

            for (int i = 0; i < 6; i++)
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
                        if (!string.IsNullOrEmpty(localId)) 
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
                    else if (dialConfig.ActionType == "ToggleDial" || dialConfig.ActionType == "2ModeTickDial")
                    {
                        // 如果这些类型的旋钮按下确实需要一个由 RunCommand(localId) 处理的独立命令：
                        var localId = this.GetLocalDialId(dialConfig);
                        if (!string.IsNullOrEmpty(localId))
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
            PluginLog.Info($"[{this.DisplayName}] GetEncoderPressActionNames: [{string.Join(", ", pressActionNames.Select(s => s ?? "null"))}]");
            return pressActionNames;
        }

        public override PluginDynamicFolderNavigation GetNavigationArea(DeviceType _) =>
            PluginDynamicFolderNavigation.None; // 插件将通过自定义的"Back"旋钮等方式处理导航

        public override void ApplyAdjustment(string actionParameter, int ticks)
        {
            // SDK 传入的 actionParameter 通常是 CreateAdjustmentName(localId) 中的 localId
            var localDialId = actionParameter; 

            var dialConfig = this._folderDialConfigs.FirstOrDefault(dc => this.GetLocalDialId(dc) == localDialId);

            if (dialConfig == null || dialConfig.ActionType == "Placeholder")
            {
                // PluginLog.Warning($"[{this.DisplayName}] ApplyAdjustment: 未找到配置或为Placeholder: '{localDialId}'.");
                return; 
            }
            
            bool listChanged = false;       // 标记按钮/列表项是否因调整而改变 (用于 ButtonActionNamesChanged)
            bool valueChanged = false;      // 标记旋钮本身显示的值是否改变 (用于 AdjustmentValueChanged)
            bool pageCountChanged = false;  // 标记总页数是否因过滤器调整而改变

            switch (dialConfig.ActionType)
            {
                case "FilterDial":
                    var filterName = dialConfig.DisplayName;
                    if (this._filterOptions.TryGetValue(filterName, out var options) && options.Any())
                    {
                        var currentSelectedValue = this._currentFilterValues.ContainsKey(filterName) ? this._currentFilterValues[filterName] : options[0];
                        var currentIndex = options.IndexOf(currentSelectedValue);
                        if (currentIndex == -1) currentIndex = 0;

                        var newIndex = (currentIndex + ticks + options.Count) % options.Count;
                        if (newIndex < 0) newIndex += options.Count; // 确保正索引
                        this._currentFilterValues[filterName] = options[newIndex];
                        
                        this._currentPage = 0; // 过滤器改变，重置到第一页
                        this.UpdateDisplayedDynamicItemsList(); // 这会更新 _totalPages
                        
                        listChanged = true;    // 列表内容已改变
                        valueChanged = true;   // 此旋钮显示的值已改变
                        pageCountChanged = true; // 总页数可能已改变
                        PluginLog.Info($"[{this.DisplayName}] FilterDial '{filterName}' 值变为: {this._currentFilterValues[filterName]}");
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

                    if (!EqualityComparer<KeyValuePair<string, ButtonConfig>>.Default.Equals(globalParamKvp, default(KeyValuePair<string, ButtonConfig>)))
                    {
                        string globalActionParameter = globalParamKvp.Key;
                        Logic_Manager_Base.Instance.ProcessDialAdjustment(globalActionParameter, ticks);
                        // Logic_Manager_Base.ProcessDialAdjustment 内部应负责调用 CommandStateNeedsRefresh
                        // 这会触发 OnCommandStateNeedsRefresh, 进而调用 AdjustmentValueChanged
                        // 所以这里可能不需要直接调用 this.AdjustmentValueChanged(actionParameter);
                        // 但如果Logic_Manager不保证刷新本旋钮，则需要调用。ToggleDial等状态变化会触发。ParameterDial的值变化需要本地刷新。
                        if(dialConfig.ActionType == "ParameterDial") valueChanged = true;
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
                            int newParamIndex = (currentIndex + ticks + dialConfig.Parameter.Count) % dialConfig.Parameter.Count;
                            if (newParamIndex < 0) newParamIndex += dialConfig.Parameter.Count;
                            this._parameterDialCurrentIndexes[localDialId] = newParamIndex;
                            valueChanged = true;
                            PluginLog.Info($"[{this.DisplayName}] ParameterDial '{dialConfig.DisplayName}' (本地处理) 值变为索引 {newParamIndex}: '{dialConfig.Parameter[newParamIndex]}'.");
                            // 如果ParameterDial的值改变需要更新关联的ParameterButton，Logic_Manager会通过CommandStateNeedsRefresh处理
                        }
                        else
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
                    if(!string.IsNullOrEmpty(pageDialLocalId))
                    {
                        this.AdjustmentValueChanged(this.CreateAdjustmentName(pageDialLocalId)); 
                    }
                }
            }
        }

        public override void RunCommand(string actionParameter)
        {
            string commandLocalIdToLookup = actionParameter;
            const string commandPrefix = "plugin:command:"; // Loupedeck SDK 可能会添加的前缀
            if (actionParameter.StartsWith(commandPrefix))
            {
                commandLocalIdToLookup = actionParameter.Substring(commandPrefix.Length);
            }
            PluginLog.Info($"[{this.DisplayName}] RunCommand 正在查找 localId: '{commandLocalIdToLookup}' (原始: '{actionParameter}')");

            // 1. 检查是否为旋钮按下
            var dialConfigPressed = this._folderDialConfigs.FirstOrDefault(dc => this.GetLocalDialId(dc) == commandLocalIdToLookup);
            if (dialConfigPressed != null)
            {
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
                
                // 对于 ToggleDial, 2ModeTickDial，其按下逻辑由 Logic_Manager_Base.ProcessDialPress 处理
                // 需要将文件夹本地的 commandLocalIdToLookup 转换为 Logic_Manager 能识别的全局参数
                if (dialConfigPressed.ActionType == "ToggleDial" || dialConfigPressed.ActionType == "2ModeTickDial")
                {
                    // 尝试从 Logic_Manager 中找到此旋钮的全局注册项
                    var globalParamKvp = Logic_Manager_Base.Instance.GetAllConfigs().FirstOrDefault(kvp => 
                        (kvp.Value.GroupName == this.DisplayName || kvp.Value.GroupName == dialConfigPressed.GroupName) && 
                        kvp.Value.DisplayName == dialConfigPressed.DisplayName && 
                        kvp.Value.ActionType == dialConfigPressed.ActionType
                    );

                    if (!EqualityComparer<KeyValuePair<string, ButtonConfig>>.Default.Equals(globalParamKvp, default(KeyValuePair<string, ButtonConfig>)))
                    {
                        string globalDialActionParameter = globalParamKvp.Key;
                        if (Logic_Manager_Base.Instance.ProcessDialPress(globalDialActionParameter))
                        {
                            // ProcessDialPress 返回true通常表示状态已改变 (例如2ModeTickDial切换了模式)
                            // 需要刷新此旋钮的图像
                            this.AdjustmentValueChanged(this.CreateAdjustmentName(commandLocalIdToLookup)); 
                        }
                        PluginLog.Info($"[{this.DisplayName}] 已处理旋钮 '{dialConfigPressed.DisplayName}' (Type: {dialConfigPressed.ActionType}) 的按下事件，通过Logic_Manager。");
                        return; // 假设旋钮按下事件到此结束
                    }
                    else
                    {
                        PluginLog.Warning($"[{this.DisplayName}] 未能在Logic_Manager中找到旋钮 '{dialConfigPressed.DisplayName}' (Type: {dialConfigPressed.ActionType}) 的全局配置以处理按下事件。");
                        // 如果找不到全局配置，可能意味着这个旋钮的按下没有在Logic_Manager中定义特定行为
                        // 代码会继续尝试匹配是否为列表项或静态按钮 (虽然通常旋钮的localId不会与之冲突)
                    }
                }
                // 如果是其他类型的旋钮按下，且没有在此处定义特定行为，则允许继续尝试匹配其他类型命令
            }

            // 2. 检查是否为动态列表项 (如果 _isButtonListDynamic == true)
            if (this._isButtonListDynamic)
            {
                // commandLocalIdToLookup 对于动态列表项，应该是 CreateActionParameterForItem 生成的 "/GroupName/DisplayName" 格式
                if (this._allListItems.TryGetValue(commandLocalIdToLookup, out var itemConfig))
                {
                    // Logic_Manager_Base 将使用 this.DisplayName (文件夹名), itemConfig.GroupName (顶层分组), itemConfig.DisplayName (项名)
                    // 来构建 "/FolderName/Add/TopGroup/ItemName" 格式的OSC地址
                    Logic_Manager_Base.Instance.ProcessUserAction(commandLocalIdToLookup, this.DisplayName, itemConfig);

                    // UI反馈
                    this._lastPressTimes[commandLocalIdToLookup] = DateTime.Now;
                    this.ButtonActionNamesChanged(); // 通知SDK列表项(按钮)状态可能已改变，需要重绘
                    Task.Delay(200).ContinueWith(_ => { this.ButtonActionNamesChanged(); }); // 短暂高亮后恢复
                    PluginLog.Info($"[{this.DisplayName}] 动态列表项 '{itemConfig.DisplayName}' (localId: '{commandLocalIdToLookup}') 被按下。");
                    return;
                }
            }
            // 3. 检查是否为静态按钮 (如果 _isButtonListDynamic == false)
            else 
            {
                if (this._localIdToConfig_Buttons.TryGetValue(commandLocalIdToLookup, out var staticButtonConfig))
                {
                    if (!this._localIdToGlobalActionParameter_Buttons.TryGetValue(commandLocalIdToLookup, out var globalButtonParam))
                    {
                        PluginLog.Warning($"[{this.DisplayName}] RunCommand: 未找到静态按钮 localId '{commandLocalIdToLookup}' 对应的 globalActionParameter。");
                        return;
                    }

                    Logic_Manager_Base.Instance.ProcessUserAction(globalButtonParam, this.DisplayName, staticButtonConfig);

                    // UI反馈 (基于旧 Dynamic_Folder_Base 的逻辑)
                    if (staticButtonConfig.ActionType == "TriggerButton")
                    {
                        this._lastTriggerPressTimes[commandLocalIdToLookup] = DateTime.Now;
                        this.ButtonActionNamesChanged();
                        Task.Delay(200).ContinueWith(_ => { this.ButtonActionNamesChanged(); }); 
                    }
                    else if (staticButtonConfig.ActionType == "CombineButton") 
                    {
                        this._lastCombineButtonPressTimes[commandLocalIdToLookup] = DateTime.Now;
                        this.ButtonActionNamesChanged();
                        Task.Delay(200).ContinueWith(_ => { this.ButtonActionNamesChanged(); }); 
                    }
                    else if (staticButtonConfig.ActionType == "ToggleButton" || staticButtonConfig.ActionType == "ParameterButton")
                    {
                        // ToggleButton 和 ParameterButton 的状态由 Logic_Manager 通过 CommandStateNeedsRefresh 触发刷新
                        // 但如果按下动作本身也需要即时反馈（即使状态可能没变），可以调用 ButtonActionNamesChanged()
                        this.ButtonActionNamesChanged(); 
                    }
                    PluginLog.Info($"[{this.DisplayName}] 静态按钮 '{staticButtonConfig.DisplayName}' (localId: '{commandLocalIdToLookup}') 被按下。");
                    return;
                }
            }
            
            // 如果以上均未匹配
            if(dialConfigPressed == null) // 仅当它不是一个可识别的旋钮按下时才报告此最终警告
            {
                 PluginLog.Warning($"[{this.DisplayName}] RunCommand: 未找到与 localId '{commandLocalIdToLookup}' 匹配的旋钮、动态列表项或静态按钮配置。");
            }
            // 如果是旋钮按下但未在前面return，则意味着该旋钮按下无特定RunCommand逻辑，此处不再重复警告
        }

        public override Boolean ProcessButtonEvent2(String actionParameter, DeviceButtonEvent2 buttonEvent)
        {
            // 此方法可以处理更底层的按钮事件。
            // 主要确保 "Back" 导航按钮的按下事件能可靠地关闭文件夹。

            if (buttonEvent.EventType == DeviceButtonEventType.Press)
            {
                string commandLocalIdToLookup = actionParameter;
                const string commandPrefix = "plugin:command:"; 
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
        public override BitmapImage GetCommandImage(string actionParameter, PluginImageSize imageSize)
        {
            string localId = actionParameter; // SDK传入的通常已经是localId
            const string commandPrefix = "plugin:command:"; 
            if (actionParameter.StartsWith(commandPrefix))
            {
                localId = actionParameter.Substring(commandPrefix.Length);
            }

            // 处理 NavigateUpActionName (Loupedeck SDK 自动添加的返回上一级文件夹的按钮)
            if (localId.Equals(NavigateUpActionName))
            {
                // 可以选择让基类处理，或者提供一个自定义的"向上"图标
                using(var bb = new BitmapBuilder(imageSize))
                {
                    bb.Clear(BitmapColor.Black);
                    // 简单绘制文字，也可以加载一个嵌入的"向上箭头"图标
                    bb.DrawText("Up", BitmapColor.White, GetButtonFontSize("Up")); 
                    return bb.ToImage();
                }
            }

            ButtonConfig configToDraw = null;
            string globalParamForState = null; // 用于获取ToggleButton等的状态
            bool isStaticButton = false;

            if (this._isButtonListDynamic)
            {
                // 对于动态列表项，localId 此时应为 CreateActionParameterForItem 生成的 "/GroupName/DisplayName" 格式
                if (!this._allListItems.TryGetValue(localId, out configToDraw))
                {
                    PluginLog.Warning($"[{this.DisplayName}|GetCommandImage] 未在 _allListItems 中找到动态列表项配置 for localId/actionParameter '{localId}'.");
                    return PluginImage.DrawElement(imageSize, null, "Itm?", isActive:true); 
                }
            }
            else // 静态按钮列表
            {
                isStaticButton = true;
                if (!this._localIdToConfig_Buttons.TryGetValue(localId, out configToDraw) || 
                    !this._localIdToGlobalActionParameter_Buttons.TryGetValue(localId, out globalParamForState))
                {
                    PluginLog.Warning($"[{this.DisplayName}|GetCommandImage] 未在 _localIdToConfig_Buttons 中找到静态按钮配置 for localId '{localId}'.");
                    return PluginImage.DrawElement(imageSize, null, "Btn?", isActive:true); 
                }
            }

            if (configToDraw == null) // 双重检查，理论上不应发生
            {
                return PluginImage.DrawElement(imageSize, null, "Err", isActive:true);
            }

            // 准备 PluginImage.DrawElement 参数
            BitmapImage customIcon = null;
            string mainTitleOverride = null; // 通常让 PluginImage 内部处理标题
            string valueText = null;       // 按钮通常不直接显示valueText
            bool isActive = false;
            int currentModeForDrawing = 0; // 按钮的模式通常不影响主要绘制，除非特定类型

            // 1. 加载自定义图标 (如果配置了)
            string imagePathToLoad = !String.IsNullOrEmpty(configToDraw.ButtonImage) ? configToDraw.ButtonImage : null;
            if (string.IsNullOrEmpty(imagePathToLoad) && !string.IsNullOrEmpty(configToDraw.DisplayName)) // 如果没有明确指定ButtonImage，尝试根据DisplayName推断
            {
                imagePathToLoad = $"{Logic_Manager_Base.SanitizeOscPathSegment(configToDraw.DisplayName)}.png";
            }

            if (!string.IsNullOrEmpty(imagePathToLoad))
            {
                try
                {
                    customIcon = PluginResources.ReadImage(imagePathToLoad);
                }
                catch (Exception ex)
                {
                    PluginLog.Warning(ex, $"[{this.DisplayName}|GetCommandImage] 加载图标 '{imagePathToLoad}' 失败 for localId '{localId}'. 将只绘制文本。");
                    customIcon = null;
                }
            }

            // 2. 确定 isActive 状态
            if (configToDraw.ActionType == "TriggerButton" || configToDraw.ActionType == "CombineButton")
            {
                DateTime pressTime = DateTime.MinValue; // 初始化 pressTime
                bool foundInDynamic = !isStaticButton && this._lastPressTimes.TryGetValue(localId, out pressTime);
                // 如果 foundInDynamic 为 true，pressTime 已被赋值，后续 TryGetValue 的 out pressTime 会覆盖它
                // 如果为 false，pressTime 仍为 MinValue，然后下一个 TryGetValue 会尝试赋值
                bool foundInStaticTrigger = isStaticButton && configToDraw.ActionType == "TriggerButton" && this._lastTriggerPressTimes.TryGetValue(localId, out pressTime);
                bool foundInStaticCombine = isStaticButton && configToDraw.ActionType == "CombineButton" && this._lastCombineButtonPressTimes.TryGetValue(localId, out pressTime);

                if (foundInDynamic || foundInStaticTrigger || foundInStaticCombine)
                {
                    // 此时 pressTime 持有的是最后一个成功的 TryGetValue 的结果，或者如果都失败则是初始化的 MinValue
                    // 但由于外层if判断，能进入此块，说明至少有一个为true，pressTime已被有效赋值。
                    if ((DateTime.Now - pressTime).TotalMilliseconds < 200)
                    {
                        isActive = true; // 用于瞬时高亮
                    }
                }
            }
            else if (configToDraw.ActionType == "ToggleButton")
            {
                if (isStaticButton && !string.IsNullOrEmpty(globalParamForState))
                {
                    isActive = Logic_Manager_Base.Instance.GetToggleState(globalParamForState);
                }
                // else: 动态列表中的ToggleButton，其状态可能也需要通过Logic_Manager或特定机制获取，这里暂时不处理
                // 如果动态列表项也可以是ToggleButton并由LogicManager管理状态，那么ProcessUserAction时传递的actionParameter应该是全局的
            }
            // ParameterButton 的主标题会动态变化，isActive通常不直接影响其背景，更多是通过标题颜色或特定图标
            else if (configToDraw.ActionType == "ParameterButton" && isStaticButton)
            {
                // ParameterButton 的显示标题由其源 ParameterDial 决定
                // isActive状态可以设为false，除非有特定高亮逻辑
                var sourceDialGlobalParam = this.FindSourceDialGlobalActionParameter(configToDraw, configToDraw.ParameterSourceDial);
                mainTitleOverride = !string.IsNullOrEmpty(sourceDialGlobalParam)
                    ? (Logic_Manager_Base.Instance.GetParameterDialSelectedTitle(sourceDialGlobalParam) ?? configToDraw.Title ?? configToDraw.DisplayName)
                    : $"Err:{configToDraw.ParameterSourceDial?.Substring(0, Math.Min(configToDraw.ParameterSourceDial?.Length ?? 0, 4))}";
            }
            // 其他类型的按钮 (如 SelectModeButton) 的 isActive 状态和标题可能更复杂，依赖于模式等
            // PluginImage.DrawElement 内部已有一些针对 SelectModeButton 的处理逻辑

            return PluginImage.DrawElement(
                imageSize,
                configToDraw,
                mainTitleOverride, 
                valueText,
                isActive,
                currentModeForDrawing,
                customIcon
            );
        }

        // 辅助方法，用于根据标题长度自动获取按钮的字体大小 (如果PluginImage需要，但它内部有自己的)
        // 此方法可以从旧 Dynamic_Folder_Base 迁移，但 PluginImage.GetButtonFontSize 更通用
        private static int GetButtonFontSize(String title) { if (String.IsNullOrEmpty(title)) return 23; var len = title.Length; return len switch { 1 => 38, 2 => 33, 3 => 31, 4 => 26, 5 => 23, 6 => 22, 7 => 20, 8 => 18, _ => 16 }; }

        // --- IV. 辅助方法与事件处理 ---
        // (GetLocalDialId, CreateActionParameterForItem, FindSourceDialGlobalActionParameter 已实现)

        private void OnCommandStateNeedsRefresh(object sender, string globalActionParameterThatChanged)
        {
            if (string.IsNullOrEmpty(globalActionParameterThatChanged))
            {
                return;
            }

            // 检查是否为本文件夹管理的静态按钮相关的状态变化
            var staticButtonEntry = this._localIdToGlobalActionParameter_Buttons.FirstOrDefault(kvp => kvp.Value == globalActionParameterThatChanged);
            if (!EqualityComparer<KeyValuePair<string, string>>.Default.Equals(staticButtonEntry, default(KeyValuePair<string, string>)))
            {
                var localButtonId = staticButtonEntry.Key;
                if (this._localIdToConfig_Buttons.TryGetValue(localButtonId, out var buttonConfig))
                {
                    PluginLog.Info($"[{this.DisplayName}] OnCommandStateNeedsRefresh: 静态按钮 '{buttonConfig.DisplayName}' (localId: {localButtonId}, global: {globalActionParameterThatChanged}) 状态改变，触发UI刷新。");
                    this.ButtonActionNamesChanged(); // 通知SDK按钮可能需要重绘

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

                if (!EqualityComparer<KeyValuePair<string, ButtonConfig>>.Default.Equals(globalDialKvp, default(KeyValuePair<string, ButtonConfig>)) && globalDialKvp.Key == globalActionParameterThatChanged)
                {
                    var localDialId = this.GetLocalDialId(dialConfig);
                    if (!string.IsNullOrEmpty(localDialId))
                    {
                        PluginLog.Info($"[{this.DisplayName}] OnCommandStateNeedsRefresh: 旋钮 '{dialConfig.DisplayName}' (localId: {localDialId}, global: {globalActionParameterThatChanged}) 状态改变，触发UI刷新。");
                        this.AdjustmentValueChanged(this.CreateAdjustmentName(localDialId));

                        // 如果是ParameterDial改变，需要检查是否有链接的ParameterButton在此文件夹内并刷新它们
                        if (dialConfig.ActionType == "ParameterDial")
                        {
                            bool linkedButtonNeedsRefresh = false;
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
                            
                            if(linkedButtonNeedsRefresh) this.ButtonActionNamesChanged();
                        }
                        return; // 处理完毕
                    }
                }
            }
            // PluginLog.Verbose($"[{this.DisplayName}] OnCommandStateNeedsRefresh: globalActionParameter '{globalActionParameterThatChanged}' 与当前文件夹管理的任何已知项不直接匹配。");
        }

        public override BitmapImage GetAdjustmentImage(string actionParameter, PluginImageSize imageSize)
        {
            if (string.IsNullOrEmpty(actionParameter))
            {
                PluginLog.Verbose($"[{this.DisplayName}|GetAdjustmentImage] actionParameter 为空，绘制空白图像。");
                using (var bb = new BitmapBuilder(imageSize)) { bb.Clear(BitmapColor.Black); return bb.ToImage(); }
            }

            var localDialId = actionParameter; // SDK 传入的就是 localId
            var dialConfig = this._folderDialConfigs.FirstOrDefault(dc => this.GetLocalDialId(dc) == localDialId);

            if (dialConfig == null) 
            {
                PluginLog.Verbose($"[{this.DisplayName}|GetAdjustmentImage] 未找到旋钮配置 for localId: '{localDialId}'. 绘制空白图像。");
                using (var bb = new BitmapBuilder(imageSize)) { bb.Clear(BitmapColor.Black); return bb.ToImage(); }
            }

            if (dialConfig.ActionType == "Placeholder")
            {
                PluginLog.Verbose($"[{this.DisplayName}|GetAdjustmentImage] 配置 for localId: '{localDialId}' 是 Placeholder 类型。绘制空白图像。");
                using (var bb = new BitmapBuilder(imageSize)) { bb.Clear(BitmapColor.Black); return bb.ToImage(); }
            }
            
            string mainTitleToDraw = dialConfig.Title ?? dialConfig.DisplayName;
            // 如果 ShowTitle 配置为 "No"，则不显示主标题，让valueText占据主要位置 (如果valueText存在)
            if (dialConfig.ShowTitle?.Equals("No", StringComparison.OrdinalIgnoreCase) == true)
            {
                mainTitleToDraw = null; 
            }

            string valueTextToDisplay = null; 
            bool isActiveStatus = false; 
            int currentModeStatus = 0;    
            
            // 尝试获取此旋钮在LogicManager中注册的全局参数 (如果它需要从LogicManager获取状态)
            string globalParamForState = null;
            var globalParamKvp = Logic_Manager_Base.Instance.GetAllConfigs().FirstOrDefault(kvp => 
                (kvp.Value.GroupName == this.DisplayName || kvp.Value.GroupName == dialConfig.GroupName) && 
                kvp.Value.DisplayName == dialConfig.DisplayName && 
                kvp.Value.ActionType == dialConfig.ActionType
            );
            if (!EqualityComparer<KeyValuePair<string, ButtonConfig>>.Default.Equals(globalParamKvp, default(KeyValuePair<string, ButtonConfig>)))
            {
                globalParamForState = globalParamKvp.Key;
            }

            switch (dialConfig.ActionType)
            {
                case "FilterDial":
                    valueTextToDisplay = this._currentFilterValues.TryGetValue(dialConfig.DisplayName, out var val) ? val : "N/A";
                    break;
                case "PageDial":
                    valueTextToDisplay = $"{this._currentPage + 1} / {this._totalPages}";
                    break;
                case "ParameterDial":
                    if (this._parameterDialCurrentIndexes.TryGetValue(localDialId, out var currentIndex) && 
                        dialConfig.Parameter != null && currentIndex >= 0 && currentIndex < dialConfig.Parameter.Count)
                    {
                        valueTextToDisplay = dialConfig.Parameter[currentIndex];
                    }
                    else
                    {
                        valueTextToDisplay = dialConfig.Parameter?.FirstOrDefault() ?? "N/A"; // Fallback
                    }
                    break;
                case "ToggleDial":
                    if(globalParamForState != null) isActiveStatus = Logic_Manager_Base.Instance.GetToggleState(globalParamForState);
                    else PluginLog.Warning($"[{this.DisplayName}|GetAdjustmentImage] ToggleDial '{dialConfig.DisplayName}' 未在LogicManager中找到全局状态。");
                    // ToggleDial 通常不显示valueText，而是通过isActive状态改变背景/标题颜色
                    break;
                case "2ModeTickDial":
                    if(globalParamForState != null) currentModeStatus = Logic_Manager_Base.Instance.GetDialMode(globalParamForState);
                    else PluginLog.Warning($"[{this.DisplayName}|GetAdjustmentImage] 2ModeTickDial '{dialConfig.DisplayName}' 未在LogicManager中找到全局状态。");
                    // 2ModeTickDial 的标题和颜色由 PluginImage.DrawElement 根据 currentMode 处理
                    break;
                // NavigationDial, TickDial 等类型会使用默认的 mainTitleToDraw 和空的 valueTextToDisplay
                // PluginImage.DrawElement 会根据 dialConfig.ShowTitle 和 valueTextToDisplay 是否为空来决定如何绘制
            }
            
            return PluginImage.DrawElement(
                imageSize,
                dialConfig, 
                mainTitleToDraw,    // 传递处理过的标题
                valueTextToDisplay, // 传递要显示的值
                isActiveStatus,     // 传递 ToggleDial 的状态
                currentModeStatus,  // 传递 2ModeTickDial 的模式
                null,               // customIcon - 旋钮通常无自定义图标
                forceTextOnly: true, // 旋钮通常强制纯文本显示
                actualAuxText: null // 旋钮通常无辅助文本
            );
        }

        public override string GetCommandDisplayName(string actionParameter, PluginImageSize imageSize)
        {
            string localId = actionParameter;
            const string commandPrefix = "plugin:command:";
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

        public override string GetAdjustmentDisplayName(string actionParameter, PluginImageSize imageSize) => null; // 旋钮名称直接绘制在图像上

        public override string GetAdjustmentValue(string actionParameter) => null; // 旋钮值直接绘制在图像上

        public override BitmapImage GetButtonImage(PluginImageSize imageSize)
        {
            if (this._entryConfig == null) 
            {
                PluginLog.Warning($"[{this.DisplayName ?? "UnknownFolder"}|GetButtonImage] 文件夹入口配置 (_entryConfig) 为空。绘制默认文本图像。");
                // 提供一个默认的文件夹入口图像，如果_entryConfig未加载
                using (var bb = new BitmapBuilder(imageSize))
                {
                    bb.Clear(BitmapColor.Black);
                    // 使用文件夹自身的DisplayName (如果可用)，否则使用一个通用占位符
                    var title = !string.IsNullOrEmpty(this.DisplayName) ? this.DisplayName : "Folder";
                    bb.DrawText(title, BitmapColor.White, GetButtonFontSize(title)); 
                    return bb.ToImage();
                }
            }
            
            // 使用 PluginImage.DrawElement 绘制文件夹入口按钮
            // 通常，文件夹入口按钮不显示动态值 (valueText = null),
            // isActive 和 currentMode 通常也为 false/0，除非有特殊设计。
            // customIcon 可以通过 _entryConfig.ButtonImage 加载 (如果 PluginImage.DrawElement 支持的话，或在这里预加载)
            // PluginImage.DrawElement 内部会处理 _entryConfig 的 Title, DisplayName, BackgroundColor, TitleColor, Text 等属性。

            BitmapImage entryIcon = null;
            if (!string.IsNullOrEmpty(this._entryConfig.ButtonImage))
            {
                try 
                {
                    entryIcon = PluginResources.ReadImage(this._entryConfig.ButtonImage);
                }
                catch (Exception ex)
                {
                    PluginLog.Warning(ex, $"[{this.DisplayName}|GetButtonImage] 加载文件夹入口图标 '{this._entryConfig.ButtonImage}' 失败。");
                    entryIcon = null;
                }
            }

            return PluginImage.DrawElement(
                imageSize,
                this._entryConfig,
                mainTitleOverride: null, // PluginImage会使用_entryConfig.Title或DisplayName
                valueText: null,         
                isActive: false,         
                currentMode: 0,          
                customIcon: entryIcon, // 传递预加载的图标
                forceTextOnly: false,
                actualAuxText: null      // PluginImage会使用_entryConfig.Text
            );
        }

        // 从旧 Dynamic_Folder_Base 迁移，用于 ParameterButton 查找其数据源 ParameterDial
        private string FindSourceDialGlobalActionParameter(ButtonConfig parameterButtonConfig, string sourceDialDisplayNameFromButtonConfig)
        {
            if (parameterButtonConfig == null || string.IsNullOrEmpty(parameterButtonConfig.GroupName) || string.IsNullOrEmpty(sourceDialDisplayNameFromButtonConfig))
            {
                PluginLog.Warning($"[{this.DisplayName}] FindSourceDialGlobal: 无效的参数。ParameterButtonConfig 或其 GroupName 为空，或 sourceDialDisplayName 为空。");
                return null;
            }

            var allConfigs = Logic_Manager_Base.Instance.GetAllConfigs();
            var foundDialEntry = allConfigs.FirstOrDefault(kvp => 
                kvp.Value.DisplayName == sourceDialDisplayNameFromButtonConfig &&
                kvp.Value.GroupName == parameterButtonConfig.GroupName && 
                kvp.Value.ActionType == "ParameterDial");

            if (!EqualityComparer<KeyValuePair<string, ButtonConfig>>.Default.Equals(foundDialEntry, default(KeyValuePair<string, ButtonConfig>)) && !string.IsNullOrEmpty(foundDialEntry.Key))
            {
                return foundDialEntry.Key;
            }
            
            PluginLog.Warning($"[{this.DisplayName}] FindSourceDialGlobal: ParameterButton '{parameterButtonConfig.DisplayName}' (JSON Group: '{parameterButtonConfig.GroupName}') 未能找到源 ParameterDial，其 DisplayName 为 '{sourceDialDisplayNameFromButtonConfig}'.");
            return null;
        }
    }
} 