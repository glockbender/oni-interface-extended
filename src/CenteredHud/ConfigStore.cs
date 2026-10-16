using System;
using System.IO;
using Newtonsoft.Json;

namespace CenteredHud
{
    internal sealed class ConfigStore
    {
        internal string ConfigPath { get; }
        private readonly string[] legacyPaths;

        internal ConfigStore(string modsDirectory, string modDirectory)
        {
            ConfigPath = Path.Combine(modsDirectory, "config", "InterfaceExtended", "config.json");
            legacyPaths = new[] {
                Path.Combine(modDirectory, "config.json"),
                Path.Combine(modsDirectory, "Local", "CenteredHud", "config.json")
            };
        }

        internal Options Read(out Exception migrationError)
        {
            migrationError = null;
            if (File.Exists(ConfigPath)) return Parse(File.ReadAllText(ConfigPath));
            foreach (string legacy in legacyPaths)
            {
                if (!File.Exists(legacy)) continue;
                byte[] bytes = File.ReadAllBytes(legacy);
                Options settings;
                // StreamReader handles the BOM used by some older/manual configs.
                using (var reader = new StreamReader(new MemoryStream(bytes)))
                    settings = Parse(reader.ReadToEnd());
                try { WriteBytes(bytes, false); }
                catch (Exception error) { migrationError = error; }
                return settings;
            }
            return new Options();
        }

        private static Options Parse(string json)
        {
            Options result = JsonConvert.DeserializeObject<Options>(json);
            if (result == null) throw new InvalidDataException("Empty settings");
            result.Normalize();
            return result;
        }

        internal void Write(Options settings)
        {
            WriteBytes(System.Text.Encoding.UTF8.GetBytes(
                JsonConvert.SerializeObject(settings, Formatting.Indented)), true);
        }

        private void WriteBytes(byte[] bytes, bool replaceExisting)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath));
            string temporary = ConfigPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write))
                {
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush(true);
                }
                if (replaceExisting && File.Exists(ConfigPath)) File.Replace(temporary, ConfigPath, null);
                else File.Move(temporary, ConfigPath);
            }
            finally
            {
                if (File.Exists(temporary)) File.Delete(temporary);
            }
        }
    }
}
