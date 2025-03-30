namespace Loupedeck.ReaOSCPlugin.Render
{
    using Loupedeck;
    using Loupedeck.ReaOSCPlugin.Render;
    public class Audio_And_Video_Mode_Button : PluginDynamicCommand
    {
        BitmapColor text2color = new BitmapColor(136, 226, 255);
        public Audio_And_Video_Mode_Button()
            : base(displayName: "Audio And Video Mode Button", description: "Toggle Audio ↔ Video Mode", groupName: "Render")
        {
            RenderStateManager.StateChanged += () => this.ActionImageChanged();
            this.AddParameter("Audio And Video Mode Button", "Audio/Video", "Render");
        }

        protected override void RunCommand(string actionParameter)
        {
            // 切换模式
            RenderStateManager.ToggleAvMode();
        }

        protected override BitmapImage GetCommandImage(string actionParameter, PluginImageSize imageSize)
        {
            var isAudio = (RenderStateManager.AvMode == RenderStateManager.AvModeType.Audio);

            using (var bitmap = new BitmapBuilder(imageSize))
            {

                if (!isAudio)
                {
                    // Video模式 => 白底, “Video”黑字
                    bitmap.Clear(BitmapColor.White);
                    bitmap.DrawText("Video", fontSize: 21, color: BitmapColor.Black);
                    text2color = BitmapColor.Black;
                }
                else
                {
                    // Audio模式 => 黑底, “Audio”白字
                    bitmap.Clear(BitmapColor.Black);
                    bitmap.DrawText("Audio", fontSize: 21, color: BitmapColor.White);
                    text2color = new BitmapColor(136, 226, 255);
                }

                bitmap.DrawText(
                text: "Mode",
                x: 50,
                y: 55,
                width: 14,
                height: 14,
                fontSize: 14,
                color: this.text2color);

                return bitmap.ToImage();
            }
        }
    }
}
