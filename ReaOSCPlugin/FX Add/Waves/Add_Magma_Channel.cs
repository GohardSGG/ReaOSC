namespace Loupedeck.ReaOSCPlugin.FX_Add

{

    using Loupedeck;
    using Loupedeck.ReaOSCPlugin;

    public class Add_Magma_Channel : PluginDynamicCommand
    {
        public Add_Magma_Channel()
            : base(
                displayName: "Add Magma Channel",
                description: "插入Waves Add Magma Channel效果器",
                groupName: "Waves FX")
        {
                this.AddParameter("Add Magma Channel", "Add Magma Channel添加按钮", "FX Add");
        }

        protected override void RunCommand(string actionParameter)
        {
            ReaOSCPlugin.SendFXMessage("Add/Waves/Magma Channel", 1);
            PluginLog.Info("已触发Add Magma Channel添加请求");
        }

        protected override BitmapImage GetCommandImage(string actionParameter, PluginImageSize imageSize)
        {
            using (var bitmap = new BitmapBuilder(imageSize))
            {
                bitmap.Clear(BitmapColor.Black);
                bitmap.DrawText(
                    text: "Channel",
                    fontSize: 19,
                    color: BitmapColor.White
                );
                bitmap.DrawText(
                    text: "Magma",
                    x:45,
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
