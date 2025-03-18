namespace Loupedeck.ReaOSCPlugin.FX_Add

{

    using Loupedeck;
    using Loupedeck.ReaOSCPlugin;

    public class Add_Harmony : PluginDynamicCommand
    {
        public Add_Harmony()
            : base(
                displayName: "Add Harmony",
                description: "插入Waves Add Harmony效果器",
                groupName: "Waves FX")
        {
                this.AddParameter("Add Harmony", "Add Harmony添加按钮", "FX Add");
        }

        protected override void RunCommand(string actionParameter)
        {
            ReaOSCPlugin.SendFXMessage("Add/Waves/Harmony", 1);
            PluginLog.Info("已触发Add Harmony添加请求");
        }

        protected override BitmapImage GetCommandImage(string actionParameter, PluginImageSize imageSize)
        {
            using (var bitmap = new BitmapBuilder(imageSize))
            {
                bitmap.Clear(BitmapColor.Black);
                bitmap.DrawText(
                    text: "Harmony",
                    fontSize: 18,
                    color: BitmapColor.White
                );

                return bitmap.ToImage();
            }
        }
    }
}
