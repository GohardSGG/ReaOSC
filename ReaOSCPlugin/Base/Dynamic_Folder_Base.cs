// 文件名: Base/Dynamic_Folder_Base.cs
// 【参考用户旧代码进行修正，重点调整PopulateLocalIdMappings并移除IsLoaded和TaskScheduler.FromCurrentSynchronizationContext】
namespace Loupedeck.ReaOSCPlugin.Base
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Threading.Tasks;

    using Newtonsoft.Json;
    using Loupedeck.ReaOSCPlugin.Helpers; // 【新增】引用新的绘图类

    public abstract class Dynamic_Folder_Base : PluginDynamicFolder, IDisposable // 【我之前的版本添加了IDisposable，如果您的旧版没有，可以移除】
    {
        private ButtonConfig _entryConfig;

        // 【旧的旋钮管理方式 - 将被取代或调整用途】
        // private readonly List<string> _localAdjustmentIds = new List<string>();
        // private readonly Dictionary<string, string> _localIdToGlobalActionParameter = new Dictionary<string, string>();
        // private readonly Dictionary<string, ButtonConfig> _localIdToConfig = new Dictionary<string, ButtonConfig>();
        
        // 【新增】与 FX_Folder_Base 类似，直接存储从JSON解析的原始Dial配置列表
        private readonly List<ButtonConfig> _folderDialConfigs = new List<ButtonConfig>();

        // 【保留】按钮管理方式 (用于 _isButtonListDynamic == false 的情况)
        private readonly List<string> _localButtonIds = new List<string>();
        private readonly Dictionary<string, string> _localIdToGlobalActionParameter_Buttons = new Dictionary<string, string>();
        private readonly Dictionary<string, ButtonConfig> _localIdToConfig_Buttons = new Dictionary<string, ButtonConfig>();

        // 【保留】UI反馈和状态管理 (根据需要调整或与新的旋钮逻辑集成)
        private readonly Dictionary<string, DateTime> _lastTriggerPressTimes = new Dictionary<string, DateTime>();
        private readonly Dictionary<string, DateTime> _lastCombineButtonPressTimes = new Dictionary<string, DateTime>();
        // private const string NavigationDialActionType = "NavigationDial"; // 将通过 ActionType 字符串直接比较
        // private const string PlaceholderDialActionType = "Placeholder"; // 将通过 ActionType 字符串直接比较

        private readonly bool _isButtonListDynamic; 

        // 【新增】为 ParameterDial 存储当前选中的索引
        private readonly Dictionary<string, int> _parameterDialCurrentIndexes = new Dictionary<string, int>();

        public Dynamic_Folder_Base()
        {
            Logic_Manager_Base.Instance.Initialize();

            var folderClassName = this.GetType().Name;
            var folderBaseName = folderClassName.Replace("_Dynamic", "").Replace("_", " ");

            this._entryConfig = Logic_Manager_Base.Instance.GetConfigByDisplayName("Dynamic", folderBaseName);
            if (this._entryConfig == null)
            {
                PluginLog.Error($"[DynamicFolder] Constructor: Missing entry config for '{folderBaseName}'.");
                this.DisplayName = folderBaseName; 
                this.GroupName = "Dynamic";        
            }
            else
            {
                this.DisplayName = this._entryConfig.DisplayName;
                this.GroupName = this._entryConfig.GroupName ?? "Dynamic";
            }

            var content = Logic_Manager_Base.Instance.GetFolderContent(this.DisplayName);
            if (content != null)
            {
                this._isButtonListDynamic = content.IsButtonListDynamic;
                
                // 【修改】直接存储Dials配置，并为ParameterDial初始化索引
                if (content.Dials != null)
                {
                    foreach (var dialConfig in content.Dials)
                    {
                        this._folderDialConfigs.Add(dialConfig); // 存储原始配置，包括Placeholder
                        if (dialConfig.ActionType == "ParameterDial")
                        {
                            var localId = GetLocalDialId(dialConfig); // 使用新的GetLocalDialId
                            if (localId != null && dialConfig.Parameter != null && dialConfig.Parameter.Any())
                            {
                                this._parameterDialCurrentIndexes[localId] = 0; // 默认选中第一个参数
                            }
                        }
                    }
                }
                this.PopulateLocalIdMappings_StaticButtons(content); // 修改此方法只处理静态按钮
            }
            else
            {
                this._isButtonListDynamic = false; 
                PluginLog.Warning($"[DynamicFolder] Constructor ('{this.DisplayName}'): No folder content loaded.");
            }
            Logic_Manager_Base.Instance.CommandStateNeedsRefresh += this.OnCommandStateNeedsRefresh;
        }

        // 【新增】与 FX_Folder_Base 一致的 localId 生成逻辑 (用于旋钮)
        private string GetLocalDialId(ButtonConfig dialConfig)
        {
            if (dialConfig == null) return null;
            var groupName = dialConfig.GroupName ?? this.DisplayName; // GroupName应由LogicManager在加载时赋予文件夹名
            var displayName = dialConfig.DisplayName ?? "";
            if (string.IsNullOrEmpty(displayName) && dialConfig.ActionType != "Placeholder") // Placeholder可以没有DisplayName
            {
                 PluginLog.Warning($"[{this.DisplayName}] GetLocalDialId: DialConfig (Type: {dialConfig.ActionType}) has empty DisplayName. GroupName: {groupName}");
                 return null; 
            }
            // 为Placeholder生成一个基于其在列表中的索引的唯一ID，如果它没有DisplayName
            if (dialConfig.ActionType == "Placeholder" && string.IsNullOrEmpty(displayName))
            {
                 int placeholderIndex = this._folderDialConfigs.IndexOf(dialConfig);
                 return $"{groupName}_Placeholder_{placeholderIndex}".Replace(" ", "_");
            }
            return $"{groupName}_{displayName}".Replace(" ", "_");
        }

        // 【重命名并修改】此方法现在只负责处理静态按钮的映射
        private void PopulateLocalIdMappings_StaticButtons(FolderContentConfig content)
        {
            this._localButtonIds.Clear();
            this._localIdToGlobalActionParameter_Buttons.Clear();
            this._localIdToConfig_Buttons.Clear();

            if (this._isButtonListDynamic || content.Buttons == null || !content.Buttons.Any())
            {
                // PluginLog.Info($"[DynamicFolder] PopulateLocalIdMappings_StaticButtons ('{this.DisplayName}'): No static buttons to process or dynamic list mode.");
                return; // 如果是动态列表模式或没有静态按钮，则不处理
            }

            foreach (var buttonConfigFromJson in content.Buttons) 
            {
                // Placeholder按钮类型也应该被跳过 (如果未来按钮也支持Placeholder的话)
                if (buttonConfigFromJson.ActionType == "Placeholder") { continue; }

                var kvp = Logic_Manager_Base.Instance.GetAllConfigs().FirstOrDefault(
                    x => x.Value.DisplayName == buttonConfigFromJson.DisplayName && x.Value.GroupName == buttonConfigFromJson.GroupName
                );
                var globalActionParameter = kvp.Key;   
                var loadedConfig = kvp.Value;          

                if (string.IsNullOrEmpty(globalActionParameter) || loadedConfig == null)
                {
                    PluginLog.Warning($"[DynamicFolder] PopulateLocalIdMappings_StaticButtons ('{this.DisplayName}'): Failed to find global config for button DisplayName '{buttonConfigFromJson.DisplayName}', GroupName '{buttonConfigFromJson.GroupName ?? "(null)"}'.");
                    continue;
                }

                var localId = $"{loadedConfig.GroupName}_{loadedConfig.DisplayName}".Replace(" ", "");

                if (this._localIdToConfig_Buttons.ContainsKey(localId))
                {
                    PluginLog.Warning($"[DynamicFolder] PopulateLocalIdMappings_StaticButtons ('{this.DisplayName}'): Duplicate localId '{localId}' for button.");
                    continue;
                }
                this._localIdToGlobalActionParameter_Buttons[localId] = globalActionParameter;
                this._localIdToConfig_Buttons[localId] = loadedConfig;
                this._localButtonIds.Add(localId); // 只添加按钮的localId
            }
            PluginLog.Info($"[DynamicFolder] PopulateLocalIdMappings_StaticButtons ('{this.DisplayName}'): Processed {_localButtonIds.Count} static buttons.");
        }
        
        #region PluginDynamicFolder 核心重写 (旋钮部分将参照FX_Folder_Base)

        public override IEnumerable<string> GetButtonPressActionNames()
        {
            if (this._isButtonListDynamic) // 【重要】如果是动态列表模式，则此方法应由子类 (如FX_Folder_Base) 实现
            {
                PluginLog.Info($"[DynamicFolder] GetButtonPressActionNames for '{this.DisplayName}': Buttons are dynamic, expecting override or empty list.");
                return Enumerable.Empty<string>(); // 或者抛出NotImplementedException如果确定子类必须覆盖
            }
            return this._localButtonIds.Select(id => this.CreateCommandName(id)).ToList();
        }
        
        // 【全盘替换】使用与 FX_Folder_Base 完全相同的逻辑
        public override IEnumerable<string> GetEncoderRotateActionNames()
        {
            var rotateActionNames = new string[6];
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
                        var localId = GetLocalDialId(dialConfig);
                        if (localId != null) { rotateActionNames[i] = this.CreateAdjustmentName(localId); }
                        else { rotateActionNames[i] = null; }
                    }
                }
                else { rotateActionNames[i] = null; }
            }
            // PluginLog.Info($"[{this.DisplayName}] Dynamic GetEncoderRotateActionNames: [{string.Join(", ", rotateActionNames.Select(s => s ?? "null"))}]");
            return rotateActionNames;
        }
        
        // 【全盘替换】使用与 FX_Folder_Base 完全相同的逻辑
        public override IEnumerable<string> GetEncoderPressActionNames(DeviceType deviceType)
        {
            var pressActionNames = new string[6];
            for (int i = 0; i < 6; i++)
            {
                if (i < this._folderDialConfigs.Count)
                {
                    var dialConfig = this._folderDialConfigs[i];
                    if (dialConfig.ActionType == "Placeholder") { pressActionNames[i] = null; }
                    else if (dialConfig.ActionType == "NavigationDial" && dialConfig.DisplayName == "Back") 
                    {
                        var localId = GetLocalDialId(dialConfig);
                        if (localId != null) { pressActionNames[i] = base.CreateCommandName(localId); }
                        else { pressActionNames[i] = null; }
                    }
                    // 【新增】处理ParameterDial的按下 (可能用于循环参数或特定操作，如果定义了的话)
                    // else if (dialConfig.ActionType == "ParameterDial" && (dialConfig.Parameter?.Any() ?? false) )
                    // {
                    //     var localId = GetLocalDialId(dialConfig);
                    //     if (localId != null) { pressActionNames[i] = base.CreateCommandName(localId); } // ParameterDial按下也视为命令
                    // }
                    // 【新增】处理ToggleDial的按下
                    else if (dialConfig.ActionType == "ToggleDial")
                    {
                        var localId = GetLocalDialId(dialConfig);
                        if (localId != null) { pressActionNames[i] = base.CreateCommandName(localId); }
                    }
                    // 【新增】处理2ModeTickDial的按下 (切换模式)
                    else if (dialConfig.ActionType == "2ModeTickDial")
                    {
                        var localId = GetLocalDialId(dialConfig);
                        if (localId != null) { pressActionNames[i] = base.CreateCommandName(localId); }
                    }
                    else { pressActionNames[i] = null; } // 其他类型旋钮 (如 TickDial, FilterDial, PageDial) 默认按下无独立命令
                }
                else { pressActionNames[i] = null; }
            }
            // PluginLog.Info($"[{this.DisplayName}] Dynamic GetEncoderPressActionNames: [{string.Join(", ", pressActionNames.Select(s => s ?? "null"))}]");
            return pressActionNames;
        }
        #endregion

        #region PluginDynamicFolder 核心重写

        public override Boolean ProcessButtonEvent2(String actionParameter, DeviceButtonEvent2 buttonEvent)
        {
            // 【修改】统一通过 RunCommand 处理旋钮按下，这里主要处理静态按钮或SDK内部事件
            // 保留原始的通过 _localIdToConfig_Buttons 查找的逻辑，如果它是针对按钮的
            // 但为了安全，我们应该只让它处理非旋钮的按下，或确保 actionParameter 不会是旋钮的 localId

            if (buttonEvent.EventType == DeviceButtonEventType.Press)
            {
                // 如果 actionParameter 对应一个已注册的按钮 (非旋钮)
                if (this._localIdToConfig_Buttons.TryGetValue(actionParameter, out var buttonConf) && !buttonConf.ActionType.Contains("Dial"))
                {
                    // 特殊处理如文件夹的旧式 "back" command (如果存在且不是通过NavigationDial)
                    if (actionParameter == "back" || actionParameter == base.CreateCommandName("back"))
                    {                        
                        PluginLog.Info($"[{this.DisplayName}] ProcessButtonEvent2 (Legacy 'back' string Press): '{actionParameter}'. Closing folder.");
                        base.Close();
                        return false; 
                    }
                    // 可以添加其他静态按钮的特殊底层事件处理，但不建议用于主要逻辑
                }
                // 对于旋钮的按下，我们期望由 RunCommand 处理，这里不再重复检查 _folderDialConfigs
            }
            return base.ProcessButtonEvent2(actionParameter, buttonEvent);
        }

        public override PluginDynamicFolderNavigation GetNavigationArea(DeviceType _) =>
            PluginDynamicFolderNavigation.None;

        // 【重构 ApplyAdjustment 以使用 _folderDialConfigs 和本地状态管理】
        public override void ApplyAdjustment(string actionParameter, int ticks) 
        {
            var localDialId = actionParameter; // SDK 传入的已经是 localId

            var dialConfig = this._folderDialConfigs.FirstOrDefault(dc => GetLocalDialId(dc) == localDialId);

            if (dialConfig == null)
            {
                PluginLog.Warning($"[{this.DisplayName}] ApplyAdjustment: No dial config found for localId '{localDialId}'.");
                return; 
            }
            
            if (dialConfig.ActionType == "Placeholder") { return; } // Placeholder 无操作

            bool stateChanged = false; // 通用状态改变标记，用于刷新旋钮图像

            switch (dialConfig.ActionType)
            {
                case "ParameterDial":
                    if (dialConfig.Parameter != null && dialConfig.Parameter.Any())
                    {
                        if (!this._parameterDialCurrentIndexes.TryGetValue(localDialId, out var currentIndex))
                        {
                            currentIndex = 0; // 如果没有，则从0开始 (理论上构造函数已初始化)
                        }
                        int newIndex = (currentIndex + ticks + dialConfig.Parameter.Count) % dialConfig.Parameter.Count;
                        if (newIndex < 0) newIndex += dialConfig.Parameter.Count; // 确保正索引
                        this._parameterDialCurrentIndexes[localDialId] = newIndex;
                        stateChanged = true;
                        PluginLog.Info($"[{this.DisplayName}] ParameterDial '{dialConfig.DisplayName}' changed to index {newIndex}: '{dialConfig.Parameter[newIndex]}'.");
                        // 如果ParameterDial的值改变需要更新关联的ParameterButton，需要通知
                        // Logic_Manager_Base.Instance.NotifyLinkedParameterButtons(dialConfig.DisplayName, dialConfig.GroupName);
                        // 或者，如果ParameterButton在同一个文件夹内，可以直接调用 this.ButtonActionNamesChanged();
                    }
                    break;
                
                case "NavigationDial":
                    if (dialConfig.DisplayName == "Back")
                    {
                        PluginLog.Info($"[{this.DisplayName}] NavigationDial 'Back' rotated. Closing folder.");
                        this.Close();
                        return; // 关闭后不应再更新UI
                    }
                    break;
                
                // TODO: 添加 Dynamic_Folder_Base 特有的其他旋钮类型如 ToggleDial, 2ModeTickDial, TickDial 的本地化处理逻辑
                // 例如 ToggleDial:
                // case "ToggleDial":
                //     var globalParamForToggle = Logic_Manager_Base.Instance.GetAllConfigs().FirstOrDefault(kvp => GetLocalDialId(kvp.Value) == localDialId && kvp.Value.GroupName == this.DisplayName).Key;
                //     if(globalParamForToggle != null){
                //         bool newState = ticks > 0; // 简化：右旋ON，左旋OFF
                //         Logic_Manager_Base.Instance.SetToggleState(globalParamForToggle, newState);
                //         ReaOSCPlugin.SendOSCMessage(Logic_Manager_Base.DetermineOscAddressForAction(dialConfig, dialConfig.GroupName), newState ? 1f : 0f);
                //         stateChanged = true;
                //     }
                //     break;

                default:
                    PluginLog.Warning($"[{this.DisplayName}] ApplyAdjustment: Unhandled ActionType '{dialConfig.ActionType}' for dial '{dialConfig.DisplayName}'.");
                    break;
            }

            if(stateChanged)
            {
                this.AdjustmentValueChanged(actionParameter); 
                // 如果是FilterDial或PageDial，它们在FX_Folder_Base中会调用ButtonActionNamesChanged
                // Dynamic_Folder_Base中ParameterDial改变可能也需要，取决于是否有ParameterButton与之关联
            }
        }

        // 【重构 RunCommand 以使用 _folderDialConfigs】
        public override void RunCommand(string actionParameter) 
        {
            string commandLocalIdToLookup = actionParameter; 
            const string commandPrefix = "plugin:command:";
            if (actionParameter.StartsWith(commandPrefix))
            {
                commandLocalIdToLookup = actionParameter.Substring(commandPrefix.Length);
            }
            PluginLog.Info($"[{this.DisplayName}] RunCommand looking up localId: '{commandLocalIdToLookup}'");

            var dialConfigPressed = this._folderDialConfigs.FirstOrDefault(dc => GetLocalDialId(dc) == commandLocalIdToLookup);

            if (dialConfigPressed != null)
            {
                if (dialConfigPressed.ActionType == "Placeholder") { return; }

                if (dialConfigPressed.ActionType == "NavigationDial" && dialConfigPressed.DisplayName == "Back")
                {
                    PluginLog.Info($"[{this.DisplayName}] 'Back' NavigationDial pressed. Closing folder.");
                    this.Close();
                    return;
                }
                // TODO: 添加 Dynamic_Folder_Base 特有的其他旋钮类型按下行为
                // 例如 ToggleDial (按下发送Reset OSC), 2ModeTickDial (按下切换模式)
                // case "ToggleDial":
                // case "2ModeTickDial":
                //    var globalParam = Logic_Manager_Base.Instance.GetAllConfigs().FirstOrDefault(kvp => GetLocalDialId(kvp.Value) == commandLocalIdToLookup && kvp.Value.GroupName == this.DisplayName).Key;
                //    if(globalParam != null && Logic_Manager_Base.Instance.ProcessDialPress(globalParam)) {
                //        this.AdjustmentValueChanged(this.CreateAdjustmentName(commandLocalIdToLookup));
                //    }
                //    return;
                // case "ParameterDial": // ParameterDial 通常按下无行为
                //    return; 
                 PluginLog.Info($"[{this.DisplayName}] Dial '{dialConfigPressed.DisplayName}' (ActionType: {dialConfigPressed.ActionType}) pressed. No specific RunCommand action defined in Dynamic_Folder_Base yet.");
                 // 如果旋钮按下有独立的功能且被RunCommand处理，确保在这里return或者有相应逻辑。
            }
            
            // 处理静态按钮的按下 (使用 _localIdToConfig_Buttons)
            if (this._localIdToConfig_Buttons.TryGetValue(commandLocalIdToLookup, out var buttonConfig))
            {
                if (!this._localIdToGlobalActionParameter_Buttons.TryGetValue(commandLocalIdToLookup, out var globalButtonParam))
                {
                    PluginLog.Warning($"[DynamicFolder] RunCommand: Missing globalActionParameter for button localId '{commandLocalIdToLookup}'.");
                    return;
                }

                // 调用Logic_Manager_Base处理标准按钮逻辑
                Logic_Manager_Base.Instance.ProcessUserAction(globalButtonParam, this.DisplayName);

                // UI反馈 (可以提取为通用方法如果FX_Folder_Base也用)
                if (buttonConfig.ActionType == "TriggerButton")
                {
                    this._lastTriggerPressTimes[commandLocalIdToLookup] = DateTime.Now;
                    this.ButtonActionNamesChanged();
                    Task.Delay(200).ContinueWith(_ => { this.ButtonActionNamesChanged(); }); 
                }
                else if (buttonConfig.ActionType == "CombineButton") 
                {
                    this._lastCombineButtonPressTimes[commandLocalIdToLookup] = DateTime.Now;
                    this.ButtonActionNamesChanged();
                    Task.Delay(200).ContinueWith(_ => { this.ButtonActionNamesChanged(); }); 
                }
                else if (buttonConfig.ActionType == "ToggleButton" || buttonConfig.ActionType == "ParameterButton")
                {
                    this.ButtonActionNamesChanged(); // Toggle和ParameterButton状态变化由LogicManager触发UI刷新
                }
            }
            else if (dialConfigPressed == null) 
            {
                 PluginLog.Warning($"[{this.DisplayName}] RunCommand: No config found for localId '{commandLocalIdToLookup}' in Dials or Buttons.");
            }
        }
        
        // 【重构 GetAdjustmentImage 以使用 _folderDialConfigs】
        public override BitmapImage GetAdjustmentImage(string actionParameter, PluginImageSize imageSize)
        {
            if (string.IsNullOrEmpty(actionParameter)) 
            {
                PluginLog.Verbose($"[{this.DisplayName}] GetAdjustmentImage: actionParameter is null or empty. Drawing blank.");
                using (var bb = new BitmapBuilder(imageSize)) { bb.Clear(BitmapColor.Black); return bb.ToImage(); }
            }

            var localDialId = actionParameter; // SDK 传入的就是 localId
            var dialConfig = this._folderDialConfigs.FirstOrDefault(dc => GetLocalDialId(dc) == localDialId);

            if (dialConfig == null) // 没有找到对应的旋钮配置 (可能是一个无效的actionParameter)
            {
                PluginLog.Verbose($"[DynamicFolder|GetAdjustmentImage] No dial config found for localId: '{localDialId}'. Drawing blank.");
                using (var bb = new BitmapBuilder(imageSize)) { bb.Clear(BitmapColor.Black); return bb.ToImage(); }
            }

            if (dialConfig.ActionType == "Placeholder")
            {
                PluginLog.Verbose($"[DynamicFolder|GetAdjustmentImage] Config for localId: '{localDialId}' is ActionType 'Placeholder'. Drawing blank.");
                using (var bb = new BitmapBuilder(imageSize)) { bb.Clear(BitmapColor.Black); return bb.ToImage(); }
            }
            
            // 【为 Dynamic_Folder_Base 的旋钮类型准备参数给 PluginImage.DrawElement】
            string mainTitleOverride = dialConfig.Title ?? dialConfig.DisplayName;
            string valueTextDisplay = null; 
            bool isActive = false; // 对于ToggleDial
            int currentMode = 0;    // 对于2ModeTickDial
            // string globalParamForState = null; // 用于从LogicManager获取状态
            
            // 尝试获取此旋钮在LogicManager中注册的全局参数 (如果它需要从LogicManager获取状态)
            // 这部分可能需要优化，或者将状态管理完全本地化
            var globalParamKvp = Logic_Manager_Base.Instance.GetAllConfigs().FirstOrDefault(kvp => GetLocalDialId(kvp.Value) == localDialId && kvp.Value.GroupName == this.DisplayName);
            var globalParamForState = globalParamKvp.Key; //可能是null

            switch (dialConfig.ActionType)
            {
                case "ParameterDial":
                    // mainTitleOverride (标签) 和 valueTextDisplay (当前参数值) 将由 PluginImage 根据 ShowTitle 处理
                    // 我们只需要提供原始标题和当前选中的参数值
                    if (this._parameterDialCurrentIndexes.TryGetValue(localDialId, out var currentIndex) && 
                        dialConfig.Parameter != null && currentIndex >= 0 && currentIndex < dialConfig.Parameter.Count)
                    {
                        valueTextDisplay = dialConfig.Parameter[currentIndex];
                    }
                    else
                    {
                        valueTextDisplay = dialConfig.Parameter?.FirstOrDefault() ?? "N/A"; // Fallback
                    }
                    break;
                case "ToggleDial":
                    if(globalParamForState != null) isActive = Logic_Manager_Base.Instance.GetToggleState(globalParamForState);
                    break;
                case "2ModeTickDial":
                    if(globalParamForState != null) currentMode = Logic_Manager_Base.Instance.GetDialMode(globalParamForState);
                    break;
                // NavigationDial, TickDial 等类型会使用默认的 mainTitleOverride 和空的 valueTextDisplay
                // PluginImage.DrawElement 会根据 dialConfig.ShowTitle (如果存在) 和 valueTextDisplay 是否为空来决定如何绘制
            }
            
            return Helpers.PluginImage.DrawElement(
                imageSize,
                dialConfig, 
                mainTitleOverride,
                valueTextDisplay, 
                isActive,
                currentMode,
                null, // customIcon - Dynamic_Folder_Base 目前不处理旋钮图标
                forceTextOnly: true, 
                actualAuxText: null  
            );
        }

        public override string GetCommandDisplayName(string actionParameter, PluginImageSize imageSize)
        {
            // 为 "back" 按下命令提供显示名称 (如果需要，但通常图像更重要)
            // 如果 "back" 现在是由 PluginImage 根据 ButtonConfig 绘制，则此处的特殊处理可能不再严格需要
            // 但如果 actionParameter 是原始的 CreateCommandName("back") 结果，可以保留
            if (actionParameter == base.CreateCommandName("back"))
            {
                return "Back";
            }
            if (actionParameter != null && actionParameter.StartsWith("placeholder_d_encoder_press_"))
            {
                return ""; // 或 null
            }

            if (actionParameter.Equals(NavigateUpActionName))
                return base.GetCommandDisplayName(this.CreateCommandName(actionParameter), imageSize);
            
            // 这里的 actionParameter 是 localId
            return this._localIdToConfig_Buttons.TryGetValue(actionParameter, out var c) ? (c.Title ?? c.DisplayName) : "ErrDisp";
        }

        public override BitmapImage GetCommandImage(string actionParameter, PluginImageSize imageSize)
        {
            // actionParameter 在这里是 localId

            // 1. 处理已知的特殊 actionParameter (如占位符)
            if (actionParameter != null && actionParameter.StartsWith("placeholder_d_encoder_press_"))
            {
                return PluginImage.DrawElement(imageSize, null, "", forceTextOnly: true); // 空白图像
            }

            // 2. 获取配置
            if (!this._localIdToConfig_Buttons.TryGetValue(actionParameter, out var config) || 
                !this._localIdToGlobalActionParameter_Buttons.TryGetValue(actionParameter, out var globalActionParameter))
            {
                // 如果是LoupeDeck SDK自动添加的 NavigateUpActionName (返回上一级文件夹的按钮)
                // 它可能没有在我们的 _localIdToConfig 中，但基类会处理它的显示名称，这里我们给它一个默认图像
                if (actionParameter.Equals(NavigateUpActionName))
                {
                    using(var bb = new BitmapBuilder(imageSize))
                    {
                        bb.Clear(BitmapColor.Black);
                        bb.DrawText("Up", BitmapColor.White, 23);
                        return bb.ToImage();
                    }
                }
                PluginLog.Warning($"[DynamicFolder|GetCommandImage] Config not found for localId: {actionParameter}. Returning default image.");
                return PluginImage.DrawElement(imageSize, null, "Cfg?", isActive:true); 
            }

            try
            {
                BitmapImage customIcon = null;
                // 3. 图标加载逻辑 (统一)
                string imagePathToLoad = !String.IsNullOrEmpty(config.ButtonImage) ? config.ButtonImage : null;
                if (string.IsNullOrEmpty(imagePathToLoad))
                {
                    // 使用 config.DisplayName (来自JSON的原始定义) 来推断图标名
                    imagePathToLoad = $"{Logic_Manager_Base.SanitizeOscPathSegment(config.DisplayName)}.png";
                }

                if (!string.IsNullOrEmpty(imagePathToLoad))
                {
                    try
                    {
                        customIcon = PluginResources.ReadImage(imagePathToLoad);
                    }
                    catch (Exception ex)
                    {
                        PluginLog.Warning(ex, $"[DynamicFolder|GetCommandImage] Failed to load icon '{imagePathToLoad}' for localId '{actionParameter}' (global: '{globalActionParameter}'). Will draw text only.");
                        customIcon = null;
                    }
                }

                // 4. 确定状态和动态文本
                bool isActive = false;
                string mainTitleOverride = null;
                string valueText = null;
                int currentModeForDrawing = 0;

                // 根据ActionType和状态调整显示
                if (config.ActionType == "TriggerButton" || config.ActionType == "CombineButton")
                {
                    if ((config.ActionType == "TriggerButton" && this._lastTriggerPressTimes.TryGetValue(actionParameter, out var pressTime) && (DateTime.Now - pressTime).TotalMilliseconds < 200) ||
                        (config.ActionType == "CombineButton" && this._lastCombineButtonPressTimes.TryGetValue(actionParameter, out var combinePressTime) && (DateTime.Now - combinePressTime).TotalMilliseconds < 200))
                    {
                        isActive = true; // 用于瞬时高亮
                    }
                    mainTitleOverride = config.Title ?? config.DisplayName;
                }
                else if (config.ActionType == "ToggleButton" || config.ActionType == "ToggleDial") // ToggleDial 也可能被用作按钮按下
                {
                    isActive = Logic_Manager_Base.Instance.GetToggleState(globalActionParameter);
                    mainTitleOverride = config.Title ?? config.DisplayName;
                }
                else if (config.ActionType == "ParameterButton")
                {
                    var sourceDialGlobalParam = this.FindSourceDialGlobalActionParameter(config, config.ParameterSourceDial);
                    mainTitleOverride = !string.IsNullOrEmpty(sourceDialGlobalParam)
                        ? (Logic_Manager_Base.Instance.GetParameterDialSelectedTitle(sourceDialGlobalParam) ?? config.Title ?? config.DisplayName)
                        : $"Err:{config.ParameterSourceDial?.Substring(0, Math.Min(config.ParameterSourceDial?.Length ?? 0, 6))}";
                }
                else if (config.ActionType == "2ModeTickDial") // 如果2ModeTickDial的按下有特殊显示
                {
                    currentModeForDrawing = Logic_Manager_Base.Instance.GetDialMode(globalActionParameter);
                    // mainTitleOverride 在 PluginImage 中会根据模式和config处理
                    mainTitleOverride = config.Title ?? config.DisplayName;
                }
                //  Handle "NavigationButton" / "NavigationDial" if defined for specific title/icon for "Back"
                //  Example: if localId == "back_button_local_id"
                //  The hardcoded drawing for "back" or specific placeholder names can be removed/simplified
                //  as these will now be loaded as configs and drawn by PluginImage.DrawElement.
                else if (actionParameter == base.CreateCommandName("back")) // 旧的硬编码 "back" 处理
                {
                    // 如果 "back" 仍然这样处理，且没有自己的 ButtonConfig
                    // 我们可以直接调用 PluginImage，或在这里构建一个临时的 ButtonConfig
                    return PluginImage.DrawElement(imageSize, new ButtonConfig { DisplayName = "Back", Title = "Back"}, mainTitleOverride: "Back");
                }
                else // 其他类型或默认显示
                {
                    mainTitleOverride = config.Title ?? config.DisplayName;
                }

                // 5. 调用 PluginImage.DrawElement
                return PluginImage.DrawElement(
                    imageSize,
                    config,
                    mainTitleOverride,
                    valueText,
                    isActive,
                    currentModeForDrawing,
                    customIcon
                );
            }
            catch (Exception ex)
            {
                PluginLog.Error(ex, $"[DynamicFolder|GetCommandImage] Unhandled exception for localId '{actionParameter}'.");
                return PluginImage.DrawElement(imageSize, config, "ERR!", isActive: true);
            }
        }
        
        public override BitmapImage GetButtonImage(PluginImageSize imageSize)
        {
            if (this._entryConfig == null)
            {
                // 提供一个默认的文件夹入口图像，如果_entryConfig未加载
                using (var bb = new BitmapBuilder(imageSize))
                {
                    bb.Clear(BitmapColor.Black);
                    bb.DrawText(this.DisplayName, BitmapColor.White, GetAutomaticButtonTitleFontSize(this.DisplayName)); // 使用文件夹自身的DisplayName
                    return bb.ToImage();
                }
            }

            using (var bitmapBuilder = new BitmapBuilder(imageSize))
            {
                BitmapColor bgColor = !String.IsNullOrEmpty(this._entryConfig.BackgroundColor) ? HexToBitmapColor(this._entryConfig.BackgroundColor) : BitmapColor.Black;
                bitmapBuilder.Clear(bgColor);

                BitmapColor titleColor = !String.IsNullOrEmpty(this._entryConfig.TitleColor) ? HexToBitmapColor(this._entryConfig.TitleColor) : BitmapColor.White;
                string mainTitle = this._entryConfig.Title ?? this._entryConfig.DisplayName;

                if (!String.IsNullOrEmpty(mainTitle))
                {
                    bitmapBuilder.DrawText(mainTitle, titleColor, GetAutomaticButtonTitleFontSize(mainTitle));
                }
                if (!String.IsNullOrEmpty(this._entryConfig.Text))
                {
                    var textColor = String.IsNullOrEmpty(this._entryConfig.TextColor) ? BitmapColor.White : HexToBitmapColor(this._entryConfig.TextColor);
                    bitmapBuilder.DrawText(this._entryConfig.Text, this._entryConfig.TextX ?? 50, this._entryConfig.TextY ?? 55, this._entryConfig.TextWidth ?? 14, this._entryConfig.TextHeight ?? 14, textColor, this._entryConfig.TextSize ?? 14);
                }
                return bitmapBuilder.ToImage();
            }
        }
        #endregion

        #region UI辅助方法 (无改动)
        private static int GetAutomaticButtonTitleFontSize(String title) { if (String.IsNullOrEmpty(title)) return 23; var len = title.Length; return len switch { 1 => 38, 2 => 33, 3 => 31, 4 => 26, 5 => 23, 6 => 22, 7 => 20, 8 => 18, _ => 16 }; }
        private static int GetAutomaticDialTitleFontSize(String title) { if (String.IsNullOrEmpty(title)) return 16; var len = title.Length; return len switch { 1 => 26, 2 => 23, 3 => 21, 4 => 19, 5 => 17, 6 => 15, 7 => 15, 8 => 13, _ => 11 }; }
        private static BitmapColor HexToBitmapColor(string hex) { if (String.IsNullOrEmpty(hex)) return BitmapColor.White; try { hex = hex.TrimStart('#'); var r = (byte)Int32.Parse(hex.Substring(0, 2), NumberStyles.HexNumber); var g = (byte)Int32.Parse(hex.Substring(2, 2), NumberStyles.HexNumber); var b = (byte)Int32.Parse(hex.Substring(4, 2), NumberStyles.HexNumber); return new BitmapColor(r, g, b); } catch { return BitmapColor.Red; } }
        #endregion

        #region 事件处理与清理
        // 【注意】这部分是我之前版本添加的，用于响应Logic_Manager的状态变化。
        // 如果您的旧版工作代码没有这个，并且不需要这种响应机制，可以考虑移除或调整。
        // 如果保留，请确保它与您当前的整体设计一致。
        private void OnCommandStateNeedsRefresh(object sender, string globalActionParameterThatChanged)
        {
            var localIdKvp = this._localIdToGlobalActionParameter_Buttons.FirstOrDefault(kvp => kvp.Value == globalActionParameterThatChanged);
            if (EqualityComparer<KeyValuePair<string, string>>.Default.Equals(localIdKvp, default(KeyValuePair<string, string>)))
                return; // 这个 globalActionParameter 不由当前文件夹实例管理

            var localId = localIdKvp.Key;

            if (!this._localIdToConfig_Buttons.TryGetValue(localId, out var config))
            {
                PluginLog.Warning($"[DynamicFolder] OnCommandStateNeedsRefresh ('{this.DisplayName}'): localId '{localId}' (global: {globalActionParameterThatChanged}) not found in _localIdToConfig. This might indicate a desync.");
                return;
            }

            // 【修正】使用 config.ActionType 来决定调用哪个 Changed 方法
            if (config.ActionType.Contains("Dial"))
            {
                this.EncoderActionNamesChanged();
            }
            else // Buttons
            {
                this.ButtonActionNamesChanged();
            }


            if (config.ActionType == "ParameterDial")
            {
                string sourceDialDisplayName = config.DisplayName;
                string sourceDialJsonGroupName = config.GroupName;

                foreach (var btnEntryKvp in this._localIdToConfig_Buttons)
                {
                    var otherLocalId = btnEntryKvp.Key;
                    var otherLocalConfig = btnEntryKvp.Value;

                    if (otherLocalConfig.ActionType == "ParameterButton" &&
                        otherLocalConfig.ParameterSourceDial == sourceDialDisplayName &&
                        otherLocalConfig.GroupName == sourceDialJsonGroupName)
                    {
                        PluginLog.Info($"[DynamicFolder] OnCommandStateNeedsRefresh: ParameterDial '{sourceDialDisplayName}' (group '{sourceDialJsonGroupName}') changed, triggering UI update for linked ParameterButton '{otherLocalConfig.DisplayName}' (localId '{otherLocalId}').");
                        this.ButtonActionNamesChanged();
                        break; 
                    }
                }
            }
        }

        private string FindSourceDialGlobalActionParameter(ButtonConfig parameterButtonConfig, string sourceDialDisplayNameFromButtonConfig)
        {
            if (parameterButtonConfig == null || string.IsNullOrEmpty(parameterButtonConfig.GroupName) || string.IsNullOrEmpty(sourceDialDisplayNameFromButtonConfig))
            {
                PluginLog.Warning($"[DynamicFolder] FindSourceDialGlobal ('{this.DisplayName}'): Invalid arguments. ParameterButtonConfig or its GroupName is null/empty, or sourceDialDisplayName is null/empty.");
                return null;
            }

            // 假设 GroupName 和 DisplayName 在 Logic_Manager 中存储时可能已被清理或规范化
            // 这里我们使用原始的 JSON GroupName 和 DisplayName 来尝试匹配
            // 注意：Logic_Manager_Base.SanitizeOscPathSegment 可能不适用于此处的查找键格式
            // 应该直接使用 Logic_Manager 中构建键的方式

            // 尝试直接使用原始的 GroupName 和 DisplayName 来构建可能的全局参数名
            // 这里的键格式需要与 Logic_Manager_Base.GetAllConfigs() 中的键格式完全一致
            // 通常是 "/[GroupName]/[DisplayName]" 或类似格式
            
            // 为了更准确地找到源 ParameterDial，我们需要知道 Logic_Manager 是如何存储这些配置的键的。
            // 假设 Logic_Manager 存储的键是基于 ButtonConfig 的 GroupName 和 DisplayName
            var allConfigs = Logic_Manager_Base.Instance.GetAllConfigs();
            var foundDialEntry = allConfigs.FirstOrDefault(kvp => 
                kvp.Value.DisplayName == sourceDialDisplayNameFromButtonConfig &&
                kvp.Value.GroupName == parameterButtonConfig.GroupName && // 使用 ParameterButton 的 GroupName 来匹配 Dial
                kvp.Value.ActionType == "ParameterDial");

            if (!string.IsNullOrEmpty(foundDialEntry.Key))
            {
                return foundDialEntry.Key;
            }
            
            PluginLog.Warning($"[DynamicFolder] FindSourceDialGlobal ('{this.DisplayName}'): ParameterButton '{parameterButtonConfig.DisplayName}' (JSON Group: '{parameterButtonConfig.GroupName}') could not find source ParameterDial with DisplayName '{sourceDialDisplayNameFromButtonConfig}'.");
            return null;
        }

        // 【注意】IDisposable 和这个 Dispose 方法是我之前版本添加的。
        // 如果您的旧版工作代码没有实现 IDisposable，可以移除这部分。
        public void Dispose()
        {
            if (Logic_Manager_Base.Instance != null)
            {
                Logic_Manager_Base.Instance.CommandStateNeedsRefresh -= this.OnCommandStateNeedsRefresh;
            }
            this._lastTriggerPressTimes.Clear();
            this._lastCombineButtonPressTimes.Clear();
            this._localButtonIds.Clear();
            this._localIdToGlobalActionParameter_Buttons.Clear();
            this._localIdToConfig_Buttons.Clear();
            PluginLog.Info($"[DynamicFolder] '{this.DisplayName}' Disposed.");
        }
        #endregion
    }
}