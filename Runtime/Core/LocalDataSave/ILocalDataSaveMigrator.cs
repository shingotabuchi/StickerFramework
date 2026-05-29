namespace StickerFwk.Core.LocalDataSave
{
    public interface ILocalDataSaveMigrator<T> where T : class, new()
    {
        int CurrentVersion { get; }

        T CreateDefault();

        T Migrate(T data, int loadedVersion);
    }
}
