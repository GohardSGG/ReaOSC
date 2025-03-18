namespace Loupedeck.ReaOSCPlugin.FX_Add

{

    using Loupedeck;
    using Loupedeck.ReaOSCPlugin;

    public class Add_True_Level : PluginDynamicCommand
    {
        public Add_True_Level()
            : base(
                displayName: "True Level",
                description: "插入Sonible True Level效果器",
                groupName: "Sonible FX")
        {
            this.AddParameter("Add True Level", "True Level添加按钮", "FX Add");
        }

        protected override void RunCommand(string actionParameter)
        {
            ReaOSCPlugin.SendFXMessage("Add/Sonible/True Level", 1);
            PluginLog.Info("已触发True Level添加请求");
        }

        protected override BitmapImage GetCommandImage(string actionParameter, PluginImageSize imageSize)
        {
            using (var bitmap = new BitmapBuilder(imageSize))
            {
                bitmap.Clear(BitmapColor.Black);
                bitmap.DrawText(
                    text: "Level",
                    fontSize: 23,
                    color: BitmapColor.White
                );
                bitmap.DrawText(
                    text: "True",
                    x:50,
                    y:55,
                    width:14,
                    height:14,
                    fontSize: 14,
                    color: new BitmapColor(255, 0, 0)
                );
                return bitmap.ToImage();
            }
        }
    }
}
