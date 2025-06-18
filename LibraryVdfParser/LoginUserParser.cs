using Microsoft.Win32;
using ValveKeyValue;
using static SteamGameInfoParser.GameLibrary;

namespace SteamGameInfoParser {
    public struct SteamUser { 
        public long TimeStamp { get; set; }
    }

    public struct GameInfo { 
        public long LastPlayed { get; set; }
        public int Playtime { get; set; }
        public int Playtime2wks { get; set; }
    }

    public struct LocalConfigVdfRoot {
        public struct SSoftware {
            public struct SValve {
                public struct SSteam {
                    public Dictionary<string, GameInfo> apps { get; set; }
                }

                public SSteam Steam { get; set; }
            }

            public SValve Valve { get; set; }
        }

        public SSoftware Software { get; set; }

    }


    class NoLoginUserException: Exception { }

    public class LoginUserParser: IDisposable {
        public string SteamPath { get; private set; }
        public UInt64 UserId { get; private set; }
        public Dictionary<string, GameInfo> GameInfoDict { get; private set; }

        FileSystemWatcher? ConfigWatcher { get; set; }

        public event EventHandler? Changed;

        static LoginUserParser? _instance { get; set; }

        public static LoginUserParser Instance {
            get {
                _instance ??= new LoginUserParser();
                return _instance;
            }
        }

        private LoginUserParser() {
            SteamPath =
                Registry.GetValue("HKEY_LOCAL_MACHINE\\SOFTWARE\\Valve\\Steam", "InstallPath", null)?.ToString()
                ?? Registry.GetValue("HKEY_LOCAL_MACHINE\\SOFTWARE\\Wow6432Node\\Valve\\Steam", "InstallPath", null)?.ToString()
                ?? throw new SteamNotFoundException();

            UserId = GetLastUserId();
            UpdateUserGameRecord();
            if (GameInfoDict == null) {
                throw new InvalidDataException();
            }
            StartWatch();
        }

        public UInt64 GetLastUserId() {
            var loginUserVdf = Path.Combine(SteamPath, "config", "loginusers.vdf");

            using var file = File.OpenRead(loginUserVdf);
            var ser = KVSerializer.Create(KVSerializationFormat.KeyValues1Text);
            var result = ser.Deserialize<Dictionary<UInt64, SteamUser>>(file);

            UInt64? lastUserId = null;
            long lastTime = 0;
            foreach (var id in result.Keys) {
                var u = result[id];
                if (u.TimeStamp >= lastTime) {
                    lastTime = u.TimeStamp;
                    lastUserId = id;
                }
            }
            return lastUserId ?? throw new NoLoginUserException();
        }

        public void UpdateUserGameRecord() {
            lock (GameInfoDict) {
                var path = Path.Combine(SteamPath, "userdata", ((UInt32)UserId).ToString(), "config", "localconfig.vdf");
                using var file = File.OpenRead(path);

                var ser = KVSerializer.Create(KVSerializationFormat.KeyValues1Text);
                var result = ser.Deserialize<LocalConfigVdfRoot>(file, new KVSerializerOptions { HasEscapeSequences = true });

                GameInfoDict = result.Software.Valve.Steam.apps;
                Changed?.Invoke(this, new());
            }
        }

        private void StartWatch() {
            ConfigWatcher = new() {
                Path = Path.Combine(SteamPath, "userdata", ((UInt32)UserId).ToString(), "config"),
                Filter = "localconfig.vdf",
                NotifyFilter = NotifyFilters.FileName,
                EnableRaisingEvents = true
            };
            ConfigWatcher.Renamed += (e, sender) => {
                UpdateUserGameRecord();
            };
        }


        public void Dispose() {
            GC.SuppressFinalize(this);
            ConfigWatcher?.Dispose();
            _instance = null;
        }
    }
}
