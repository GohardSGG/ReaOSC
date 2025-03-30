namespace Loupedeck.ReaOSCPlugin.Render
{
    using Loupedeck;
    using Loupedeck.ReaOSCPlugin.Render;
    public class Format_2_Button : PluginDynamicCommand
    {
        public Format_2_Button()
            : base(displayName: "Format 2 Button", description: "Select Format2", groupName: "Render")
        {
            RenderStateManager.StateChanged += () => this.ActionImageChanged();
            this.AddParameter("Format 2 Button", "Format2", "Render");
        }

        protected override void RunCommand(string actionParameter)
        {
            RenderStateManager.SetFormat(RenderStateManager.FormatType.Format2);
        }

        protected override BitmapImage GetCommandImage(string actionParameter, PluginImageSize imageSize)
        {
            var isActive = (RenderStateManager.Format == RenderStateManager.FormatType.Format2);
            var isAudio = (RenderStateManager.AvMode == RenderStateManager.AvModeType.Audio);
            var text = isAudio ? "MP3" : "GIF";

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
