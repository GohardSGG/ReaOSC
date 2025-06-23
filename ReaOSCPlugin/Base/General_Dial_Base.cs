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
            if (config != null && (config.ActionType == "ToggleDial" || config.ActionType == "2ModeTickDial" || config.ActionType == "ControlDial"))
            {
                PluginLog.Info($"[GeneralDialBase] 接收到状态刷新请求 for '{actionParameterThatChanged}' (Type: {config.ActionType}), 调用 ActionImageChanged。");
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
                BitmapImage loadedIcon = PluginImage.TryLoadIcon(config, "GeneralDialBase_Press");

                Boolean isActive = false; 
                String preliminaryTitle = null;
                String valueText = null; 
                Int32 currentMode = 0;    

                if (config.ActionType == "ToggleDial")
                {
                    isActive = this._logicManager.GetToggleState(actionParameter); 
                    preliminaryTitle = config.Title ?? config.DisplayName;
                }
                else if (config.ActionType == "2ModeTickDial")
                {
                    currentMode = this._logicManager.GetDialMode(actionParameter);
                    preliminaryTitle = (currentMode == 1 && !String.IsNullOrEmpty(config.Title_Mode2)) ? config.Title_Mode2 : (config.Title ?? config.DisplayName);
                }
                else if (config.ActionType == "ControlDial") 
                {
                    preliminaryTitle = config.Title ?? config.DisplayName;
                }
                else 
                {
                    preliminaryTitle = config.Title ?? config.DisplayName;
                }
                
                // 使用 ResolveTextWithMode 解析标题和辅助文本
                String mainTitleToDraw = this._logicManager.ResolveTextWithMode(config, preliminaryTitle);
                String auxTextToDraw = this._logicManager.ResolveTextWithMode(config, config.Text); // config.Text 是原始模板
                
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
                String preliminaryTitle = null; // 用于 ResolveTextWithMode 的原始标题模板

                // 1. 根据 ActionType 确定初步的标题模板 和其他状态
                if (config.ActionType == "ToggleDial")
                {
                    isActive = this._logicManager.GetToggleState(actionParameter); 
                    preliminaryTitle = config.Title ?? config.DisplayName; 
                }
                else if (config.ActionType == "2ModeTickDial")
                {
                    currentMode = this._logicManager.GetDialMode(actionParameter);
                    preliminaryTitle = (currentMode == 1 && !String.IsNullOrEmpty(config.Title_Mode2)) ? config.Title_Mode2 : (config.Title ?? config.DisplayName);
                }
                else if (config.ActionType == "ControlDial") 
                {
                    valueTextForAdjustment = this._logicManager.GetControlDialValue(actionParameter).ToString();
                    preliminaryTitle = config.Title ?? config.DisplayName; 
                }
                else // 其他旋钮类型，如 TickDial, ParameterDial (由Folder处理值) 等
                {
                    preliminaryTitle = config.Title ?? config.DisplayName;
                }

                // 2. 使用 ResolveTextWithMode 解析标题和辅助文本
                String mainTitleToDraw = this._logicManager.ResolveTextWithMode(config, preliminaryTitle);
                String auxTextToDraw = this._logicManager.ResolveTextWithMode(config, config.Text); // config.Text 是原始模板

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