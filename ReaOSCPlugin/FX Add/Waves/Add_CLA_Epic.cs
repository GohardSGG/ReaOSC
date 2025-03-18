namespace Loupedeck.ReaOSCPlugin.FX_Add

{

    using Loupedeck;
    using Loupedeck.ReaOSCPlugin;

    public class Add_CLA_Epic : PluginDynamicCommand
    {
        public Add_CLA_Epic()
            : base(
                displayName: "Add CLA Epic",
                description: "插入Waves Add CLA Epic效果器",
                groupName: "Waves FX")
        {
                this.AddParameter("Add CLA Epic", "Add CLA Epic添加按钮", "FX Add");
        }

        protected override void RunCommand(string actionParameter)
        {
            ReaOSCPlugin.SendFXMessage("Add/Waves/CLA Epic", 1);
            PluginLog.Info("已触发Add CLA Epic添加请求");
        }

        protected override BitmapImage GetCommandImage(string actionParameter, PluginImageSize imageSize)
        {
            using (var bitmap = new BitmapBuilder(imageSize))
            {
                bitmap.Clear(BitmapColor.Black);
                bitmap.DrawText(
                    text: "Epic",
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
