using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;

namespace SunshineLibrary.Services
{
    internal interface IWindowsHdrController
    {
        HdrSwitchResult SetEnabled(bool enabled);
    }

    internal sealed class HdrSwitchResult
    {
        public bool Success { get; set; }
        public bool? PreviousState { get; set; }
        public string Error { get; set; }

        public static HdrSwitchResult Failed(string error) => new HdrSwitchResult
        {
            Success = false,
            Error = error,
        };
    }

    /// <summary>
    /// Controls advanced color on the Windows primary display using the DisplayConfig API.
    /// The implementation follows the same native API path used by Playnite's own HDR
    /// support, but supports both enabling and explicitly disabling HDR.
    /// </summary>
    internal sealed class WindowsHdrController : IWindowsHdrController
    {
        private const int ErrorSuccess = 0;
        private const int ErrorInsufficientBuffer = 122;

        public HdrSwitchResult SetEnabled(bool enabled)
        {
            try
            {
                DisplayTarget target;
                string error;
                if (!TryGetPrimaryTarget(out target, out error)) return HdrSwitchResult.Failed(error);

                AdvancedColorInfo current;
                var result = GetColorInfo(target, out current);
                if (result != ErrorSuccess) return HdrSwitchResult.Failed("Windows could not query HDR state (error " + result + ").");
                if (!current.AdvancedColorSupported) return HdrSwitchResult.Failed("The primary client display does not support HDR.");

                var previous = current.AdvancedColorEnabled;
                if (previous == enabled)
                {
                    return new HdrSwitchResult { Success = true, PreviousState = previous };
                }

                var set = new SetAdvancedColorState
                {
                    Header = DeviceInfoHeader.Create(DeviceInfoType.SetAdvancedColorState, target),
                    Value = enabled ? 1u : 0u,
                };
                result = DisplayConfigSetDeviceInfo(ref set.Header);
                if (result != ErrorSuccess) return HdrSwitchResult.Failed("Windows could not change HDR state (error " + result + ").");

                // DisplayConfig usually updates immediately, but a short bounded retry
                // avoids launching Moonlight during a transient state change.
                for (var attempt = 0; attempt < 10; attempt++)
                {
                    Thread.Sleep(50);
                    result = GetColorInfo(target, out current);
                    if (result == ErrorSuccess && current.AdvancedColorEnabled == enabled)
                    {
                        return new HdrSwitchResult { Success = true, PreviousState = previous };
                    }
                }

                return HdrSwitchResult.Failed("Windows accepted the HDR change but the primary display did not reach the requested state.");
            }
            catch (Exception ex) when (ex is DllNotFoundException || ex is EntryPointNotFoundException || ex is SEHException)
            {
                return HdrSwitchResult.Failed("Windows HDR control is unavailable: " + ex.Message);
            }
        }

        private static int GetColorInfo(DisplayTarget target, out AdvancedColorInfo info)
        {
            info = new AdvancedColorInfo
            {
                Header = DeviceInfoHeader.Create(DeviceInfoType.GetAdvancedColorInfo, target),
            };
            return DisplayConfigGetDeviceInfo(ref info);
        }

        private static bool TryGetPrimaryTarget(out DisplayTarget target, out string error)
        {
            target = default(DisplayTarget);
            error = null;

            for (var attempt = 0; attempt < 2; attempt++)
            {
                uint pathCount;
                uint modeCount;
                var result = GetDisplayConfigBufferSizes(QueryFlags.OnlyActivePaths, out pathCount, out modeCount);
                if (result != ErrorSuccess)
                {
                    error = "Windows could not size the active display configuration (error " + result + ").";
                    return false;
                }

                var paths = new DisplayPathInfo[pathCount];
                var modes = new DisplayModeInfo[modeCount];
                result = QueryDisplayConfig(QueryFlags.OnlyActivePaths, ref pathCount, paths, ref modeCount, modes, IntPtr.Zero);
                if (result == ErrorInsufficientBuffer) continue;
                if (result != ErrorSuccess)
                {
                    error = "Windows could not query the active display configuration (error " + result + ").";
                    return false;
                }

                var primarySources = modes
                    .Where(mode => mode.InfoType == ModeInfoType.Source &&
                                   mode.ModeInfo.SourceMode.Position.X == 0 &&
                                   mode.ModeInfo.SourceMode.Position.Y == 0)
                    .ToList();

                foreach (var source in primarySources)
                {
                    foreach (var path in paths)
                    {
                        if (path.SourceInfo.Id == source.Id && path.SourceInfo.AdapterId.Equals(source.AdapterId))
                        {
                            target = new DisplayTarget { AdapterId = path.TargetInfo.AdapterId, Id = path.TargetInfo.Id };
                            return true;
                        }
                    }
                }

                error = "Windows did not report a primary display target.";
                return false;
            }

            error = "The active display configuration changed while it was being queried.";
            return false;
        }

