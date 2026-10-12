using System;
using System.Runtime.InteropServices;

namespace SuperDev;

/// <summary>
/// Plays a short chime when one check is completed, and a longer
/// rising phrase when the whole checklist is completed.
/// </summary>
internal static class SuccessSound
{
    private const int SampleRate = 22050;

    private static readonly byte[] CheckWav = Render(
        0.30,
        new Tone(988, 0, 0.09, 0.22),
        new Tone(1318.5, 0.07, 0.20, 0.28));

    private static readonly byte[] CompleteWav = Render(
        0.85,
        new Tone(523.25, 0.00, 0.28, 0.20),
        new Tone(659.25, 0.12, 0.30, 0.22),
        new Tone(783.99, 0.24, 0.32, 0.24),
        new Tone(1046.50, 0.38, 0.42, 0.28),
        new Tone(1567.98, 0.48, 0.30, 0.07));

    private static readonly GCHandle CheckPin = GCHandle.Alloc(CheckWav, GCHandleType.Pinned);
    private static readonly GCHandle CompletePin = GCHandle.Alloc(CompleteWav, GCHandleType.Pinned);
    private static nint _macSound;

    public static void PlayCheck() => Play(CheckWav, CheckPin);

    public static void PlayComplete() => Play(CompleteWav, CompletePin);

    private static void Play(byte[] wav, GCHandle pin)
    {
        try
        {
            if (OperatingSystem.IsMacOS())
                PlayMac(wav);
            else if (OperatingSystem.IsWindows())
                PlayWindows(pin.AddrOfPinnedObject());
        }
        catch (Exception)
        {
            // A missing audio device should not block completing a check.
        }
    }

    private readonly record struct Tone(double Frequency, double Start, double Duration, double Gain);

    private static byte[] Render(double seconds, params Tone[] tones)
    {
        var count = Math.Max(1, (int)(seconds * SampleRate));
        var samples = new short[count];

        foreach (var tone in tones)
        {
            var start = (int)(tone.Start * SampleRate);
            var length = Math.Max(1, (int)(tone.Duration * SampleRate));
            var attack = Math.Max(1, (int)(0.012 * SampleRate));

            for (var i = 0; i < length; i++)
            {
                var index = start + i;
                if ((uint)index >= (uint)count)
                    break;

                var progress = i / (double)length;
                var rise = Math.Min(1, i / (double)attack);
                var envelope = rise * Math.Pow(1 - progress, 1.7);
                var wave = Math.Sin(2 * Math.PI * tone.Frequency * (i / (double)SampleRate));
                var mixed = samples[index] + (int)(wave * envelope * tone.Gain * short.MaxValue);
                samples[index] = (short)Math.Clamp(mixed, short.MinValue, short.MaxValue);
            }
        }

        var dataBytes = count * 2;
        var wav = new byte[44 + dataBytes];
        WriteText(wav, 0, "RIFF");
        WriteInt(wav, 4, 36 + dataBytes);
        WriteText(wav, 8, "WAVE");
        WriteText(wav, 12, "fmt ");
        WriteInt(wav, 16, 16);
        WriteShort(wav, 20, 1);
        WriteShort(wav, 22, 1);
        WriteInt(wav, 24, SampleRate);
        WriteInt(wav, 28, SampleRate * 2);
        WriteShort(wav, 32, 2);
        WriteShort(wav, 34, 16);
        WriteText(wav, 36, "data");
        WriteInt(wav, 40, dataBytes);

        for (var i = 0; i < count; i++)
            WriteShort(wav, 44 + i * 2, samples[i]);

        return wav;
    }

    private static void WriteText(byte[] buffer, int offset, string text)
    {
        for (var i = 0; i < text.Length; i++)
            buffer[offset + i] = (byte)text[i];
    }

    private static void WriteInt(byte[] buffer, int offset, int value)
    {
        buffer[offset] = (byte)value;
        buffer[offset + 1] = (byte)(value >> 8);
        buffer[offset + 2] = (byte)(value >> 16);
        buffer[offset + 3] = (byte)(value >> 24);
    }

    private static void WriteShort(byte[] buffer, int offset, short value)
    {
        buffer[offset] = (byte)value;
        buffer[offset + 1] = (byte)(value >> 8);
    }

    private static void PlayMac(byte[] wav)
    {
        var dataClass = objc_getClass("NSData");
        var soundClass = objc_getClass("NSSound");
        if (dataClass == 0 || soundClass == 0)
            return;

        var pinned = GCHandle.Alloc(wav, GCHandleType.Pinned);
        try
        {
            var data = Send(
                dataClass,
                sel_registerName("dataWithBytes:length:"),
                pinned.AddrOfPinnedObject(),
                wav.Length);
            if (data == 0)
                return;

            var sound = Send(soundClass, sel_registerName("alloc"));
            sound = Send(sound, sel_registerName("initWithData:"), data);
            if (sound == 0)
                return;

            if (_macSound != 0)
            {
                Send(_macSound, sel_registerName("stop"));
                Send(_macSound, sel_registerName("release"));
            }

            _macSound = sound;
            Send(sound, sel_registerName("play"));
        }
        finally
        {
            pinned.Free();
        }
    }

    private static void PlayWindows(nint wav)
    {
        const uint asyncMemory = 0x0001 | 0x0004 | 0x0002;
        PlaySound(wav, 0, asyncMemory);
    }

    private const string ObjC = "/usr/lib/libobjc.A.dylib";

    [DllImport(ObjC)]
    private static extern nint objc_getClass(string name);

    [DllImport(ObjC)]
    private static extern nint sel_registerName(string name);

    [DllImport(ObjC, EntryPoint = "objc_msgSend")]
    private static extern nint Send(nint receiver, nint selector);

    [DllImport(ObjC, EntryPoint = "objc_msgSend")]
    private static extern nint Send(nint receiver, nint selector, nint arg);

    [DllImport(ObjC, EntryPoint = "objc_msgSend")]
    private static extern nint Send(nint receiver, nint selector, nint arg, nint arg2);

    [DllImport("winmm.dll", EntryPoint = "PlaySoundW", SetLastError = true)]
    private static extern bool PlaySound(nint sound, nint module, uint flags);
}
