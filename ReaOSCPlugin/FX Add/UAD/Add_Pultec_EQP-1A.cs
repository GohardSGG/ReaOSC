namespace Loupedeck.ReaOSCPlugin.FX_Add

{

    using Loupedeck;
    using Loupedeck.ReaOSCPlugin;

    public class Add_EQP_1A_Pultec : PluginDynamicCommand
    {
        public Add_EQP_1A_Pultec()
            : base(
                displayName: "Pultec EQP-1A",
                description: "插入UAD Pultec EQP-1A效果器",
                groupName: "UAD FX")
        {
            this.AddParameter("Add Pultec EQP-1A", "Pultec EQP-1A添加按钮", "FX Add");
        }

        protected override void RunCommand(string actionParameter)
        {
            ReaOSCPlugin.SendFXMessage("Add/UAD/Pultec EQP-1A", 1);
            PluginLog.Info("已触发EQP-1A Pultec添加请求");
        }

        protected override BitmapImage GetCommandImage(string actionParameter, PluginImageSize imageSize)
        {
            using (var bitmap = new BitmapBuilder(imageSize))
            {
                bitmap.Clear(BitmapColor.Black);
                bitmap.DrawText(
                    text: "EQP-1A",
                    fontSize: 26,
                    color: BitmapColor.White
                );
                bitmap.DrawText(
                    text: "Pultec",
                    x:50,
                    y:55,
                    width:14,
                    height:14,
                    fontSize: 14,
                    color: new BitmapColor(255, 226, 0)
                );
                return bitmap.ToImage();
            }
        }
    }
}
