namespace Loupedeck.ReaOSCPlugin.FX_Add

{

    using Loupedeck;
    using Loupedeck.ReaOSCPlugin;

    public class Add_Native_BusComp_2 : PluginDynamicCommand
    {
        public Add_Native_BusComp_2()
            : base(
                displayName: "Native BusComp 2",
                description: "插入SSL Native BusComp 2效果器",
                groupName: "SSL FX")
        {
            this.AddParameter("Add_Native_BusComp_2", "Native BusComp 2添加按钮", "FX Add");
        }

        protected override void RunCommand(string actionParameter)
        {
            ReaOSCPlugin.SendFXMessage("Add/SSL/Native BusComp 2", 1);
            PluginLog.Info("已触发Native BusComp 2添加请求");
        }

        protected override BitmapImage GetCommandImage(string actionParameter, PluginImageSize imageSize)
        {
            using (var bitmap = new BitmapBuilder(imageSize))
            {
                bitmap.Clear(BitmapColor.Black);
                bitmap.DrawText(
                    text: "BusComp 2",
                    fontSize: 26,
                    color: BitmapColor.White
                );
                bitmap.DrawText(
                    text: "Native",
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
