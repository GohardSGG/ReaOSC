namespace Loupedeck.ReaOSCPlugin.General.FX
{
    using Loupedeck;

    public class FX_Toggle_Bypass_Button : PluginDynamicCommand, IDisposable
    {
        public FX_Toggle_Bypass_Button()
            : base("Bypass", "FX or Chain Bypass", "FX")
        {
            FX_State_Manager.FXStateChanged += this.ActionImageChanged;
            OSCStateManager.Instance.StateChanged += this.OnOscStateChanged; // 关键回调订阅
            this.AddParameter("/FX/Bypass", "Bypass", "FX");
        }

        private void OnOscStateChanged(object sender, OSCStateManager.StateChangedEventArgs e)
        {
            var currentAddress = $"/FX/{FX_State_Manager.GetToggleOscAddress("Bypass")}";
            if (e.Address == currentAddress)
            {
                FX_State_Manager.SetToggleActive("Bypass", e.Value > 0.5f);
            }
        }

        protected override void RunCommand(string actionParameter)
        {
            var active = FX_State_Manager.GetToggleActive("Bypass");
            var addr = FX_State_Manager.GetToggleOscAddress("Bypass");
            ReaOSCPlugin.SendOSCMessage($"/FX/{addr}", active ? 0f : 1f);
            FX_State_Manager.SetToggleActive("Bypass", !active);
        }

        protected override BitmapImage GetCommandImage(string actionParameter, PluginImageSize imageSize)
        {
            var active = FX_State_Manager.GetToggleActive("Bypass");

            using (var bitmap = new BitmapBuilder(imageSize))
            {
                bitmap.Clear(active ? BitmapColor.White : BitmapColor.Black);
                bitmap.DrawText("Bypass", fontSize: 21, color: active ? BitmapColor.Black : BitmapColor.White);

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
