namespace Loupedeck.ReaOSCPlugin.Automation
{
    using Loupedeck;

    using System;
    using System.Threading;
    using System.Threading.Tasks;

    public class Note_Move_Dial : PluginDynamicAdjustment
    {
        public const string FullName = "Note Move Dial";
        public const string ChineseName = "音符移动";
        public const string TypeName = "MIDI";

        private enum DialState { Shift, Octave }
        private DialState _currentState = DialState.Shift;

        public Note_Move_Dial()
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

            var prefix = "MIDI/Note";

            if (_currentState == DialState.Shift)
            {
                if (ticks > 0)
                    ReaOSCPlugin.SendGeneralMessage($"{prefix}/Shift Up", 1);
                else
                    ReaOSCPlugin.SendGeneralMessage($"{prefix}/Shift Down", 1);
            }
            else
            {
                if (ticks > 0)
                    ReaOSCPlugin.SendGeneralMessage($"{prefix}/Octave Up", 1);
                else
                    ReaOSCPlugin.SendGeneralMessage($"{prefix}/Octave Down", 1);
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
