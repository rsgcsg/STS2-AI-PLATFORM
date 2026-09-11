namespace STS2HumanAnnotator.Core;

/// <summary>
/// Appends one pre-serialized batch with an exception-recovery boundary. A
/// partial write or flush exception truncates only bytes at/after the original
/// stream length before the exception escapes. This does not claim filesystem,
/// process-crash, or power-loss atomicity and is not a retry or second ledger.
/// </summary>
public static class RecoverableAppendBatch
{
    public static void Write(Stream stream, IReadOnlyList<byte[]> lines)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(lines);
        if (!stream.CanWrite || !stream.CanSeek)
            throw new ArgumentException("Recoverable append requires a writable seekable stream.", nameof(stream));
        if (lines.Count == 0)
            return;

        using var payload = new MemoryStream();
        foreach (byte[] line in lines)
        {
            ArgumentNullException.ThrowIfNull(line);
            payload.Write(line);
            payload.WriteByte((byte)'\n');
        }

        long originalLength = stream.Length;
        stream.Position = originalLength;
        try
        {
            stream.Write(payload.GetBuffer(), 0, checked((int)payload.Length));
            stream.Flush();
        }
        catch (Exception writeFailure)
        {
            try
            {
                stream.SetLength(originalLength);
                stream.Position = originalLength;
                stream.Flush();
            }
            catch (Exception rollbackFailure)
            {
                throw new AppendRollbackFailedException(
                    originalLength,
                    writeFailure,
                    rollbackFailure);
            }
            throw;
        }
    }
}

public sealed class AppendRollbackFailedException : IOException
{
    public AppendRollbackFailedException(
        long originalLength,
        Exception writeFailure,
        Exception rollbackFailure)
        : base(
            "Semantic append failed and its partial batch could not be restored to the original boundary.",
            writeFailure)
    {
        OriginalLength = originalLength;
        RollbackFailure = rollbackFailure;
    }

    public long OriginalLength { get; }
    public Exception RollbackFailure { get; }
}
