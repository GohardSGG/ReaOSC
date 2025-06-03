// 文件名: Base/General_Button_Base.cs
namespace Loupedeck.ReaOSCPlugin.Base
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;

    public class General_Button_Base : PluginDynamicCommand, IDisposable
    {
        private readonly Logic_Manager_Base _logicManager = Logic_Manager_Base.Instance;
        private readonly Dictionary<string, Action> _modeHandlers = new Dictionary<string, Action>();
        private readonly Dictionary<string, EventHandler<OSCStateManager.StateChangedEventArgs>> _oscHandlers = new Dictionary<string, EventHandler<OSCStateManager.StateChangedEventArgs>>();

        public General_Button_Base()
        {
            foreach (var kvp in this._logicManager.GetAllConfigs())
            {
                var actionParameter = kvp.Key;
                var config = kvp.Value;

                if (config.ActionType != "TriggerButton" && config.ActionType != "ToggleButton" && config.ActionType != "SelectModeButton")
                {
                    continue;
                }

                this.AddParameter(actionParameter, config.DisplayName, config.GroupName, config.Description);

                if (config.ActionType == "SelectModeButton")
                {
                    this._logicManager.RegisterModeGroup(config);
                    Action handler = () => this.ActionImageChanged(actionParameter);
                    this._modeHandlers[actionParameter] = handler;
                    this._logicManager.SubscribeToModeChange(config.DisplayName, handler);
                }
                else if (!string.IsNullOrEmpty(config.ModeName))
                {
                    Action handler = () => this.ActionImageChanged(actionParameter);
                    this._modeHandlers[actionParameter] = handler;
                    this._logicManager.SubscribeToModeChange(config.ModeName, handler);
                    
                    if (config.ActionType == "ToggleButton")
                    {
                        EventHandler<OSCStateManager.StateChangedEventArgs> oscHandler = (s, e) => this.OnModeButtonOscStateChanged(s, e, config, actionParameter);
                        this._oscHandlers[actionParameter] = oscHandler;
                        OSCStateManager.Instance.StateChanged += oscHandler;
                    }
                }
                else if (config.ActionType == "ToggleButton")
                {
                    EventHandler<OSCStateManager.StateChangedEventArgs> oscHandler = (s, e) => {
                        if (e.Address == config.OscAddress)
                        {
                            this._logicManager.SetToggleState(actionParameter, e.Value > 0.5f);
                            this.ActionImageChanged(actionParameter);
                        }
                    };
                    this._oscHandlers[actionParameter] = oscHandler;
                    OSCStateManager.Instance.StateChanged += oscHandler;
                }
            }
        }

        private void OnModeButtonOscStateChanged(object sender, OSCStateManager.StateChangedEventArgs e, ButtonConfig config, string actionParameter)
        {
            var modeIndex = this._logicManager.GetCurrentModeIndex(config.ModeName);
            string expectedAddress;

            if (config.OscAddresses?.Count > modeIndex && modeIndex != -1)
            {
                expectedAddress = config.OscAddresses[modeIndex];
            }
            else if (!string.IsNullOrEmpty(config.OscAddress))
            {
                var currentMode = this._logicManager.GetCurrentModeString(config.ModeName);
                if (string.IsNullOrEmpty(currentMode)) return;
                expectedAddress = config.OscAddress.Replace("{mode}", currentMode);
            }
            else
            {
                return;
            }

            if (e.Address == expectedAddress)
            {
                this._logicManager.SetToggleState(actionParameter, e.Value > 0.5f);
                this.ActionImageChanged(actionParameter);
            }
        }

        protected override void RunCommand(string actionParameter)
        {
            if (this._logicManager.GetConfig(actionParameter) is not { } config) return;

            if (config.ActionType == "SelectModeButton")
            {
                this._logicManager.ToggleMode(config.DisplayName);
            }
            else if (!string.IsNullOrEmpty(config.ModeName))
            {
                var modeIndex = this._logicManager.GetCurrentModeIndex(config.ModeName);
                if (modeIndex == -1) return;

                string finalOscAddress;
                if (config.OscAddresses?.Count > modeIndex)
                {
                    finalOscAddress = config.OscAddresses[modeIndex];
                }
                else
                {
                    var currentMode = this._logicManager.GetCurrentModeString(config.ModeName);
                    finalOscAddress = config.OscAddress.Replace("{mode}", currentMode);
                }

                if (config.ActionType == "ToggleButton")
                {
                    var currentState = this._logicManager.GetToggleState(actionParameter);
                    ReaOSCPlugin.SendOSCMessage(finalOscAddress, currentState ? 0f : 1f);
                    this._logicManager.SetToggleState(actionParameter, !currentState);
                    this.ActionImageChanged(actionParameter);
                }
                else
                {
                    ReaOSCPlugin.SendOSCMessage(finalOscAddress, 1f);
                }
            }
            else
            {
                if (config.ActionType == "ToggleButton")
                {
                    var currentState = this._logicManager.GetToggleState(actionParameter);
                    ReaOSCPlugin.SendOSCMessage(config.OscAddress, currentState ? 0f : 1f);
                    this._logicManager.SetToggleState(actionParameter, !currentState);
                    this.ActionImageChanged(actionParameter);
                }
                else
                {
                    ReaOSCPlugin.SendOSCMessage(config.OscAddress, 1f);
                }
            }
        }

        protected override BitmapImage GetCommandImage(string actionParameter, PluginImageSize imageSize)
        {
            if (this._logicManager.GetConfig(actionParameter) is not { } config) return null;
            
            using (var bitmapBuilder = new BitmapBuilder(imageSize))
            {
                if (config.ActionType == "SelectModeButton")
                {
                    var currentModeString = this._logicManager.GetCurrentModeString(config.DisplayName);
                    var isModeActive = config.Modes?.IndexOf(currentModeString) > 0;
                    var bgColor = isModeActive ? HexToBitmapColor(config.ActiveColor) : BitmapColor.Black;
                    var titleColor = isModeActive ? HexToBitmapColor(config.TitleColor) : HexToBitmapColor(config.DeactiveTextColor);
                    bitmapBuilder.Clear(bgColor);
                    bitmapBuilder.DrawText(currentModeString, color: titleColor, fontSize: 23);
                }
                else 
                {
                    var isActive = config.ActionType == "ToggleButton" && this._logicManager.GetToggleState(actionParameter);
                    var bgColor = isActive ? HexToBitmapColor(config.ActiveColor) : BitmapColor.Black;
                    var titleColor = isActive ? HexToBitmapColor(config.ActiveTextColor ?? config.TitleColor) : HexToBitmapColor(config.DeactiveTextColor ?? config.TitleColor);
                    
                    bitmapBuilder.Clear(bgColor);

                    var title = config.Title ?? config.DisplayName;
                    if (!string.IsNullOrEmpty(config.ModeName))
                    {
                        var modeIndex = this._logicManager.GetCurrentModeIndex(config.ModeName);
                        if (config.Titles?.Count > modeIndex && modeIndex != -1)
                        {
                            title = config.Titles[modeIndex];
                        }
                    }
                    bitmapBuilder.DrawText(title, color: titleColor, fontSize: 21);
                    
                    if(!string.IsNullOrEmpty(config.ModeName))
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

        private static BitmapColor HexToBitmapColor(string hex) { if (String.IsNullOrEmpty(hex)) return BitmapColor.White; try { hex = hex.TrimStart('#'); var r = (byte)Int32.Parse(hex.Substring(0, 2), NumberStyles.HexNumber); var g = (byte)Int32.Parse(hex.Substring(2, 2), NumberStyles.HexNumber); var b = (byte)Int32.Parse(hex.Substring(4, 2), NumberStyles.HexNumber); return new BitmapColor(r, g, b); } catch { return BitmapColor.Red; } }
    }
}