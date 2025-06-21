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

    public abstract class Dynamic_Folder_Base : PluginDynamicFolder, IDisposable // 【我之前的版本添加了IDisposable，如果您的旧版没有，可以移除】
    {
        private ButtonConfig _entryConfig;

        private readonly List<string> _localButtonIds = new List<string>();
        private readonly List<string> _localAdjustmentIds = new List<string>();

        private readonly Dictionary<string, string> _localIdToGlobalActionParameter = new Dictionary<string, string>();
        private readonly Dictionary<string, ButtonConfig> _localIdToConfig = new Dictionary<string, ButtonConfig>();

        private readonly Dictionary<string, DateTime> _lastTriggerPressTimes = new Dictionary<string, DateTime>();
        private readonly Dictionary<string, DateTime> _lastCombineButtonPressTimes = new Dictionary<string, DateTime>(); // 用于CombineButton的UI反馈

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
        
        // 修改 GetEncoderRotateActionNames 以按顺序加载 _localAdjustmentIds 中的旋钮，并将"Back"置于索引5
        public override IEnumerable<string> GetEncoderRotateActionNames()
        {
            var rotateActionNames = new List<String>(new String[6]); // 假设6个编码器

            // 从 _localAdjustmentIds 填充编码器0-4，最多5个
            for (int i = 0; i < 5; i++)
            {
                if (i < this._localAdjustmentIds.Count)
                {
                    rotateActionNames[i] = this._localAdjustmentIds[i]; // 使用 localId
                }
                else
                {
                    rotateActionNames[i] = null; // 如果没有那么多旋钮，则为 null
                }
            }

            rotateActionNames[5] = "Back";  // 索引 5: "Back" (用于旋转)
            // 注意: _localAdjustmentIds 不再由此方法直接添加，用户需另行处理或修改此处
            return rotateActionNames.Select(s => String.IsNullOrEmpty(s) ? null : this.CreateAdjustmentName(s));
        }

        // 修改 GetEncoderPressActionNames 以便在编码器0-4有旋钮时，按下操作对应旋钮的localId，否则为占位符。索引5为"back"。
        public override IEnumerable<String> GetEncoderPressActionNames(DeviceType deviceType)
        {
            var pressActionNames = new List<String>(new String[6]); // 假设6个编码器, 初始化为null

            // 从 _localAdjustmentIds 填充编码器0-4的按下事件
            for (int i = 0; i < 5; i++)
            {
                if (i < this._localAdjustmentIds.Count)
                {
                    pressActionNames[i] = this._localAdjustmentIds[i]; // 使用旋钮的 localId 作为按下动作参数
                }
                else
                {
                    // 如果没有对应的旋钮，则使用占位符
                    pressActionNames[i] = $"placeholder_d_encoder_press_{i}"; 
                }
            }
            
            pressActionNames[5] = "back"; // "back" 按下在索引5                     
            // 注意: _localAdjustmentIds 对应的按下事件不再由此方法直接添加
            return pressActionNames.Select(s => String.IsNullOrEmpty(s) ? null : base.CreateCommandName(s));
        }
        
        // 新增 ProcessButtonEvent2 以处理编码器按下事件，与 FX_Folder_Base.cs 一致
        public override Boolean ProcessButtonEvent2(String actionParameter, DeviceButtonEvent2 buttonEvent)
        {
            if (buttonEvent.EventType == DeviceButtonEventType.Press)
            {
                if (actionParameter == "back") // 匹配简单小写名称 "back"
                {
                    PluginLog.Info($"[{this.DisplayName}] ProcessButtonEvent2: Matched 'back'. Closing folder.");
                    base.Close();
                    return false; // 与 FX_Folder_Base.cs 保持一致
                }
            }
            return base.ProcessButtonEvent2(actionParameter, buttonEvent);
        }

        public override PluginDynamicFolderNavigation GetNavigationArea(DeviceType _) =>
            PluginDynamicFolderNavigation.None;
        public override void ApplyAdjustment(string actionParameter, int ticks) 
        {
            if (actionParameter == "Back") // 处理来自索引5的 "Back" 旋转
            {
                PluginLog.Info($"[FX_Folder_Base] ApplyAdjustment: Matched 'Back' for rotation. Closing folder.");
                this.Close();
                return;
            }

            if (!this._localIdToGlobalActionParameter.TryGetValue(actionParameter, out var globalActionParameter))
                return;
            
            Logic_Manager_Base.Instance.ProcessDialAdjustment(globalActionParameter, ticks);
            this.AdjustmentValueChanged(this.CreateAdjustmentName(actionParameter)); 
        }

        public override void RunCommand(string actionParameter) 
        {
            // "back" 按下事件由 ProcessButtonEvent2 处理，但如果 "Back" 命令从其他地方触发，这里也可以处理
            if (actionParameter == "Back" || actionParameter == "back") 
            {
                base.Close(); 
                return;
            }

            if (actionParameter.Equals(NavigateUpActionName)) 
            {
                this.Close();
                return;
            }
            
            if (!this._localIdToGlobalActionParameter.TryGetValue(actionParameter, out var globalActionParameter))
                return;
            if (!this._localIdToConfig.TryGetValue(actionParameter, out var config))
                return;

            if (config.ActionType.Contains("Dial"))
            {
                if (Logic_Manager_Base.Instance.ProcessDialPress(globalActionParameter))
                {
                    this.AdjustmentValueChanged(this.CreateAdjustmentName(actionParameter)); 
                }
            }
            else 
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
        

        public override string GetCommandDisplayName(string actionParameter, PluginImageSize imageSize)
        {
            // 为 "back" 按下命令提供显示名称 (如果需要，但通常图像更重要)
            if (actionParameter == base.CreateCommandName("back"))
            {
                return "Back";
            }
            // 为占位符提供显示名称 (如果需要)
            if (actionParameter != null && actionParameter.StartsWith("placeholder_d_encoder_press_"))
            {
                return ""; // 或 null
            }

            if (actionParameter.Equals(NavigateUpActionName))
                return base.GetCommandDisplayName(this.CreateCommandName(actionParameter), imageSize);
            return this._localIdToConfig.TryGetValue(actionParameter, out var c) ? (c.Title ?? c.DisplayName) : "ErrDisp";
        }

        public override BitmapImage GetCommandImage(string actionParameter, PluginImageSize imageSize)
        {
            // 与 FX_Folder_Base.cs 一致，优先处理 "back" 和占位符
            if (actionParameter == base.CreateCommandName("back"))
            {
                using (var bitmapBuilder = new BitmapBuilder(imageSize))
                {
                    bitmapBuilder.Clear(BitmapColor.Black);
                    var fontSize = 12; 
                    bitmapBuilder.DrawText("Back", color: BitmapColor.White, fontSize: fontSize);
                    return bitmapBuilder.ToImage();
                }
            }
            else if (actionParameter != null && actionParameter.StartsWith("placeholder_d_encoder_press_"))
            {
                using (var bitmapBuilder = new BitmapBuilder(imageSize))
                {
                    bitmapBuilder.Clear(BitmapColor.Black); // 占位符为空白
                    return bitmapBuilder.ToImage();
                }
            }

            // --- 原有的 Dynamic_Folder_Base.cs 绘制逻辑 ---
            if (!this._localIdToConfig.TryGetValue(actionParameter, out var config) ||
                !this._localIdToGlobalActionParameter.TryGetValue(actionParameter, out var globalActionParameter))
            {
                // 对于其他未明确处理的动作（非 "back"，非占位符，且不在 _localIdToConfig 中）
                // 可以绘制 "?" 或一个错误指示
                using (var errBB = new BitmapBuilder(imageSize))
                { 
                    errBB.Clear(BitmapColor.Black); // 与占位符一致，或用不同颜色标示错误
                    errBB.DrawText("?", BitmapColor.White, GetAutomaticButtonTitleFontSize("?")); 
                    return errBB.ToImage(); 
                }
            }

            // --- 剩余的绘制逻辑来自原 Dynamic_Folder_Base.cs ---
            using (var bitmapBuilder = new BitmapBuilder(imageSize))
            {
                BitmapColor currentBgColor = !String.IsNullOrEmpty(config.BackgroundColor) ? HexToBitmapColor(config.BackgroundColor) : BitmapColor.Black;
                BitmapColor currentTitleColor = String.IsNullOrEmpty(config.TitleColor) ? BitmapColor.White : HexToBitmapColor(config.TitleColor);
                string mainTitleToDraw = config.Title ?? config.DisplayName;
                int fontSize;

                if (config.ActionType == "TriggerButton")
                {
                    if (this._lastTriggerPressTimes.TryGetValue(actionParameter, out var pressTime) && (DateTime.Now - pressTime).TotalMilliseconds < 200)
                        currentBgColor = String.IsNullOrEmpty(config.ActiveColor) ? new BitmapColor(0x50, 0x50, 0x50) : HexToBitmapColor(config.ActiveColor);
                }
                else if (config.ActionType == "CombineButton")
                {
                    if (this._lastCombineButtonPressTimes.TryGetValue(actionParameter, out var pressTime) && (DateTime.Now - pressTime).TotalMilliseconds < 200)
                        currentBgColor = String.IsNullOrEmpty(config.ActiveColor) ? new BitmapColor(0x50, 0x50, 0x50) : HexToBitmapColor(config.ActiveColor);
                }
                else if (config.ActionType == "ToggleButton" || config.ActionType == "ToggleDial")
                {
                    var isActive = Logic_Manager_Base.Instance.GetToggleState(globalActionParameter);
                    currentBgColor = isActive && !String.IsNullOrEmpty(config.ActiveColor) ? HexToBitmapColor(config.ActiveColor) : currentBgColor;
                    currentTitleColor = isActive
                        ? (String.IsNullOrEmpty(config.ActiveTextColor) ? BitmapColor.White : HexToBitmapColor(config.ActiveTextColor))
                        : (String.IsNullOrEmpty(config.DeactiveTextColor) ? currentTitleColor : HexToBitmapColor(config.DeactiveTextColor));
                }
                else if (config.ActionType == "ParameterDial")
                {
                    mainTitleToDraw = (config.ShowParameterInDial == "Yes")
                        ? (Logic_Manager_Base.Instance.GetParameterDialSelectedTitle(globalActionParameter) ?? mainTitleToDraw)
                        : mainTitleToDraw;
                }
                else if (config.ActionType == "ParameterButton")
                {
                    var sourceDialGlobalParam = this.FindSourceDialGlobalActionParameter(config, config.ParameterSourceDial);
                    mainTitleToDraw = !string.IsNullOrEmpty(sourceDialGlobalParam)
                        ? (Logic_Manager_Base.Instance.GetParameterDialSelectedTitle(sourceDialGlobalParam) ?? config.Title ?? config.DisplayName)
                        : $"Err:{config.ParameterSourceDial?.Substring(0, Math.Min(config.ParameterSourceDial?.Length ?? 0, 6))}";
                }
                else if (config.ActionType == "2ModeTickDial")
                {
                    var currentMode = Logic_Manager_Base.Instance.GetDialMode(globalActionParameter);
                    mainTitleToDraw = currentMode == 0 ? (config.Title ?? config.DisplayName) : (config.Title_Mode2 ?? config.Title ?? config.DisplayName);
                    currentTitleColor = currentMode == 0 ? (String.IsNullOrEmpty(config.TitleColor) ? BitmapColor.White : HexToBitmapColor(config.TitleColor)) : (String.IsNullOrEmpty(config.TitleColor_Mode2) ? BitmapColor.White : HexToBitmapColor(config.TitleColor_Mode2));
                    currentBgColor = currentMode == 0 ? (String.IsNullOrEmpty(config.BackgroundColor) ? BitmapColor.Black : HexToBitmapColor(config.BackgroundColor)) : (String.IsNullOrEmpty(config.BackgroundColor_Mode2) ? BitmapColor.Black : HexToBitmapColor(config.BackgroundColor_Mode2));
                }

                bitmapBuilder.Clear(currentBgColor);
                fontSize = config.ActionType.Contains("Dial") ? GetAutomaticDialTitleFontSize(mainTitleToDraw) : GetAutomaticButtonTitleFontSize(mainTitleToDraw);

                if (!String.IsNullOrEmpty(mainTitleToDraw))
                {
                    bitmapBuilder.DrawText(mainTitleToDraw, currentTitleColor, fontSize);
                }
                if (!String.IsNullOrEmpty(config.Text))
                {
                    var textColor = String.IsNullOrEmpty(config.TextColor) ? BitmapColor.White : HexToBitmapColor(config.TextColor);
                    bitmapBuilder.DrawText(config.Text, config.TextX ?? 50, config.TextY ?? 55, config.TextWidth ?? 14, config.TextHeight ?? 14, textColor, config.TextSize ?? 14);
                }
                return bitmapBuilder.ToImage();
            }
        }
        
        public override BitmapImage GetAdjustmentImage(string actionParameter, PluginImageSize imageSize)
        {
            if (string.IsNullOrEmpty(actionParameter)) return null;

            // 与 FX_Folder_Base.cs 一致，为 "Back" 旋转调整绘制图像
            if (actionParameter == this.CreateAdjustmentName("Back")) // 比较完整的调整名
            {
                using (var bitmapBuilder = new BitmapBuilder(imageSize))
                {
                    bitmapBuilder.Clear(BitmapColor.Black);
                    bitmapBuilder.DrawText("Back", BitmapColor.White, GetAutomaticDialTitleFontSize("Back"));
                    return bitmapBuilder.ToImage();
                }
            }
            // 原 Dynamic_Folder_Base.cs 中对 "Back" 的处理是基于简单名称，
            // 为保持一致性，如果 CreateAdjustmentName("Back") 结果不等于 "Back"，也处理简单名称。
            // 但 FX_Folder_Base 的逻辑是直接用简单名称 "Back" 注册和处理，这里我们优先匹配完整名称。
            else if (actionParameter == "Back") // 简单名称 "Back" 作为后备
            {
                 using (var bitmapBuilder = new BitmapBuilder(imageSize))
                {
                    bitmapBuilder.Clear(BitmapColor.Black);
                    bitmapBuilder.DrawText("Back", BitmapColor.White, GetAutomaticDialTitleFontSize("Back"));
                    return bitmapBuilder.ToImage();
                }
            }


            if (!this._localIdToConfig.TryGetValue(actionParameter, out var config) ||
                !this._localIdToGlobalActionParameter.TryGetValue(actionParameter, out var globalActionParameter))
            {
                using (var errBB = new BitmapBuilder(imageSize))
                { 
                    errBB.Clear(new BitmapColor(100, 0, 0)); 
                    errBB.DrawText("Cfg?", BitmapColor.White, GetAutomaticDialTitleFontSize("Cfg?")); 
                    return errBB.ToImage(); 
                }
            }

            using (var bitmapBuilder = new BitmapBuilder(imageSize))
            {
                BitmapColor currentBgColor = !String.IsNullOrEmpty(config.BackgroundColor) ? HexToBitmapColor(config.BackgroundColor) : BitmapColor.Black;
                BitmapColor currentTitleColor = String.IsNullOrEmpty(config.TitleColor) ? BitmapColor.White : HexToBitmapColor(config.TitleColor);
                string mainTitleToDraw = config.Title ?? config.DisplayName; 

                if (config.ActionType == "ToggleDial")
                {
                    var isActive = Logic_Manager_Base.Instance.GetToggleState(globalActionParameter);
                    currentBgColor = isActive && !String.IsNullOrEmpty(config.ActiveColor) ? HexToBitmapColor(config.ActiveColor) : currentBgColor;
                    currentTitleColor = isActive
                        ? (String.IsNullOrEmpty(config.ActiveTextColor) ? BitmapColor.White : HexToBitmapColor(config.ActiveTextColor))
                        : (String.IsNullOrEmpty(config.DeactiveTextColor) ? currentTitleColor : HexToBitmapColor(config.DeactiveTextColor));
                }
                else if (config.ActionType == "ParameterDial" && config.ShowParameterInDial == "Yes")
                {
                     mainTitleToDraw = Logic_Manager_Base.Instance.GetParameterDialSelectedTitle(globalActionParameter) ?? mainTitleToDraw;
                }
                else if (config.ActionType == "2ModeTickDial")
                {
                    var currentMode = Logic_Manager_Base.Instance.GetDialMode(globalActionParameter);
                    mainTitleToDraw = currentMode == 0 ? (config.Title ?? config.DisplayName) : (config.Title_Mode2 ?? config.Title ?? config.DisplayName);
                    currentTitleColor = currentMode == 0 
                        ? (String.IsNullOrEmpty(config.TitleColor) ? BitmapColor.White : HexToBitmapColor(config.TitleColor)) 
                        : (String.IsNullOrEmpty(config.TitleColor_Mode2) ? BitmapColor.White : HexToBitmapColor(config.TitleColor_Mode2));
                    currentBgColor = currentMode == 0 
                        ? (String.IsNullOrEmpty(config.BackgroundColor) ? BitmapColor.Black : HexToBitmapColor(config.BackgroundColor)) 
                        : (String.IsNullOrEmpty(config.BackgroundColor_Mode2) ? BitmapColor.Black : HexToBitmapColor(config.BackgroundColor_Mode2));
                }

                bitmapBuilder.Clear(currentBgColor);
                var titleFontSize = GetAutomaticDialTitleFontSize(mainTitleToDraw); 
                
                string valueToDisplay = null; // Placeholder for potential value string logic

                if (!String.IsNullOrEmpty(valueToDisplay))
                {
                    bitmapBuilder.DrawText(mainTitleToDraw, 0, 5, bitmapBuilder.Width, 30, currentTitleColor, titleFontSize); 
                    var valueFontSize = GetAutomaticDialTitleFontSize(valueToDisplay); 
                    bitmapBuilder.DrawText(valueToDisplay, 0, 35, bitmapBuilder.Width, bitmapBuilder.Height - 40, currentTitleColor, valueFontSize); 
                }
                else
                {
                    bitmapBuilder.DrawText(mainTitleToDraw, currentTitleColor, titleFontSize);
                }
                
                return bitmapBuilder.ToImage();
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