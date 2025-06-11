using ValveKeyValue;

namespace SteamGameInfoParser {
    struct LibraryFolder { 
        public string path { get; set; }
    }
    public class LibraryFolderVdfParser(): IDisposable {
        public List<string> apps { get; private set; } = [];
        List<FileSystemWatcher> ConfigWatcher { get; set; } = [];
        string SteamPath { get; set; } = "";

        public event EventHandler? Changed;

        public LibraryFolderVdfParser(string SteamPath): this() {
            this.SteamPath = SteamPath;
            UpdateGamesRecord();
        }

        string[] GetLibraryPaths() {
            var path = Path.Combine(SteamPath, "config", "libraryfolders.vdf");
            using var file = File.OpenRead(path);

            var ser = KVSerializer.Create(KVSerializationFormat.KeyValues1Text);
            var result = ser.Deserialize<LibraryFolder[]>(file, new KVSerializerOptions { HasEscapeSequences = true });

            return [.. result.Select(l => l.path)];
        }

        public void UpdateGamesRecord() {
            apps.Clear();
            foreach (var library in GetLibraryPaths()) {
                var dir = new DirectoryInfo(Path.Join(library, "steamapps"));
                var files = dir.GetFiles("appmanifest_*.acf");
                foreach (var f in files) {
                    apps.Add(f.Name[12..^4]);
                }
            }
        }

        public void StartWatch() {
            StopWatch();
            
            var debounced = Debounce(() => {
                UpdateGamesRecord();
                Changed?.Invoke(this, new EventArgs());
            }, 5000);

            FileSystemWatcher lfw = new() {
                Path = Path.Combine(SteamPath, "config"),
                Filter = "libraryfolders.vdf",
                NotifyFilter = NotifyFilters.FileName,
                EnableRaisingEvents = true
            };
            lfw.Renamed += (sender, e) => {
                debounced();
                Debounce(StartWatch, 5000)();
            };
            ConfigWatcher.Add(lfw);
            foreach (var library in GetLibraryPaths()) {
                FileSystemWatcher acfw = new() {
                    Path = Path.Join(library, "steamapps"),
                    Filter = "appmanifest_*.acf",
                    EnableRaisingEvents = true
                };
                acfw.Renamed += (sender, e) => {
                    debounced();
                };
                acfw.Deleted += (sender, e) => {
                    debounced();
                };
                ConfigWatcher.Add(acfw);
            }

        }

        public void StopWatch() {
            foreach (var w in ConfigWatcher) {
                w.Dispose();
            }
            ConfigWatcher.Clear();
        }

        public void Dispose() {
            GC.SuppressFinalize(this);
            StopWatch();
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
