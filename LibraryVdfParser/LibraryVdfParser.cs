using System.Buffers;
using System.Collections.ObjectModel;
using System.Text;
using ValveKeyValue;

namespace SteamGameInfoParser {

#pragma warning disable CA1707, IDE1006
    public struct AppDetailCommon {
        public string name { get; set; }
        public string type { get; set; }
        public string logo { get; set; }
        public string logo_small { get; set; }
        public string icon { get; set; }
        public string clienttga { get; set; }
        public string clienticon { get; set; }
        public Dictionary<string, string> small_capsule { get; set; }
        public Dictionary<string, string> header_image { get; set; }
        public Dictionary<string, string>? name_localized { get; set; }
    }

    [Serializable]
    public struct AppDetail {
        public int appid { get; set; }
        public AppDetailCommon common { get; set; }
    }
#pragma warning restore CA1707, IDE1006

    public class App {
        public uint AppID { get; set; }

        public uint InfoState { get; set; }

        public DateTime LastUpdated { get; set; }

        public ulong Token { get; set; }

        public ReadOnlyCollection<byte> Hash { get; set; }
        public ReadOnlyCollection<byte> BinaryDataHash { get; set; }

        public uint ChangeNumber { get; set; }

        public AppDetail Data { get; set; }
    }

    public class LibraryVdfParser {

        private const uint Magic29 = 0x07_56_44_29;
        private const uint Magic28 = 0x07_56_44_28;
        private const uint Magic = 0x07_56_44_27;

        /// <summary>
        /// Opens and reads the given filename.
        /// </summary>
        /// <param name="filename">The file to open and read.</param>
        public static Dictionary<uint, App> Read(string filename) {
            using var fs = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            return Read(fs);
        }

        /// <summary>
        /// Reads the given <see cref="Stream"/>.
        /// </summary>
        /// <param name="input">The input <see cref="Stream"/> to read from.</param>
        public static Dictionary<uint, App> Read(Stream input) {
            var Apps = new Dictionary<uint, App>();
            using var reader = new BinaryReader(input);
            var magic = reader.ReadUInt32();

            if (magic != Magic && magic != Magic28 && magic != Magic29) {
                throw new InvalidDataException($"Unknown magic header: {magic:X}");
            }

            reader.ReadUInt32();


            var options = new KVSerializerOptions();

            if (magic == Magic29) {
                var stringTableOffset = reader.ReadInt64();
                var offset = reader.BaseStream.Position;
                reader.BaseStream.Position = stringTableOffset;
                var stringCount = reader.ReadUInt32();
                var stringPool = new string[stringCount];

                for (var i = 0; i < stringCount; i++) {
                    stringPool[i] = ReadNullTermUtf8String(reader.BaseStream);
                }

                reader.BaseStream.Position = offset;

                options.StringTable = new(stringPool);
            }

            var deserializer = KVSerializer.Create(KVSerializationFormat.KeyValues1Binary);


            do {
                var appid = reader.ReadUInt32();

                if (appid == 0) {
                    break;
                }

                var size = reader.ReadUInt32(); // size until end of Data
                var end = reader.BaseStream.Position + size;

                var app = new App {
                    AppID = appid,
                    InfoState = reader.ReadUInt32(),
                    LastUpdated = DateTimeFromUnixTime(reader.ReadUInt32()),
                    Token = reader.ReadUInt64(),
                    Hash = new ReadOnlyCollection<byte>(reader.ReadBytes(20)),
                    ChangeNumber = reader.ReadUInt32(),
                };

                if (magic == Magic28 || magic == Magic29) {
                    app.BinaryDataHash = new ReadOnlyCollection<byte>(reader.ReadBytes(20));
                }

                app.Data = deserializer.Deserialize<AppDetail>(input, options);

                if (reader.BaseStream.Position != end) {
                    throw new InvalidDataException();
                }

                Apps.Add(appid, app);
            }
            while (true);

            return Apps;
        }

        private static DateTime DateTimeFromUnixTime(uint unixTime) {
            return new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc).AddSeconds(unixTime);
        }

        private static string ReadNullTermUtf8String(Stream stream) {
            var buffer = ArrayPool<byte>.Shared.Rent(32);

            try {
                var position = 0;

                do {
                    var b = stream.ReadByte();

                    if (b <= 0) // null byte or stream ended
                    {
                        break;
                    }

                    if (position >= buffer.Length) {
                        var newBuffer = ArrayPool<byte>.Shared.Rent(buffer.Length * 2);
                        Buffer.BlockCopy(buffer, 0, newBuffer, 0, buffer.Length);
                        ArrayPool<byte>.Shared.Return(buffer);
                        buffer = newBuffer;
                    }

                    buffer[position++] = (byte)b;
                }
                while (true);

                return Encoding.UTF8.GetString(buffer[..position]);
            } finally {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }
    }
}
