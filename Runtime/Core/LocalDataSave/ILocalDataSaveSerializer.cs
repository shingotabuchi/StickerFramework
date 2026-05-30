namespace StickerFwk.Core.LocalDataSave
{
    public interface ILocalDataSaveSerializer
    {
        byte[] Serialize<T>(T value) where T : class;

        T Deserialize<T>(byte[] bytes) where T : class;
    }
}
