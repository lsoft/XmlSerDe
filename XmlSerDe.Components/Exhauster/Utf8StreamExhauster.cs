using System;
using System.Buffers;
using System.IO;

namespace XmlSerDe.Components.Exhauster
{
    /// <summary>
    /// Пишет UTF-8 в чужой <see cref="Stream"/>. Поток не закрывает и не
    /// сбрасывает: <see cref="Flush"/> и <see cref="Dispose"/> выталкивают
    /// только внутренний буфер.
    ///
    /// Генератор отдаёт теги мелкими <c>Append</c>, поэтому без буфера каждый
    /// литерал - отдельный <see cref="Stream.Write(byte[], int, int)"/>.
    /// Сюда порции копируются, пока не наберётся <see cref="BufferSize"/>;
    /// кусок не короче буфера уходит в поток напрямую, без второго копирования.
    /// </summary>
    public class Utf8StreamExhauster : Utf8BinaryExhauster, IDisposable
    {
        public const int BufferSize = 16 * 1024;

        private readonly Stream _stream;
        private byte[] _buffer;
        private int _pos;
        private bool _disposed;

        public Utf8StreamExhauster(Stream stream)
        {
            _stream = stream ?? throw new ArgumentNullException(nameof(stream));
            _buffer = ArrayPool<byte>.Shared.Rent(BufferSize);
        }

        protected override void Write(byte[] data, int length)
        {
            if (length == 0)
            {
                return;
            }

            ThrowIfDisposed();

            var remaining = _buffer.Length - _pos;
            if (length <= remaining)
            {
                data.AsSpan(0, length).CopyTo(_buffer.AsSpan(_pos));
                _pos += length;
                return;
            }

            FlushBuffer();

            if (length >= _buffer.Length)
            {
                _stream.Write(data, 0, length);
                return;
            }

            data.AsSpan(0, length).CopyTo(_buffer);
            _pos = length;
        }

        /// <summary>
        /// Выталкивает накопленное в поток. Сам поток не сбрасывает.
        /// </summary>
        public void Flush()
        {
            ThrowIfDisposed();
            FlushBuffer();
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            try
            {
                FlushBuffer();
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(_buffer);
                _buffer = Array.Empty<byte>();
            }
        }

        private void FlushBuffer()
        {
            if (_pos == 0)
            {
                return;
            }

            _stream.Write(_buffer, 0, _pos);
            _pos = 0;
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(Utf8StreamExhauster));
            }
        }
    }
}
