namespace Shane32.GraphQL.AspNetCore.WebSockets
{
    internal class ReusableMemoryReaderStream : Stream
    {
        private readonly byte[] _buffer;
        private int _position;
        private int _length;

        public ReusableMemoryReaderStream(byte[] buffer)
        {
            _buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
        }

        public override bool CanRead => true;

        public override bool CanSeek => true;

        public override bool CanWrite => false;

        public override long Length => _length;

        public override long Position {
            get => _position;
            set => _position = Math.Max(Math.Min(checked((int)value), _length), 0);
        }

        public override void Flush() => throw new NotSupportedException();

        public override int Read(byte[] buffer, int offset, int count)
            => Read(new Span<byte>(buffer, offset, count));

        public override int Read(Span<byte> buffer)
        {
            var count = Math.Min(_length - _position, buffer.Length);
            var source = new Span<byte>(_buffer, _position, count);
            _position += count;
            source.CopyTo(buffer);
            return count;
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            => Task.FromResult(Read(buffer, offset, count));

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            => new(Read(buffer.Span));

        public override long Seek(long offset, SeekOrigin origin)
            => Position =
                origin == SeekOrigin.Begin ? offset :
                origin == SeekOrigin.Current ? offset + _position :
                origin == SeekOrigin.End ? offset + _length :
                throw new ArgumentOutOfRangeException(nameof(origin));

        public override void SetLength(long value)
        {
            _length = checked((int)Math.Max(Math.Min(value, _buffer.Length), 0));
            if (_position > _length)
                _position = _length;
        }

        public void ResetLength(int value)
        {
            _length = Math.Max(Math.Min(value, _buffer.Length), 0);
            _position = 0;
        }

        public override void Write(byte[] buffer, int offset, int count)
            => throw new NotSupportedException();

        public override int ReadByte()
        {
            if (_position == _length)
                return -1;
            return _buffer[_position++];
        }
    }
}
