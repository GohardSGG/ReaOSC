namespace Loupedeck.ReaOSCPlugin.FX_Add

{

    using Loupedeck;
    using Loupedeck.ReaOSCPlugin;

    public class Add_Multi_BusComp_G3 : PluginDynamicCommand
    {
        public Add_Multi_BusComp_G3()
            : base(
                displayName: "Multi BusComp G3",
                description: "插入SSL Multi BusComp G3效果器",
                groupName: "SSL FX")
        {
            this.AddParameter("Add_Multi_BusComp_G3", "Multi BusComp G3添加按钮", "FX Add");
        }

        protected override void RunCommand(string actionParameter)
        {
            ReaOSCPlugin.SendFXMessage("Add/SSL/Multi BusComp_G3", 1);
            PluginLog.Info("已触发Multi BusComp G3添加请求");
        }

        protected override BitmapImage GetCommandImage(string actionParameter, PluginImageSize imageSize)
        {
            using (var bitmap = new BitmapBuilder(imageSize))
            {
                bitmap.Clear(BitmapColor.Black);
                bitmap.DrawText(
                    text: "BusComp G3",
                    fontSize: 26,
                    color: BitmapColor.White
                );
                bitmap.DrawText(
                    text: "Multi",
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
