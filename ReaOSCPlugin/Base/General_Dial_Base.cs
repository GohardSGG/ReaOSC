// 文件名: Base/General_Dial_Base.cs
namespace Loupedeck.ReaOSCPlugin.Base
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.IO; // 【新增】 用于Path相关操作

    using Loupedeck.ReaOSCPlugin.Helpers; // 【新增】引用新的绘图类

    // 【修改】实现 IDisposable 接口
    public class General_Dial_Base : PluginDynamicAdjustment, IDisposable
    {
        private readonly Dictionary<string, DateTime> _lastEventTimes = new Dictionary<string, DateTime>();
        private readonly Logic_Manager_Base _logicManager = Logic_Manager_Base.Instance; // 【新增】方便引用

        public General_Dial_Base() : base(hasReset: true)
        {
            this._logicManager.Initialize();

            foreach (var entry in this._logicManager.GetAllConfigs())
            {
                var config = entry.Value;
                if (config.ActionType == "TickDial" || config.ActionType == "ToggleDial" || config.ActionType == "2ModeTickDial")
                {
                    this.AddParameter(entry.Key, config.DisplayName, config.GroupName, $"旋钮调整: {config.DisplayName}");
                    if (config.ActionType == "TickDial") // TickDial仍需管理自己的lastEventTimes，用于加速度
                    {
                        this._lastEventTimes[entry.Key] = DateTime.MinValue;
                    }
                }
            }

            // 【移除旧的、有问题的OSCStateManager订阅】
            // OSCStateManager.Instance.StateChanged += (s, e) => { ... };

            // 【新增】订阅Logic_Manager_Base的CommandStateNeedsRefresh事件
            this._logicManager.CommandStateNeedsRefresh += this.OnLogicManagerCommandStateNeedsRefresh;
        }

        // 【新增】处理来自Logic_Manager_Base的状态刷新通知的方法
        private void OnLogicManagerCommandStateNeedsRefresh(object sender, string actionParameterThatChanged)
        {
            // 当Logic_Manager_Base通知某个actionParameter的状态已更新时，
            // 我们需要检查这个actionParameter是否由当前这个General_Dial_Base实例管理。
            // PluginDynamicAdjustment基类并没有一个简单的方法来获取它已注册的所有参数名。
            // 但是，Loupedeck SDK的ActionImageChanged如果被调用了一个不属于此实例的参数名，
            // 它应该会忽略或者至少不会出错。
            // 为了更精确（尤其如果一个插件中因为某种原因注册了多个General_Dial_Base实例），
            // 我们应该只对本实例确实添加过的参数调用ActionImageChanged。
            // 我们可以通过检查_allConfigs（虽然它是Logic_Manager_Base的成员）来确认类型，
            // 但更关键的是这个actionParameterThatChanged是不是this.AddParameter注册的entry.Key之一。

            // 一个简单有效的方法是：如果当前类的设计是每个General_Dial_Base实例都注册了
            // 一批全局唯一的actionParameter（这是当前的设计），那么直接调用即可。
            var config = this._logicManager.GetConfig(actionParameterThatChanged);
            if (config != null && (config.ActionType == "ToggleDial")) // 主要关心ToggleDial的外部状态更新
            {
                PluginLog.Info($"[GeneralDialBase] 接收到状态刷新请求 for '{actionParameterThatChanged}', 调用 ActionImageChanged。");
                this.ActionImageChanged(actionParameterThatChanged);
            }
        }

        protected override void ApplyAdjustment(string actionParameter, int ticks) // 【无改动】
        {
            var config = this._logicManager.GetConfig(actionParameter);
            if (config == null)
                return;
            this._logicManager.ProcessDialAdjustment(config, ticks, actionParameter, this._lastEventTimes);
            this.AdjustmentValueChanged(actionParameter); // 通知Loupedeck值已改变 (即使我们不直接显示值)
        }

        protected override void RunCommand(string actionParameter) // 【无改动】
        {
            var config = this._logicManager.GetConfig(actionParameter);
            if (config == null)
                return;
            if (this._logicManager.ProcessDialPress(config, actionParameter)) // 如果ProcessDialPress返回true (例如2ModeTickDial切换了模式)
            {
                this.ActionImageChanged(actionParameter); // 则刷新UI
            }
        }

        protected override string GetAdjustmentValue(string actionParameter) => null; // 【无改动】

        protected override BitmapImage GetCommandImage(string actionParameter, PluginImageSize imageSize)
        {
            var config = this._logicManager.GetConfig(actionParameter);
            if (config == null)
            {
                PluginLog.Warning($"[GeneralDialBase|GetCommandImage] Config not found for actionParameter: {actionParameter}. Returning default image.");
                return PluginImage.DrawElement(imageSize, null); 
            }

            try
            {
                BitmapImage customIcon = null;
                // 1. 图标加载逻辑 (统一) - 参照 General_Button_Base
                string imagePathToLoad = !String.IsNullOrEmpty(config.ButtonImage) ? config.ButtonImage : null;
                if (string.IsNullOrEmpty(imagePathToLoad))
                {
                    var displayNameFromActionParam = actionParameter.Split('/').LastOrDefault(); 
                    // Extract the part after the last '/', which should be DisplayName or DisplayName + "/DialAction"
                    var namePartToMatch = displayNameFromActionParam;
                    if (namePartToMatch.EndsWith("/DialAction"))
                    {
                        namePartToMatch = namePartToMatch.Substring(0, namePartToMatch.Length - "/DialAction".Length);
                    }

                    if (!string.IsNullOrEmpty(namePartToMatch) &&
                        Logic_Manager_Base.SanitizeOscPathSegment(namePartToMatch) == Logic_Manager_Base.SanitizeOscPathSegment(config.DisplayName))
                    {
                        imagePathToLoad = $"{Logic_Manager_Base.SanitizeOscPathSegment(config.DisplayName)}.png";
                    }
                }

                if (!string.IsNullOrEmpty(imagePathToLoad))
                {
                    try
                    {
                        customIcon = PluginResources.ReadImage(imagePathToLoad);
                    }
                    catch (Exception ex)
                    {
                        PluginLog.Error(ex, $"[GeneralDialBase|GetCommandImage] Failed to load icon '{imagePathToLoad}' for action '{actionParameter}'. Will draw text only.");
                        customIcon = null;
                    }
                }

                // 2. 确定状态和动态文本
                bool isActive = false; // 主要用于ToggleDial
                string mainTitleOverride = null;
                string valueText = null; // 用于显示如参数值等
                int currentMode = 0;    // 主要用于2ModeTickDial

                if (config.ActionType == "ToggleDial")
                {
                    isActive = this._logicManager.GetToggleState(actionParameter);
                    mainTitleOverride = config.Title ?? config.DisplayName;
                }
                else if (config.ActionType == "2ModeTickDial")
                {
                    currentMode = this._logicManager.GetDialMode(actionParameter);
                    // mainTitleOverride 将在 PluginImage.DrawElement 中根据 currentMode 和 config 确定
                    // 所以这里不需要特别设置 mainTitleOverride，除非有特定逻辑
                    mainTitleOverride = config.Title ?? config.DisplayName; // 基准标题
                }
                // ParameterDial 通常在 Dynamic_Folder_Base 中处理其特定显示逻辑
                // 但如果 General_Dial_Base 需要支持 ParameterDial 类型的独立旋钮，则在这里添加
                // else if (config.ActionType == "ParameterDial") { ... }
                else // TickDial 及其他
                {
                    mainTitleOverride = config.Title ?? config.DisplayName;
                }

                // 如果有 config.Text, 它会被 PluginImage.DrawElement 处理

                return PluginImage.DrawElement(
                    imageSize,
                    config,
                    mainTitleOverride,
                    valueText,         // 传递给 PluginImage，如果以后有值显示需求
                    isActive,          // 对于 ToggleDial
                    currentMode,       // 对于 2ModeTickDial
                    customIcon,
                    false
                );
            }
            catch (Exception ex)
            {
                PluginLog.Error(ex, $"[GeneralDialBase|GetCommandImage] Unhandled exception for action '{actionParameter}'.");
                return PluginImage.DrawElement(imageSize, config, "ERR!", isActive: true); 
            }
        }

        // 【新增】Dispose 方法来取消事件订阅
        public void Dispose()
        {
            if (Logic_Manager_Base.Instance != null) // 确保单例仍然存在
            {
                Logic_Manager_Base.Instance.CommandStateNeedsRefresh -= this.OnLogicManagerCommandStateNeedsRefresh;
            }
            // 如果 PluginDynamicAdjustment 基类实现了 IDisposable，并且需要调用 base.Dispose()，
            // 则应在此处添加 base.Dispose(); (但通常Loupedeck的命令和调整项不需要显式调用基类Dispose)
        }

        #region UI辅助方法 
        // 【移除】GetAutomaticDialTitleFontSize 和 HexToBitmapColor，因为它们已移至 PluginImage.cs
        // private int GetAutomaticDialTitleFontSize(String title) { ... }
        // private static BitmapColor HexToBitmapColor(string hexColor) { ... }
        #endregion
    }
}