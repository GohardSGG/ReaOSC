namespace Loupedeck.ReaOSCPlugin.FX_Add

{

    using Loupedeck;
    using Loupedeck.ReaOSCPlugin;

    public class Add_Fusion_HF_Comp : PluginDynamicCommand
    {
        public Add_Fusion_HF_Comp()
            : base(
                displayName: "Fusion HF Comp",
                description: "插入SSL Fusion HF Comp效果器",
                groupName: "SSL FX")
        {
            this.AddParameter("Add_Fusion_HF_Comp", "Fusion HF Comp添加按钮", "FX Add");
        }

        protected override void RunCommand(string actionParameter)
        {
            ReaOSCPlugin.SendFXMessage("Add/SSL/Fusion HF Comp", 1);
            PluginLog.Info("已触发Fusion HF Comp添加请求");
        }

        protected override BitmapImage GetCommandImage(string actionParameter, PluginImageSize imageSize)
        {
            using (var bitmap = new BitmapBuilder(imageSize))
            {
                bitmap.Clear(BitmapColor.Black);
                bitmap.DrawText(
                    text: "HF Comp",
                    fontSize: 26,
                    color: BitmapColor.White
                );
                bitmap.DrawText(
                    text: "Fusion",
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
