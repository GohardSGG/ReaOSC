namespace Loupedeck.ReaOSCPlugin.General.FX
{
    using Loupedeck;

    public class FX_Toggle_Offline_Button : PluginDynamicCommand, IDisposable
    {
        public FX_Toggle_Offline_Button()
            : base("Offline", "FX or Chain Offline", "FX")
        {
            FX_State_Manager.FXStateChanged += this.ActionImageChanged;
            OSCStateManager.Instance.StateChanged += this.OnOscStateChanged;
            this.AddParameter("/FX/Offline", "Offline", "FX");
        }

        private void OnOscStateChanged(object sender, OSCStateManager.StateChangedEventArgs e)
        {
            var currentAddress = $"/FX/{FX_State_Manager.GetToggleOscAddress("Offline")}";
            if (e.Address == currentAddress)
            {
                FX_State_Manager.SetToggleActive("Offline", e.Value > 0.5f);
            }
        }

        protected override void RunCommand(string actionParameter)
        {
            var active = FX_State_Manager.GetToggleActive("Offline");
            var addr = FX_State_Manager.GetToggleOscAddress("Offline");
            ReaOSCPlugin.SendOSCMessage($"/FX/{addr}", active ? 0f : 1f);
            FX_State_Manager.SetToggleActive("Offline", !active);
        }

        protected override BitmapImage GetCommandImage(string actionParameter, PluginImageSize imageSize)
        {
            var active = FX_State_Manager.GetToggleActive("Offline");

            using (var bitmap = new BitmapBuilder(imageSize))
            {
                bitmap.Clear(active ? BitmapColor.White : BitmapColor.Black);
                bitmap.DrawText("Offline", fontSize: 21, color: active ? BitmapColor.Black : BitmapColor.White);
                var mode = FX_State_Manager.CurrentMode == FX_State_Manager.FXModeType.FX ? "FX" : "Chain";
                bitmap.DrawText(mode, x: 50, y: 55, width: 14, height: 14, fontSize: 14, color: new BitmapColor(136, 226, 255));
                return bitmap.ToImage();
            }
        }

        public void Dispose()
        {
            OSCStateManager.Instance.StateChanged -= this.OnOscStateChanged;
            FX_State_Manager.FXStateChanged -= this.ActionImageChanged;
        }
    }
}
