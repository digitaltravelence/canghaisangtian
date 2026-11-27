using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Security;
using System.Text;
using System.Xml;

namespace MountainTide
{
    internal struct AudioPreferences
    {
        internal const string FileName = "音频设置.xml";
        internal int MusicVolume, EffectsVolume;
        internal bool MusicEnabled, EffectsEnabled;

        internal static string DefaultPath
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                                      "沧海桑田", FileName); }
        }

        static AudioPreferences Defaults
        {
            get { return new AudioPreferences { MusicVolume = 100, EffectsVolume = 100,
                                                 MusicEnabled = true, EffectsEnabled = true }; }
        }

        internal static int ClampVolume(int volume) { return Math.Max(0, Math.Min(100, volume)); }

        static int ReadVolume(XmlElement root, string name, int fallback)
        {
            int volume;
            return int.TryParse(root.GetAttribute(name), NumberStyles.Integer, CultureInfo.InvariantCulture,
                                out volume) ? ClampVolume(volume) : fallback;
        }

        static bool ReadSwitch(XmlElement root, string name, bool fallback)
        {
            bool enabled;
            return bool.TryParse(root.GetAttribute(name), out enabled) ? enabled : fallback;
        }

        internal static AudioPreferences Load(string path)
        {
            AudioPreferences result = Defaults;
            foreach (string candidate in new[] { path, path + ".bak" })
            {
                if (!File.Exists(candidate)) continue;
                try
                {
                    XmlReaderSettings settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit,
                        XmlResolver = null, MaxCharactersInDocument = 4096 };
                    XmlDocument doc = new XmlDocument { XmlResolver = null };
                    using (FileStream stream = File.OpenRead(candidate))
                    using (XmlReader reader = XmlReader.Create(stream, settings)) doc.Load(reader);
                    XmlElement root = doc.DocumentElement;
                    if (root == null || root.Name != "AudioPreferences") continue;
                    result.MusicVolume = ReadVolume(root, "musicVolume", result.MusicVolume);
                    result.EffectsVolume = ReadVolume(root, "effectsVolume", result.EffectsVolume);
                    result.MusicEnabled = ReadSwitch(root, "musicEnabled", result.MusicEnabled);
                    result.EffectsEnabled = ReadSwitch(root, "effectsEnabled", result.EffectsEnabled);
                    return result;
                }
                catch (XmlException) { }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
                catch (SecurityException) { }
            }
            return result;
        }

        internal bool Save(string path)
        {
            string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
                XmlWriterSettings settings = new XmlWriterSettings { Encoding = new UTF8Encoding(false), Indent = true };
                using (FileStream stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    using (XmlWriter writer = XmlWriter.Create(stream, settings))
                    {
                        writer.WriteStartElement("AudioPreferences");
                        writer.WriteAttributeString("musicVolume", XmlConvert.ToString(MusicVolume));
                        writer.WriteAttributeString("effectsVolume", XmlConvert.ToString(EffectsVolume));
                        writer.WriteAttributeString("musicEnabled", XmlConvert.ToString(MusicEnabled));
                        writer.WriteAttributeString("effectsEnabled", XmlConvert.ToString(EffectsEnabled));
                        writer.WriteEndElement();
                    }
                    stream.Flush(true);
                }
                if (File.Exists(path)) File.Replace(temporary, path, path + ".bak", true);
                else File.Move(temporary, path);
                return true;
            }
            catch (IOException error) { Debug.WriteLine(error.Message); }
            catch (UnauthorizedAccessException error) { Debug.WriteLine(error.Message); }
            catch (SecurityException error) { Debug.WriteLine(error.Message); }
            finally
            {
                try { File.Delete(temporary); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
                catch (SecurityException) { }
            }
            return false;
        }
    }
}
