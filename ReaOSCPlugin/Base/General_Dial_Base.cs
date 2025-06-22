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
        private readonly Dictionary<String, DateTime> _lastEventTimes = new Dictionary<String, DateTime>();
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
        private void OnLogicManagerCommandStateNeedsRefresh(Object sender, String actionParameterThatChanged)
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

        protected override void ApplyAdjustment(String actionParameter, Int32 ticks) // 【无改动】
        {
            var config = this._logicManager.GetConfig(actionParameter);
            if (config == null)
            {
                return;
            }

            this._logicManager.ProcessDialAdjustment(config, ticks, actionParameter, this._lastEventTimes);
            this.AdjustmentValueChanged(actionParameter); // 通知Loupedeck值已改变 (即使我们不直接显示值)
        }

        protected override void RunCommand(String actionParameter) // 【无改动】
        {
            var config = this._logicManager.GetConfig(actionParameter);
            if (config == null)
            {
                return;
            }

            if (this._logicManager.ProcessDialPress(config, actionParameter)) // 如果ProcessDialPress返回true (例如2ModeTickDial切换了模式)
            {
                this.ActionImageChanged(actionParameter); // 则刷新UI
            }
        }

        protected override String GetAdjustmentValue(String actionParameter) => null; // 【无改动】值通常不单独显示，而是绘制在图像中

        protected override BitmapImage GetCommandImage(String actionParameter, PluginImageSize imageSize)
        {
            var config = this._logicManager.GetConfig(actionParameter);
            if (config == null)
            {
                PluginLog.Warning($"[GeneralDialBase|GetCommandImage] Config not found for actionParameter: {actionParameter}. Returning default image.");
                return PluginImage.DrawElement(imageSize, null, preferIconOnlyForDial: true); 
            }

            try
            {
                BitmapImage loadedIcon = PluginImage.TryLoadIcon(config, "GeneralDialBase_Press"); // Context for press

                Boolean isActive = false; 
                String mainTitleOverride = null;
                String valueText = null; // 旋钮按下通常不显示值文本
                Int32 currentMode = 0;    

                if (config.ActionType == "ToggleDial")
                {
                    isActive = this._logicManager.GetToggleState(actionParameter); // 获取ToggleDial的当前状态
                    mainTitleOverride = config.Title ?? config.DisplayName;
                }
                else if (config.ActionType == "2ModeTickDial")
                {
                    currentMode = this._logicManager.GetDialMode(actionParameter); // 获取2ModeTickDial的当前模式
                    // 标题会由PluginImage.DrawElement根据模式和配置决定，这里可以传递基础标题
                    mainTitleOverride = config.Title ?? config.DisplayName; 
                }
                else // 其他旋钮类型，如TickDial，按下时可能只显示标题
                {
                    mainTitleOverride = config.Title ?? config.DisplayName;
                }
                
                return PluginImage.DrawElement(
                    imageSize,
                    config,
                    mainTitleOverride,
                    valueText, // 通常为null
                    isActive,          
                    currentMode,       
                    loadedIcon, 
                    false, // forceTextOnly
                    config.Text, // 辅助文本，在无图标或非纯图标模式下可能显示
                    preferIconOnlyForDial: true // 对于旋钮（包括按下），如果图标存在，则只显示图标
                );
            }
            catch (Exception ex)
            {
                PluginLog.Error(ex, $"[GeneralDialBase|GetCommandImage] Unhandled exception for action '{actionParameter}'.");
                return PluginImage.DrawElement(imageSize, config, "ERR!", isActive: true, preferIconOnlyForDial: true);
            }
        }

        // 【新增】重写 GetAdjustmentImage 以实现图标优先逻辑
        protected override BitmapImage GetAdjustmentImage(String actionParameter, PluginImageSize imageSize)
        {
            var config = this._logicManager.GetConfig(actionParameter);
            if (config == null)
            {
                PluginLog.Warning($"[GeneralDialBase|GetAdjustmentImage] Config not found for actionParameter: {actionParameter}. Returning default image.");
                return PluginImage.DrawElement(imageSize, null, preferIconOnlyForDial: true); 
            }

            try
            {
                BitmapImage loadedIcon = PluginImage.TryLoadIcon(config, "GeneralDialBase_Adjustment");

                Boolean isActive = false; 
                Int32 currentMode = 0;   
                String valueTextForAdjustment = null; 
                // mainTitleOverride 会在 PluginImage.DrawElement 中根据 config.Title/DisplayName 和 preferIconOnlyForDial 确定
                // 如果是纯图标模式，标题会被忽略；如果是文本模式，会使用config的标题。
                String mainTitleOverride = config.Title ?? config.DisplayName; // 供文本模式使用

                if (config.ActionType == "ToggleDial")
                {
                    isActive = this._logicManager.GetToggleState(actionParameter); 
                }
                else if (config.ActionType == "2ModeTickDial")
                {
                    currentMode = this._logicManager.GetDialMode(actionParameter);
                }
                // 对于 ParameterDial, FilterDial, PageDial 等，它们的 valueText (如果有的话) 
                // 是由 General_Folder_Base.GetAdjustmentImage 负责准备并传入 PluginImage.DrawElement 的。
                // General_Dial_Base 通常不直接处理这些特定类型的 valueText。
                // 如果 General_Dial_Base 要独立支持这些，需要在这里添加获取valueText的逻辑。
                // 当前 PluginImage.DrawElement 在文本模式下，会自己根据 dialConfig.ActionType 和传入的 valueText 决定如何显示。

                return PluginImage.DrawElement(
                    imageSize,
                    config,
                    mainTitleOverride, // 供文本模式使用，图标模式下通常被忽略（除非按钮模式）
                    valueTextForAdjustment, // 通常为null，除非此基类直接处理带值的旋钮
                    isActive,          
                    currentMode,       
                    loadedIcon, 
                    false, // forceTextOnly
                    config.Text, // 辅助文本，在无图标或非纯图标模式下可能显示
                    preferIconOnlyForDial: true // 对于旋钮，如果图标存在，则只显示图标
                );
            }
            catch (Exception ex)
            {
                PluginLog.Error(ex, $"[GeneralDialBase|GetAdjustmentImage] Unhandled exception for action '{actionParameter}'.");
                return PluginImage.DrawElement(imageSize, config, "ERR!", isActive: true, preferIconOnlyForDial: true);
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