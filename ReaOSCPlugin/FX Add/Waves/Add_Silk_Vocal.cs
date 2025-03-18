namespace Loupedeck.ReaOSCPlugin.FX_Add

{

    using Loupedeck;
    using Loupedeck.ReaOSCPlugin;

    public class Add_Silk_Vocal : PluginDynamicCommand
    {
        public Add_Silk_Vocal()
            : base(
                displayName: "Add Silk Vocal",
                description: "插入Waves Add Silk Vocal效果器",
                groupName: "Waves FX")
        {
                this.AddParameter("Add Silk Vocal", "Add Silk Vocal添加按钮", "FX Add");
        }

        protected override void RunCommand(string actionParameter)
        {
            ReaOSCPlugin.SendFXMessage("Add/Waves/Silk Vocal", 1);
            PluginLog.Info("已触发Add Silk Vocal添加请求");
        }

        protected override BitmapImage GetCommandImage(string actionParameter, PluginImageSize imageSize)
        {
            using (var bitmap = new BitmapBuilder(imageSize))
            {
                bitmap.Clear(BitmapColor.Black);
                bitmap.DrawText(
                    text: "Silk Vocal",
                    fontSize: 23,
                    color: BitmapColor.White
                );

                return bitmap.ToImage();
            }
        }
    }
}
