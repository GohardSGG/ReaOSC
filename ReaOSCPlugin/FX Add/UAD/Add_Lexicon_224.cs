namespace Loupedeck.ReaOSCPlugin.FX_Add

{

    using Loupedeck;
    using Loupedeck.ReaOSCPlugin;

    public class Add_Lexicon_224 : PluginDynamicCommand
    {
        public Add_Lexicon_224()
            : base(
                displayName: "Lexicon 224",
                description: "插入UAD Lexicon 224效果器",
                groupName: "UAD FX")
        {
            this.AddParameter("Add Lexicon 224", "Lexicon 224添加按钮", "FX Add");
        }

        protected override void RunCommand(string actionParameter)
        {
            ReaOSCPlugin.SendFXMessage("Add/UAD/Lexicon 224", 1);
            PluginLog.Info("已触发Lexicon 224添加请求");
        }

        protected override BitmapImage GetCommandImage(string actionParameter, PluginImageSize imageSize)
        {
            using (var bitmap = new BitmapBuilder(imageSize))
            {
                bitmap.Clear(BitmapColor.Black);
                bitmap.DrawText(
                    text: "224",
                    fontSize: 26,
                    color: BitmapColor.White
                );
                bitmap.DrawText(
                    text: "Lexicon",
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
