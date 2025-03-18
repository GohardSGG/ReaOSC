namespace Loupedeck.ReaOSCPlugin.FX_Add


{

    using Loupedeck;
    using Loupedeck.ReaOSCPlugin;

    public class Add_H3000_Delays_II : PluginDynamicCommand
    {
        public Add_H3000_Delays_II()
            : base(
                displayName: "H3000 Delays II",
                description: "插入Eventide H3000 Delays II效果器",
                groupName: "Eventide FX")
        {
            this.AddParameter("Add H3000 Delays II", "H3000 Delays II添加按钮", "FX Add");
        }

        protected override void RunCommand(string actionParameter)
        {
            ReaOSCPlugin.SendFXMessage("Add/Eventide/H3000 Delays II", 1);
            PluginLog.Info("已触发H3000 Delays II添加请求");
        }

        protected override BitmapImage GetCommandImage(string actionParameter, PluginImageSize imageSize)
        {
            using (var bitmap = new BitmapBuilder(imageSize))
            {
                bitmap.Clear(BitmapColor.Black);
                bitmap.DrawText(
                    text: "H3000",
                    fontSize: 23,
                    color: BitmapColor.White
                );
                bitmap.DrawText(
                    text: "Delays II",
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
