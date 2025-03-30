namespace Loupedeck.ReaOSCPlugin.Render
{
    using System;

    using Loupedeck;
    // 整合的全局状态管理器
    public static class RenderStateManager
    {
        public enum SourceType { Master, Track, Item }
        public static SourceType Source { get; private set; } = SourceType.Master;

        public enum AvModeType { Audio, Video }
        public static AvModeType AvMode { get; private set; } = AvModeType.Audio;

        public enum FormatType { Format1, Format2, Format3 }
        public static FormatType Format { get; private set; } = FormatType.Format1;

        public static bool MidiActive { get; private set; } = false;

        public enum SampleRateType { SR48k, SR96k, SR192k }
        public static SampleRateType SampleRate { get; private set; } = SampleRateType.SR48k;

        public enum BitDepthType { B16I, B24I, B32F, B32I }
        public static BitDepthType BitDepth { get; private set; } = BitDepthType.B16I;

        public static bool TimecodeActive { get; private set; } = false;

        public static event Action StateChanged;

        public static void SetSource(SourceType newSource)
        {
            if (Source != newSource)
            {
                Source = newSource;
                OnStateChanged();
            }
        }

        public static void ToggleAvMode()
        {
            AvMode = AvMode == AvModeType.Audio ? AvModeType.Video : AvModeType.Audio;
            OnStateChanged();
        }

        public static void SetFormat(FormatType newFormat)
        {
            if (Format != newFormat)
            {
                Format = newFormat;
                OnStateChanged();
            }
        }

        public static void ToggleMidi()
        {
            MidiActive = !MidiActive;
            OnStateChanged();
        }

        public static void SetSampleRate(SampleRateType sr)
        {
            if (SampleRate != sr)
            {
                SampleRate = sr;
                OnStateChanged();
            }
        }

        public static void SetBitDepth(BitDepthType bd)
        {
            if (BitDepth != bd)
            {
                BitDepth = bd;
                OnStateChanged();
            }
        }

        public static void SetTimecode(bool active)
        {
            if (TimecodeActive != active)
            {
                TimecodeActive = active;
                OnStateChanged();
            }
        }

        private static void OnStateChanged() => StateChanged?.Invoke();
    }

    // Render_Export_Button类
    public class Render_Export_Button : PluginDynamicCommand
    {
        public Render_Export_Button()
            : base("Export", "发送渲染OSC地址", "Render")
        {
            this.AddParameter("Export_Button", "Export", "Render");
        }

        protected override void RunCommand(string actionParameter)
        {
            var oscAddress = BuildExportAddress();
            ReaOSCPlugin.SendOSCMessage(oscAddress, 1f);
        }

        private string BuildExportAddress()
        {
            if (RenderStateManager.MidiActive)
            {
                return "/Render/Source/Range/MIDI";
            }

            var src = RenderStateManager.Source.ToString();
            var srText = RenderStateManager.SampleRate switch
            {
                RenderStateManager.SampleRateType.SR48k => "48K",
                RenderStateManager.SampleRateType.SR96k => "96K",
                RenderStateManager.SampleRateType.SR192k => "192K",
                _ => "UnknownSR"
            };

            var bdText = RenderStateManager.BitDepth switch
            {
                RenderStateManager.BitDepthType.B16I => "16I",
                RenderStateManager.BitDepthType.B24I => "24I",
                RenderStateManager.BitDepthType.B32F => "32F",
                RenderStateManager.BitDepthType.B32I => "32I",
                _ => "UnknownBD"
            };

            var isAudio = RenderStateManager.AvMode == RenderStateManager.AvModeType.Audio;
            var formatText = RenderStateManager.Format switch
            {
                RenderStateManager.FormatType.Format1 => isAudio ? "WAV" : "MP4",
                RenderStateManager.FormatType.Format2 => isAudio ? "MP3" : "GIF",
                RenderStateManager.FormatType.Format3 => isAudio ? "ADM" : "AAC",
                _ => "UnknownFmt"
            };

            var tcText = RenderStateManager.TimecodeActive ? "Timecode" : "NoTimecode";

            return $"/Render/{src}/Range/{srText}/{bdText}/{formatText}/{tcText}";
        }

        protected override BitmapImage GetCommandImage(string actionParameter, PluginImageSize imageSize)
        {
            using (var bitmap = new BitmapBuilder(imageSize))
            {
                bitmap.Clear(BitmapColor.Black);
                bitmap.DrawText("Export", fontSize: 23, color: BitmapColor.White);
                return bitmap.ToImage();
            }
        }
    }
}
