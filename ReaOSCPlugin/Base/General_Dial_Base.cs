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

        [Obsolete]
        public General_Dial_Base() : base(hasReset: true)
        {
            this._logicManager.Initialize();

            foreach (var entry in this._logicManager.GetAllConfigs())
            {
                var config = entry.Value;
                if (config.ActionType == "TickDial" || config.ActionType == "ToggleDial" || config.ActionType == "2ModeTickDial" || config.ActionType == "ControlDial")
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
            var config = this._logicManager.GetConfig(actionParameterThatChanged);
            if (config != null)
            {
                // 检查是否是本实例管理的旋钮之一
                // PluginDynamicAdjustment 基类没有直接方法获取已注册参数，
                // 但如果 actionParameterThatChanged 是有效的，并且其 config.ActionType 是旋钮类型，就刷新。
                // 涵盖所有可能需要因动态标题而刷新的旋钮类型。
                if (config.ActionType == "TickDial" || 
                    config.ActionType == "ToggleDial" || 
                    config.ActionType == "2ModeTickDial" || 
                    config.ActionType == "ControlDial" ||
                    config.ActionType == "ModeControlDial" || 
                    config.ActionType == "ParameterDial" || 
                    config.ActionType == "FilterDial" ||    
                    config.ActionType == "PageDial" ||      
                    config.ActionType == "NavigationDial")  
                {
                    PluginLog.Info($"[GeneralDialBase] 接收到状态刷新请求 for '{actionParameterThatChanged}' (DisplayName: '{config.DisplayName}', Type: {config.ActionType}), 调用 ActionImageChanged。");
                    this.ActionImageChanged(actionParameterThatChanged);
                }
                // else
                // {
                //     PluginLog.Verbose($"[GeneralDialBase] 状态刷新请求 for '{actionParameterThatChanged}' (Type: {config.ActionType}) 不是当前处理的旋钮类型，不调用 ActionImageChanged。");
                // }
            }
            // else
            // {
            //    PluginLog.Warning($"[GeneralDialBase] 接收到状态刷新请求，但未能找到 actionParameter '{actionParameterThatChanged}' 的配置。");
            // }
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
                BitmapImage loadedIcon = PluginImage.TryLoadIcon(config, "GeneralDialBase_Press");

                Boolean isActive = false; 
                String preliminaryTitleTemplate = null; // 用于存储从LogicManager获取的原始模板
                String valueText = null; 
                Int32 currentMode = 0;    
                String titleFieldNameForLogic; // 告知LogicManager要获取哪个字段

                if (config.ActionType == "ToggleDial")
                {
                    isActive = this._logicManager.GetToggleState(actionParameter); 
                    titleFieldNameForLogic = "Title";
                    preliminaryTitleTemplate = this._logicManager.GetCurrentTitleTemplate(actionParameter, titleFieldNameForLogic, config);
                }
                else if (config.ActionType == "2ModeTickDial")
                {
                    currentMode = this._logicManager.GetDialMode(actionParameter);
                    // 根据当前模式确定是取 Title 还是 Title_Mode2
                    titleFieldNameForLogic = (currentMode == 1 && !String.IsNullOrEmpty(config.Title_Mode2)) ? "Title_Mode2" : "Title";
                    preliminaryTitleTemplate = this._logicManager.GetCurrentTitleTemplate(actionParameter, titleFieldNameForLogic, config, currentMode); 
                }
                else if (config.ActionType == "ControlDial") 
                {
                    // ControlDial 通常主要显示其参数值，但其固定标题也可以是动态的
                    titleFieldNameForLogic = "Title";
                    preliminaryTitleTemplate = this._logicManager.GetCurrentTitleTemplate(actionParameter, titleFieldNameForLogic, config);
                    // valueText 将在 GetAdjustmentImage 中由 GetControlDialDisplayText 获取，这里 GetCommandImage 通常不显示ControlDial的值文本
                }
                else // 其他 TickDial 等
                {
                    titleFieldNameForLogic = "Title";
                    preliminaryTitleTemplate = this._logicManager.GetCurrentTitleTemplate(actionParameter, titleFieldNameForLogic, config);
                }
                
                // 使用 ResolveTextWithMode 解析标题和辅助文本
                String mainTitleToDraw = this._logicManager.ResolveTextWithMode(config, preliminaryTitleTemplate);
                String auxTextToDraw = this._logicManager.ResolveTextWithMode(config, config.Text); // config.Text 是原始模板
                
                // 回退逻辑: 如果 mainTitleToDraw 为空，并且 ShowTitle 不是 "No"，则使用 DisplayName
                if (String.IsNullOrEmpty(mainTitleToDraw))
                {
                    if (config.ShowTitle?.Equals("No", StringComparison.OrdinalIgnoreCase) != true)
                    {
                        mainTitleToDraw = config.DisplayName;
                    }
                }
                
                return PluginImage.DrawElement(
                    imageSize,
                    config,
                    mainTitleToDraw,    // 解析后的标题
                    valueText, 
                    isActive,          
                    currentMode,       
                    loadedIcon, 
                    false, 
                    auxTextToDraw,      // 解析后的辅助文本
                    preferIconOnlyForDial: true 
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
                String preliminaryTitleTemplate = null; // 用于存储从LogicManager获取的原始模板
                String titleFieldNameForLogic; // 告知LogicManager要获取哪个字段

                // 1. 根据 ActionType 确定初步的标题模板 和其他状态
                if (config.ActionType == "ToggleDial")
                {
                    isActive = this._logicManager.GetToggleState(actionParameter); 
                    titleFieldNameForLogic = "Title";
                    preliminaryTitleTemplate = this._logicManager.GetCurrentTitleTemplate(actionParameter, titleFieldNameForLogic, config);
                }
                else if (config.ActionType == "2ModeTickDial")
                {
                    currentMode = this._logicManager.GetDialMode(actionParameter);
                    titleFieldNameForLogic = (currentMode == 1 && !String.IsNullOrEmpty(config.Title_Mode2)) ? "Title_Mode2" : "Title";
                    preliminaryTitleTemplate = this._logicManager.GetCurrentTitleTemplate(actionParameter, titleFieldNameForLogic, config, currentMode);
                }
                else if (config.ActionType == "ControlDial") 
                {
                    valueTextForAdjustment = this._logicManager.GetControlDialDisplayText(actionParameter); 
                    titleFieldNameForLogic = "Title";
                    preliminaryTitleTemplate = this._logicManager.GetCurrentTitleTemplate(actionParameter, titleFieldNameForLogic, config);
                }
                else // 其他旋钮类型，如 TickDial, ParameterDial (由Folder处理值) 等
                {
                    titleFieldNameForLogic = "Title";
                    preliminaryTitleTemplate = this._logicManager.GetCurrentTitleTemplate(actionParameter, titleFieldNameForLogic, config);
                }

                // 2. 使用 ResolveTextWithMode 解析标题和辅助文本
                String mainTitleToDraw = this._logicManager.ResolveTextWithMode(config, preliminaryTitleTemplate);
                String auxTextToDraw = this._logicManager.ResolveTextWithMode(config, config.Text); // config.Text 是原始模板

                // 回退逻辑: 如果 mainTitleToDraw 为空，并且 ShowTitle 不是 "No"，则使用 DisplayName
                if (String.IsNullOrEmpty(mainTitleToDraw))
                {
                    if (config.ShowTitle?.Equals("No", StringComparison.OrdinalIgnoreCase) != true)
                    {
                        mainTitleToDraw = config.DisplayName;
                    }
                }

                return PluginImage.DrawElement(
                    imageSize,
                    config,
                    mainTitleToDraw,        // 解析后的主标题 (文本模式下使用)
                    valueTextForAdjustment, // 特定旋钮类型的值文本
                    isActive,          
                    currentMode,       
                    loadedIcon, 
                    false, 
                    auxTextToDraw,          // 解析后的辅助文本
                    preferIconOnlyForDial: true 
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