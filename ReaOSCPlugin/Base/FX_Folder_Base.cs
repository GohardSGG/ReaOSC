// 文件名: Base/FX_Folder_Base.cs
// 描述: 这是为效果器浏览器这类具有复杂过滤和分页逻辑的动态文件夹创建的基类。
// 它封装了品牌/类型过滤、分页、自定义UI绘制等所有通用逻辑。
// 【重构】此类将适配新的动态文件夹配置及数据源加载方式。

namespace Loupedeck.ReaOSCPlugin.Base
{
    using System;
    using System.Collections.Generic;
    // using System.Globalization; // 根据后续使用情况决定是否保留
    // using System.IO; // 可能不再需要直接IO操作
    using System.Linq;
    // using System.Reflection; // 可能不再需要
    using System.Text.RegularExpressions; // 【新增】为 SanitizeForOsc 方法
    using System.Threading.Tasks;
    using System.Globalization; // 【新增】为 HexToBitmapColor

    // using Newtonsoft.Json; // 如果JObject处理足够，可能不需要直接用JsonConvert在此类中
    using Newtonsoft.Json.Linq; // 用于处理数据源

    public abstract class FX_Folder_Base : PluginDynamicFolder
    {
        // --- 配置与数据源 ---
        private ButtonConfig _entryConfig; // 文件夹入口配置 (来自Dynamic_List.json中本文件夹的定义)
        private JObject _dataSourceJson;   // 完整的数据源 (例如Effect_List.json的内容)

        // --- 动态列表项管理 ---
        // 【新】存储所有从数据源解析的动态列表项 (ButtonConfig需要有FilterableProperties)
        private readonly Dictionary<string, ButtonConfig> _allListItems = new Dictionary<string, ButtonConfig>(); 
        // 【新】当前筛选和分页后显示的项的ActionParameter列表
        private List<string> _currentDisplayedItemActionNames = new List<string>(); 

        // --- 过滤器管理 ---
        // 【新】存储此文件夹在Dynamic_List.json中定义的旋钮配置
        private readonly List<ButtonConfig> _folderDialConfigs = new List<ButtonConfig>(); 
        // 【新】键: FilterDial的DisplayName, 值: 该过滤器的选项列表
        private readonly Dictionary<string, List<string>> _filterOptions = new Dictionary<string, List<string>>(); 
        // 【新】键: FilterDial的DisplayName, 值: 该过滤器的当前选中值
        private readonly Dictionary<string, string> _currentFilterValues = new Dictionary<string, string>(); 
         // 【新】标记为主过滤器的FilterDial的DisplayName (ButtonConfig需要有方法获取BusFilter标记)
        private string _busFilterDialDisplayName = null;
        // 【新】收藏项的DisplayName列表
        private List<string> _favoriteItemDisplayNames = new List<string>();

        // --- 分页管理 ---
        private int _currentPage = 0; // （保留）
        private int _totalPages = 1;  // （保留）
        
        // --- UI反馈辅助 ---
        private readonly Dictionary<string, DateTime> _lastPressTimes = new Dictionary<string, DateTime>(); // （保留） 用于按钮按下瞬时反馈

        public FX_Folder_Base()
        {
            var folderClassName = this.GetType().Name;
            var folderBaseName = folderClassName.Replace("_Dynamic", "").Replace("_", " ");
            this._entryConfig = Logic_Manager_Base.Instance.GetConfigByDisplayName("Dynamic", folderBaseName);

            if (this._entryConfig == null)
            {
                PluginLog.Error($"[FX_Folder_Base] Constructor: 未能在 Logic_Manager 中找到文件夹入口 '{folderBaseName}' 的配置项。");
                this.DisplayName = folderBaseName;
                this.GroupName = "Dynamic";
                return;
            }

            this.DisplayName = this._entryConfig.DisplayName;
            this.GroupName = this._entryConfig.GroupName ?? "Dynamic";

            // 【新】获取此文件夹定义的旋钮配置
            var folderContentConfig = Logic_Manager_Base.Instance.GetFolderContent(this.DisplayName);
            if (folderContentConfig != null && folderContentConfig.Dials != null)
            {
                this._folderDialConfigs.AddRange(folderContentConfig.Dials);
            }
            else
            {
                PluginLog.Warning($"[FX_Folder_Base] Constructor for '{this.DisplayName}': 未能从 Logic_Manager 获取到旋钮配置。");
            }
            
            // 【新】获取数据源
            this._dataSourceJson = Logic_Manager_Base.Instance.GetFxData(this.DisplayName);
            if (this._dataSourceJson == null)
            {
                PluginLog.Error($"[FX_Folder_Base] Constructor for '{this.DisplayName}': 未能从 Logic_Manager 获取数据源。文件夹将为空。");
                return; // 后续Initialize会失败，列表为空
            }

            // 【新】初始化过滤器选项、解析列表项数据
            this.InitializeFiltersAndParseListData();

            // 【新增日志】确认Back旋钮的预期localId
            var backDialForLog = this._folderDialConfigs.FirstOrDefault(dc => dc.ActionType == "NavigationDial" && dc.DisplayName == "Back");
            if (backDialForLog != null)
            {
                // GroupName 应该是由 Logic_Manager_Base 在 ProcessFolderContentConfigs -> RegisterConfigs 中赋予的文件夹名
                PluginLog.Info($"[{this.DisplayName}] Constructor: 'Back' Dial Config loaded with GroupName='{backDialForLog.GroupName}', DisplayName='{backDialForLog.DisplayName}'.");
                var expectedLocalId = $"{backDialForLog.GroupName}_{backDialForLog.DisplayName}".Replace(" ", "_");
                PluginLog.Info($"[{this.DisplayName}] Constructor: 'Back' Dial expected localId for RunCommand/ApplyAdjustment: '{expectedLocalId}'.");
            }
            else
            {
                PluginLog.Warning($"[{this.DisplayName}] Constructor: 'Back' Dial Config NOT FOUND in _folderDialConfigs.");
            }
        }

