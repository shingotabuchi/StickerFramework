using System;
using Newtonsoft.Json;
using StickerFwk.Core.LocalDataSave;

namespace StickerFwk.Infrastructure.LocalDataSave
{
    public sealed class JsonLocalDataSaveSerializer : ILocalDataSaveSerializer
    {
        private readonly JsonSerializerSettings _settings;

        public JsonLocalDataSaveSerializer()
        {
            _settings = new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore
            };
        }

        public byte[] Serialize<T>(T value)
        {
            var json = JsonConvert.SerializeObject(value, _settings);
            return System.Text.Encoding.UTF8.GetBytes(json);
        }

        public T Deserialize<T>(byte[] bytes) where T : class
        {
            if (bytes == null || bytes.Length == 0)
            {
                return null;
            }

            var json = System.Text.Encoding.UTF8.GetString(bytes);
            return JsonConvert.DeserializeObject<T>(json, _settings);
        }
    }
}
