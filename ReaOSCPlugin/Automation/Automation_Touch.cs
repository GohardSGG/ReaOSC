namespace Loupedeck.ReaOSCPlugin.Automation
{

    using Loupedeck;
    using Loupedeck.ReaOSCPlugin.Automation;

    public class Automation_Touch : PluginDynamicCommand
    {
        public static string fullName = "Automation Touch";
        public static string chineseName = "自动化模式为Touch";
        public static string typeName = "Automation";
        public Automation_Touch()
            : base(
                displayName: fullName,
                description: chineseName,
                groupName: typeName)
        {
            this.AddParameter(fullName, chineseName + "按钮", typeName);
        }

        protected override void RunCommand(string actionParameter)
        {
            ReaOSCPlugin.SendGeneralMessage("Automation/Touch", 1);
            PluginLog.Info("已触发" + chineseName + "请求");
        }

        protected override BitmapImage GetCommandImage(string actionParameter, PluginImageSize imageSize)
        {
            using (var bitmap = new BitmapBuilder(imageSize))
            {
                bitmap.Clear(BitmapColor.Black);
                bitmap.DrawText(
                    text: "Touch",
                    fontSize: 26,
                    color: new BitmapColor(241, 207, 67)
                );
                bitmap.DrawText(
                    text: "",
                    x:50,
                    y:55,
                    width:14,
                    height:14,
                    fontSize: 14,
                    color: new BitmapColor(146, 121, 24)
                );
                return bitmap.ToImage();
            }
        }
    }
}
