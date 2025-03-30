namespace Loupedeck.ReaOSCPlugin.MIDI
{
    using Loupedeck;

    using System;
    using System.Threading;
    using System.Threading.Tasks;

    public class Note_Shift_Dial : PluginDynamicAdjustment
    {
        public const string FullName = "Note Shift Dial";
        public const string ChineseName = "音符移调";
        public const string TypeName = "MIDI";

        private enum DialState { Shift, Octave }
        private DialState _currentState = DialState.Shift;

        public Note_Shift_Dial()
            : base(displayName: FullName,
                  description: ChineseName,
                  groupName: TypeName,
                  hasReset: true)
        {
            this.AddParameter(FullName, ChineseName + "按钮", TypeName);
        }

        protected override void RunCommand(string actionParameter)
        {
            _currentState = _currentState == DialState.Shift ? DialState.Octave : DialState.Shift;
            AdjustmentValueChanged(actionParameter);
        }

        protected override void ApplyAdjustment(string actionParameter, int ticks)
        {
            if (ticks == 0)
                return;

            var prefix = "/MIDI/Note";

            if (_currentState == DialState.Shift)
            {
                if (ticks > 0)
                    ReaOSCPlugin.SendOSCMessage($"{prefix}/Shift/Up", 1f);
                else
                    ReaOSCPlugin.SendOSCMessage($"{prefix}/Shift/Down", 1f);
            }
            else
            {
                if (ticks > 0)
                    ReaOSCPlugin.SendOSCMessage($"{prefix}/Octave/Up", 1f);
                else
                    ReaOSCPlugin.SendOSCMessage($"{prefix}/Octave/Down", 1f);
            }

            AdjustmentValueChanged(actionParameter);
        }

        protected override BitmapImage GetCommandImage(string actionParameter, PluginImageSize imageSize)
        {
            using (var bitmap = new BitmapBuilder(imageSize))
            {
                var bgColor = _currentState == DialState.Shift ? BitmapColor.Black : BitmapColor.White;
                var fgColor = _currentState == DialState.Shift ? BitmapColor.White : BitmapColor.Black;

                bitmap.Clear(bgColor);

                bitmap.DrawText(
                    text: _currentState.ToString(),
                    fontSize: 18,
                    color: fgColor
                );

                return bitmap.ToImage();
            }
        }

    }
}
