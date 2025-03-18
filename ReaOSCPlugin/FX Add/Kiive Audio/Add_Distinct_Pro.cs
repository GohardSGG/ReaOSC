namespace Loupedeck.ReaOSCPlugin.FX_Add

{

    using Loupedeck;
    using Loupedeck.ReaOSCPlugin;

    public class Add_Distinct_Pro : PluginDynamicCommand
    {
        public Add_Distinct_Pro()
            : base(
                displayName: "Distinct Pro",
                description: "插入Kiive Audio Distinct Pro效果器",
                groupName: "Kiive Audio FX")
        {
            this.AddParameter("Add Distinct Pro", "Distinct Pro添加按钮", "FX Add");
        }

        protected override void RunCommand(string actionParameter)
        {
            ReaOSCPlugin.SendFXMessage("Add/Kiive Audio/Distinct Pro", 1);
            PluginLog.Info("已触发Distinct Pro添加请求");
        }

        protected override BitmapImage GetCommandImage(string actionParameter, PluginImageSize imageSize)
        {
            using (var bitmap = new BitmapBuilder(imageSize))
            {
                bitmap.Clear(BitmapColor.Black);
                bitmap.DrawText(
                    text: "Distinct Pro",
                    fontSize: 26,
                    color: BitmapColor.White
                );
                bitmap.DrawText(
                    text: "",
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