        /// <summary>
        /// 【新】初始化过滤器、加载收藏夹、并从数据源解析所有动态列表项。
        /// </summary>
        private void InitializeFiltersAndParseListData()
        {
            PluginLog.Info($"[{this.DisplayName}] 开始初始化过滤器和解析列表数据...");
            if (this._dataSourceJson == null)
            {
                PluginLog.Error($"[{this.DisplayName}] InitializeFiltersAndParseListData: _dataSourceJson 为空，无法继续。");
                this.UpdateDisplayedItemsList(); // 确保列表状态被设置为空
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

            foreach (var dialConfig in this._folderDialConfigs)
            {
                if (dialConfig.ActionType == "FilterDial")
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

                        // 检查是否为 BusFilter 
                        bool isBusFilter = !string.IsNullOrEmpty(dialConfig.BusFilter) && dialConfig.BusFilter.Equals("Yes", StringComparison.OrdinalIgnoreCase);

                        if (this._favoriteItemDisplayNames.Any() && !options.Contains("Favorite"))
                        {
                            // 如果是BusFilter，或者（如果没有BusFilter）它是列表中的第一个FilterDial，则添加Favorite选项
                            bool shouldAddFavorite = isBusFilter || 
                                                     (this._busFilterDialDisplayName == null && 
                                                      _folderDialConfigs.Where(dc => dc.ActionType == "FilterDial").FirstOrDefault() == dialConfig);
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
            }
            
            // --- 第3步: 解析动态列表项 ---
            this._allListItems.Clear();
            var processedTopLevelKeys = new HashSet<string> { "Favorite", "All" }; 
            foreach (var filterNameInDataSource in this._filterOptions.Keys)
            {
                processedTopLevelKeys.Add(filterNameInDataSource); 
            }

            foreach (var property in this._dataSourceJson.Properties())
            {
                var topLevelKey = property.Name; 
                if (processedTopLevelKeys.Contains(topLevelKey))
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

                                itemConfig.GroupName = topLevelKey; 

                                if (itemConfig.FilterableProperties == null) // 确保字典已初始化 (ButtonConfig类应负责此)
                                {
                                    itemConfig.FilterableProperties = new Dictionary<string, string>();
                                }

                                foreach (var subFilterName in this._filterOptions.Keys)
                                {
                                    if (subFilterName == this._busFilterDialDisplayName) 
                                        continue;

                                    if (itemJObject.TryGetValue(subFilterName, out JToken propValToken) && propValToken.Type != JTokenType.Null)
                                    {
                                        itemConfig.FilterableProperties[subFilterName] = propValToken.ToString();
                                    }
                                }
                                
                                string actionParameter = this.CreateActionParameter(itemConfig);
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
            this.UpdateDisplayedItemsList();
            PluginLog.Info($"[{this.DisplayName}] 完成初始化过滤器和解析列表数据。");
        }

        /// <summary>
        /// 【新】根据当前过滤器设置，更新当前可显示的动态列表项，并处理分页。
        /// </summary>
        private void UpdateDisplayedItemsList()
        {
            PluginLog.Info($"[{this.DisplayName}] 开始更新显示列表...");
            IEnumerable<ButtonConfig> itemsToDisplay = this._allListItems.Values;

            bool favoriteFilterActive = false;
            if (!string.IsNullOrEmpty(this._busFilterDialDisplayName) &&
                this._currentFilterValues.TryGetValue(this._busFilterDialDisplayName, out var busFilterValue) &&
                busFilterValue == "Favorite")
            {
                if (this._favoriteItemDisplayNames.Any())
                {
                    itemsToDisplay = itemsToDisplay.Where(item => this._favoriteItemDisplayNames.Contains(item.DisplayName));
                    favoriteFilterActive = true;
                    PluginLog.Info($"[{this.DisplayName}] 应用Favorite过滤器。");
                }
                else
                {
                    itemsToDisplay = Enumerable.Empty<ButtonConfig>();
                    favoriteFilterActive = true;
                    PluginLog.Info($"[{this.DisplayName}] Favorite过滤器激活，但收藏列表为空。");
                }
            }

            if (!favoriteFilterActive && !string.IsNullOrEmpty(this._busFilterDialDisplayName) &&
                this._currentFilterValues.TryGetValue(this._busFilterDialDisplayName, out busFilterValue) && // Re-fetch, could be non-Favorite
                busFilterValue != "All") 
            {
                itemsToDisplay = itemsToDisplay.Where(item => item.GroupName == busFilterValue);
                PluginLog.Info($"[{this.DisplayName}] 应用BusFilter '{this._busFilterDialDisplayName}' = '{busFilterValue}'");
            }

            foreach (var filterEntry in this._currentFilterValues)
            {
                var filterName = filterEntry.Key;
                var selectedValue = filterEntry.Value;

                if (filterName == this._busFilterDialDisplayName) 
                    continue;
                
                if (selectedValue != "All")
                {
                    itemsToDisplay = itemsToDisplay.Where(item =>
                        item.FilterableProperties != null &&
                        item.FilterableProperties.TryGetValue(filterName, out var propVal) &&
                        propVal.Equals(selectedValue, StringComparison.OrdinalIgnoreCase) // Case-insensitive compare for property values
                    );
                    PluginLog.Info($"[{this.DisplayName}] 应用次级过滤器 '{filterName}' = '{selectedValue}'");
                }
            }

            this._currentDisplayedItemActionNames = itemsToDisplay.Select(config => this.CreateActionParameter(config)).ToList();
            
            this._totalPages = (int)Math.Ceiling(this._currentDisplayedItemActionNames.Count / 12.0); 
            if (this._totalPages == 0) this._totalPages = 1;
            this._currentPage = Math.Min(this._currentPage, this._totalPages - 1);
            if (this._currentPage < 0) this._currentPage = 0;

            PluginLog.Info($"[{this.DisplayName}] 列表更新完毕。当前显示 {_currentDisplayedItemActionNames.Count} 项。总页数: {this._totalPages}, 当前页: {this._currentPage + 1}");
        }

        // --- 旧的成员变量 (已被替换或移除) ---
        // private string _currentBrandFilter = "Favorite"; 
        // private string _currentEffectTypeFilter = "All"; 
        // private readonly List<string> _brandOptions; 
        // private readonly List<string> _effectTypeOptions = new List<string> { "All", "EQ", "Comp", "Reverb", "Delay", "Saturate" };
        // private readonly Dictionary<string, ButtonConfig> _allFxConfigs = new Dictionary<string, ButtonConfig>(); 
        // private List<string> _currentFxActionNames = new List<string>(); 
        // private List<string> _favoriteFxNames = new List<string>(); 
        // private List<string> _originalBrandOrder = null; 

        // --- 旧的方法 (已被替换或移除) ---
        // private void ParseFxData(JObject root) { /* ... */ }
        // private void UpdateFxList() { /* ... */ }

        // ... (其他方法如 GetButtonPressActionNames, ApplyAdjustment 等将需要后续修改) ...
        
        // 辅助方法，用于从 ButtonConfig 创建唯一的动作参数 (通常是 /GroupName/DisplayName)
        private string CreateActionParameter(ButtonConfig config) => $"/{config.GroupName}/{config.DisplayName}".Replace(" ", "_").Replace("//", "/");
        
        public override IEnumerable<string> GetButtonPressActionNames()
        {
            // 已持有ActionParameter列表，无需再CreateCommandName
            return this._currentDisplayedItemActionNames
                .Skip(this._currentPage * 12) // 假设每页12个
                .Take(12)
                .Select(actionParameter => this.CreateCommandName(actionParameter)); // SDK需要完整的命令名
        }
        
        // 【新增】辅助方法，用于生成旋钮的内部localId
        private string GetLocalDialId(ButtonConfig dialConfig)
        {
            if (dialConfig == null) return null;
            // GroupName 可能已由 Logic_Manager_Base 赋予为文件夹的 DisplayName
            var groupName = dialConfig.GroupName ?? this.DisplayName; 
            var displayName = dialConfig.DisplayName ?? ""; // 确保 DisplayName 不为 null
            if (string.IsNullOrEmpty(displayName)) 
            {
                // PluginLog.Warning($"[{this.DisplayName}] GetLocalDialId: DialConfig的DisplayName为空。GroupName: {groupName}");
                return null; // 没有DisplayName无法构成有效ID
            }
            return $"{groupName}_{displayName}".Replace(" ", "_");
        }

        // 【再重构】以确保localId的正确生成和使用
        public override IEnumerable<string> GetEncoderRotateActionNames()
        {
            var rotateActionNames = new string[6]; 
            var assignedSlots = new bool[6];

            var backDialConfig = this._folderDialConfigs.FirstOrDefault(dc => dc.ActionType == "NavigationDial" && dc.DisplayName == "Back");
            if (backDialConfig != null)
            {
                var localBackDialId = GetLocalDialId(backDialConfig);
                if (localBackDialId != null)
                {
                    rotateActionNames[5] = this.CreateAdjustmentName(localBackDialId);
                    assignedSlots[5] = true;
                }
            }

            int currentIndex = 0;
            foreach (var dialConfig in this._folderDialConfigs.Where(dc => dc.ActionType == "FilterDial" || dc.ActionType == "PageDial"))
            {
                while (currentIndex < 6 && assignedSlots[currentIndex]) { currentIndex++; }
                if (currentIndex < 6)
                {
                    var localDialId = GetLocalDialId(dialConfig);
                    if (localDialId != null)
                    {
                        rotateActionNames[currentIndex] = this.CreateAdjustmentName(localDialId);
                        assignedSlots[currentIndex] = true;
                    }
                }
                else { 
                    // PluginLog.Warning($"[{this.DisplayName}] GetEncoderRotateActionNames: 旋钮槽位不足以分配给Filter/PageDial '{dialConfig?.DisplayName}'");
                    break; 
                }
            }

            currentIndex = 0; 
            foreach (var dialConfig in this._folderDialConfigs.Where(dc => dc.ActionType == "PlaceholderDial"))
            {
                while (currentIndex < 6 && assignedSlots[currentIndex]) { currentIndex++; }
                if (currentIndex < 6)
                {
                    var localDialId = GetLocalDialId(dialConfig);
                    if (localDialId != null)
                    {
                        rotateActionNames[currentIndex] = this.CreateAdjustmentName(localDialId); 
                        assignedSlots[currentIndex] = true;
                    }
                }
                else { 
                    // PluginLog.Warning($"[{this.DisplayName}] GetEncoderRotateActionNames: 旋钮槽位不足以分配给PlaceholderDial '{dialConfig?.DisplayName}'");
                    break; 
                }
            }
            return rotateActionNames;
        }
        
        // 【再重构】以确保localId的正确生成和使用
        public override IEnumerable<string> GetEncoderPressActionNames(DeviceType deviceType)
        {
            var pressActionNames = new string[6];
            var tempRotateDialConfigsInOrder = new ButtonConfig[6]; 
            bool[] tempAssignedSlots = new bool[6];
            var assignableDials = this._folderDialConfigs.ToList(); // 创建副本以进行移除操作

            var backDial = assignableDials.FirstOrDefault(dc => dc.ActionType == "NavigationDial" && dc.DisplayName == "Back");
            if (backDial != null)
            {
                tempRotateDialConfigsInOrder[5] = backDial;
                tempAssignedSlots[5] = true;
                assignableDials.Remove(backDial); 
            }

            int slotIndex = 0;
            foreach (var dial in assignableDials.Where(dc => dc.ActionType == "FilterDial" || dc.ActionType == "PageDial").ToList()) // ToList() for safe removal if needed, though not removing here
            {
                while (slotIndex < 6 && tempAssignedSlots[slotIndex]) { slotIndex++; }
                if (slotIndex < 6) { tempRotateDialConfigsInOrder[slotIndex] = dial; tempAssignedSlots[slotIndex] = true; }
                else { break; }
            }

            slotIndex = 0;
            foreach (var dial in assignableDials.Where(dc => dc.ActionType == "PlaceholderDial").ToList())
            {
                while (slotIndex < 6 && tempAssignedSlots[slotIndex]) { slotIndex++; }
                if (slotIndex < 6) { tempRotateDialConfigsInOrder[slotIndex] = dial; tempAssignedSlots[slotIndex] = true; }
                else { break; }
            }

            for (int i = 0; i < 6; i++)
            {
                var dialConfigForSlot = tempRotateDialConfigsInOrder[i];
                if (dialConfigForSlot != null)
                {
                    var localId = GetLocalDialId(dialConfigForSlot);
                    if (localId != null)
                    {
                        if (dialConfigForSlot.ActionType == "NavigationDial" && dialConfigForSlot.DisplayName == "Back")
                        {                            
                            pressActionNames[i] = base.CreateCommandName(localId); 
                        }
                        else
                        {                            
                            pressActionNames[i] = null; 
                        }
                    }
                }
            }
            return pressActionNames;
        }

        public override PluginDynamicFolderNavigation GetNavigationArea(DeviceType _) =>
            PluginDynamicFolderNavigation.None;

        // 【重构】核心旋钮调整逻辑
        public override void ApplyAdjustment(string actionParameter, int ticks)
        {
            var localDialId = actionParameter; 
            PluginLog.Info($"[{this.DisplayName}] ApplyAdjustment received localDialId: '{localDialId}'"); // 【新增日志】

            var dialConfig = this._folderDialConfigs.FirstOrDefault(dc => 
                $"{dc.GroupName}_{dc.DisplayName}".Replace(" ", "_") == localDialId);

            if (dialConfig == null)
            {
                PluginLog.Warning($"[{this.DisplayName}] ApplyAdjustment: 未找到与localId '{localDialId}' 匹配的旋钮配置。");
                return;
            }

            bool listChanged = false;
            bool pageCountChanged = false; // 标记总页数是否改变 (通常由FilterDial导致)

            if (dialConfig.ActionType == "FilterDial")
            {
                var filterName = dialConfig.DisplayName;
                if (this._filterOptions.TryGetValue(filterName, out var options) && options.Any())
                {
                    // 获取当前选中项的索引
                    var currentSelectedValue = this._currentFilterValues.ContainsKey(filterName) ? this._currentFilterValues[filterName] : options[0];
                    var currentIndex = options.IndexOf(currentSelectedValue);
                    if (currentIndex == -1) currentIndex = 0; // 如果当前值不在选项中，默认从第一个开始

                    var newIndex = (currentIndex + ticks + options.Count) % options.Count;
                    this._currentFilterValues[filterName] = options[newIndex];
                    
                    this._currentPage = 0; // 过滤器改变，重置到第一页
                    this.UpdateDisplayedItemsList(); // 这会更新 _totalPages
                    listChanged = true;
                    pageCountChanged = true; 
                    PluginLog.Info($"[{this.DisplayName}] FilterDial '{filterName}' 值变为: {this._currentFilterValues[filterName]}");
                }
            }
            else if (dialConfig.ActionType == "PageDial")
            {
                var newPage = this._currentPage + ticks;
                if (newPage >= 0 && newPage < this._totalPages)
                {
                    this._currentPage = newPage;
                    listChanged = true; // 仅当前页项目列表改变
                    PluginLog.Info($"[{this.DisplayName}] PageDial: 当前页变为 {this._currentPage + 1}");
                }
            }
            else if (dialConfig.ActionType == "NavigationDial" && dialConfig.DisplayName == "Back")
            {
                PluginLog.Info($"[{this.DisplayName}] NavigationDial 'Back' rotated. Closing folder.");
                this.Close();
                return; 
            }
            else if (dialConfig.ActionType == "PlaceholderDial")
            {
                return; // 占位符旋钮，不执行任何操作
            }

            if(listChanged)
            {
                this.ButtonActionNamesChanged(); 
                this.AdjustmentValueChanged(actionParameter); 
            }
            if(pageCountChanged) 
            {
                var pageDialConfig = this._folderDialConfigs.FirstOrDefault(dc => dc.ActionType == "PageDial");
                if(pageDialConfig != null)
                {
                    var pageDialLocalId = $"{pageDialConfig.GroupName}_{pageDialConfig.DisplayName}".Replace(" ", "_");
                    this.AdjustmentValueChanged(this.CreateAdjustmentName(pageDialLocalId)); // 通知PageDial更新其显示(总页数)
                }
            }
        }
        
        // 【再修正】处理按钮命令和旋钮按下命令，确保localId匹配逻辑统一
        public override void RunCommand(string actionParameter) 
        {
            PluginLog.Info($"[{this.DisplayName}] RunCommand received full actionParameter: '{actionParameter}'"); 

            string commandLocalIdToLookup = actionParameter; 
            const string commandPrefix = "plugin:command:";
            if (actionParameter.StartsWith(commandPrefix))
            {
                commandLocalIdToLookup = actionParameter.Substring(commandPrefix.Length);
            }
            PluginLog.Info($"[{this.DisplayName}] RunCommand looking up localId: '{commandLocalIdToLookup}'");

            // 统一查找被按下的旋钮配置
            var dialConfigPressed = this._folderDialConfigs.FirstOrDefault(dc => 
                $"{dc.GroupName}_{dc.DisplayName}".Replace(" ", "_") == commandLocalIdToLookup);

            if (dialConfigPressed != null)
            {
                if (dialConfigPressed.ActionType == "NavigationDial" && dialConfigPressed.DisplayName == "Back")
                {
                    PluginLog.Info($"[{this.DisplayName}] 'Back' NavigationDial (localId: '{commandLocalIdToLookup}') pressed via RunCommand. Closing folder.");
                    this.Close();
                    return;
                }
                // 可以为其他类型的旋钮按下添加行为，如果它们也通过RunCommand触发
                // 例如: Log or specific action for FilterDial press etc.
                // PluginLog.Info($"[{this.DisplayName}] Dial '{dialConfigPressed.DisplayName}' (ActionType: {dialConfigPressed.ActionType}, localId: '{commandLocalIdToLookup}') pressed via RunCommand. No specific action defined beyond this log.");
                // 如果旋钮按下有独立的功能且被RunCommand处理，确保在这里return或者有相应逻辑。
                // 如果没有，允许代码继续尝试在_allListItems中查找（虽然通常旋钮的localId不会与列表项的localId冲突）。
            }
            
            // 如果不是已知的旋钮按下（或旋钮按下无特定操作让代码继续），则尝试作为列表项按钮处理
            if (this._allListItems.TryGetValue(commandLocalIdToLookup, out var itemConfig)) 
            {
                string oscAddress = DetermineOscAddress(itemConfig); 
                ReaOSCPlugin.SendOSCMessage(oscAddress, 1.0f);
                this._lastPressTimes[commandLocalIdToLookup] = DateTime.Now;
                this.ButtonActionNamesChanged(); 
                Task.Delay(200).ContinueWith(_ => this.ButtonActionNamesChanged());
                PluginLog.Info($"[{this.DisplayName}] Item '{itemConfig.DisplayName}' (localId: '{commandLocalIdToLookup}') pressed. OSC: {oscAddress}");
            }
            else if (dialConfigPressed == null) // 只有当它不是一个已识别的旋钮，也不是列表项时，才报此警告
            {
                 PluginLog.Warning($"[{this.DisplayName}] RunCommand: 未在 _allListItems 或 _folderDialConfigs 中找到与 localId '{commandLocalIdToLookup}' 匹配的项。Full ActionParameter: {actionParameter}");
            }
        }

        // 【再修正】ProcessButtonEvent2 - 使用统一的localId匹配逻辑
        public override Boolean ProcessButtonEvent2(String actionParameter, DeviceButtonEvent2 buttonEvent)
        {
            if (buttonEvent.EventType == DeviceButtonEventType.Press)
            {
                string commandLocalIdToLookup = actionParameter;
                const string commandPrefix = "plugin:command:"; 
                if (actionParameter.StartsWith(commandPrefix))
                {
                    commandLocalIdToLookup = actionParameter.Substring(commandPrefix.Length);
                }
                // 这里不打印日志，避免与RunCommand重复，除非用于调试
                // PluginLog.Info($"[{this.DisplayName}] ProcessButtonEvent2 looking up localId: '{commandLocalIdToLookup}'");

                var dialConfigPressed = this._folderDialConfigs.FirstOrDefault(dc => 
                    $"{dc.GroupName}_{dc.DisplayName}".Replace(" ", "_") == commandLocalIdToLookup);

                if (dialConfigPressed != null)
                {
                    if (dialConfigPressed.ActionType == "NavigationDial" && dialConfigPressed.DisplayName == "Back")
                    {
                        PluginLog.Info($"[{this.DisplayName}] ProcessButtonEvent2: 'Back' NavigationDial (localId: '{commandLocalIdToLookup}') pressed. Closing folder.");
                        this.Close();
                        return false; // 事件已处理
                    }
                    // 可以为其他类型的旋钮按键在此处添加特定处理逻辑
                    // if (return false) for other dial types if handled here
                }
            }
            return base.ProcessButtonEvent2(actionParameter, buttonEvent); 
        }

        // ... (ProcessButtonEvent2, GetCommandImage, GetAdjustmentImage, 辅助方法等) ...

        // 【新增】根据ButtonConfig确定OSC地址 (移植自旧版FX_Folder_Base)
        private string DetermineOscAddress(ButtonConfig config)
        {
            var vendorPart = SanitizeForOsc(config.GroupName); 
            var effectPart = config.DisplayName.StartsWith("Add ") 
                ? SanitizeForOsc(config.DisplayName.Substring(4)) 
                : SanitizeForOsc(config.DisplayName);
            return $"/FX/Add/{vendorPart}/{effectPart}";
        }

        // 【新增】清理字符串以用于OSC地址 (移植自旧版FX_Folder_Base)
        private string SanitizeForOsc(string input)
        {
            if (string.IsNullOrEmpty(input)) return "";
            string sanitized = Regex.Replace(input, @"[^a-zA-Z0-9_]+", "_"); 
            sanitized = Regex.Replace(sanitized, @"_{2,}", "_");
            return sanitized.Trim('_');
        }

        // 【重构】根据 _allListItems 中的 ButtonConfig 绘制动态列表项的图像
        public override BitmapImage GetCommandImage(string actionParameter, PluginImageSize imageSize) 
        {
            // actionParameter 是 _allListItems 中的键 (例如 /Eventide/Blackhole)
            if (!this._allListItems.TryGetValue(actionParameter, out var config))
            {
                PluginLog.Warning($"[{this.DisplayName}] GetCommandImage: 未在 _allListItems 中找到配置 for actionParameter '{actionParameter}'. Drawing '?'");
                return DrawErrorImage(imageSize); // 使用辅助方法绘制错误图像
            }
            
            using (var bitmapBuilder = new BitmapBuilder(imageSize))
            {
                // 1. 背景色处理 (包括按下反馈)
                var currentBgColor = BitmapColor.Black; // 默认背景色
                if (this._lastPressTimes.TryGetValue(actionParameter, out var pressTime) && (DateTime.Now - pressTime).TotalMilliseconds < 200)
                {
                    currentBgColor = new BitmapColor(0x50, 0x50, 0x50); // 按下时的背景色
                }
                else if (!String.IsNullOrEmpty(config.BackgroundColor))
                {
                    currentBgColor = HexToBitmapColor(config.BackgroundColor);
                }
                bitmapBuilder.Clear(currentBgColor);

                // 2. 主标题绘制
                var titleToDraw = !String.IsNullOrEmpty(config.Title) ? config.Title : config.DisplayName;
                var titleColor = String.IsNullOrEmpty(config.TitleColor) ? BitmapColor.White : HexToBitmapColor(config.TitleColor);
                var titleFontSize = GetAutomaticButtonTitleFontSize(titleToDraw, config.Text); 
                bitmapBuilder.DrawText(text: titleToDraw, fontSize: titleFontSize, color: titleColor); // SDK DrawText会尝试居中

                // 3. 副文本 (小字) 绘制
                if (!String.IsNullOrEmpty(config.Text))
                {
                    var subTextColor = String.IsNullOrEmpty(config.TextColor) ? BitmapColor.White : HexToBitmapColor(config.TextColor);
                    var subTextSize = config.TextSize ?? GetAutomaticSubTextFontSize(config.Text, titleToDraw.Length > 10); 
                    
                    // 副文本位置和尺寸的默认逻辑：尝试绘制在主标题下方并居中
                    var textX = config.TextX ?? 0; // 如果用0, width为总宽，DrawText会尝试居中
                    var textY = config.TextY ?? (int)(bitmapBuilder.Height * 0.65); // 大约在下方三分之一处开始
                    var textWidth = config.TextWidth ?? bitmapBuilder.Width;
                    var textHeight = config.TextHeight ?? (int)(bitmapBuilder.Height * 0.3); // 占据约30%高度
                    
                    // 使用 Loupedeck SDK 的 DrawText。它本身可能不直接支持复杂的对齐参数。
                    // 居中通常是通过计算x坐标或确保width足够大由其内部实现。
                    // 如果需要更精确的控制，可能需要 MeasureText。
                    bitmapBuilder.DrawText(
                        text: config.Text, 
                        x: textX, 
                        y: textY, 
                        width: textWidth, 
                        height: textHeight, 
                        color: subTextColor, 
                        fontSize: subTextSize
                        // Loupedeck.BitmapTextAlignmentHorizontal.Center, // 移除不支持的参数
                        // Loupedeck.BitmapTextAlignmentVertical.Top
                        );
                }
                return bitmapBuilder.ToImage();
            }
        }

        // 【再修正】GetAdjustmentImage 以使用正确的localId查找
        public override BitmapImage GetAdjustmentImage(string actionParameter, PluginImageSize imageSize) 
        {
            if (String.IsNullOrEmpty(actionParameter)) 
            {
                PluginLog.Warning($"[{this.DisplayName}] GetAdjustmentImage: actionParameter 为空。");
                return DrawErrorImage(imageSize); 
            }

            var localDialId = actionParameter; // SDK传入的actionParameter就是localId
            
            var dialConfig = this._folderDialConfigs.FirstOrDefault(dc => GetLocalDialId(dc) == localDialId);

            if (dialConfig == null)
            {
                PluginLog.Warning($"[{this.DisplayName}] GetAdjustmentImage: 未找到与localId '{localDialId}' 匹配的旋钮配置。ActionParameter: {actionParameter}");
                return DrawErrorImage(imageSize);
            }
            
            string title = dialConfig.Title ?? dialConfig.DisplayName ?? ""; 
            string valueToDisplay = ""; 
            bool showValue = true; 

            switch (dialConfig.ActionType)
            {
                case "FilterDial":
                    valueToDisplay = this._currentFilterValues.TryGetValue(dialConfig.DisplayName, out var val) ? val : "N/A";
                    break;
                case "PageDial":
                    valueToDisplay = $"{this._currentPage + 1} / {this._totalPages}";
                    break;
                case "NavigationDial": 
                    showValue = false;
                    break;
                case "PlaceholderDial":
                    showValue = false; 
                    break;
                default:
                    PluginLog.Warning($"[{this.DisplayName}] GetAdjustmentImage: 未知的旋钮ActionType: '{dialConfig.ActionType}' for '{localDialId}'");
                    showValue = false;
                    break;
            }
            
            using (var bitmapBuilder = new BitmapBuilder(imageSize))
            {
                BitmapColor bgColor = !String.IsNullOrEmpty(dialConfig.BackgroundColor) ? HexToBitmapColor(dialConfig.BackgroundColor) : BitmapColor.Black;
                bitmapBuilder.Clear(bgColor);
                BitmapColor textColor = !String.IsNullOrEmpty(dialConfig.TitleColor) ? HexToBitmapColor(dialConfig.TitleColor) : BitmapColor.White;

                if (!showValue) 
                {
                    var titleFontSize = GetAutomaticDialTitleFontSize(title, isTopTitle: false); 
                    bitmapBuilder.DrawText(title, textColor, titleFontSize);
                }
                else 
                {
                    var titleFontSize = GetAutomaticDialTitleFontSize(title, isTopTitle: true); 
                    bitmapBuilder.DrawText(title, 0, 5, bitmapBuilder.Width, 30, textColor, titleFontSize);
                    
                    var valueFontSize = GetAutomaticDialValueFontSize(valueToDisplay);
                    bitmapBuilder.DrawText(valueToDisplay, 0, 35, bitmapBuilder.Width, bitmapBuilder.Height - 40, textColor, valueFontSize);
                }
                return bitmapBuilder.ToImage();
            }
        }

        // 【移植并调整】十六进制颜色转BitmapColor
        private static BitmapColor HexToBitmapColor(string hex) 
        {
            if (String.IsNullOrEmpty(hex)) return BitmapColor.White;
            try 
            {
                hex = hex.TrimStart('#');
                var r = (byte)Int32.Parse(hex.Substring(0, 2), NumberStyles.HexNumber);
                var g = (byte)Int32.Parse(hex.Substring(2, 2), NumberStyles.HexNumber);
                var b = (byte)Int32.Parse(hex.Substring(4, 2), NumberStyles.HexNumber);
                return new BitmapColor(r, g, b);
            }
            catch 
            {
                return BitmapColor.Red; // 解析失败返回红色
            }
        }

        // 【移植并调整】按钮主标题字体大小 (可用于列表项按钮)
        private static int GetAutomaticButtonTitleFontSize(String title, String subText = null) 
        { 
            if (String.IsNullOrEmpty(title)) return 23; 
            var len = title.Length; 
            bool hasSubText = !String.IsNullOrEmpty(subText);
            if (hasSubText)
            {
                return len switch { 1 => 28, 2 => 26, 3 => 24, 4 => 22, 5 => 20, 6 => 18, 7 => 16, 8 => 15, _ => 14 };
            }
            return len switch { 1 => 38, 2 => 33, 3 => 31, 4 => 26, 5 => 23, 6 => 22, 7 => 20, 8 => 18, _ => 16 }; 
        }

        // 【新增】按钮副文本/小字字体大小
        private static int GetAutomaticSubTextFontSize(String text, bool mainTitleIsLong)
        {
            if (String.IsNullOrEmpty(text)) return 10;
            var len = text.Length;
            int baseSize = mainTitleIsLong ? 10 : 12;
            // 根据文本长度稍微调整基础大小
            if (len <= 5) return baseSize;
            if (len <= 10) return baseSize - 1;
            if (len <= 15) return baseSize - 2;
            return baseSize - 3;
        }

        // 【新增或调整自旧版】旋钮标题字体大小
        private int GetAutomaticDialTitleFontSize(String title, bool isTopTitle = false) 
        { 
            if (String.IsNullOrEmpty(title)) return isTopTitle ? 12 : 16; 
            var totalLengthWithSpaces = title.Length;
            int effectiveLength = totalLengthWithSpaces; // 简化：直接使用总长度
            
            // 旧版FX_Folder_Base有一个更复杂的effectiveLength计算，这里简化处理
            // if (totalLengthWithSpaces <= 8) { effectiveLength = totalLengthWithSpaces; } 
            // else { var words = title.Split(' '); effectiveLength = words.Length > 0 ? words.Max(word => word.Length) : 0; if (effectiveLength == 0 && totalLengthWithSpaces > 0) { effectiveLength = totalLengthWithSpaces; } }
            
            if(isTopTitle) // 用于绘制在值上方的标题，通常较小
                return effectiveLength switch { <= 3 => 14, <= 5 => 13, <= 7 => 12, <= 9 => 11, _ => 10 };
            // 用于单行居中显示的旋钮标题 (例如 Back, Placeholder)
            return effectiveLength switch { <= 3 => 18, <= 5 => 16, <= 7 => 14, <= 9 => 13, _ => 12 };
        }

        // 【新增】旋钮值字体大小
        private int GetAutomaticDialValueFontSize(String value) 
        { 
            if (String.IsNullOrEmpty(value)) return 18; 
            var len = value.Length; 
            // 根据值的长度调整大小
            return len switch { <= 3 => 22, <= 5 => 20, <= 8 => 18, <= 10 => 16, <= 12 => 14, _ => 12 };
        }

        // 【新增】简单的错误图像绘制方法
        private static BitmapImage DrawErrorImage(PluginImageSize imageSize) 
        {
            using (var bb = new BitmapBuilder(imageSize)) 
            {
                bb.Clear(BitmapColor.Black);
                bb.DrawText("?", BitmapColor.Red, (int)(bb.Height * 0.5)); // 使用 bb.Height
                return bb.ToImage();
            }
        }

        // GetAdjustmentDisplayName 和 GetAdjustmentValue 通常对于动态调整的旋钮返回null，由图像直接显示信息
        public override string GetAdjustmentDisplayName(string actionParameter, PluginImageSize imageSize) => null;
        public override string GetAdjustmentValue(string actionParameter) => null;
        
        // GetButtonImage(PluginImageSize) 是文件夹入口按钮的图像
        // ... (其余代码) ...
    }
} 