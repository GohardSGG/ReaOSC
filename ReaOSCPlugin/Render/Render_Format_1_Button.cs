namespace Loupedeck.ReaOSCPlugin.Render
{
    using Loupedeck;
    using Loupedeck.ReaOSCPlugin.Render;
    public class Format_1_Button : PluginDynamicCommand
    {
        public Format_1_Button()
            : base(displayName: "Format 1 Button", description: "Select Format1", groupName: "Render")
        {
            RenderStateManager.StateChanged += () => this.ActionImageChanged();
            this.AddParameter("Format 1 Button", "Format1", "Render");
        }

        protected override void RunCommand(string actionParameter)
        {
            // 设置 Format=Format1
            RenderStateManager.SetFormat(RenderStateManager.FormatType.Format1);
        }

        protected override BitmapImage GetCommandImage(string actionParameter, PluginImageSize imageSize)
        {
            var isActive = (RenderStateManager.Format == RenderStateManager.FormatType.Format1);
            // 根据模式决定文字
            var isAudio = (RenderStateManager.AvMode == RenderStateManager.AvModeType.Audio);
            var text = isAudio ? "WAV" : "MP4";

            using (var bitmap = new BitmapBuilder(imageSize))
            {
                if (isActive)
                {
                    bitmap.Clear(BitmapColor.White);
                    bitmap.DrawText(text, fontSize: 23, color: BitmapColor.Black);
                }
                else
                {
                    bitmap.Clear(BitmapColor.Black);
                    bitmap.DrawText(text, fontSize: 23, color: BitmapColor.White);
                }
                return bitmap.ToImage();
            }
        }
    }
}
