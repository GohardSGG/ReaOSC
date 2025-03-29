namespace Loupedeck.ReaOSCPlugin.Base
{
    using System;
    using System.IO;

    using Loupedeck;
    using Loupedeck.ReaOSCPlugin;

    using BitmapColor = Loupedeck.BitmapColor;

    /// <summary>
    /// 抽象基类：可切换状态按钮，支持在按钮中央显示图标，底部显示文字。
    /// </summary>
    public abstract class Toggle_Button_Base : PluginDynamicCommand, IDisposable
    {
        protected bool _isActive;                     // 当前按钮是否处于激活状态
        protected readonly string DisplayName;         // 用于显示的文字
        protected readonly string FullOscAddress;      // 完整的 OSC 地址
        protected readonly BitmapColor ActiveColor;    // 激活时的背景颜色
        protected readonly BitmapColor DefaultColor = BitmapColor.Black; // 未激活时的背景

        protected Toggle_Button_Base(
            string displayName,
            string description,
            string groupName,
            string oscAddress,
            BitmapColor activeColor)
            : base(displayName, description, groupName)
        {
            this.DisplayName = displayName;
            this.FullOscAddress = $"/{groupName?.Trim('/')}/{oscAddress}";
            this.ActiveColor = activeColor;

            // 读取初始状态
            this._isActive = OSCStateManager.Instance.GetState(this.FullOscAddress) > 0.5f;

            // 注册 OSC 状态变更监听
            OSCStateManager.Instance.StateChanged += this.OnOSCStateChanged;

            // 注册 Loupedeck 参数（完整地址）
            this.AddParameter(this.FullOscAddress, displayName, groupName);
        }

        /// <summary>
        /// OSC 状态更新回调。
        /// </summary>
        private void OnOSCStateChanged(object sender, OSCStateManager.StateChangedEventArgs e)
        {
            if (e.Address == this.FullOscAddress)
            {
                this._isActive = e.Value > 0.5f;
                this.ActionImageChanged();
            }
        }

        /// <summary>
        /// 按钮被点击时发送反向值到 OSC，不直接更新 _isActive，等待 OSC 回调同步。
        /// </summary>
        protected override void RunCommand(string actionParameter)
        {
            var newValue = this._isActive ? 0f : 1f;
            ReaOSCPlugin.SendOSCMessage(this.FullOscAddress, newValue);
        }

        /// <summary>
        /// 核心：绘制按钮图像。参考 FX.cs 的写法：先画底色，再 SetBackgroundImage，最后 DrawText。
        /// </summary>
        protected override BitmapImage GetCommandImage(string actionParameter, PluginImageSize imageSize)
        {
            using (var bitmapBuilder = new BitmapBuilder(imageSize))
            {
                // 1) 先用激活或默认颜色铺满背景
                var bgColor = this._isActive ? this.ActiveColor : this.DefaultColor;
                bitmapBuilder.DrawRectangle(0, 0, bitmapBuilder.Width, bitmapBuilder.Height, bgColor);
                bitmapBuilder.FillRectangle(0, 0, bitmapBuilder.Width, bitmapBuilder.Height, bgColor);

                // 2) 检查传入参数中是否带有图标路径，例如 "/Group/Osc|C:\\MyIcons\\mute.png"
                var imagePath = this.ParseButtonImagePath(actionParameter);
                if (!string.IsNullOrEmpty(imagePath) && File.Exists(imagePath))
                {
                    // 如果有图标，就用这张图覆盖整个按钮背景
                    var icon = BitmapImage.FromFile(imagePath);
                    bitmapBuilder.SetBackgroundImage(icon);
                }

                // 3) 在按钮下方绘制文字（可根据实际按钮高度微调 y 坐标和字体大小）
                var textY = bitmapBuilder.Height - 20;   // 距离底部 20 像素处开始画文字
                var textH = 20;                          // 绘制文字所占的高度
                bitmapBuilder.DrawText(
                    text: this.DisplayName,
                    x: 0,
                    y: textY,
                    width: bitmapBuilder.Width,
                    height: textH,
                    fontSize: 10,
                    color: BitmapColor.White);

                return bitmapBuilder.ToImage();
            }
        }

        /// <summary>
        /// 从 actionParameter 中解析出图标路径。假设用 '|' 作为分隔符： "/group/oscAddress|D:\\icons\\mute.png"
        /// </summary>
        private string ParseButtonImagePath(string actionParameter)
        {
            if (string.IsNullOrEmpty(actionParameter))
            {
                return null;
            }
            var parts = actionParameter.Split('|');
            return parts.Length > 1 ? parts[1] : null;
        }

        /// <summary>
        /// 当没有图标路径时，仍可使用原有文字逻辑（如果在别的地方需要）。
        /// 这里展示了一个简化的示例。
        /// </summary>
        protected virtual void DrawButtonContent(BitmapBuilder bitmap)
        {
            bitmap.DrawText(this.DisplayName, fontSize: 14, color: BitmapColor.White);
        }

        public void Dispose()
        {
            OSCStateManager.Instance.StateChanged -= this.OnOSCStateChanged;
        }
    }
}
