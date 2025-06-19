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
            this.Navigation = PluginDynamicFolderNavigation.ButtonArea;

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
        public override IEnumerable<string> GetEncoderRotateActionNames() => this._localAdjustmentIds.Select(id => this.CreateAdjustmentName(id)).ToList();
        public override IEnumerable<string> GetEncoderPressActionNames() => this._localAdjustmentIds.Select(id => this.CreateAdjustmentName(id)).ToList();

        public override void ApplyAdjustment(string actionParameter, int ticks) // actionParameter 是 localId
        {
            if (!this._localIdToGlobalActionParameter.TryGetValue(actionParameter, out var globalActionParameter))
                return;
            // 【注意】旧代码中ProcessDialAdjustment的签名与Logic_Manager_Base当前版本可能不同
            // 当前Logic_Manager_Base.ProcessDialAdjustment(string globalActionParameter, int ticks)
            Logic_Manager_Base.Instance.ProcessDialAdjustment(globalActionParameter, ticks);
            // AdjustmentValueChanged 需要的是由CreateAdjustmentName生成的、Loupedeck SDK 可识别的完整参数名
            this.AdjustmentValueChanged(this.CreateAdjustmentName(actionParameter));
        }

        public override void RunCommand(string actionParameter) // actionParameter 是 localId
        {
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
                // 调用Logic_Manager_Base中处理旋钮按下的方法
                if (Logic_Manager_Base.Instance.ProcessDialPress(globalActionParameter))
                {
                    this.EncoderActionNamesChanged();
                }
            }
            else
            {
                // 调用Logic_Manager_Base中处理按钮按下的方法 (ProcessUserAction是较新的统一接口)
                Logic_Manager_Base.Instance.ProcessUserAction(globalActionParameter, this.DisplayName);

                if (config.ActionType == "TriggerButton")
                {
                    this._lastTriggerPressTimes[actionParameter] = DateTime.Now;
                    this.ButtonActionNamesChanged();
                    Task.Delay(200).ContinueWith(_ =>
                    {
                        // 确保在回调中访问的字典仍然包含该键，并且插件上下文仍然有效。
                        // 您的旧代码仅调用 this.ButtonActionNamesChanged()。为保持一致性：
                        this.ButtonActionNamesChanged();
                    }); // 【修正】移除了 TaskScheduler.FromCurrentSynchronizationContext() 和 IsLoaded
                }
                else if (config.ActionType == "CombineButton") // CombineButton的UI反馈
                {
                    this._lastCombineButtonPressTimes[actionParameter] = DateTime.Now;
                    this.ButtonActionNamesChanged();
                    Task.Delay(200).ContinueWith(_ =>
                    {
                        this.ButtonActionNamesChanged();
                    }); // 【修正】移除了 TaskScheduler.FromCurrentSynchronizationContext() 和 IsLoaded
                }
                else if (config.ActionType == "ToggleButton" || config.ActionType == "ParameterButton")
                {
                    this.ButtonActionNamesChanged();
                }
            }
        }

        public override string GetCommandDisplayName(string actionParameter, PluginImageSize imageSize)
        {
            if (actionParameter.Equals(NavigateUpActionName))
                return base.GetCommandDisplayName(this.CreateCommandName(actionParameter), imageSize);
            // 使用 Title 属性（如果存在），否则使用 DisplayName
            return this._localIdToConfig.TryGetValue(actionParameter, out var c) ? (c.Title ?? c.DisplayName) : "ErrDisp";
        }

        public override BitmapImage GetCommandImage(string actionParameter, PluginImageSize imageSize)
        {
            if (!this._localIdToConfig.TryGetValue(actionParameter, out var config) ||
                !this._localIdToGlobalActionParameter.TryGetValue(actionParameter, out var globalActionParameter))
            {
                using (var errBB = new BitmapBuilder(imageSize))
                { errBB.Clear(new BitmapColor(139, 0, 0)); errBB.DrawText("CFG?", BitmapColor.White, GetAutomaticButtonTitleFontSize("CFG?")); return errBB.ToImage(); }
            }

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

            if (config.ActionType.Contains("Dial"))
            {
                this.EncoderActionNamesChanged();
            }
            else
            {
                this.ButtonActionNamesChanged();
            }

            // 特别处理 ParameterDial 更新时，需要刷新关联的 ParameterButton
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
                        // ButtonActionNamesChanged() 已经在前面根据 config 类型调用过了。
                        // 如果 ParameterDial 的变化也触发了 ButtonActionNamesChanged() (例如如果它不被视为Dial类型，或者逻辑如此)，
                        // 则这里不需要额外调用。但如果ParameterDial被视为Dial，则上面会调用EncoderActionNamesChanged()。
                        // 此时，为了更新ParameterButton（它是Button），确实需要调用ButtonActionNamesChanged()。
                        this.ButtonActionNamesChanged();
                        break; // 假设一次刷新就够了，因为会刷新所有按钮
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

            string dialsJsonGroupName = Logic_Manager_Base.SanitizeOscPathSegment(parameterButtonConfig.GroupName);
            string dialDisplayName = Logic_Manager_Base.SanitizeOscPathSegment(sourceDialDisplayNameFromButtonConfig);

            string globalParamAttempt = $"/{dialsJsonGroupName}/{dialDisplayName}/DialAction".Replace("//", "/");

            var foundDialConfig = Logic_Manager_Base.Instance.GetConfig(globalParamAttempt);
            if (foundDialConfig != null && foundDialConfig.ActionType == "ParameterDial")
            {
                return globalParamAttempt;
            }

            PluginLog.Warning($"[DynamicFolder] FindSourceDialGlobal ('{this.DisplayName}'): ParameterButton '{parameterButtonConfig.DisplayName}' could not find source ParameterDial '{sourceDialDisplayNameFromButtonConfig}' using key '{globalParamAttempt}' (ParameterButton's JSON Group: '{parameterButtonConfig.GroupName}'). Check if the dial exists with this GroupName/DisplayName in Logic_Manager.");
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