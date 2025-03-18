namespace Loupedeck.ReaOSCPlugin.FX_Add

{

    using Loupedeck;
    using Loupedeck.ReaOSCPlugin;

    public class Add_Filkchannel_MK2 : PluginDynamicCommand
    {
        public Add_Filkchannel_MK2()
            : base(
                displayName: "Filkchannel MK2",
                description: "插入Kiive Audio Filkchannel MK2效果器",
                groupName: "Kiive Audio FX")
        {
            this.AddParameter("Add Filkchannel MK2", "Filkchannel MK2添加按钮", "FX Add");
        }

        protected override void RunCommand(string actionParameter)
        {
            ReaOSCPlugin.SendFXMessage("Add/Kiive Audio/Filkchannel MK2", 1);
            PluginLog.Info("已触发Filkchannel MK2添加请求");
        }

        protected override BitmapImage GetCommandImage(string actionParameter, PluginImageSize imageSize)
        {
            using (var bitmap = new BitmapBuilder(imageSize))
            {
                bitmap.Clear(BitmapColor.Black);
                bitmap.DrawText(
                    text: "Filkchannel",
                    fontSize: 26,
                    color: BitmapColor.White
                );
                bitmap.DrawText(
                    text: "MK2",
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
