namespace Loupedeck.ReaOSCPlugin.FX_Add

{

    using Loupedeck;
    using Loupedeck.ReaOSCPlugin;

    public class Add_Pultec_HLF_3C : PluginDynamicCommand
    {
        public Add_Pultec_HLF_3C()
            : base(
                displayName: "Pultec HLF-3C",
                description: "插入UAD Pultec HLF-3C效果器",
                groupName: "UAD FX")
        {
            this.AddParameter("Add Pultec HLF 3C", "Pultec HLF-3C添加按钮", "FX Add");
        }

        protected override void RunCommand(string actionParameter)
        {
            ReaOSCPlugin.SendFXMessage("Add/UAD/Pultec HLF-3C", 1);
            PluginLog.Info("已触发HLF-3C Pultec添加请求");
        }

        protected override BitmapImage GetCommandImage(string actionParameter, PluginImageSize imageSize)
        {
            using (var bitmap = new BitmapBuilder(imageSize))
            {
                bitmap.Clear(BitmapColor.Black);
                bitmap.DrawText(
                    text: "HLF-3C",
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
