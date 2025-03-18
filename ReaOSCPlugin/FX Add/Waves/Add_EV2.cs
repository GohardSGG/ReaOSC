namespace Loupedeck.ReaOSCPlugin.FX_Add

{

    using Loupedeck;
    using Loupedeck.ReaOSCPlugin;

    public class Add_EV2 : PluginDynamicCommand
    {
        public Add_EV2()
            : base(
                displayName: "Add EV2",
                description: "插入Waves Add EV2效果器",
                groupName: "Waves FX")
        {
                this.AddParameter("Add EV2", "Add EV2添加按钮", "FX Add");
        }

        protected override void RunCommand(string actionParameter)
        {
            ReaOSCPlugin.SendFXMessage("Add/Waves/EV2", 1);
            PluginLog.Info("已触发Add EV2添加请求");
        }

        protected override BitmapImage GetCommandImage(string actionParameter, PluginImageSize imageSize)
        {
            using (var bitmap = new BitmapBuilder(imageSize))
            {
                bitmap.Clear(BitmapColor.Black);
                bitmap.DrawText(
                    text: "EV2",
                    fontSize: 33,
                    color: BitmapColor.White
                );

                return bitmap.ToImage();
            }
        }
    }
}
