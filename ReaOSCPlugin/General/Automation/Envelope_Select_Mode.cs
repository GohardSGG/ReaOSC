// Envelope_Select_Mode.cs
namespace Loupedeck.ReaOSCPlugin.General.Automation
{
    using Loupedeck;

    using System;

    public class Envelope_Select_Mode : PluginDynamicCommand
    {
        public const string FullName = "Envelope Select Mode";
        public const string ChineseName = "包络模式切换";
        private const string TypeName = "Envelope";

        public enum EnvelopeMode { Track, Take }
        private static EnvelopeMode _currentMode = EnvelopeMode.Track;

        public static event EventHandler ModeChanged;

        public Envelope_Select_Mode()
            : base(displayName: FullName,
                  description: ChineseName,
                  groupName: TypeName)
        {
            this.AddParameter("mode", "Envelope Mode", "Mode");
        }

        protected override void RunCommand(string actionParameter)
        {
            _currentMode = _currentMode == EnvelopeMode.Track ?
                EnvelopeMode.Take :
                EnvelopeMode.Track;

            ModeChanged?.Invoke(this, EventArgs.Empty);
            ActionImageChanged();
        }

        protected override BitmapImage GetCommandImage(string actionParameter, PluginImageSize imageSize)
        {
            using (var bitmap = new BitmapBuilder(imageSize))
            {
                bitmap.Clear(_currentMode == EnvelopeMode.Track ?
                    BitmapColor.Black :
                    BitmapColor.White);

                bitmap.DrawText(
                    text: _currentMode.ToString(),
                    fontSize: 26,
                    color: _currentMode == EnvelopeMode.Track ?
                        BitmapColor.White :
                        BitmapColor.Black
                );

                bitmap.DrawText(
                    x: 50,
                    y: 55,
                    width: 14,
                    height: 14,
                    text: "Mode",
                    fontSize: 14,
                    color: _currentMode == EnvelopeMode.Track ?
                        BitmapColor.White :
                        BitmapColor.Black
                );

                return bitmap.ToImage();
            }
        }

        public static string GetModePrefix() =>
            _currentMode == EnvelopeMode.Track ? "Track" : "Take";
    }
}