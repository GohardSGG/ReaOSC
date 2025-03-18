namespace Loupedeck.ReaOSCPlugin.FX_Add


{

    using Loupedeck;
    using Loupedeck.ReaOSCPlugin;

    public class Add_MicroPitch_Immersive : PluginDynamicCommand
    {
        public Add_MicroPitch_Immersive()
            : base(
                displayName: "MicroPitch Immersive",
                description: "插入Eventide MicroPitch Immersive效果器",
                groupName: "Eventide FX")
        {
            this.AddParameter("Add MicroPitch Immersive", "MicroPitch Immersive添加按钮", "FX Add");
        }

        protected override void RunCommand(string actionParameter)
        {
            ReaOSCPlugin.SendFXMessage("Add/Eventide/MicroPitch Immersive", 1);
            PluginLog.Info("已触发MicroPitch Immersive添加请求");
        }

        protected override BitmapImage GetCommandImage(string actionParameter, PluginImageSize imageSize)
        {
            using (var bitmap = new BitmapBuilder(imageSize))
            {
                bitmap.Clear(BitmapColor.Black);
                bitmap.DrawText(
                    text: "MicroPitch",
                    fontSize: 23,
                    color: BitmapColor.White
                );
                bitmap.DrawText(
                    text: "Immersive",
                    x:35,
                    y:55,
                    width:14,
                    height:14,
                    fontSize: 14,
                    color: new BitmapColor(255, 226, 0)
                );
                return bitmap.ToImage();
            }
        }
    }
}
