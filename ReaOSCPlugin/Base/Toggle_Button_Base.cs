namespace Loupedeck.ReaOSCPlugin.Base
{
    using System;
    using Loupedeck;
    using Loupedeck.ReaOSCPlugin;

    using Loupedeck;

    public abstract class Toggle_Button_Base : PluginDynamicCommand, IDisposable
    {
        protected bool _isActive;
        protected readonly string DisplayName; // <--- 我们用这个来画我们自己的文字
        protected readonly string FullOscAddress;
        protected readonly BitmapColor ActiveColor;
        private readonly string _buttonImageName;
        protected readonly BitmapColor _actualActiveTextColor;
        protected readonly BitmapColor _actualDeactiveTextColor;

        protected Toggle_Button_Base(
            string displayName, // <--- 这里接收真实的 displayName
            string description,
            string groupName,
            string oscAddress,
            BitmapColor activeColor,
            BitmapColor? activeTextColor = null,
            BitmapColor? deactiveTextColor = null,
            string buttonImage = null
        )
            // === 关键修改 1：传递一个空格 " " 给 base 构造函数 ===
            : base(" ", description, groupName)
        {
            // === 关键修改 2：把真实的 displayName 保存到我们自己的字段 ===
            this.DisplayName = displayName;

            this.FullOscAddress = $"/{groupName?.Trim('/')}/{oscAddress}";
            this.ActiveColor = activeColor;
            this._buttonImageName = buttonImage;

            this._isActive = OSCStateManager.Instance.GetState(this.FullOscAddress) > 0.5f;
            OSCStateManager.Instance.StateChanged += this.OnOSCStateChanged;

            // === 关键修改 3：AddParameter 时也使用 " " 吗？ 这需要测试，先用真实的试试 ===
            // 为了 UI 列表可能需要真实名字，但为了避免崩溃，也许也该用 " "？
            // 我们先用真实名字，如果还崩溃，再改成 " "。
            // 但如果为了避免崩溃用 " "，那 UI 里可能也显示空格了。
            // 还是先用真实的吧，因为崩溃是 base(null) 引起的。
            this.AddParameter(this.FullOscAddress, displayName, groupName);

            // 颜色逻辑
            if (!activeTextColor.HasValue && !deactiveTextColor.HasValue)
            {
                this._actualActiveTextColor = BitmapColor.White;
                this._actualDeactiveTextColor = BitmapColor.White;
            }
            else if (activeTextColor.HasValue && !deactiveTextColor.HasValue)
            {
                this._actualActiveTextColor = activeTextColor.Value;
                this._actualDeactiveTextColor = BitmapColor.White;
            }
            else if (!activeTextColor.HasValue && deactiveTextColor.HasValue)
            {
                this._actualActiveTextColor = BitmapColor.Black;
                this._actualDeactiveTextColor = deactiveTextColor.Value;
            }
            else
            {
                this._actualActiveTextColor = activeTextColor.Value;
                this._actualDeactiveTextColor = deactiveTextColor.Value;
            }
        }

        private void OnOSCStateChanged(object sender, OSCStateManager.StateChangedEventArgs e)
        {
            if (e.Address == this.FullOscAddress)
            {
                var newState = e.Value > 0.5f;
                if (this._isActive != newState)
                {
                    this._isActive = newState;
                    this.ActionImageChanged();
                }
            }
        }

        protected override void RunCommand(string actionParameter)
        {
            var newValue = this._isActive ? 0f : 1f;
            ReaOSCPlugin.SendOSCMessage(this.FullOscAddress, newValue);
        }

        protected override BitmapImage GetCommandImage(string actionParameter, PluginImageSize imageSize)
        {
            using (var bitmap = new BitmapBuilder(imageSize))
            {
                var bgColor = this._isActive ? this.ActiveColor : BitmapColor.Black;
                bitmap.FillRectangle(0, 0, bitmap.Width, bitmap.Height, bgColor);

                if (!string.IsNullOrEmpty(this._buttonImageName))
                {
                    try
                    {
                        var icon = PluginResources.ReadImage(this._buttonImageName);
                        int iconHeight = 46;
                        int iconWidth = icon.Width * iconHeight / icon.Height;
                        int iconX = (bitmap.Width - iconWidth) / 2;
                        int iconY = 8;
                        bitmap.DrawImage(icon, iconX, iconY, iconWidth, iconHeight);

                        var textColor = this._isActive ? this._actualActiveTextColor : this._actualDeactiveTextColor;
                        // === 使用我们自己保存的真实 DisplayName 来绘图 ===
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
                        PluginLog.Warning($"图标加载失败: {this._buttonImageName} -> {ex.Message}");
                        this.DrawButtonContent(bitmap);
                    }
                }
                else
                {
                    this.DrawButtonContent(bitmap);
                }
                return bitmap.ToImage();
            }
        }

        // === 关键修改 4：让 GetCommandDisplayName 返回 " " ===
        //protected override String GetCommandDisplayName(String actionParameter, PluginImageSize imageSize)
        //{
            // 返回一个空格，让系统画一个“看不见”的名字
            //return null;
        //}

        protected virtual void DrawButtonContent(BitmapBuilder bitmap)
        {
            var fontSize = this.CalculateOptimalFontSize(this.DisplayName);
            var textColor = this._isActive ? this._actualActiveTextColor : this._actualDeactiveTextColor;
            // === 确保这里也用 this.DisplayName ===
            bitmap.DrawText(
                text: this.DisplayName,
                fontSize: fontSize,
                color: textColor
                );
        }

        private int CalculateOptimalFontSize(string text)
        {
            if (string.IsNullOrEmpty(text))
                return 26;
            if (text.Length > 15)
                return 14;
            if (text.Length > 5)
                return 21;
            return 26;
        }

        public void Dispose()
        {
            OSCStateManager.Instance.StateChanged -= this.OnOSCStateChanged;
        }
    }
}