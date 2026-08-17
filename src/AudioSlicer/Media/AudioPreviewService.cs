using System.IO;
using NAudio.Wave;

namespace AudioSlicer.Media;

public sealed class AudioPreviewService : IDisposable
{
    private IWavePlayer? _output;
    private AudioFileReader? _reader;

    public void Play(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("任务音频文件不存在，请重新生成。", filePath);
        }

        Stop();
        _reader = new AudioFileReader(filePath);
        _output = new WaveOutEvent();
        _output.Init(_reader);
        _output.PlaybackStopped += OnPlaybackStopped;
        _output.Play();
    }

    public void Stop()
    {
        if (_output is not null)
        {
            _output.PlaybackStopped -= OnPlaybackStopped;
            _output.Stop();
            _output.Dispose();
            _output = null;
        }

        _reader?.Dispose();
        _reader = null;
    }

    private void OnPlaybackStopped(object? sender, StoppedEventArgs e)
    {
        if (_output is not null)
        {
            _output.PlaybackStopped -= OnPlaybackStopped;
            _output.Dispose();
            _output = null;
        }

        _reader?.Dispose();
        _reader = null;
    }

    public void Dispose() => Stop();
}
