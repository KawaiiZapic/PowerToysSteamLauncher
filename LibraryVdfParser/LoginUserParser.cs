using System.ComponentModel;
using ValveKeyValue;

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
        string SteamPath { get; set; }
        public UInt64 UserId { get; set; }
        public Dictionary<string, GameInfo> GameInfoDict { get; set; }

        FileSystemWatcher? ConfigWatcher { get; set; }

        public event EventHandler? Changed;

        public LoginUserParser(string steamPath) {
            this.SteamPath = steamPath;
            this.UserId = GetLastUserId();
            UpdateUserGameRecord();
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
            var path = Path.Combine(SteamPath, "userdata", ((UInt32)UserId).ToString(), "config", "localconfig.vdf");
            using var file = File.OpenRead(path);

            var ser = KVSerializer.Create(KVSerializationFormat.KeyValues1Text);
            var result = ser.Deserialize<LocalConfigVdfRoot>(file, new KVSerializerOptions { HasEscapeSequences = true });

            GameInfoDict = result.Software.Valve.Steam.apps;
            Changed?.Invoke(this, new());
        }

        public void StartWatch() {
            ConfigWatcher = new() {
                Path = Path.Combine(SteamPath, "userdata", ((UInt32)UserId).ToString(), "config"),
                Filter = "localconfig.vdf",
                NotifyFilter = NotifyFilters.FileName,
                EnableRaisingEvents = true
            };
            var debounced = Debounce(UpdateUserGameRecord, 5000);
            ConfigWatcher.Renamed += (e, sender) => {
                debounced();
            };
        }


        public void Dispose() {
            GC.SuppressFinalize(this);
            ConfigWatcher?.Dispose();
        }


        static Action Debounce(Action func, int milliseconds) {
            var last = 0;
            return () => {
                var current = Interlocked.Increment(ref last);
                Task.Delay(milliseconds).ContinueWith(task => {
                    if (current == last)
                        func();
                    task.Dispose();
                });
            };
        }
    }
}
