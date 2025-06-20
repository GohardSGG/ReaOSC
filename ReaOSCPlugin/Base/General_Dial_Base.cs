// 文件名: Base/General_Dial_Base.cs
namespace Loupedeck.ReaOSCPlugin.Base
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;

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

        protected override BitmapImage GetCommandImage(string actionParameter, PluginImageSize imageSize) // 【无改动】
        {
            var config = this._logicManager.GetConfig(actionParameter);
            if (config == null)
                return null;

            using (var bitmapBuilder = new BitmapBuilder(imageSize))
            {
                if (config.ActionType == "2ModeTickDial")
                {
                    var currentMode = this._logicManager.GetDialMode(actionParameter);
                    var title = currentMode == 0 ? config.Title : config.Title_Mode2;
                    var titleColor = currentMode == 0 ? (String.IsNullOrEmpty(config.TitleColor) ? BitmapColor.White : HexToBitmapColor(config.TitleColor)) : (String.IsNullOrEmpty(config.TitleColor_Mode2) ? BitmapColor.White : HexToBitmapColor(config.TitleColor_Mode2));
                    var bgColor = currentMode == 0 ? (String.IsNullOrEmpty(config.BackgroundColor) ? BitmapColor.Black : HexToBitmapColor(config.BackgroundColor)) : (String.IsNullOrEmpty(config.BackgroundColor_Mode2) ? BitmapColor.Black : HexToBitmapColor(config.BackgroundColor_Mode2));
                    bitmapBuilder.Clear(bgColor);
                    if (!String.IsNullOrEmpty(title))
                    { bitmapBuilder.DrawText(text: title, fontSize: this.GetAutomaticDialTitleFontSize(title), color: titleColor); }
                }
                else // TickDial and ToggleDial
                {
                    BitmapColor currentBgColor = BitmapColor.Black;
                    BitmapColor currentTitleColor = String.IsNullOrEmpty(config.TitleColor) ? BitmapColor.White : HexToBitmapColor(config.TitleColor);

                    if (config.ActionType == "ToggleDial")
                    {
                        // 读取的是_toggleStates[actionParameter]，这个状态现在应该能被正确更新了
                        var isActive = this._logicManager.GetToggleState(actionParameter);
                        currentBgColor = isActive 
                            ? (String.IsNullOrEmpty(config.ActiveColor) ? BitmapColor.Black : HexToBitmapColor(config.ActiveColor))
                            : (String.IsNullOrEmpty(config.DeactiveColor) ? BitmapColor.Black : HexToBitmapColor(config.DeactiveColor));
                        currentTitleColor = isActive
                           ? (String.IsNullOrEmpty(config.ActiveTextColor) ? BitmapColor.White : HexToBitmapColor(config.ActiveTextColor))
                           : (String.IsNullOrEmpty(config.DeactiveTextColor) ? BitmapColor.White : HexToBitmapColor(config.DeactiveTextColor));
                    }
                    // TickDial 默认背景黑色，标题颜色来自 TitleColor

                    bitmapBuilder.Clear(currentBgColor);
                    if (!String.IsNullOrEmpty(config.Title))
                    {
                        bitmapBuilder.DrawText(text: config.Title, fontSize: this.GetAutomaticDialTitleFontSize(config.Title), color: currentTitleColor);
                    }
                    if (!String.IsNullOrEmpty(config.Text)) // 次要文本，所有旋钮类型都可以有
                    {
                        bitmapBuilder.DrawText(text: config.Text, x: config.TextX ?? 5, y: config.TextY ?? (bitmapBuilder.Height - 20), width: config.TextWidth ?? (bitmapBuilder.Width - 10), height: config.TextHeight ?? 18, color: String.IsNullOrEmpty(config.TextColor) ? BitmapColor.White : HexToBitmapColor(config.TextColor), fontSize: config.TextSize ?? 12);
                    }
                }
                return bitmapBuilder.ToImage();
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

        #region UI辅助方法 // 【无改动】
        private int GetAutomaticDialTitleFontSize(String title) { if (String.IsNullOrEmpty(title)) return 16; var totalLengthWithSpaces = title.Length; int effectiveLength; if (totalLengthWithSpaces <= 10) { effectiveLength = totalLengthWithSpaces; } else { var words = title.Split(' '); effectiveLength = words.Length > 0 ? words.Max(word => word.Length) : 0; if (effectiveLength == 0 && totalLengthWithSpaces > 0) { effectiveLength = totalLengthWithSpaces; } } return effectiveLength switch { 1 => 26, 2 => 23, 3 => 21, 4 => 19, 5 => 17, >= 6 and <= 7 => 15, 8 => 13, 9 => 12, 10 => 11, 11 => 10, _ => 9 }; }
        private static BitmapColor HexToBitmapColor(string hexColor) { if (String.IsNullOrEmpty(hexColor) || !hexColor.StartsWith("#")) return BitmapColor.White; var hex = hexColor.Substring(1); if (hex.Length != 6) return BitmapColor.White; try { var r = (byte)Int32.Parse(hex.Substring(0, 2), NumberStyles.HexNumber); var g = (byte)Int32.Parse(hex.Substring(2, 2), NumberStyles.HexNumber); var b = (byte)Int32.Parse(hex.Substring(4, 2), NumberStyles.HexNumber); return new BitmapColor(r, g, b); } catch { return BitmapColor.Red; } }
        #endregion
    }
}