// 文件名: Base/General_Button_Base.cs
namespace Loupedeck.ReaOSCPlugin.Base
{
    using System;
    using System.Collections.Generic;

    // 一个统一的基类，可以处理普通按钮、模式选择按钮和受模式控制的按钮
    public class General_Button_Base : PluginDynamicCommand, IDisposable
    {
        private readonly Logic_Manager_Base _logicManager = Logic_Manager_Base.Instance;
        private readonly Dictionary<string, Action> _modeHandlers = new Dictionary<string, Action>();

        // vvvvvvvvvv 【已修正】 vvvvvvvvvv
        // 错误 CS0426 的修正：将 OSCStateManager.StateChangedEventHandler 替换为正确的 EventHandler<OSCStateManager.StateChangedEventArgs>
        private readonly Dictionary<string, EventHandler<OSCStateManager.StateChangedEventArgs>> _oscHandlers = new Dictionary<string, EventHandler<OSCStateManager.StateChangedEventArgs>>();
        // ^^^^^^^^^^^ 【已修正】 ^^^^^^^^^^^

        public General_Button_Base()
        {
            // 遍历 Logic_Manager 中加载的所有按钮配置
            foreach (var kvp in this._logicManager.GetAllConfigs())
            {
                var actionParameter = kvp.Key;
                var config = kvp.Value;

                // 只处理本基类应该负责的按钮类型
                if (config.ActionType != "TriggerButton" && config.ActionType != "ToggleButton" && config.ActionType != "SelectModeButton")
                {
                    continue;
                }

                // 为每个按钮创建参数，这样它们才会出现在Loupedeck的UI中
                this.AddParameter(actionParameter, config.DisplayName, config.GroupName, config.Description);

                // 根据配置类型，设置不同的逻辑
                if (config.ActionType == "SelectModeButton")
                {
                    // 行为1: 这是一个主管理按钮
                    this._logicManager.RegisterModeGroup(config);

                    // 创建一个唯一的委托来订阅事件
                    Action handler = () => this.ActionImageChanged(actionParameter);
                    this._modeHandlers[actionParameter] = handler;
                    this._logicManager.SubscribeToModeChange(config.DisplayName, handler);
                }
                else if (!string.IsNullOrEmpty(config.ModeName))
                {
                    // 行为2: 这是一个受控按钮
                    Action handler = () => this.ActionImageChanged(actionParameter);
                    this._modeHandlers[actionParameter] = handler;
                    this._logicManager.SubscribeToModeChange(config.ModeName, handler);

                    // 如果是ToggleButton，还需要订阅OSC状态反馈
                    if (config.ActionType == "ToggleButton")
                    {
                        // vvvvvvvvvv 【已修正】 vvvvvvvvvv
                        // 同样修正这里的类型声明
                        EventHandler<OSCStateManager.StateChangedEventArgs> oscHandler = (s, e) => this.OnOscStateChanged(s, e, config, actionParameter);
                        // ^^^^^^^^^^^ 【已修正】 ^^^^^^^^^^^
                        this._oscHandlers[actionParameter] = oscHandler;
                        OSCStateManager.Instance.StateChanged += oscHandler;
                    }
                }
                // 行为3: 普通按钮，无需额外设置
            }
        }

        private void OnOscStateChanged(object sender, OSCStateManager.StateChangedEventArgs e, ButtonConfig config, string actionParameter)
        {
            var currentMode = this._logicManager.GetCurrentModeString(config.ModeName);
            if (string.IsNullOrEmpty(currentMode) || string.IsNullOrEmpty(config.OscAddress))
                return;

            var expectedAddress = config.OscAddress.Replace("{mode}", currentMode);
            if (e.Address == expectedAddress)
            {
                this._logicManager.SetToggleState(actionParameter, e.Value > 0.5f);
                this.ActionImageChanged(actionParameter);
            }
        }

