namespace Loupedeck.ReaOSCPlugin.FX_Add

{

    using Loupedeck;
    using Loupedeck.ReaOSCPlugin;

    public class Add_LA_2A_Silver : PluginDynamicCommand
    {
        public Add_LA_2A_Silver()
            : base(
                displayName: "LA-2A Silver",
                description: "插入UAD LA-2A Silver效果器",
                groupName: "UAD FX")
        {
            this.AddParameter("Add LA-2A Silver", "LA-2A Silver添加按钮", "FX Add");
        }

        protected override void RunCommand(string actionParameter)
        {
            ReaOSCPlugin.SendFXMessage("Add/UAD/LA-2A Silver", 1);
            PluginLog.Info("已触发LA-2A Silver添加请求");
        }

        protected override BitmapImage GetCommandImage(string actionParameter, PluginImageSize imageSize)
        {
            using (var bitmap = new BitmapBuilder(imageSize))
            {
                bitmap.Clear(BitmapColor.Black);
                bitmap.DrawText(
                    text: "LA-2A",
                    fontSize: 26,
                    color: BitmapColor.White
                );
                bitmap.DrawText(
                    text: "Silver",
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
