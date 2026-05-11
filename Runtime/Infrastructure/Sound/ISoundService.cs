using System.Threading;
using Cysharp.Threading.Tasks;

namespace StickerFwk.Infrastructure.Sound
{
    public interface ISoundService
    {
        float SEVolume { get; set; }
        float BGMVolume { get; set; }

        float GetVolume(SoundType soundType);
        void SetVolume(SoundType soundType, float volume);

        UniTask LoadCueSheetAsync(string addressableKey, CancellationToken cancellationToken = default);
        void UnloadCueSheet(string addressableKey);
        bool IsCueSheetLoaded(string addressableKey);
        bool HasSound(string soundName);

        void PlaySeOneShot(string soundName, float volume = 1.0f);
        UniTask PlayBgm(string soundName, int channel = 0, float volume = 1.0f, float crossfadeDuration = 1.0f);
        void PlayBgmImmediate(string soundName, int channel = 0, float volume = 1.0f);
        UniTask StopBgm(int channel = 0, float fadeDuration = 1.0f);
        void StopBgmImmediate(int channel = 0);
        UniTask StopAllBgm(float fadeDuration = 1.0f);
        void StopAllBgmImmediate();
        UniTask PauseBgm(int channel = 0, float fadeDuration = 1.0f);
        void PauseBgmImmediate(int channel = 0);
        UniTask ResumeBgm(int channel = 0, float fadeDuration = 1.0f);
        void ResumeBgmImmediate(int channel = 0);
        UniTask PauseAllBgm(float fadeDuration = 1.0f);
        void PauseAllBgmImmediate();
        UniTask ResumeAllBgm(float fadeDuration = 1.0f);
        void ResumeAllBgmImmediate();
    }
}
