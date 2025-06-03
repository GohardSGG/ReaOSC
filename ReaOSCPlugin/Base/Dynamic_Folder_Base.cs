// 文件名: Base/Dynamic_Folder_Base.cs
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

    public abstract class Dynamic_Folder_Base : PluginDynamicFolder
    {
        private ButtonConfig _entryConfig;

        // --- 核心数据结构 ---
        // 存储“本地ID”，这些将在Get...ActionNames方法中用于生成完整的ActionParameter
        private readonly List<string> _localButtonIds = new List<string>();
        private readonly List<string> _localAdjustmentIds = new List<string>();

        private readonly Dictionary<string, string> _localIdToGlobalActionParameter = new Dictionary<string, string>();
        private readonly Dictionary<string, ButtonConfig> _localIdToConfig = new Dictionary<string, ButtonConfig>();

        private readonly Dictionary<string, DateTime> _lastTriggerPressTimes = new Dictionary<string, DateTime>();

        public Dynamic_Folder_Base()
        {
            Logic_Manager_Base.Instance.Initialize();

            var folderClassName = this.GetType().Name;
            var folderBaseName = folderClassName.Replace("_Dynamic", "").Replace("_", " ");

            this._entryConfig = Logic_Manager_Base.Instance.GetConfigByDisplayName("ReaOSC Dynamic", folderBaseName);
            if (this._entryConfig == null)
            {
                PluginLog.Error($"[DynamicFolder] 未能在 Logic_Manager 中找到 '{folderBaseName}' (GroupName: ReaOSC Dynamic) 的配置项。");
                this.DisplayName = folderBaseName;
                this.GroupName = "Dynamic";
                return;
            }

            this.DisplayName = this._entryConfig.DisplayName;
            this.GroupName = this._entryConfig.GroupName;
            this.Navigation = PluginDynamicFolderNavigation.ButtonArea;

            var folderContentFileName = $"{folderClassName.Replace("_Dynamic", "")}_List.json";
            var content = this.LoadFolderContent(folderContentFileName);
            if (content == null)
                return;

            this.PopulateLocalIdMappings(content);
        }

        #region 初始化与动作填充
        private FolderContentConfig LoadFolderContent(string fileName)
        {
            try
            {
                var resourceName = $"Loupedeck.ReaOSCPlugin.Dynamic.{fileName}";
                var assembly = Assembly.GetExecutingAssembly();
                using (var stream = assembly.GetManifestResourceStream(resourceName))
                {
                    if (stream == null)
                    { PluginLog.Error($"[DynamicFolder] 找不到资源: {resourceName}"); return null; }
                    using (var reader = new StreamReader(stream))
                    { return JsonConvert.DeserializeObject<FolderContentConfig>(reader.ReadToEnd()); }
                }
            }
            catch (Exception ex)
            {
                PluginLog.Error(ex, $"[DynamicFolder] 加载或解析 '{fileName}' 失败。");
                return null;
            }
        }

        // 【核心修正】此方法现在只填充映射和本地ID列表
        private void PopulateLocalIdMappings(FolderContentConfig content)
        {
            var allActions = content.Buttons.Concat(content.Dials);

            foreach (var config in allActions)
            {
                var kvp = Logic_Manager_Base.Instance.GetAllConfigs().FirstOrDefault(x => x.Value.DisplayName == config.DisplayName && x.Value.GroupName == config.GroupName);
                var globalActionParameter = kvp.Key;
                var loadedConfig = kvp.Value;

                if (string.IsNullOrEmpty(globalActionParameter) || loadedConfig == null)
                {
                    PluginLog.Warning($"[DynamicFolder] PopulateLocalIdMappings: 未能找到 '{config.DisplayName}' (GroupName: {config.GroupName}) 的全局配置。");
                    continue;
                }

                // 本地ID可以更简单，例如只用 DisplayName（需要确保在单个JSON内唯一）
                // 为了安全，我们组合 GroupName 和 DisplayName 来确保唯一性
                var localId = $"{loadedConfig.GroupName}_{loadedConfig.DisplayName}".Replace(" ", "");

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
        }
        #endregion

        #region PluginDynamicFolder 核心重写 (SDK 标准实现)

        // 【核心修正】在此处调用 CreateCommandName / CreateAdjustmentName
        public override IEnumerable<string> GetButtonPressActionNames() => this._localButtonIds.Select(id => this.CreateCommandName(id)).ToList();
        public override IEnumerable<string> GetEncoderRotateActionNames() => this._localAdjustmentIds.Select(id => this.CreateAdjustmentName(id)).ToList();
        public override IEnumerable<string> GetEncoderPressActionNames() => this._localAdjustmentIds.Select(id => this.CreateAdjustmentName(id)).ToList(); // 旋钮按下也用AdjustmentName

        public override void ApplyAdjustment(string actionParameter, int ticks)
        {
            // actionParameter 是本地ID
            if (!this._localIdToGlobalActionParameter.TryGetValue(actionParameter, out var globalActionParameter))
                return;
            if (!this._localIdToConfig.TryGetValue(actionParameter, out var config))
                return;

            var tempLastEventTimes = new Dictionary<string, DateTime>();
            Logic_Manager_Base.Instance.ProcessDialAdjustment(config, ticks, globalActionParameter, tempLastEventTimes);
            // 注意: AdjustmentValueChanged 需要的是由CreateAdjustmentName生成的完整参数，但我们只有本地ID。
            // 不过通常此方法用于通知值变化，对于OSC控制可能不是必须的，除非有UI直接显示此值。
            // 如果需要精确，需要将CreateAdjustmentName(actionParameter)的结果存储起来，或者这里直接用 globalActionParameter
            this.AdjustmentValueChanged(globalActionParameter);
        }

        public override void RunCommand(string actionParameter)
        {
            // actionParameter 是本地ID
            if (actionParameter.Equals(NavigateUpActionName))
            { this.Close(); return; }
            if (!this._localIdToGlobalActionParameter.TryGetValue(actionParameter, out var globalActionParameter))
                return;
            if (!this._localIdToConfig.TryGetValue(actionParameter, out var config))
                return;

            if (config.ActionType.Contains("Dial")) // 旋钮按下
            {
                if (Logic_Manager_Base.Instance.ProcessDialPress(config, globalActionParameter))
                {
                    this.EncoderActionNamesChanged();
                }
            }
            else // 普通按钮按下
            {
                Logic_Manager_Base.Instance.ProcessGeneralButtonPress(config, globalActionParameter);
                if (config.ActionType == "TriggerButton")
                {
                    this._lastTriggerPressTimes[actionParameter] = DateTime.Now; // 使用本地ID作为key
                    this.ButtonActionNamesChanged();
                    Task.Delay(200).ContinueWith(_ => this.ButtonActionNamesChanged());
                }
            }
        }

        public override string GetCommandDisplayName(string actionParameter, PluginImageSize imageSize) => this._localIdToConfig.TryGetValue(actionParameter, out var c) ? c.DisplayName : "Err";

        public override BitmapImage GetCommandImage(string actionParameter, PluginImageSize imageSize)
        {
            // actionParameter 是本地ID
            if (!this._localIdToConfig.TryGetValue(actionParameter, out var config))
                return null;

            using (var bitmapBuilder = new BitmapBuilder(imageSize))
            {
                BitmapColor currentBgColor = BitmapColor.Black;
                BitmapColor currentTitleColor = String.IsNullOrEmpty(config.TitleColor) ? BitmapColor.White : HexToBitmapColor(config.TitleColor);

                if (config.ActionType == "TriggerButton")
                {
                    if (this._lastTriggerPressTimes.TryGetValue(actionParameter, out var pressTime) && (DateTime.Now - pressTime).TotalMilliseconds < 200) // 使用本地ID
                    {
                        currentBgColor = String.IsNullOrEmpty(config.ActiveColor) ? new BitmapColor(0x50, 0x50, 0x50) : HexToBitmapColor(config.ActiveColor);
                    }
                }
                else if (config.ActionType == "ToggleButton" || config.ActionType == "ToggleDial")
                {
                    this._localIdToGlobalActionParameter.TryGetValue(actionParameter, out var globalParam); // 获取全局参数以查询状态
                    var isActive = Logic_Manager_Base.Instance.GetToggleState(globalParam);
                    currentBgColor = isActive && !String.IsNullOrEmpty(config.ActiveColor) ? HexToBitmapColor(config.ActiveColor) : BitmapColor.Black;
                    currentTitleColor = isActive ? (String.IsNullOrEmpty(config.ActiveTextColor) ? BitmapColor.White : HexToBitmapColor(config.ActiveTextColor)) : (String.IsNullOrEmpty(config.DeactiveTextColor) ? BitmapColor.White : HexToBitmapColor(config.DeactiveTextColor));
                }
                else if (config.ActionType == "2ModeTickDial")
                {
                    this._localIdToGlobalActionParameter.TryGetValue(actionParameter, out var globalParam); // 获取全局参数以查询状态
                    var currentMode = Logic_Manager_Base.Instance.GetDialMode(globalParam);
                    var title = currentMode == 0 ? config.Title : config.Title_Mode2;
                    var titleColor = currentMode == 0 ? (String.IsNullOrEmpty(config.TitleColor) ? BitmapColor.White : HexToBitmapColor(config.TitleColor)) : (String.IsNullOrEmpty(config.TitleColor_Mode2) ? BitmapColor.White : HexToBitmapColor(config.TitleColor_Mode2));
                    var bgColor = currentMode == 0 ? (String.IsNullOrEmpty(config.BackgroundColor) ? BitmapColor.Black : HexToBitmapColor(config.BackgroundColor)) : (String.IsNullOrEmpty(config.BackgroundColor_Mode2) ? BitmapColor.Black : HexToBitmapColor(config.BackgroundColor_Mode2));
                    bitmapBuilder.Clear(bgColor);
                    if (!String.IsNullOrEmpty(title))
                        bitmapBuilder.DrawText(text: title, fontSize: GetAutomaticDialTitleFontSize(title), color: titleColor);
                    return bitmapBuilder.ToImage();
                }

                bitmapBuilder.Clear(currentBgColor);
                if (!String.IsNullOrEmpty(config.Title))
                {
                    int fontSize = config.ActionType.Contains("Dial") ? GetAutomaticDialTitleFontSize(config.Title) : GetAutomaticButtonTitleFontSize(config.Title);
                    bitmapBuilder.DrawText(text: config.Title, fontSize: fontSize, color: currentTitleColor);
                }
                if (!String.IsNullOrEmpty(config.Text))
                {
                    var textColor = String.IsNullOrEmpty(config.TextColor) ? BitmapColor.White : HexToBitmapColor(config.TextColor);
                    bitmapBuilder.DrawText(text: config.Text, x: config.TextX ?? 50, y: config.TextY ?? 55, width: config.TextWidth ?? 14, height: config.TextHeight ?? 14, color: textColor, fontSize: config.TextSize ?? 14);
                }
                return bitmapBuilder.ToImage();
            }
        }

        public override BitmapImage GetButtonImage(PluginImageSize imageSize)
        {
            if (this._entryConfig == null)
                return base.GetButtonImage(imageSize);
            using (var bitmapBuilder = new BitmapBuilder(imageSize))
            {
                bitmapBuilder.Clear(BitmapColor.Black);
                if (!String.IsNullOrEmpty(this._entryConfig.Title))
                {
                    bitmapBuilder.DrawText(text: this._entryConfig.Title, fontSize: GetAutomaticButtonTitleFontSize(this._entryConfig.Title), color: BitmapColor.White);
                }
                if (!String.IsNullOrEmpty(this._entryConfig.Text))
                {
                    var textColor = String.IsNullOrEmpty(this._entryConfig.TextColor) ? BitmapColor.White : HexToBitmapColor(this._entryConfig.TextColor);
                    bitmapBuilder.DrawText(text: this._entryConfig.Text, x: this._entryConfig.TextX ?? 50, y: this._entryConfig.TextY ?? 55, width: this._entryConfig.TextWidth ?? 14, height: this._entryConfig.TextHeight ?? 14, color: textColor, fontSize: this._entryConfig.TextSize ?? 14);
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
    }
}