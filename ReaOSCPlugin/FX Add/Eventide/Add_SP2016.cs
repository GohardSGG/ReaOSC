namespace Loupedeck.ReaOSCPlugin.FX_Add


{

    using Loupedeck;
    using Loupedeck.ReaOSCPlugin;

    public class Add_SP2016 : PluginDynamicCommand
    {
        public Add_SP2016()
            : base(
                displayName: "SP2016",
                description: "插入Eventide SP2016效果器",
                groupName: "Eventide FX")
        {
            this.AddParameter("Add SP2016", "SP2016添加按钮", "FX Add");
        }

        protected override void RunCommand(string actionParameter)
        {
            ReaOSCPlugin.SendFXMessage("Add/Eventide/SP2016", 1);
            PluginLog.Info("已触发SP2016添加请求");
        }

        protected override BitmapImage GetCommandImage(string actionParameter, PluginImageSize imageSize)
        {
            using (var bitmap = new BitmapBuilder(imageSize))
            {
                bitmap.Clear(BitmapColor.Black);
                bitmap.DrawText(
                    text: "SP2016",
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
