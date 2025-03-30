namespace Loupedeck.ReaOSCPlugin.General.FX
{
    using Loupedeck;

    public class FX_Copy_Button : PluginDynamicCommand
    {
        public FX_Copy_Button()
            : base("Copy", "FX or Chain Copy", "FX")
        {
            FX_State_Manager.FXStateChanged += this.ActionImageChanged;
            this.AddParameter("/FX/Copy", "Copy", "FX");
        }

        protected override void RunCommand(string actionParameter)
        {
            var addr = FX_State_Manager.GetToggleOscAddress("Copy");
            ReaOSCPlugin.SendOSCMessage($"/FX/{addr}", 1f);
        }

        protected override BitmapImage GetCommandImage(string actionParameter, PluginImageSize imageSize)
        {
            var mode = FX_State_Manager.CurrentMode == FX_State_Manager.FXModeType.FX ? "FX" : "Chain";

            using (var bitmap = new BitmapBuilder(imageSize))
            {
                bitmap.Clear(BitmapColor.Black);
                bitmap.DrawText("Copy", fontSize: 21, color: BitmapColor.White);
                bitmap.DrawText(
    text: mode,
    x: 50,
    y: 55,
    width: 14,
    height: 14,
    fontSize: 14,
    color: new BitmapColor(136, 226, 255)
);
                return bitmap.ToImage();
            }
        }
    }
}
