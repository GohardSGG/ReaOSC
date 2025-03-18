namespace Loupedeck.ReaOSCPlugin.FX_Add

{

    using Loupedeck;
    using Loupedeck.ReaOSCPlugin;

    public class Add_1176LN_Rev_E : PluginDynamicCommand
    {
        public Add_1176LN_Rev_E()
            : base(
                displayName: "1176LN Rev E",
                description: "插入UAD 1176LN Rev E效果器",
                groupName: "UAD FX")
        {
            this.AddParameter("Add 1176LN Rev E", "1176LN Rev E添加按钮", "FX Add");
        }

        protected override void RunCommand(string actionParameter)
        {
            ReaOSCPlugin.SendFXMessage("Add/UAD/1176LN Rev E", 1);
            PluginLog.Info("已触发1176LN Rev E添加请求");
        }

        protected override BitmapImage GetCommandImage(string actionParameter, PluginImageSize imageSize)
        {
            using (var bitmap = new BitmapBuilder(imageSize))
            {
                bitmap.Clear(BitmapColor.Black);
                bitmap.DrawText(
                    text: "1176LN",
                    fontSize: 26,
                    color: BitmapColor.White
                );
                bitmap.DrawText(
                    text: "Rev.E",
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
