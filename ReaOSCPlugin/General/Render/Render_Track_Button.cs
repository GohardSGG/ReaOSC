namespace Loupedeck.ReaOSCPlugin.General.Render
{
    public class Track_Button : PluginDynamicCommand
    {
        public Track_Button()
            : base(displayName: "Track Button", description: "Set Source=Track", groupName: "Render")
        {
            RenderStateManager.StateChanged += () => this.ActionImageChanged();
            this.AddParameter("Track Button", "Track", "Render");
        }

        protected override void RunCommand(string actionParameter)
        {
            RenderStateManager.SetSource(RenderStateManager.SourceType.Track);
        }

        protected override BitmapImage GetCommandImage(string actionParameter, PluginImageSize imageSize)
        {
            var isActive = RenderStateManager.Source == RenderStateManager.SourceType.Track;

            using (var bitmap = new BitmapBuilder(imageSize))
            {
                if (isActive)
                {
                    bitmap.Clear(BitmapColor.White);
                    bitmap.DrawText("Track", fontSize: 23, color: BitmapColor.Black);
                }
                else
                {
                    bitmap.Clear(BitmapColor.Black);
                    bitmap.DrawText("Track", fontSize: 23, color: BitmapColor.White);
                }
                return bitmap.ToImage();
            }
        }
    }
}
