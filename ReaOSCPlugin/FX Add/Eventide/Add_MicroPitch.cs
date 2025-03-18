namespace Loupedeck.ReaOSCPlugin.FX_Add


{

    using Loupedeck;
    using Loupedeck.ReaOSCPlugin;

    public class Add_MicroPitch : PluginDynamicCommand
    {
        public Add_MicroPitch()
            : base(
                displayName: "MicroPitch",
                description: "插入Eventide MicroPitch效果器",
                groupName: "Eventide FX")
        {
            this.AddParameter("Add MicroPitch", "MicroPitch添加按钮", "FX Add");
        }

        protected override void RunCommand(string actionParameter)
        {
            ReaOSCPlugin.SendFXMessage("Add/Eventide/MicroPitch", 1);
            PluginLog.Info("已触发MicroPitch添加请求");
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
                    text: "",
                    x:50,
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