        [DllImport("user32.dll")]
        private static extern int GetDisplayConfigBufferSizes(QueryFlags flags, out uint pathCount, out uint modeCount);

        [DllImport("user32.dll")]
        private static extern int QueryDisplayConfig(
            QueryFlags flags,
            ref uint pathCount,
            [Out] DisplayPathInfo[] paths,
            ref uint modeCount,
            [Out] DisplayModeInfo[] modes,
            IntPtr topologyId);

        [DllImport("user32.dll")]
        private static extern int DisplayConfigGetDeviceInfo(ref AdvancedColorInfo packet);

        [DllImport("user32.dll")]
        private static extern int DisplayConfigSetDeviceInfo(ref DeviceInfoHeader packet);

        [StructLayout(LayoutKind.Sequential)]
        private struct Luid : IEquatable<Luid>
        {
            public int LowPart;
            public int HighPart;
            public bool Equals(Luid other) => LowPart == other.LowPart && HighPart == other.HighPart;
        }

        private struct DisplayTarget
        {
            public Luid AdapterId;
            public uint Id;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct PointL { public int X; public int Y; }

        [StructLayout(LayoutKind.Sequential)]
        private struct Rational { public uint Numerator; public uint Denominator; }

        [StructLayout(LayoutKind.Sequential)]
        private struct Region2D { public uint Cx; public uint Cy; }

        [StructLayout(LayoutKind.Sequential)]
        private struct VideoSignalInfo
        {
            public ulong PixelRate;
            public Rational HSyncFreq;
            public Rational VSyncFreq;
            public Region2D ActiveSize;
            public Region2D TotalSize;
            public uint VideoStandard;
            public ScanlineOrdering ScanlineOrdering;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct TargetMode { public VideoSignalInfo TargetVideoSignalInfo; }

        [StructLayout(LayoutKind.Sequential)]
        private struct SourceMode
        {
            public uint Width;
            public uint Height;
            public PixelFormat PixelFormat;
            public PointL Position;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct ModeInfoUnion
        {
            [FieldOffset(0)] public TargetMode TargetMode;
            [FieldOffset(0)] public SourceMode SourceMode;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DisplayModeInfo
        {
            public ModeInfoType InfoType;
            public uint Id;
            public Luid AdapterId;
            public ModeInfoUnion ModeInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct PathSourceInfo
        {
            public Luid AdapterId;
            public uint Id;
            public uint ModeInfoIdx;
            public uint StatusFlags;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct PathTargetInfo
        {
            public Luid AdapterId;
            public uint Id;
            public uint ModeInfoIdx;
            public VideoOutputTechnology OutputTechnology;
            public Rotation Rotation;
            public Scaling Scaling;
            public Rational RefreshRate;
            public ScanlineOrdering ScanLineOrdering;
            [MarshalAs(UnmanagedType.Bool)] public bool TargetAvailable;
            public uint StatusFlags;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DisplayPathInfo
        {
            public PathSourceInfo SourceInfo;
            public PathTargetInfo TargetInfo;
            public uint Flags;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DeviceInfoHeader
        {
            public DeviceInfoType Type;
            public uint Size;
            public Luid AdapterId;
            public uint Id;

            public static DeviceInfoHeader Create(DeviceInfoType type, DisplayTarget target) => new DeviceInfoHeader
            {
                Type = type,
                Size = type == DeviceInfoType.GetAdvancedColorInfo
                    ? (uint)Marshal.SizeOf(typeof(AdvancedColorInfo))
                    : (uint)Marshal.SizeOf(typeof(SetAdvancedColorState)),
                AdapterId = target.AdapterId,
                Id = target.Id,
            };
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct AdvancedColorInfo
        {
            public DeviceInfoHeader Header;
            public uint Value;
            public ColorEncoding ColorEncoding;
            public uint BitsPerColorChannel;
            public bool AdvancedColorSupported => (Value & 0x1) != 0;
            public bool AdvancedColorEnabled => (Value & 0x2) != 0;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct SetAdvancedColorState
        {
            public DeviceInfoHeader Header;
            public uint Value;
        }

        private enum QueryFlags : uint { OnlyActivePaths = 0x2 }
        private enum ModeInfoType : uint { Source = 1, Target = 2 }
        private enum DeviceInfoType : uint { GetAdvancedColorInfo = 9, SetAdvancedColorState = 10 }
        private enum PixelFormat : uint { EightBpp = 1, SixteenBpp = 2, TwentyFourBpp = 3, ThirtyTwoBpp = 4, NonGdi = 5 }
        private enum ColorEncoding : uint { Rgb = 0, YCbCr444 = 1, YCbCr422 = 2, YCbCr420 = 3, Intensity = 4 }
        private enum VideoOutputTechnology : uint { Other = 0xffffffff }
        private enum Rotation : uint { Identity = 1 }
        private enum Scaling : uint { Identity = 1 }
        private enum ScanlineOrdering : uint { Unspecified = 0 }
    }
}
