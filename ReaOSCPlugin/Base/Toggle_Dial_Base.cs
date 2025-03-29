namespace Loupedeck.ReaOSCPlugin.Base
{
    using System;

    using Loupedeck;
    using Loupedeck.ReaOSCPlugin;

    using BitmapColor = Loupedeck.BitmapColor;

    public abstract class Toggle_Dial_Base : PluginDynamicAdjustment, IDisposable
    {
        protected bool _isActive;
        protected readonly string DisplayName;
        protected readonly string OscAddress;
        protected readonly string ResetOscAddress;
        protected readonly BitmapColor ActiveColor;
        protected readonly BitmapColor DefaultColor = BitmapColor.Black;

        protected Toggle_Dial_Base(
            string displayName,
            string description,
            string groupName,
            string oscAddress,
            BitmapColor activeColor,
            string resetOscAddress = null)
            : base(displayName, description, groupName, hasReset: !string.IsNullOrEmpty(resetOscAddress))
        {
            DisplayName = displayName;
            ActiveColor = activeColor;

            // 构建完整OSC地址
            OscAddress = $"/{groupName?.Trim('/')}/{oscAddress?.Trim('/')}";
            ResetOscAddress = resetOscAddress != null
                ? $"/{groupName?.Trim('/')}/{resetOscAddress?.Trim('/')}"
                : null;

            // 初始状态从OSC管理器获取
            _isActive = OSCStateManager.Instance.GetState(OscAddress) > 0.5f;

            // 注册状态监听
            OSCStateManager.Instance.StateChanged += OnOscStateChanged;

            this.AddParameter(OscAddress, displayName, groupName);
        }

        private void OnOscStateChanged(object sender, OSCStateManager.StateChangedEventArgs e)
        {
            if (e.Address == OscAddress)
            {
                _isActive = e.Value > 0.5f;
                ActionImageChanged(); // 更新UI
            }
        }

        protected override void ApplyAdjustment(string actionParameter, int ticks)
        {
            if (ticks == 0)
                return;

            float newValue = -1f;

            if (ticks > 0 && !_isActive) // 右转激活
            {
                newValue = 1f;
            }
            else if (ticks < 0 && _isActive) // 左转取消激活
            {
                newValue = 0f;
            }

            if (newValue >= 0)
            {
                ReaOSCPlugin.SendOSCMessage(OscAddress, newValue);
            }
        }

        protected override void RunCommand(string actionParameter)
        {

                ReaOSCPlugin.SendOSCMessage(ResetOscAddress, 1f);

        }

        protected override BitmapImage GetCommandImage(string actionParameter, PluginImageSize imageSize)
        {
            using (var bitmap = new BitmapBuilder(imageSize))
            {
                bitmap.Clear(_isActive ? ActiveColor : DefaultColor);
                DrawDialContent(bitmap);
                return bitmap.ToImage();
            }
        }

        protected virtual void DrawDialContent(BitmapBuilder bitmap)
        {
            var fontSize = CalculateFontSize(DisplayName);
            bitmap.DrawText(
                text: DisplayName,
                fontSize: fontSize,
                color: BitmapColor.White
            );
        }

        private int CalculateFontSize(string text)
        {
            if (text.Length > 15)
                return 12;
            if (text.Length > 10)
                return 14;
            return 16;
        }

        public void Dispose() => OSCStateManager.Instance.StateChanged -= OnOscStateChanged;
    }
}