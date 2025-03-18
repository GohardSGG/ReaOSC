namespace Loupedeck.ReaOSCPlugin.FX_Add

{

    using Loupedeck;
    using Loupedeck.ReaOSCPlugin;

    public class Add_IDX_Dynamics : PluginDynamicCommand
    {
        public Add_IDX_Dynamics()
            : base(
                displayName: "Add IDX Dynamics",
                description: "插入Waves Add IDX Dynamics效果器",
                groupName: "Waves FX")
        {
                this.AddParameter("Add IDX Dynamics", "Add IDX Dynamics添加按钮", "FX Add");
        }

        protected override void RunCommand(string actionParameter)
        {
            ReaOSCPlugin.SendFXMessage("Add/Waves/IDX Dynamics", 1);
            PluginLog.Info("已触发Add IDX Dynamics添加请求");
        }

        protected override BitmapImage GetCommandImage(string actionParameter, PluginImageSize imageSize)
        {
            using (var bitmap = new BitmapBuilder(imageSize))
            {
                bitmap.Clear(BitmapColor.Black);
                bitmap.DrawText(
                    text: "Dynamics",
                    fontSize: 16,
                    color: BitmapColor.White
                );
                bitmap.DrawText(
                    text: "IDX",
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
