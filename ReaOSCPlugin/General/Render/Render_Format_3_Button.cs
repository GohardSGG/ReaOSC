namespace Loupedeck.ReaOSCPlugin.General.Render
{
    using Loupedeck;

    public class Format_3_Button : PluginDynamicCommand
    {
        public Format_3_Button()
            : base(displayName: "Format 3 Button", description: "Select Format3", groupName: "Render")
        {
            RenderStateManager.StateChanged += () => this.ActionImageChanged();
            this.AddParameter("Format 3 Button", "Format3", "Render");
        }

        protected override void RunCommand(string actionParameter)
        {
            RenderStateManager.SetFormat(RenderStateManager.FormatType.Format3);
        }

        protected override BitmapImage GetCommandImage(string actionParameter, PluginImageSize imageSize)
        {
            var isActive = RenderStateManager.Format == RenderStateManager.FormatType.Format3;
            var isAudio = RenderStateManager.AvMode == RenderStateManager.AvModeType.Audio;
            var text = isAudio ? "ADM" : "AAC";

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
