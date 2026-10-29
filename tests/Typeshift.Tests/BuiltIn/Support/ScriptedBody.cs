using System.Text;

namespace Typeshift.Tests.BuiltIn.Support;

// A body that never ends, or that stalls once the headers arrived, as a server streaming forever or hanging would
public sealed class ScriptedBody : Stream
{
    private readonly byte[]? _pattern;
    private long _position;

    private ScriptedBody(byte[]? pattern) => _pattern = pattern;

    public override bool CanRead => true;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => _position;
        set => throw new NotSupportedException();
    }

    public static ScriptedBody Endless(string pattern) => new(Encoding.UTF8.GetBytes(pattern));

    public static ScriptedBody Stalled() => new(pattern: null);

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default)
    {
        if (_pattern is null) return new(Stall.Until<int>(ct));

        var span = buffer.Span;

        for (var i = 0; i < span.Length; i++)
        {
            span[i] = _pattern[_position++ % _pattern.Length];
        }

        return new(span.Length);
    }

    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    public override void Flush() => throw new NotSupportedException();

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
