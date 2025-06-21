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

        private readonly List<string> _localButtonIds = new List<string>();
        private readonly List<string> _localAdjustmentIds = new List<string>();

        private readonly Dictionary<string, string> _localIdToGlobalActionParameter = new Dictionary<string, string>();
        private readonly Dictionary<string, ButtonConfig> _localIdToConfig = new Dictionary<string, ButtonConfig>();

        private readonly Dictionary<string, DateTime> _lastTriggerPressTimes = new Dictionary<string, DateTime>();
        private readonly Dictionary<string, DateTime> _lastCombineButtonPressTimes = new Dictionary<string, DateTime>(); // 用于CombineButton的UI反馈

        private const string NavigationDialActionType = "NavigationDial";
        private const string PlaceholderDialActionType = "PlaceholderDial";

        public Dynamic_Folder_Base()
        {
            Logic_Manager_Base.Instance.Initialize();

            var folderClassName = this.GetType().Name;
            var folderBaseName = folderClassName.Replace("_Dynamic", "").Replace("_", " ");

            // 【注意】根据您提供的 Dynamic_List.json，GroupName 是 "Dynamic"
            // 如果您的旧代码中 "ReaOSC Dynamic" 是正确的，请确保 Dynamic_List.json 也使用它
            this._entryConfig = Logic_Manager_Base.Instance.GetConfigByDisplayName("Dynamic", folderBaseName);
            if (this._entryConfig == null)
            {
                PluginLog.Error($"[DynamicFolder] Constructor: 未能在 Logic_Manager 中找到文件夹入口 '{folderBaseName}' (尝试的GroupName: Dynamic) 的配置项。");
                this.DisplayName = folderBaseName; // Fallback display name
                this.GroupName = "Dynamic";        // Fallback group name
                // 不return，尝试加载内容，即使入口配置缺失
            }
            else
            {
                this.DisplayName = this._entryConfig.DisplayName;
                this.GroupName = this._entryConfig.GroupName; // GroupName 应来自配置，例如 "Dynamic"
            }
            // 移除 this.Navigation = PluginDynamicFolderNavigation.ButtonArea; 
            // 导航模式将由 GetNavigationArea 控制

            // 【重构】不再从独立文件加载，而是从 Logic_Manager 获取预加载的内容
            var content = Logic_Manager_Base.Instance.GetFolderContent(folderBaseName);
            if (content != null)
            {
                this.PopulateLocalIdMappings(content);
            }
            else
            {
                PluginLog.Warning($"[DynamicFolder] Constructor ('{this.DisplayName}'): 未能从 Logic_Manager 加载文件夹内容。");
            }
            // 【注意】我之前的版本有订阅CommandStateNeedsRefresh，您的旧代码示例中没有。如果需要，可以加回来。
            Logic_Manager_Base.Instance.CommandStateNeedsRefresh += this.OnCommandStateNeedsRefresh;
        }

        #region 初始化与动作填充
        // 【移除】LoadFolderContent 方法，因为现在内容由 Logic_Manager 统一加载

        // 【核心修正】采用您旧代码中的 PopulateLocalIdMappings 逻辑
        private void PopulateLocalIdMappings(FolderContentConfig content)
        {
            var allActions = content.Buttons.Concat(content.Dials);

            foreach (var configFromJson in allActions) // configFromJson 是从当前文件夹的 _List.json 读取的配置项
            {
                // 使用从JSON读取的DisplayName和GroupName在Logic_Manager_Base的所有配置中查找匹配项
                var kvp = Logic_Manager_Base.Instance.GetAllConfigs().FirstOrDefault(
                    x => x.Value.DisplayName == configFromJson.DisplayName && x.Value.GroupName == configFromJson.GroupName
                );
                var globalActionParameter = kvp.Key;   // 这是Logic_Manager中注册的唯一键
                var loadedConfig = kvp.Value;          // 这是Logic_Manager中存储的完整ButtonConfig

                if (string.IsNullOrEmpty(globalActionParameter) || loadedConfig == null)
                {
                    PluginLog.Warning($"[DynamicFolder] PopulateLocalIdMappings ('{this.DisplayName}'): 未能通过 DisplayName '{configFromJson.DisplayName}' 和 JSON GroupName '{configFromJson.GroupName}' 从 Logic_Manager 找到全局配置。该控件将不可用。");
                    continue;
                }

                // 本地ID用于在此文件夹实例内部唯一标识一个控件
                // 使用 loadedConfig (来自 Logic_Manager, 源自 JSON) 的 GroupName 和 DisplayName 来确保一致性
                var localId = $"{loadedConfig.GroupName}_{loadedConfig.DisplayName}".Replace(" ", "");

                if (this._localIdToConfig.ContainsKey(localId))
                {
                    PluginLog.Warning($"[DynamicFolder] PopulateLocalIdMappings ('{this.DisplayName}'): 本地ID '{localId}' (对应全局参数 '{globalActionParameter}') 已存在。请检查 '{this.DisplayName}_List.json' 文件中是否存在重复的 GroupName/DisplayName 组合。将跳过重复项。");
                    continue;
                }

                this._localIdToGlobalActionParameter[localId] = globalActionParameter;
                this._localIdToConfig[localId] = loadedConfig;

                if (loadedConfig.ActionType.Contains("Dial"))
                {
                    this._localAdjustmentIds.Add(localId);
                }
                else
                {
                    this._localButtonIds.Add(localId);
                }
            }
            PluginLog.Info($"[DynamicFolder] PopulateLocalIdMappings ('{this.DisplayName}'): 完成。加载了 {_localButtonIds.Count} 个按钮和 {_localAdjustmentIds.Count} 个旋钮。");
        }
        #endregion

        #region PluginDynamicFolder 核心重写

        public override IEnumerable<string> GetButtonPressActionNames() => this._localButtonIds.Select(id => this.CreateCommandName(id)).ToList();
        
        public override IEnumerable<string> GetEncoderRotateActionNames()
        {
            var sortedAdjustmentLocalIds = new string[6]; // 6 encoder slots
            var regularDials = new List<string>();
            string navigationDialLocalId = null;

            foreach (var localId in this._localAdjustmentIds)
            {
                if (this._localIdToConfig.TryGetValue(localId, out var config))
                {
                    if (config.ActionType == NavigationDialActionType)
                    {
                        navigationDialLocalId = localId;
                    }
                    else if (config.ActionType != PlaceholderDialActionType)
                    {
                        regularDials.Add(localId);
                    }
                    // PlaceholderDials are implicitly handled by not being added to regular
                    // and not being the navigationDial. If a slot remains null after filling regular
                    // and navigation dials, it can be considered a placeholder for drawing if needed,
                    // or SDK handles null action names as empty.
                }
            }

            // Fill first 5 slots with regular dials
            for (int i = 0; i < 5; i++)
            {
                if (i < regularDials.Count)
                {
                    sortedAdjustmentLocalIds[i] = regularDials[i];
                }
                else
                {
                    // If fewer than 5 regular dials, these slots will remain null for now
                    // We could explicitly map placeholder localIds here if _localAdjustmentIds was sorted
                    // or if JSON guarantees order and includes placeholders.
                    // For now, relying on null for empty, or PluginImage to draw placeholder config if its localId is passed.
                    sortedAdjustmentLocalIds[i] = this._localAdjustmentIds
                        .FirstOrDefault(locId => 
                            this._localIdToConfig.TryGetValue(locId, out var cfg) && 
                            cfg.ActionType == PlaceholderDialActionType && 
                            !sortedAdjustmentLocalIds.Contains(locId) // Ensure placeholder not already used
                        );
                }
            }

            // Place NavigationDial (Back) at index 5
            if (navigationDialLocalId != null)
            {
                sortedAdjustmentLocalIds[5] = navigationDialLocalId;
            }
            else
            {
                // Fallback or error if NavigationDial is not found in JSON for this folder
                PluginLog.Warning($"[DynamicFolder] GetEncoderRotateActionNames for '{this.DisplayName}': NavigationDial (Back) not found in JSON config. Slot 5 will be empty.");
                sortedAdjustmentLocalIds[5] = null; 
            }
            
            return sortedAdjustmentLocalIds.Select(localId => 
                string.IsNullOrEmpty(localId) ? null : this.CreateAdjustmentName(localId)
            ).ToList();
        }

        public override IEnumerable<String> GetEncoderPressActionNames(DeviceType deviceType)
        {
            var pressActionLocalIds = new string[6]; // 6 encoder slots
            var regularDialsForPress = new List<string>();
            string navigationDialLocalIdForPress = null;

            // Similar logic to GetEncoderRotateActionNames to identify regular and navigation dials
            foreach (var localId in this._localAdjustmentIds)
            {
                if (this._localIdToConfig.TryGetValue(localId, out var config))
                {
                    if (config.ActionType == NavigationDialActionType)
                    {
                        navigationDialLocalIdForPress = localId;
                    }
                    // For press actions, even PlaceholderDials might not have a press action, 
                    // but regular dials do (even if it's just a mode switch or reset)
                    else if (config.ActionType != PlaceholderDialActionType) 
                    {
                        regularDialsForPress.Add(localId);
                    }
                }
            }

            for (int i = 0; i < 5; i++)
            {
                if (i < regularDialsForPress.Count)
                {
                    pressActionLocalIds[i] = regularDialsForPress[i]; // Use the dial's localId for its press action
                }
                else
                {
                    // If no regular dial for this slot, use the old placeholder naming convention for the action parameter
                    // This ensures SDK compatibility if it expects specific placeholder names for unassigned press actions.
                    // However, our PluginImage drawing for these would be for a null config.
                    pressActionLocalIds[i] = $"placeholder_d_encoder_press_{i}"; 
                }
            }

            if (navigationDialLocalIdForPress != null)
            {
                pressActionLocalIds[5] = navigationDialLocalIdForPress; // NavigationDial's press action (Back)
            }
            else
            {
                PluginLog.Warning($"[DynamicFolder] GetEncoderPressActionNames for '{this.DisplayName}': NavigationDial (Back) not found for press. Slot 5 press will be unassigned.");
                pressActionLocalIds[5] = "placeholder_d_encoder_press_5"; // Fallback placeholder
            }

            return pressActionLocalIds.Select(localId => 
                string.IsNullOrEmpty(localId) ? null : base.CreateCommandName(localId) // Press actions are commands
            ).ToList();
        }
        
        public override Boolean ProcessButtonEvent2(String actionParameter, DeviceButtonEvent2 buttonEvent)
        {
            // actionParameter here is the one returned by GetEncoderPressActionNames, which can be a localId or a placeholder string
            if (buttonEvent.EventType == DeviceButtonEventType.Press)
            {
                // Check if it's a localId first (for our configured dials, including NavigationDial)
                if (this._localIdToConfig.TryGetValue(actionParameter, out var config) && config.ActionType == NavigationDialActionType)
                {
                    PluginLog.Info($"[{this.DisplayName}] ProcessButtonEvent2 (NavigationDial Press): '{actionParameter}'. Closing folder.");
                    base.Close();
                    return false; 
                }
                // Fallback for old hardcoded string, though should be covered by localId check if JSON is correct
                else if (actionParameter == "back" || actionParameter == base.CreateCommandName("back")) 
                {
                    PluginLog.Info($"[{this.DisplayName}] ProcessButtonEvent2 (Legacy 'back' string Press): '{actionParameter}'. Closing folder.");
                    base.Close();
                    return false; 
                }
                // Placeholders like "placeholder_d_encoder_press_i" won't be in _localIdToConfig
                // and will fall through to base.ProcessButtonEvent2 which should ignore them or handle as needed.
            }
            return base.ProcessButtonEvent2(actionParameter, buttonEvent);
        }

        public override PluginDynamicFolderNavigation GetNavigationArea(DeviceType _) =>
            PluginDynamicFolderNavigation.None;
        public override void ApplyAdjustment(string actionParameter, int ticks) 
        {
            // actionParameter here is the one returned by GetEncoderRotateActionNames, which should be a localId
            if (this._localIdToConfig.TryGetValue(actionParameter, out var config) && config.ActionType == NavigationDialActionType)
            {
                PluginLog.Info($"[{this.DisplayName}] ApplyAdjustment (NavigationDial Rotate): '{actionParameter}'. Closing folder.");
                this.Close();
                return;
            }
            if (this._localIdToConfig.TryGetValue(actionParameter, out var placeholderConfig) && placeholderConfig.ActionType == PlaceholderDialActionType)
            {
                // Placeholder dial rotated, do nothing
                return;
            }

            if (!this._localIdToGlobalActionParameter.TryGetValue(actionParameter, out var globalActionParameter))
                return;
            
            Logic_Manager_Base.Instance.ProcessDialAdjustment(globalActionParameter, ticks);
            this.AdjustmentValueChanged(this.CreateAdjustmentName(actionParameter)); 
        }

        public override void RunCommand(string actionParameter) 
        {
             // actionParameter here can be from a button press (localId) or an encoder press (localId or placeholder string)

            // Handle NavigateUpActionName (SDK's own back button for button area, if enabled)
            if (actionParameter.Equals(NavigateUpActionName)) 
            {
                this.Close();
                return;
            }

            // Check if it's a localId for a configured item (button or dial press)
            if (this._localIdToConfig.TryGetValue(actionParameter, out var config))
            {
                if (config.ActionType == NavigationDialActionType) // Press on our NavigationDial (Back)
                {
                    PluginLog.Info($"[{this.DisplayName}] RunCommand (NavigationDial Press): '{actionParameter}'. Closing folder.");
                    this.Close();
                    return;
                }
                if (config.ActionType == PlaceholderDialActionType) // Press on a PlaceholderDial
                {
                    // Placeholder dial pressed, do nothing
                    return;
                }

                // Regular button or dial press logic
                if (!this._localIdToGlobalActionParameter.TryGetValue(actionParameter, out var globalActionParameter))
                    return;

                if (config.ActionType.Contains("Dial")) // Covers ToggleDial, ParameterDial, 2ModeTickDial presses
                {
                    if (Logic_Manager_Base.Instance.ProcessDialPress(globalActionParameter))
                    {
                        this.AdjustmentValueChanged(this.CreateAdjustmentName(actionParameter)); 
                    }
                }
                else // Button actions (TriggerButton, ToggleButton, CombineButton, ParameterButton)
                {
                    Logic_Manager_Base.Instance.ProcessUserAction(globalActionParameter, this.DisplayName);

                    if (config.ActionType == "TriggerButton")
                    {
                        this._lastTriggerPressTimes[actionParameter] = DateTime.Now;
                        this.ButtonActionNamesChanged();
                        Task.Delay(200).ContinueWith(_ => { this.ButtonActionNamesChanged(); }); 
                    }
                    else if (config.ActionType == "CombineButton") 
                    {
                        this._lastCombineButtonPressTimes[actionParameter] = DateTime.Now;
                        this.ButtonActionNamesChanged();
                        Task.Delay(200).ContinueWith(_ => { this.ButtonActionNamesChanged(); }); 
                    }
                    else if (config.ActionType == "ToggleButton" || config.ActionType == "ParameterButton")
                    {
                        this.ButtonActionNamesChanged();
                    }
                }
            }
            // If actionParameter was not a localId (e.g., a placeholder string from GetEncoderPressActionNames, or old "Back"/"back")
            // It might be handled by ProcessButtonEvent2 for specific cases like legacy back, or ignored.
            // Legacy "Back" or "back" command string from other sources.
            else if (actionParameter == "Back" || actionParameter == "back") 
            {
                PluginLog.Info($"[{this.DisplayName}] RunCommand (Legacy 'Back'/'back' string): '{actionParameter}'. Closing folder.");
                base.Close(); 
                return;
            }
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
            return this._localIdToConfig.TryGetValue(actionParameter, out var c) ? (c.Title ?? c.DisplayName) : "ErrDisp";
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
            if (!this._localIdToConfig.TryGetValue(actionParameter, out var config) || 
                !this._localIdToGlobalActionParameter.TryGetValue(actionParameter, out var globalActionParameter))
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
        
        public override BitmapImage GetAdjustmentImage(string actionParameter, PluginImageSize imageSize)
        {
            // actionParameter 在这里是 localId
            if (string.IsNullOrEmpty(actionParameter)) return null;

            // 1. 获取配置
            // 如果是 "Back" 旋钮的特殊处理 (如果它没有自己的ButtonConfig)
            // 假设 "Back" 旋钮的 localId 就是 "Back" 或者通过 CreateAdjustmentName("Back") 创建
            if (actionParameter == this.CreateAdjustmentName("Back") || actionParameter == "Back")
            {
                // TODO: 如果Back也通过ButtonConfig定义，则下面这块不需要，会走标准路径
                return PluginImage.DrawElement(imageSize, new ButtonConfig { DisplayName = "Back", Title = "Back", ActionType="Dial" }, mainTitleOverride: "Back");
            }
            
            if (!this._localIdToConfig.TryGetValue(actionParameter, out var config) ||
                !this._localIdToGlobalActionParameter.TryGetValue(actionParameter, out var globalActionParameter))
            {
                PluginLog.Warning($"[DynamicFolder|GetAdjustmentImage] Config not found for localId: {actionParameter}. Returning default error image.");
                return PluginImage.DrawElement(imageSize, null, "Cfg?", isActive: true); 
            }

            try
            {
                BitmapImage customIcon = null;
                // 2. 图标加载逻辑
                string imagePathToLoad = !String.IsNullOrEmpty(config.ButtonImage) ? config.ButtonImage : null;
                if (string.IsNullOrEmpty(imagePathToLoad))
                {
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
                        PluginLog.Warning(ex, $"[DynamicFolder|GetAdjustmentImage] Failed to load icon '{imagePathToLoad}' for localId '{actionParameter}'. Will draw text only.");
                        customIcon = null;
                    }
                }

                // 3. 确定状态和动态文本
                bool isActive = false; // For ToggleDial
                string mainTitleOverride = config.Title ?? config.DisplayName;
                string valueTextDisplay = null; // For ParameterDial value
                int currentMode = 0;    // For 2ModeTickDial

                if (config.ActionType == "ToggleDial")
                {
                    isActive = Logic_Manager_Base.Instance.GetToggleState(globalActionParameter);
                }
                else if (config.ActionType == "ParameterDial" && config.ShowParameterInDial == "Yes")
                {
                     mainTitleOverride = Logic_Manager_Base.Instance.GetParameterDialSelectedTitle(globalActionParameter) ?? mainTitleOverride;
                }
                else if (config.ActionType == "2ModeTickDial")
                {
                    currentMode = Logic_Manager_Base.Instance.GetDialMode(globalActionParameter);
                    // title/color for mode is handled by PluginImage.DrawElement based on currentMode and config
                }
                // 旧的 GetAdjustmentImage 有一个 valueToDisplay 逻辑，目前PluginImage.DrawElement用valueText参数
                // 如果 ParameterDial (ShowParameterInDial="No") 需要在下方显示值，可以通过 valueText 传递
                // if (config.ActionType == "ParameterDial" && config.ShowParameterInDial != "Yes")
                // {
                //     valueTextDisplay = Logic_Manager_Base.Instance.GetParameterDialSelectedTitle(globalActionParameter);
                // }

                // 4. 调用 PluginImage.DrawElement
                return PluginImage.DrawElement(
                    imageSize,
                    config,
                    mainTitleOverride,
                    valueTextDisplay, 
                    isActive,
                    currentMode,
                    customIcon
                );
            }
            catch (Exception ex)
            {
                PluginLog.Error(ex, $"[DynamicFolder|GetAdjustmentImage] Unhandled exception for localId '{actionParameter}'.");
                return PluginImage.DrawElement(imageSize, config, "ERR!", isActive:true);
            }
        }

        public override string GetAdjustmentDisplayName(string actionParameter, PluginImageSize imageSize) => null;
        public override string GetAdjustmentValue(string actionParameter) => null;


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
            var localIdKvp = this._localIdToGlobalActionParameter.FirstOrDefault(kvp => kvp.Value == globalActionParameterThatChanged);
            if (EqualityComparer<KeyValuePair<string, string>>.Default.Equals(localIdKvp, default(KeyValuePair<string, string>)))
                return; // 这个 globalActionParameter 不由当前文件夹实例管理

            var localId = localIdKvp.Key;

            if (!this._localIdToConfig.TryGetValue(localId, out var config))
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

                foreach (var btnEntryKvp in this._localIdToConfig)
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
            this._localAdjustmentIds.Clear();
            this._localIdToGlobalActionParameter.Clear();
            this._localIdToConfig.Clear();
            PluginLog.Info($"[DynamicFolder] '{this.DisplayName}' Disposed.");
        }
        #endregion
    }
}