namespace Loupedeck.ReaOSCPlugin.FX_Add

{

    using Loupedeck;
    using Loupedeck.ReaOSCPlugin;

    public class Add_Kontakt_6 : PluginDynamicCommand
    {
        public Add_Kontakt_6()
            : base(
                displayName: "Add Kontakt 6",
                description: "插入Native Instruments Kontakt 6效果器",
                groupName: "Native Instruments FX")
        {
            this.AddParameter("Add Kontakt 6", "Kontakt 6添加按钮", "FX Add");
        }

        protected override void RunCommand(string actionParameter)
        {
            ReaOSCPlugin.SendFXMessage("Add/Native Instruments/Kontakt 6", 1);
            PluginLog.Info("已触发Kontakt 6添加请求");
        }

        protected override BitmapImage GetCommandImage(string actionParameter, PluginImageSize imageSize)
        {
            using (var bitmap = new BitmapBuilder(imageSize))
            {
                bitmap.Clear(BitmapColor.Black);
                bitmap.DrawText(
                    text: "Kontakt",
                    fontSize: 26,
                    color: BitmapColor.White
                );
                bitmap.DrawText(
                    text: "6",
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
