namespace Loupedeck.ReaOSCPlugin.FX_Add

{

    using Loupedeck;
    using Loupedeck.ReaOSCPlugin;

    public class Add_CLA_2A : PluginDynamicCommand
    {
        public Add_CLA_2A()
            : base(
                displayName: "Add CLA-2A",
                description: "插入Waves Add CLA-2A效果器",
                groupName: "Waves FX")
        {
                this.AddParameter("Add CLA-2A", "Add CLA-2A添加按钮", "FX Add");
        }

        protected override void RunCommand(string actionParameter)
        {
            ReaOSCPlugin.SendFXMessage("Add/Waves/CLA-2A", 1);
            PluginLog.Info("已触发Add CLA-2A添加请求");
        }

        protected override BitmapImage GetCommandImage(string actionParameter, PluginImageSize imageSize)
        {
            using (var bitmap = new BitmapBuilder(imageSize))
            {
                bitmap.Clear(BitmapColor.Black);
                bitmap.DrawText(
                    text: "2A",
                    fontSize: 33,
                    color: BitmapColor.White
                );
                bitmap.DrawText(
                    text: "CLA",
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
