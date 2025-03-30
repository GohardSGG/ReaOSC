using Loupedeck;
using Loupedeck.ReaOSCPlugin.Render;

namespace Loupedeck.ReaOSCPlugin.Render
{
    public class Item_Button : PluginDynamicCommand
    {
        public Item_Button()
            : base(displayName: "Item Button", description: "Set Source=Item", groupName: "Render")
        {
            RenderStateManager.StateChanged += () => this.ActionImageChanged();
            this.AddParameter("Item Button", "Item", "Render");
        }

        protected override void RunCommand(string actionParameter)
        {
            RenderStateManager.SetSource(RenderStateManager.SourceType.Item);
        }

        protected override BitmapImage GetCommandImage(string actionParameter, PluginImageSize imageSize)
        {
            var isActive = (RenderStateManager.Source == RenderStateManager.SourceType.Item);

            using (var bitmap = new BitmapBuilder(imageSize))
            {
                if (isActive)
                {
                    bitmap.Clear(BitmapColor.White);
                    bitmap.DrawText("Item", fontSize: 23, color: BitmapColor.Black);
                }
                else
                {
                    bitmap.Clear(BitmapColor.Black);
                    bitmap.DrawText("Item", fontSize: 23, color: BitmapColor.White);
                }
                return bitmap.ToImage();
            }
        }
    }
}
