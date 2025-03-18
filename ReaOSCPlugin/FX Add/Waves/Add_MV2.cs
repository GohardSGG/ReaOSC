namespace Loupedeck.ReaOSCPlugin.FX_Add

{

    using Loupedeck;
    using Loupedeck.ReaOSCPlugin;

    public class Add_MV2 : PluginDynamicCommand
    {
        public Add_MV2()
            : base(
                displayName: "Add MV2",
                description: "插入Waves Add MV2效果器",
                groupName: "Waves FX")
        {
                this.AddParameter("Add MV2", "Add MV2添加按钮", "FX Add");
        }

        protected override void RunCommand(string actionParameter)
        {
            ReaOSCPlugin.SendFXMessage("Add/Waves/MV2", 1);
            PluginLog.Info("已触发Add MV2添加请求");
        }

        protected override BitmapImage GetCommandImage(string actionParameter, PluginImageSize imageSize)
        {
            using (var bitmap = new BitmapBuilder(imageSize))
            {
                bitmap.Clear(BitmapColor.Black);
                bitmap.DrawText(
                    text: "MV2",
                    fontSize: 33,
                    color: BitmapColor.White
                );

                return bitmap.ToImage();
            }
        }
    }
}
