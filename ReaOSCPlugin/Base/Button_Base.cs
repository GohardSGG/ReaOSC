namespace Loupedeck.ReaOSCPlugin.Base
{
    using System;

    using Loupedeck;
    using Loupedeck.ReaOSCPlugin;

    using BitmapColor = Loupedeck.BitmapColor;

    public abstract class Button_Base : PluginDynamicCommand, IDisposable
    {
        protected bool _isActive;
        protected readonly string OriginalOscAddress; // 存储原始传入的地址（保留大小写）
        protected readonly string FullOscAddress;     // 完整的OSC地址（含分组）
        protected readonly BitmapColor ActiveColor;
        protected readonly BitmapColor DefaultColor = BitmapColor.Black;

        protected Button_Base(
            string displayName,
            string description,
            string groupName,
            string oscAddress, // 原始传入的OSC地址（需保留大小写）
            BitmapColor activeColor)
            : base(displayName, description, groupName)
        {
            // 存储原始地址（保留用户传入的大小写）
            OriginalOscAddress = oscAddress?.Trim('/');

            // 构建完整OSC地址（格式：/GroupName/OscAddress）
            FullOscAddress = $"/{groupName?.Trim('/')}/{OriginalOscAddress}";

            ActiveColor = activeColor;

            // 初始状态从OSC管理器获取
            _isActive = OSCStateManager.Instance.GetState(FullOscAddress) > 0.5f;

            // 注册状态变更监听
            OSCStateManager.Instance.StateChanged += OnOSCStateChanged;

            // 参数注册使用完整地址
            this.AddParameter(FullOscAddress, displayName, groupName);
        }

        private void OnOSCStateChanged(object sender, OSCStateManager.StateChangedEventArgs e)
        {
            if (e.Address == FullOscAddress)
            {
                // 严格根据OSC反馈更新状态
                _isActive = e.Value > 0.5f;
                ActionImageChanged(); // 触发UI刷新
            }
        }

        protected override void RunCommand(string actionParameter)
        {
            // 发送当前状态的反向值（激活发0，未激活发1）
            var newValue = _isActive ? 0f : 1f;
            ReaOSCPlugin.SendOSCMessage(FullOscAddress, newValue);

            // 注意：不直接修改_isActive，等待OSC反馈更新
        }

        protected override BitmapImage GetCommandImage(string actionParameter, PluginImageSize imageSize)
        {
            using (var bitmap = new BitmapBuilder(imageSize))
            {
                // 背景颜色根据激活状态切换
                bitmap.Clear(_isActive ? ActiveColor : DefaultColor);

                // 绘制原始OSC地址文本（保留大小写）
                DrawButtonContent(bitmap);

                return bitmap.ToImage();
            }
        }

        protected virtual void DrawButtonContent(BitmapBuilder bitmap)
        {
            // 使用原始地址显示，自动调整字体大小
            var fontSize = CalculateOptimalFontSize(OriginalOscAddress);

            bitmap.DrawText(
                text: OriginalOscAddress,
                fontSize: fontSize,
                color: BitmapColor.White);
        }

        private int CalculateOptimalFontSize(string text)
        {
            // 动态字体大小逻辑（示例）
            if (text.Length > 15)
                return 14;
            if (text.Length > 10)
                return 16;
            return 21;
        }

        public void Dispose() => OSCStateManager.Instance.StateChanged -= OnOSCStateChanged;
    }
}