        protected override void RunCommand(string actionParameter)
        {
            if (this._logicManager.GetConfig(actionParameter) is not { } config)
                return;

            // 根据按钮类型执行不同的命令
            if (config.ActionType == "SelectModeButton")
            {
                this._logicManager.ToggleMode(config.DisplayName);
            }
            else if (!string.IsNullOrEmpty(config.ModeName))
            {
                // 受控按钮
                var currentMode = this._logicManager.GetCurrentModeString(config.ModeName);
                if (string.IsNullOrEmpty(currentMode))
                    return;

                var finalOscAddress = config.OscAddress.Replace("{mode}", currentMode);

                if (config.ActionType == "ToggleButton")
                {
                    var currentState = this._logicManager.GetToggleState(actionParameter);
                    ReaOSCPlugin.SendOSCMessage(finalOscAddress, currentState ? 0f : 1f);
                    this._logicManager.SetToggleState(actionParameter, !currentState);
                    this.ActionImageChanged(actionParameter);
                }
                else // TriggerButton
                {
                    ReaOSCPlugin.SendOSCMessage(finalOscAddress, 1f);
                }
            }
            else
            {
                // 普通按钮
                if (config.ActionType == "ToggleButton")
                {
                    var currentState = this._logicManager.GetToggleState(actionParameter);
                    ReaOSCPlugin.SendOSCMessage(config.OscAddress, currentState ? 0f : 1f);
                    this._logicManager.SetToggleState(actionParameter, !currentState);
                    this.ActionImageChanged(actionParameter);
                }
                else // TriggerButton
                {
                    ReaOSCPlugin.SendOSCMessage(config.OscAddress, 1f);
                }
            }
        }

        protected override BitmapImage GetCommandImage(string actionParameter, PluginImageSize imageSize)
        {
            if (this._logicManager.GetConfig(actionParameter) is not { } config)
                return null;

            using (var bitmapBuilder = new BitmapBuilder(imageSize))
            {
                var title = config.Title ?? config.DisplayName;

                if (config.ActionType == "SelectModeButton")
                {
                    // 绘制主管理按钮
                    var currentModeString = this._logicManager.GetCurrentModeString(config.DisplayName);
                    var isModeActive = config.Modes?.IndexOf(currentModeString) > 0;

                    var bgColor = isModeActive ? HexToBitmapColor(config.ActiveColor) : BitmapColor.Black;
                    var titleColor = isModeActive ? HexToBitmapColor(config.TitleColor) : HexToBitmapColor(config.DeactiveTextColor);

                    bitmapBuilder.Clear(bgColor);
                    bitmapBuilder.DrawText(currentModeString, color: titleColor, fontSize: 23);
                }
                else
                {
                    // 绘制普通或受控按钮
                    var isActive = config.ActionType == "ToggleButton" && this._logicManager.GetToggleState(actionParameter);

                    var bgColor = isActive ? HexToBitmapColor(config.ActiveColor) : BitmapColor.Black;
                    var titleColor = isActive ? HexToBitmapColor(config.ActiveTextColor ?? config.TitleColor) : HexToBitmapColor(config.DeactiveTextColor ?? config.TitleColor);

                    bitmapBuilder.Clear(bgColor);
                    bitmapBuilder.DrawText(title, color: titleColor, fontSize: 21);

                    // 如果是受控按钮，额外绘制模式小字
                    if (!string.IsNullOrEmpty(config.ModeName))
                    {
                        var currentMode = this._logicManager.GetCurrentModeString(config.ModeName);
                        bitmapBuilder.DrawText(currentMode, x: 50, y: 55, width: 14, height: 14, fontSize: 14, color: new BitmapColor(136, 226, 255));
                    }
                }
                return bitmapBuilder.ToImage();
            }
        }

        public void Dispose()
        {
            // 循环并取消所有事件订阅，防止内存泄漏
            foreach (var kvp in this._logicManager.GetAllConfigs())
            {
                var actionParameter = kvp.Key;
                var config = kvp.Value;

                if (this._modeHandlers.TryGetValue(actionParameter, out var handler))
                {
                    var modeName = config.ActionType == "SelectModeButton" ? config.DisplayName : config.ModeName;
                    if (!string.IsNullOrEmpty(modeName))
                    {
                        this._logicManager.UnsubscribeFromModeChange(modeName, handler);
                    }
                }
                if (this._oscHandlers.TryGetValue(actionParameter, out var oscHandler))
                {
                    OSCStateManager.Instance.StateChanged -= oscHandler;
                }
            }
        }

        // 颜色转换辅助方法
        private static BitmapColor HexToBitmapColor(string hex) { if (String.IsNullOrEmpty(hex)) return BitmapColor.White; try { hex = hex.TrimStart('#'); var r = (byte)Int32.Parse(hex.Substring(0, 2), System.Globalization.NumberStyles.HexNumber); var g = (byte)Int32.Parse(hex.Substring(2, 2), System.Globalization.NumberStyles.HexNumber); var b = (byte)Int32.Parse(hex.Substring(4, 2), System.Globalization.NumberStyles.HexNumber); return new BitmapColor(r, g, b); } catch { return BitmapColor.Red; } }
    }
}