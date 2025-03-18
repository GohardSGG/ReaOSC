namespace Loupedeck.ReaOSCPlugin.FX_Add

{

    using Loupedeck;
    using Loupedeck.ReaOSCPlugin;

    public class Add_Tube_KC_1 : PluginDynamicCommand
    {
        public Add_Tube_KC_1()
            : base(
                displayName: "Tube KC-1",
                description: "插入Kiive Audio Tube KC-1效果器",
                groupName: "Kiive Audio FX")
        {
            this.AddParameter("Add Tube KC-1", "Tube KC-1添加按钮", "FX Add");
        }

        protected override void RunCommand(string actionParameter)
        {
            ReaOSCPlugin.SendFXMessage("Add/Kiive Audio/Tube KC-1", 1);
            PluginLog.Info("已触发Tube KC-1添加请求");
        }

        protected override BitmapImage GetCommandImage(string actionParameter, PluginImageSize imageSize)
        {
            using (var bitmap = new BitmapBuilder(imageSize))
            {
                bitmap.Clear(BitmapColor.Black);
                bitmap.DrawText(
                    text: "KC-1",
                    fontSize: 26,
                    color: BitmapColor.White
                );
                bitmap.DrawText(
                    text: "Tube",
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
