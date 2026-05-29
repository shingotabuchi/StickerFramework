namespace StickerFwk.Core.LocalDataSave
{
    public interface ILocalDataSaveSerializer
    {
        byte[] Serialize<T>(T value);

        T Deserialize<T>(byte[] bytes) where T : class;
    }
}
