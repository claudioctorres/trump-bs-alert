#if WINDOWS
using Windows.Media.Core;
using Windows.Media.Playback;
#endif

namespace TrumpBsAlert.Services;

public class SoundLoopService : ISoundLoopService
{
#if WINDOWS
    private MediaPlayer? _player;

    public void Start()
    {
        if (_player is not null) return; // idempotent

        var filePath = Path.Combine(AppContext.BaseDirectory, "alert.wav");
        _player = new MediaPlayer
        {
            IsLoopingEnabled = true,
            Source = MediaSource.CreateFromUri(new Uri(filePath))
        };
        _player.Play();
    }

    public void Stop()
    {
        if (_player is null) return;
        _player.Pause();
        _player.Dispose();
        _player = null;
    }
#else
    public void Start() { }
    public void Stop() { }
#endif
}
