namespace Loupedeck.ReaOSCPlugin.Base
{
    using System;

    using Loupedeck;
    using Loupedeck.ReaOSCPlugin;

    using BitmapColor = Loupedeck.BitmapColor;

    public abstract class Toggle_Button_Base : PluginDynamicCommand, IDisposable
    {
        protected bool _isActive;
        protected readonly string DisplayName;
        protected readonly string FullOscAddress;

        protected readonly BitmapColor ActiveColor;
        //protected readonly BitmapColor DefaultColor = BitmapColor.Black;

        // 这里保留原有的“只保存文件名”
        private readonly string _buttonImageName;

        // 新增：记录激活/未激活时的文字颜色
        // 根据“只传一个颜色就自动设另一个颜色”的需求做了逻辑处理
        protected readonly BitmapColor _actualActiveTextColor;
        protected readonly BitmapColor _actualDeactiveTextColor;

        /// <summary>
        /// 允许用户可选传入 activeTextColor 与 deactiveTextColor：
        /// 1) 全部不传 => 文字均使用 White
        /// 2) 只传 activeTextColor => 另一个默认 White
        /// 3) 只传 deactiveTextColor => activeTextColor 默认 Black
        /// 4) 都传 => 分别使用
        /// </summary>
        protected Toggle_Button_Base(
            string displayName,
            string description,
            string groupName,
            string oscAddress,
            BitmapColor activeColor,
            BitmapColor? activeTextColor = null,
            BitmapColor? deactiveTextColor = null,
            string buttonImage = null
        )
            : base(displayName, description, groupName)
        {
            this.DisplayName = displayName;
            this.FullOscAddress = $"/{groupName?.Trim('/')}/{oscAddress}";
            this.ActiveColor = activeColor;
            this._buttonImageName = buttonImage;

            this._isActive = OSCStateManager.Instance.GetState(this.FullOscAddress) > 0.5f;
            OSCStateManager.Instance.StateChanged += this.OnOSCStateChanged;

            this.AddParameter(this.FullOscAddress, displayName, groupName);

            // 如果全部都没有传 => 都设为白色
            if (!activeTextColor.HasValue && !deactiveTextColor.HasValue)
            {
                this._actualActiveTextColor = BitmapColor.White;
                this._actualDeactiveTextColor = BitmapColor.White;
            }
            // 只传了 activeTextColor => deactiveTextColor = White
            else if (activeTextColor.HasValue && !deactiveTextColor.HasValue)
            {
                this._actualActiveTextColor = activeTextColor.Value;
                this._actualDeactiveTextColor = BitmapColor.White;
            }
            // 只传了 deactiveTextColor => activeTextColor = Black
            else if (!activeTextColor.HasValue && deactiveTextColor.HasValue)
            {
                this._actualActiveTextColor = BitmapColor.Black;
                this._actualDeactiveTextColor = deactiveTextColor.Value;
            }
            else
            {
                // 都传 => 分别使用
                this._actualActiveTextColor = activeTextColor.Value;
                this._actualDeactiveTextColor = deactiveTextColor.Value;
            }
        }

        private void OnOSCStateChanged(object sender, OSCStateManager.StateChangedEventArgs e)
        {
            if (e.Address == this.FullOscAddress)
            {
                this._isActive = e.Value > 0.5f;
                this.ActionImageChanged();
            }
        }

        /// <summary>
        /// 默认点击动作：切换激活状态(向 OSC 发送反向值)。
        /// </summary>
        protected override void RunCommand(string actionParameter)
        {
            var newValue = this._isActive ? 0f : 1f;
            ReaOSCPlugin.SendOSCMessage(this.FullOscAddress, newValue);
        }

        /// <summary>
        /// Loupedeck 请求绘制按钮图像时，会调用此方法。
        /// 先填充背景，然后如果有图标就绘制图标+文字，否则仅调用 DrawButtonContent。
        /// </summary>
        protected override BitmapImage GetCommandImage(string actionParameter, PluginImageSize imageSize)
        {
            using (var bitmap = new BitmapBuilder(imageSize))
            {
                // 背景颜色：激活时 ActiveColor，否则黑色
                var bgColor = this._isActive ? this.ActiveColor : BitmapColor.Black;
                bitmap.FillRectangle(0, 0, bitmap.Width, bitmap.Height, bgColor);

                // 如果提供了图标文件名，则尝试绘制图标 + 文字
                if (!string.IsNullOrEmpty(this._buttonImageName))
                {
                    try
                    {
                        // 读取并绘制图标
                        var icon = PluginResources.ReadImage(this._buttonImageName);

                        // 保持你原先的图标位置与大小逻辑
                        int iconHeight = 46;
                        int iconWidth = icon.Width * iconHeight / icon.Height;
                        int iconX = (bitmap.Width - iconWidth) / 2;
                        int iconY = 8;

                        bitmap.DrawImage(icon, iconX, iconY, iconWidth, iconHeight);

                        // 在图标下方显示文字
                        var textColor = this._isActive ? this._actualActiveTextColor : this._actualDeactiveTextColor;
                        bitmap.DrawText(
                            text: this.DisplayName,
                            x: 0,
                            y: bitmap.Height - 23,
                            width: bitmap.Width,
                            height: 20,
                            fontSize: 12,
                            color: textColor);
                    }
                    catch (Exception ex)
                    {
                        this.Log.Warning($"图标加载失败: {this._buttonImageName} -> {ex.Message}");
                        // 如果图标加载失败，则使用纯文字的逻辑
                        this.DrawButtonContent(bitmap);
                    }
                }
                else
                {
                    // 未提供图标 => 直接走文字逻辑
                    this.DrawButtonContent(bitmap);
                }

                return bitmap.ToImage();
            }
        }

        /// <summary>
        /// 当未提供图标，或者图标加载失败时，使用此方法绘制按钮纯文字内容。
        /// </summary>
        protected virtual void DrawButtonContent(BitmapBuilder bitmap)
        {
            var fontSize = this.CalculateOptimalFontSize(this.DisplayName);

            // 根据激活状态决定文字颜色（如果都没传，则默认都 White）
            var textColor = this._isActive
                ? this._actualActiveTextColor
                : this._actualDeactiveTextColor;

            bitmap.DrawText(
                text: this.DisplayName,
                fontSize: fontSize,
                color: textColor);
        }

        /// <summary>
        /// 依据文字长度简单算一个字体大小。
        /// </summary>
        private int CalculateOptimalFontSize(string text)
        {
            if (text.Length > 15)
                return 14;
            if (text.Length > 5)
                return 21;
            return 26;
        }

        public void Dispose() => OSCStateManager.Instance.StateChanged -= this.OnOSCStateChanged;
    }
}
