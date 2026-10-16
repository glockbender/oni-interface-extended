using System;
using System.IO;
using System.Linq;
using System.Text;
using CenteredHud;

public static class ConfigStoreChecks
{
    public static void Run(string root)
    {
        string mods = Path.Combine(root, "mods");
        string installed = Path.Combine(mods, "Steam", "123456");
        Directory.CreateDirectory(installed);
        var store = new ConfigStore(mods, installed);
        string oldLocal = Path.Combine(mods, "Local", "CenteredHud", "config.json");
        string oldInstalled = Path.Combine(installed, "config.json");
        Exception migrationError;

        Require(store.ConfigPath == Path.Combine(mods, "config", "InterfaceExtended", "config.json"),
            "Config must use the shared data directory, not the installation folder");
        Require(store.Read(out migrationError).WidthPixels == 2560 && migrationError == null,
            "Fresh install must use production defaults");
        Require(!File.Exists(store.ConfigPath), "Reading defaults must not create a config");

        Directory.CreateDirectory(Path.GetDirectoryName(oldLocal));
        byte[] original = new UTF8Encoding(true).GetPreamble().Concat(Encoding.UTF8.GetBytes(
            "{\n  \"WidthPixels\": 2720, \"EnableShortcuts\": true, \"SettingsKey\": \"F10\", \"Extra\": \"keep\"\n}")).ToArray();
        File.WriteAllBytes(oldLocal, original);
        Options migrated = store.Read(out migrationError);
        Require(migrated.WidthPixels == 2720 && migrated.EnableShortcuts && migrated.SettingsKey == "F10"
            && migrationError == null, "Local-to-Workshop migration lost custom settings");
        Require(File.ReadAllBytes(store.ConfigPath).SequenceEqual(original), "Migration must preserve exact bytes and unknown keys");
        Require(File.ReadAllBytes(oldLocal).SequenceEqual(original), "Migration must leave its source untouched");

        File.WriteAllText(oldInstalled, "{\"WidthPixels\": 3360}");
        Require(store.Read(out migrationError).WidthPixels == 2720, "Existing shared config must win over both legacy paths");
        store.Write(new Options { WidthPixels = 3000, ShowGuides = true });
        Require(store.Read(out migrationError).WidthPixels == 3000 && store.Read(out migrationError).ShowGuides,
            "Replacing the shared config must persist current settings");
        Require(File.ReadAllBytes(oldLocal).SequenceEqual(original), "Saving must not change the legacy local config");

        File.Delete(store.ConfigPath);
        Require(store.Read(out migrationError).WidthPixels == 3360 && migrationError == null,
            "An installation's own legacy config must take precedence over another local installation");
        File.Delete(store.ConfigPath);
        File.WriteAllText(oldInstalled, "broken JSON");
        ExpectReadFailure(store);
        Require(!File.Exists(store.ConfigPath), "Malformed legacy JSON must never be copied to the shared location");
        File.WriteAllText(oldInstalled, "null");
        ExpectReadFailure(store);
        Require(!File.Exists(store.ConfigPath), "Null legacy settings must not be migrated");

        File.WriteAllText(oldInstalled, "{\"WidthPixels\": 3360}");
        File.WriteAllText(store.ConfigPath, "broken primary");
        ExpectReadFailure(store);
        Require(File.ReadAllText(store.ConfigPath) == "broken primary", "An invalid shared config must not be replaced by stale legacy data");
        File.Delete(store.ConfigPath);

        // A directory at the destination models a deterministic write failure on every OS.
        Directory.CreateDirectory(store.ConfigPath);
        Require(store.Read(out migrationError).WidthPixels == 3360 && migrationError != null,
            "Migration failure must retain usable legacy settings and report the error");
        Require(File.ReadAllText(oldInstalled) == "{\"WidthPixels\": 3360}", "Failed migration changed its source");
        Require(!Directory.GetFiles(Path.GetDirectoryName(store.ConfigPath), "*.tmp").Any(), "Failed migration left a temporary file");
        Directory.Delete(store.ConfigPath);

        File.Delete(oldInstalled);
        File.Delete(oldLocal);
        store.Write(new Options { WidthPixels = 2880 });
        Require(store.Read(out migrationError).WidthPixels == 2880, "First save must create its data directory and config");
        if (Environment.OSVersion.Platform == PlatformID.Win32NT)
        {
            using (File.Open(store.ConfigPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                bool failed = false;
                try { store.Write(new Options { WidthPixels = 3200 }); }
                catch (IOException) { failed = true; }
                Require(failed, "Locked config must report write failure");
            }
            Require(store.Read(out migrationError).WidthPixels == 2880, "Failed atomic replacement corrupted the previous config");
        }
        Require(!Directory.GetFiles(Path.GetDirectoryName(store.ConfigPath), "*.tmp").Any(), "Config writes left temporary files");

        var fresh = new ConfigStore(Path.Combine(root, "fresh-mods"), installed);
        fresh.Write(new Options { WidthPixels = 2800 });
        Require(fresh.Read(out migrationError).WidthPixels == 2800, "First save did not create missing parent directories");

        // Replacing a Workshop installation cannot affect the shared settings.
        File.WriteAllText(Path.Combine(installed, "mod.yaml"), "staticID: InterfaceExtended");
        File.WriteAllText(oldInstalled, "{\"WidthPixels\": 5120}");
        Require(new ConfigStore(mods, installed).Read(out migrationError).WidthPixels == 2880,
            "Installation update reset shared settings");
    }

    private static void ExpectReadFailure(ConfigStore store)
    {
        bool failed = false;
        try { Exception error; store.Read(out error); }
        catch (Newtonsoft.Json.JsonException) { failed = true; }
        catch (InvalidDataException) { failed = true; }
        Require(failed, "Invalid settings must be rejected");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
