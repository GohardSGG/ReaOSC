namespace Loupedeck.ReaOSCPlugin.General.Render
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    using Loupedeck;

    public class MIDI_Button : PluginDynamicCommand
    {
        public MIDI_Button()
            : base(displayName: "MIDI Button", description: "Toggle MIDI", groupName: "Render")
        {
            RenderStateManager.StateChanged += () => this.ActionImageChanged();
            this.AddParameter("MIDI Button", "MIDI", "Render");
        }

        protected override void RunCommand(string actionParameter)
        {
            RenderStateManager.ToggleMidi();
        }

        protected override BitmapImage GetCommandImage(string actionParameter, PluginImageSize imageSize)
        {
            var isActive = RenderStateManager.MidiActive;

            using (var bitmap = new BitmapBuilder(imageSize))
            {
                if (isActive)
                {
                    bitmap.Clear(BitmapColor.White);
                    bitmap.DrawText("MIDI", fontSize: 23, color: BitmapColor.Black);
                }
                else
                {
                    bitmap.Clear(BitmapColor.Black);
                    bitmap.DrawText("MIDI", fontSize: 23, color: BitmapColor.White);
                }
                return bitmap.ToImage();
            }
        }
    }
}
