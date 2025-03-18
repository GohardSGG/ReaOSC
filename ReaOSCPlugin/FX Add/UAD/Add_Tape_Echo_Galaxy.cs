namespace Loupedeck.ReaOSCPlugin.FX_Add

{

    using Loupedeck;
    using Loupedeck.ReaOSCPlugin;

    public class Add_Tape_Echo_Galaxy : PluginDynamicCommand
    {
        public Add_Tape_Echo_Galaxy()
            : base(
                displayName: "Tape Echo Galaxy",
                description: "插入UAD Tape Echo Galaxy效果器",
                groupName: "UAD FX")
        {
            this.AddParameter("Add Tape Echo Galaxy", "Tape Echo Galaxy添加按钮", "FX Add");
        }

        protected override void RunCommand(string actionParameter)
        {
            ReaOSCPlugin.SendFXMessage("Add/UAD/Tape Echo Galaxy", 1);
            PluginLog.Info("已触发Tape Echo Galaxy添加请求");
        }

        protected override BitmapImage GetCommandImage(string actionParameter, PluginImageSize imageSize)
        {
            using (var bitmap = new BitmapBuilder(imageSize))
            {
                bitmap.Clear(BitmapColor.Black);
                bitmap.DrawText(
                    text: "Tape Echo",
                    fontSize: 26,
                    color: BitmapColor.White
                );
                bitmap.DrawText(
                    text: "Galaxy",
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
