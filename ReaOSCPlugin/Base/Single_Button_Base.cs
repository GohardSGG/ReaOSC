namespace Loupedeck.ReaOSCPlugin.Base
{
    using System;
    using System.Timers;

    using Loupedeck;

    using BitmapColor = Loupedeck.BitmapColor;

    public abstract class Single_Button_Base : PluginDynamicCommand, IDisposable
    {
        protected readonly string DisplayName;
        protected readonly string FullOscAddress;
        protected readonly BitmapColor ActiveColor;
        protected readonly BitmapColor DefaultColor = BitmapColor.Black;
        private bool _isTemporaryActive;
        private readonly Timer _resetTimer;

        protected Single_Button_Base(
            string displayName,
            string description,
            string groupName,
            string oscAddress,
            BitmapColor activeColor)
            : base(displayName, description, groupName)
        {
            DisplayName = displayName;
            FullOscAddress = $"/{groupName?.Trim('/')}/{oscAddress}";
            ActiveColor = activeColor;

            // 初始化1秒计时器
            _resetTimer = new Timer(500);
            _resetTimer.Elapsed += (sender, e) =>
            {
                _isTemporaryActive = false;
                ActionImageChanged(); // 触发UI刷新
                _resetTimer.Stop();
            };

            this.AddParameter(FullOscAddress, displayName, groupName);
        }

        protected override void RunCommand(string actionParameter)
        {
            // 发送固定值1
            ReaOSCPlugin.SendOSCMessage(FullOscAddress, 1f);

            // 触发临时激活状态
            _isTemporaryActive = true;
            ActionImageChanged();

            // 重置计时器
            _resetTimer.Stop();
            _resetTimer.Start();
        }

        protected override BitmapImage GetCommandImage(string actionParameter, PluginImageSize imageSize)
        {
            using (var bitmap = new BitmapBuilder(imageSize))
            {
                // 根据临时状态显示颜色
                bitmap.Clear(_isTemporaryActive ? ActiveColor : DefaultColor);
                DrawButtonContent(bitmap);
                return bitmap.ToImage();
            }
        }

        protected virtual void DrawButtonContent(BitmapBuilder bitmap)
        {
            var fontSize = CalculateOptimalFontSize(DisplayName);
            bitmap.DrawText(
                text: DisplayName,
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

        public void Dispose()
        {
            _resetTimer?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}