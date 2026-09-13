using System.Text;
using STS2HumanAnnotator.Core;
using Xunit;

namespace STS2HumanAnnotator.Core.Tests;

public sealed class RecoverableAppendBatchTests
{
    [Fact]
    public void PartialBatchWriteRollsBackOnlyToOriginalBoundary()
    {
        using var inner = Existing();
        using var failing = new PartialWriteFailureStream(inner, 5, failRollback: false);

        Assert.Throws<IOException>(() => RecoverableAppendBatch.Write(
            failing,
            Lines()));

        Assert.Equal("existing\n", Encoding.UTF8.GetString(inner.ToArray()));
    }

    [Fact]
    public void RollbackFailureIsTypedAndDoesNotPretendTheBatchWasRemoved()
    {
        using var inner = Existing();
        using var failing = new PartialWriteFailureStream(inner, 5, failRollback: true);

        AppendRollbackFailedException failure = Assert.Throws<AppendRollbackFailedException>(
            () => RecoverableAppendBatch.Write(failing, Lines()));

        Assert.Equal("existing\n".Length, failure.OriginalLength);
        Assert.IsType<IOException>(failure.InnerException);
        Assert.IsType<IOException>(failure.RollbackFailure);
        Assert.True(inner.Length > failure.OriginalLength);
    }

    [Fact]
    public void CompleteBatchPreservesOrderedLineBoundaries()
    {
        using var stream = new MemoryStream();
        RecoverableAppendBatch.Write(stream, Lines());
        Assert.Equal("first\nsecond\n", Encoding.UTF8.GetString(stream.ToArray()));
    }

    private static MemoryStream Existing()
    {
        var stream = new MemoryStream();
        stream.Write(Encoding.UTF8.GetBytes("existing\n"));
        return stream;
    }

    private static byte[][] Lines() => new[]
    {
        Encoding.UTF8.GetBytes("first"),
        Encoding.UTF8.GetBytes("second")
    };

    private sealed class PartialWriteFailureStream : Stream
    {
        private readonly Stream _inner;
        private readonly int _bytesBeforeFailure;
        private readonly bool _failRollback;
        private bool _writeFailed;

        internal PartialWriteFailureStream(Stream inner, int bytesBeforeFailure, bool failRollback)
        {
            _inner = inner;
            _bytesBeforeFailure = bytesBeforeFailure;
            _failRollback = failRollback;
        }

        public override bool CanRead => _inner.CanRead;
        public override bool CanSeek => _inner.CanSeek;
        public override bool CanWrite => _inner.CanWrite;
        public override long Length => _inner.Length;
        public override long Position { get => _inner.Position; set => _inner.Position = value; }
        public override void Flush() => _inner.Flush();
        public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);
        public override long Seek(long offset, SeekOrigin origin) => _inner.Seek(offset, origin);
        public override void SetLength(long value)
        {
            if (_failRollback && _writeFailed)
                throw new IOException("injected rollback failure");
            _inner.SetLength(value);
        }
        public override void Write(byte[] buffer, int offset, int count)
        {
            if (!_writeFailed)
            {
                _writeFailed = true;
                int partial = Math.Min(count, _bytesBeforeFailure);
                _inner.Write(buffer, offset, partial);
                throw new IOException("injected partial semantic batch write");
            }
            _inner.Write(buffer, offset, count);
        }
    }
}
