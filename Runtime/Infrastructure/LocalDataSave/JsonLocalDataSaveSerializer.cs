using System;
using System.Text;
using Newtonsoft.Json;
using StickerFwk.Core.LocalDataSave;

namespace StickerFwk.Infrastructure.LocalDataSave
{
    public sealed class JsonLocalDataSaveSerializer : ILocalDataSaveSerializer
    {
        private readonly JsonSerializerSettings _settings;

        public JsonLocalDataSaveSerializer()
        {
            _settings = new JsonSerializerSettings();
        }

        public byte[] Serialize<T>(T value) where T : class
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            var json = JsonConvert.SerializeObject(value, Formatting.None, _settings);
            return Encoding.UTF8.GetBytes(json);
        }

        public T Deserialize<T>(byte[] bytes) where T : class
        {
            if (bytes == null || bytes.Length == 0)
            {
                return null;
            }

            var json = Encoding.UTF8.GetString(bytes);
            return JsonConvert.DeserializeObject<T>(json, _settings);
        }
    }
}
