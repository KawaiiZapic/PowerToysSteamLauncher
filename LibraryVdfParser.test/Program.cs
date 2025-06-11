using Microsoft.Win32;
using System.Text.Json;
using ValveKeyValue;

namespace SteamGameInfoParser.test {
    internal class Program {
        static ManualResetEvent _quitEvent = new(false);
        static void Main(string[] args) {

            var SteamPath =
                Registry.GetValue("HKEY_LOCAL_MACHINE\\SOFTWARE\\Valve\\Steam", "InstallPath", null)?.ToString()
                ?? Registry.GetValue("HKEY_LOCAL_MACHINE\\SOFTWARE\\Wow6432Node\\Valve\\Steam", "InstallPath", null)?.ToString()
                ?? throw new FileNotFoundException();

            FileSystemWatcher ConfigWatcher = new() {
                Path = Path.Combine(SteamPath, "userdata", "1039124823", "config"),
                Filter = "localconfig.vdf",
                NotifyFilter = NotifyFilters.FileName,
                EnableRaisingEvents = true
            };
            ConfigWatcher.Renamed += new ((sender, e) => {
                Console.WriteLine(e.ChangeType + ": " + e.FullPath);
            });
            _quitEvent.WaitOne();
            var vdfPath = args.Length > 0 ? args[0] : Console.ReadLine();
            if (File.Exists(vdfPath)) {
                var serializer = KVSerializer.Create(KVSerializationFormat.KeyValues1Text);
                using var f = new FileStream(vdfPath, FileMode.Open);

                var res = serializer.Deserialize<AppDetail>(f);


                Console.WriteLine(JsonSerializer.Serialize(res, new JsonSerializerOptions { WriteIndented = true }));
            } else {
                Console.WriteLine("VDF invaild");
            }


            var loginUserParser = new LoginUserParser(SteamPath);


            Console.WriteLine(JsonSerializer.Serialize(loginUserParser.GameInfoDict, new JsonSerializerOptions { WriteIndented = true }));
        }
    }
}
