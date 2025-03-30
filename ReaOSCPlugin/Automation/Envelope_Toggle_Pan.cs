namespace Loupedeck.ReaOSCPlugin.Automation
{
    using System;
    using System.Collections.Generic;

    using Loupedeck;

    public class Envelope_Toggle_Pan : PluginDynamicCommand
    {
        private const string BaseName = "Pan";
        private const string TypeName = "Envelope";

        // Track / Take 缓存
        private Dictionary<string, bool> _activeStates = new Dictionary<string, bool>()
        {
            { "Track", false },
            { "Take", false }
        };

        public Envelope_Toggle_Pan()
            : base(displayName: $"{BaseName} Toggle",
                  description: "声像包络切换",
                  groupName: TypeName)
        {
            // 保持原先：订阅 Envelope_Select_Mode 切换
            Envelope_Select_Mode.ModeChanged += (s, e) => this.ActionImageChanged();

            // 订阅 OSC 回调
            OSCStateManager.Instance.StateChanged += this.OnOscStateChanged;

            // 原先: AddParameter
            this.AddParameter("Toggle Pan", $"{BaseName} Toggle", TypeName);
        }

        /// <summary>
        /// 当服务器返回 /Envelope/{Track|Take}/Toggle/Pan = 0/1 时更新本地状态
        /// </summary>
        private void OnOscStateChanged(object sender, OSCStateManager.StateChangedEventArgs e)
        {
            // 当前模式
            var modePrefix = Envelope_Select_Mode.GetModePrefix(); // "Track" or "Take"
            var expectedAddress = $"/Envelope/{modePrefix}/Toggle/Pan";
            if (e.Address == expectedAddress)
            {
                this._activeStates[modePrefix] = e.Value > 0.5f;
                this.ActionImageChanged();
            }
        }

        protected override void RunCommand(string actionParameter)
        {
            var modePrefix = Envelope_Select_Mode.GetModePrefix(); // "Track" or "Take"
            var path = $"/Envelope/{modePrefix}/Toggle/Pan";

            // 看当前状态 => 决定要发 0 or 1
            var isActive = this._activeStates[modePrefix];
            var newValue = isActive ? 0f : 1f;

            // 发送
            ReaOSCPlugin.SendOSCMessage(path, newValue);
        }

        protected override BitmapImage GetCommandImage(string actionParameter, PluginImageSize imageSize)
        {
            var modePrefix = Envelope_Select_Mode.GetModePrefix();
            var isActive = this._activeStates[modePrefix];

            using (var bitmap = new BitmapBuilder(imageSize))
            {
                if (isActive)
                {
                    // 激活 => 白底, 两行黑字
                    bitmap.Clear(BitmapColor.White);
                    bitmap.DrawText(BaseName, fontSize: 26, color: BitmapColor.Black);
                    bitmap.DrawText(
                        text: modePrefix,
                        x: 50,
                        y: 55,
                        width: 14,
                        height: 14,
                        fontSize: 14,
                        color: BitmapColor.Black
                    );
                }
                else
                {
                    // 未激活 => 黑底, 第1行白, 第2行蓝(136,226,255)
                    bitmap.Clear(BitmapColor.Black);
                    bitmap.DrawText(BaseName, fontSize: 26, color: BitmapColor.White);
                    bitmap.DrawText(
                        text: modePrefix,
                        x: 50,
                        y: 55,
                        width: 14,
                        height: 14,
                        fontSize: 14,
                        color: new BitmapColor(136, 226, 255)
                    );
                }

                return bitmap.ToImage();
            }
        }
    }
}
