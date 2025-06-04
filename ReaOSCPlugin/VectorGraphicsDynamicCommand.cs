namespace Loupedeck.ReaOSCPlugin
{
    using System;

    internal class VectorGraphicsDynamicCommand : PluginDynamicCommand
    {
        public VectorGraphicsDynamicCommand()
            : base("Vector graphics", "Command that has an SVG image", "Test Group")
        {
        }

        protected override BitmapImage GetCommandImage(String actionParameter, PluginImageSize imageSize)
            => BitmapImage.FromResource(this.Plugin.Assembly, "Loupedeck.ReaOSCPlugin.metadata.Icon.Play.svg");
    }
}