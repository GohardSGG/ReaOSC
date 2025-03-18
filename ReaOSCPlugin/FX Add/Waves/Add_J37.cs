namespace Loupedeck.ReaOSCPlugin.FX_Add

{

    using Loupedeck;
    using Loupedeck.ReaOSCPlugin;

    public class Add_J37 : PluginDynamicCommand
    {
        public Add_J37()
            : base(
                displayName: "Add J37",
                description: "插入Waves Add J37效果器",
                groupName: "Waves FX")
        {
                this.AddParameter("Add J37", "Add J37添加按钮", "FX Add");
        }

        protected override void RunCommand(string actionParameter)
        {
            ReaOSCPlugin.SendFXMessage("Add/Waves/J37", 1);
            PluginLog.Info("已触发Add J37添加请求");
        }

        protected override BitmapImage GetCommandImage(string actionParameter, PluginImageSize imageSize)
        {
            using (var bitmap = new BitmapBuilder(imageSize))
            {
                bitmap.Clear(BitmapColor.Black);
                bitmap.DrawText(
                    text: "J37",
                    fontSize: 33,
                    color: BitmapColor.White
                );
                bitmap.DrawText(
                    text: "",
                    x:55,
                    y:55,
                    width:14,
                    height:14,
                    fontSize: 14,
                    color: new BitmapColor(51, 112, 255)
                );
                return bitmap.ToImage();
            }
        }
    }
}
