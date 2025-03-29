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
        protected readonly BitmapColor DefaultColor = BitmapColor.Black;

        private readonly String _buttonImageName;

        protected Toggle_Button_Base(
            string displayName,
            string description,
            string groupName,
            string oscAddress,
            BitmapColor activeColor,
            string buttonImage = null
        ) : base(displayName, description, groupName)
        {
            this.DisplayName = displayName;
            this.FullOscAddress = $"/{groupName?.Trim('/')}/{oscAddress}";
            this.ActiveColor = activeColor;
            this._buttonImageName = buttonImage; // 只保存文件名

            this._isActive = OSCStateManager.Instance.GetState(this.FullOscAddress) > 0.5f;
            OSCStateManager.Instance.StateChanged += this.OnOSCStateChanged;

            this.AddParameter(this.FullOscAddress, displayName, groupName);
        }

        private void OnOSCStateChanged(object sender, OSCStateManager.StateChangedEventArgs e)
        {
            if (e.Address == this.FullOscAddress)
            {
                this._isActive = e.Value > 0.5f;
                this.ActionImageChanged();
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
                var bgColor = this._isActive ? this.ActiveColor : this.DefaultColor;
                bitmap.FillRectangle(0, 0, bitmap.Width, bitmap.Height, bgColor);

                if (!string.IsNullOrEmpty(this._buttonImageName))
                {
                    try
                    {
                        var icon = PluginResources.ReadImage("Toggle_Record.png");

                        int iconHeight = 66;
                        int iconWidth = icon.Width * iconHeight / icon.Height; // 保持比例
                        int iconX = (bitmap.Width - iconWidth) / 2;
                        int iconY = 0;

                        bitmap.DrawImage(icon, iconX, iconY, iconWidth, iconHeight);

                        // 图标下方显示文字（不使用 alignment 参数）
                        bitmap.DrawText(
                            text: this.DisplayName,
                            x: 0,
                            y: bitmap.Height - 22,
                            width: bitmap.Width,
                            height: 20,
                            fontSize: 12,
                            color: BitmapColor.White);
                    }
                    catch (Exception ex)
                    {
                        this.Log.Warning($"图标加载失败: {this._buttonImageName} -> {ex.Message}");
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

        protected virtual void DrawButtonContent(BitmapBuilder bitmap)
        {
            var fontSize = this.CalculateOptimalFontSize(this.DisplayName);
            bitmap.DrawText(
                text: this.DisplayName,
                fontSize: fontSize,
                color: BitmapColor.White);
        }

        private int CalculateOptimalFontSize(string text)
        {
            if (text.Length > 15)
                return 14;
            if (text.Length > 10)
                return 16;
            return 21;
        }

        public void Dispose() => OSCStateManager.Instance.StateChanged -= this.OnOSCStateChanged;
    }
}
