namespace Loupedeck.ReaOSCPlugin.FX_Add

{

    using Loupedeck;
    using Loupedeck.ReaOSCPlugin;

    public class Add_True_Balance : PluginDynamicCommand
    {
        public Add_True_Balance()
            : base(
                displayName: "True Balance",
                description: "插入Sonible True Balance效果器",
                groupName: "Sonible FX")
        {
            this.AddParameter("Add True Balance", "True Balance添加按钮", "FX Add");
        }

        protected override void RunCommand(string actionParameter)
        {
            ReaOSCPlugin.SendFXMessage("Add/Sonible/True Balance", 1);
            PluginLog.Info("已触发True Balance添加请求");
        }

        protected override BitmapImage GetCommandImage(string actionParameter, PluginImageSize imageSize)
        {
            using (var bitmap = new BitmapBuilder(imageSize))
            {
                bitmap.Clear(BitmapColor.Black);
                bitmap.DrawText(
                    text: "Balance",
                    fontSize: 19,
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
