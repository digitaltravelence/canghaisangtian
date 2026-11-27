using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace MountainTide
{
    internal enum FarmSound { Till, Sow, Water, Pick, Reap, Fertilize }

    // One output stream mixes the score and all current gestures. Windows only
    // sees short, reusable PCM buffers, including when an effect overlaps itself.
    internal sealed class FarmAudio : IDisposable
    {
        const int SampleRate = 32000, Channels = 2, FramesPerBuffer = 512;
        const int MaxEffects = 12;
        readonly object gate = new object();
        readonly object saveGate = new object();
        readonly string preferencesPath;
        readonly short[] music;
        readonly short[][] effects;
        readonly List<Voice> voices = new List<Voice>();
        readonly int[] mixed = new int[FramesPerBuffer * Channels];
        readonly AutoResetEvent wake = new AutoResetEvent(false);
        readonly Thread worker;
        volatile bool disposed;
        bool musicPlaying;
        int musicPosition;
        AudioPreferences preferences, savedPreferences;
        double musicGain, effectsGain;

        sealed class Voice
        {
            internal readonly short[] Samples;
            internal int Position;
            internal Voice(short[] samples) { Samples = samples; }
        }

        public FarmAudio() : this(AudioPreferences.DefaultPath) { }

        public FarmAudio(string preferencesPath)
        {
            this.preferencesPath = preferencesPath;
            preferences = savedPreferences = AudioPreferences.Load(preferencesPath);
            musicGain = preferences.MusicEnabled ? preferences.MusicVolume / 100.0 : 0;
            effectsGain = preferences.EffectsEnabled ? preferences.EffectsVolume / 100.0 : 0;
            music = ReadPcm("pasture");
            string[] names = { "till", "sow", "water", "pick", "reap", "fertilize" };
            effects = new short[names.Length][];
            for (int i = 0; i < names.Length; i++) effects[i] = ReadPcm(names[i]);
            worker = new Thread(PlayBuffers);
            worker.IsBackground = true;
            worker.Name = "Farm audio";
            worker.Start();
        }

        public int MusicVolume
        {
            get { lock (gate) return preferences.MusicVolume; }
            set { lock (gate) preferences.MusicVolume = AudioPreferences.ClampVolume(value); }
        }

        public int EffectsVolume
        {
            get { lock (gate) return preferences.EffectsVolume; }
            set { lock (gate) preferences.EffectsVolume = AudioPreferences.ClampVolume(value); }
        }

        public bool MusicEnabled
        {
            get { lock (gate) return preferences.MusicEnabled; }
            set { lock (gate) preferences.MusicEnabled = value; }
        }

        public bool EffectsEnabled
        {
            get { lock (gate) return preferences.EffectsEnabled; }
            set { lock (gate) preferences.EffectsEnabled = value; }
        }

        public bool SavePreferences()
        {
            // File I/O stays outside the audio lock even when sliders are moving.
            lock (saveGate)
            {
                AudioPreferences snapshot;
                lock (gate) snapshot = preferences;
                if (snapshot.Equals(savedPreferences)) return true;
                if (!snapshot.Save(preferencesPath)) return false;
                savedPreferences = snapshot;
                return true;
            }
        }

        public void StartMusic() { lock (gate) { musicPlaying = true; } }
        public void PauseMusic() { lock (gate) { musicPlaying = false; } }

        public void Play(FarmSound sound)
        {
            lock (gate)
            {
                if (disposed || !preferences.EffectsEnabled || preferences.EffectsVolume == 0) return;
                if (voices.Count == MaxEffects) voices.RemoveAt(0);
                voices.Add(new Voice(effects[(int)sound]));
            }
        }

        static short[] ReadPcm(string name)
        {
            string path = "audio." + name + ".wav";
            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(path))
            {
                if (stream == null) throw new FileNotFoundException("Missing embedded audio", path);
                using (BinaryReader reader = new BinaryReader(stream, Encoding.ASCII))
                {
                    if (reader.ReadUInt32() != 0x46464952) throw new InvalidDataException(path);
                    reader.ReadUInt32();
                    if (reader.ReadUInt32() != 0x45564157) throw new InvalidDataException(path);
                    bool validFormat = false;
                    while (stream.Position + 8 <= stream.Length)
                    {
                        uint id = reader.ReadUInt32(), size = reader.ReadUInt32();
                        long end = stream.Position + size;
                        if (end > stream.Length) throw new InvalidDataException(path);
                        if (id == 0x20746D66 && size >= 16) // fmt
                        {
                            int format = reader.ReadUInt16(), channels = reader.ReadUInt16();
                            int rate = reader.ReadInt32();
                            reader.ReadUInt32();
                            int alignment = reader.ReadUInt16(), bits = reader.ReadUInt16();
                            validFormat = format == 1 && channels == Channels && rate == SampleRate
                                && alignment == Channels * sizeof(short) && bits == 16;
                        }
                        if (id == 0x61746164 && validFormat && size > 0 && size % 4 == 0) // data
                        {
                            byte[] bytes = reader.ReadBytes(checked((int)size));
                            short[] samples = new short[bytes.Length / sizeof(short)];
                            Buffer.BlockCopy(bytes, 0, samples, 0, bytes.Length);
                            return samples;
                        }
                        stream.Position = end + (size & 1);
                    }
                    throw new InvalidDataException(path);
                }
            }
        }

        void Mix(short[] output)
        {
            Array.Clear(mixed, 0, mixed.Length);
            lock (gate)
            {
                for (int n = voices.Count - 1; n >= 0; n--)
                {
                    Voice voice = voices[n];
                    int count = Math.Min(mixed.Length, voice.Samples.Length - voice.Position);
                    for (int i = 0; i < count; i++) mixed[i] += voice.Samples[voice.Position + i];
                    voice.Position += count;
                    if (voice.Position == voice.Samples.Length) voices.RemoveAt(n);
                }

                // Ramp both gains over one 16 ms buffer. Both stereo channels get
                // the same gain and the score keeps its position throughout mute.
                double targetMusic = preferences.MusicEnabled ? preferences.MusicVolume / 100.0 : 0;
                double targetEffects = preferences.EffectsEnabled ? preferences.EffectsVolume / 100.0 : 0;
                double musicStep = (targetMusic - musicGain) / FramesPerBuffer;
                double effectsStep = (targetEffects - effectsGain) / FramesPerBuffer;
                for (int frame = 0; frame < FramesPerBuffer; frame++)
                {
                    musicGain += musicStep;
                    effectsGain += effectsStep;
                    for (int channel = 0; channel < Channels; channel++)
                    {
                        int i = frame * Channels + channel;
                        double sample = mixed[i] * effectsGain;
                        if (musicPlaying) sample += music[musicPosition++] * musicGain;
                        int value = (int)Math.Round(sample), excess = Math.Abs(value) - 24000;
                        if (excess > 0) value = Math.Sign(value) * (24000 + (int)(8700L * excess / (8700 + excess)));
                        output[i] = (short)value;
                    }
                    if (musicPosition == music.Length) musicPosition = 0;
                }
                musicGain = targetMusic;
                effectsGain = targetEffects;
            }
        }

        void PlayBuffers()
        {
            IntPtr device = IntPtr.Zero;
            OutputBuffer[] buffers = new OutputBuffer[3];
            try
            {
                WaveFormat format = new WaveFormat {
                    FormatTag = 1, Channels = Channels, SamplesPerSec = SampleRate,
                    AvgBytesPerSec = SampleRate * Channels * 2, BlockAlign = Channels * 2, BitsPerSample = 16
                };
                int result = waveOutOpen(out device, uint.MaxValue, ref format,
                    wake.SafeWaitHandle.DangerousGetHandle(), IntPtr.Zero, 0x50000); // CALLBACK_EVENT
                if (result != 0) return; // The farm remains usable without an output device.
                for (int i = 0; i < buffers.Length; i++)
                    buffers[i] = new OutputBuffer(device, FramesPerBuffer * Channels);
                int next = 0;
                while (!disposed)
                {
                    OutputBuffer buffer = buffers[next];
                    if (!buffer.Ready) { wake.WaitOne(100); continue; }
                    Mix(buffer.Samples);
                    buffer.Write();
                    next = (next + 1) % buffers.Length;
                }
            }
            catch (InvalidOperationException error) { Debug.WriteLine(error.Message); }
            finally
            {
                if (device != IntPtr.Zero)
                {
                    waveOutReset(device);
                    foreach (OutputBuffer buffer in buffers) if (buffer != null) buffer.Dispose();
                    waveOutClose(device);
                }
            }
        }

        public void Dispose()
        {
            lock (gate)
            {
                if (disposed) return;
                disposed = true;
            }
            wake.Set();
            worker.Join();
            wake.Dispose();
            SavePreferences();
        }

        sealed class OutputBuffer : IDisposable
        {
            static readonly int HeaderSize = Marshal.SizeOf(typeof(WaveHeader));
            static readonly int FlagsOffset = Marshal.OffsetOf(typeof(WaveHeader), "Flags").ToInt32();
            readonly IntPtr device;
            readonly GCHandle pinned;
            readonly IntPtr header;
            internal readonly short[] Samples;
            bool queued;

            internal OutputBuffer(IntPtr output, int sampleCount)
            {
                device = output;
                Samples = new short[sampleCount];
                pinned = GCHandle.Alloc(Samples, GCHandleType.Pinned);
                header = Marshal.AllocHGlobal(HeaderSize);
                WaveHeader info = new WaveHeader { Data = pinned.AddrOfPinnedObject(), Length = sampleCount * 2 };
                Marshal.StructureToPtr(info, header, false);
                if (waveOutPrepareHeader(device, header, HeaderSize) != 0)
                {
                    Marshal.FreeHGlobal(header);
                    pinned.Free();
                    throw new InvalidOperationException("Cannot prepare farm audio buffer.");
                }
            }

            internal bool Ready { get { return !queued || (Marshal.ReadInt32(header, FlagsOffset) & 1) != 0; } }

            internal void Write()
            {
                if (waveOutWrite(device, header, HeaderSize) != 0)
                    throw new InvalidOperationException("Cannot write farm audio buffer.");
                queued = true;
            }

            public void Dispose()
            {
                waveOutUnprepareHeader(device, header, HeaderSize);
                Marshal.FreeHGlobal(header);
                pinned.Free();
            }
        }

        [StructLayout(LayoutKind.Sequential, Pack = 2)]
        struct WaveFormat
        {
            public ushort FormatTag, Channels;
            public uint SamplesPerSec, AvgBytesPerSec;
            public ushort BlockAlign, BitsPerSample, ExtraSize;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct WaveHeader
        {
            public IntPtr Data;
            public int Length, Recorded;
            public IntPtr User;
            public int Flags, Loops;
            public IntPtr Next, Reserved;
        }

        [DllImport("winmm.dll")]
        static extern int waveOutOpen(out IntPtr device, uint id, ref WaveFormat format, IntPtr callback, IntPtr instance, uint flags);
        [DllImport("winmm.dll")]
        static extern int waveOutPrepareHeader(IntPtr device, IntPtr header, int size);
        [DllImport("winmm.dll")]
        static extern int waveOutWrite(IntPtr device, IntPtr header, int size);
        [DllImport("winmm.dll")]
        static extern int waveOutReset(IntPtr device);
        [DllImport("winmm.dll")]
        static extern int waveOutUnprepareHeader(IntPtr device, IntPtr header, int size);
        [DllImport("winmm.dll")]
        static extern int waveOutClose(IntPtr device);
    }
}
