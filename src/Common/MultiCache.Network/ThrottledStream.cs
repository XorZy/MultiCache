namespace MultiCache.Network
{
    using System.Diagnostics;

    public sealed class ThrottledStream : Stream
    {
        private readonly Stopwatch _readSw = new Stopwatch();
        private readonly Stopwatch _writeSw = new Stopwatch();
        private int _readCounter;
        private double _readExtraMs;
        private int _writeCounter;
        private double _writeExtraMs;
        private bool disposedValue;

        public ThrottledStream(
            Stream baseStream,
            Speed readSpeed = default,
            Speed writeSpeed = default,
            bool leaveOpen = false
        )
        {
            BaseStream = baseStream;
            ReadSpeed = readSpeed;
            WriteSpeed = writeSpeed;
            LeaveOpen = leaveOpen;
        }

        public Stream BaseStream { get; }

        public override bool CanRead => BaseStream.CanRead;
        public override bool CanSeek => BaseStream.CanSeek;
        public override bool CanTimeout => base.CanTimeout;
        public override bool CanWrite => BaseStream.CanWrite;
        public bool LeaveOpen { get; }
        public override long Length => BaseStream.Length;

        public override long Position
        {
            get => BaseStream.Position;
            set => BaseStream.Position = value;
        }

        public Speed ReadSpeed { get; set; }

        public override int ReadTimeout
        {
            get => BaseStream.ReadTimeout;
            set => BaseStream.ReadTimeout = value;
        }

        public Speed WriteSpeed { get; set; }

        public override int WriteTimeout
        {
            get => BaseStream.WriteTimeout;
            set => BaseStream.WriteTimeout = value;
        }

        public override async ValueTask DisposeAsync()
        {
            await BaseStream.DisposeAsync().ConfigureAwait(false);
            await base.DisposeAsync().ConfigureAwait(false);
        }

        public override void Flush() => BaseStream.Flush();

        public override Task FlushAsync(CancellationToken cancellationToken) =>
            BaseStream.FlushAsync(cancellationToken);

        public override int Read(byte[] buffer, int offset, int count)
        {
            var read = BaseStream.Read(buffer, offset, count);
            if (!ReadSpeed.IsUnlimited)
            {
                Thread.Sleep(
                    ComputeDelay(_readSw, read, ref _readCounter, ref _readExtraMs, ReadSpeed)
                );
            }
            return read;
        }

        public override async Task<int> ReadAsync(
            byte[] buffer,
            int offset,
            int count,
            CancellationToken cancellationToken
        ) =>
            await ReadAsync(buffer.AsMemory(offset, count), cancellationToken)
                .ConfigureAwait(false);

        public override async ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default
        )
        {
            var read = await BaseStream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (!ReadSpeed.IsUnlimited)
            {
                await Task.Delay(
                        ComputeDelay(_readSw, read, ref _readCounter, ref _readExtraMs, ReadSpeed),
                        cancellationToken
                    )
                    .ConfigureAwait(false);
            }

            return read;
        }

        public override long Seek(long offset, SeekOrigin origin) =>
            BaseStream.Seek(offset, origin);

        public override void SetLength(long value) => BaseStream.SetLength(value);

        public override void Write(byte[] buffer, int offset, int count)
        {
            BaseStream.Write(buffer, offset, count);
            if (!ReadSpeed.IsUnlimited)
            {
                Thread.Sleep(
                    ComputeDelay(_writeSw, count, ref _writeCounter, ref _writeExtraMs, WriteSpeed)
                );
            }
        }

        public override async Task WriteAsync(
            byte[] buffer,
            int offset,
            int count,
            CancellationToken cancellationToken
        ) =>
            await WriteAsync(buffer.AsMemory(offset, count), cancellationToken)
                .ConfigureAwait(false);

        public override async ValueTask WriteAsync(
            ReadOnlyMemory<byte> buffer,
            CancellationToken cancellationToken = default
        )
        {
            await BaseStream.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (!ReadSpeed.IsUnlimited)
            {
                await Task.Delay(
                        ComputeDelay(
                            _writeSw,
                            buffer.Length,
                            ref _writeCounter,
                            ref _writeExtraMs,
                            WriteSpeed
                        ),
                        cancellationToken
                    )
                    .ConfigureAwait(false);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    if (LeaveOpen)
                    {
                        BaseStream.Dispose();
                    }
                }

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                disposedValue = true;
            }

            base.Dispose(disposing);
        }

        private static int ComputeDelay(
            Stopwatch sw,
            int increment,
            ref int counter,
            ref double extraMS,
            Speed nominalSpeed
        )
        {
            if (nominalSpeed.IsUnlimited)
            {
                return 0;
            }

            counter += increment;
            if (counter >= 1) // we measure each X bytes to reduce the measurement error
            {
                var elapsed = sw.Elapsed;
                if (elapsed.TotalMilliseconds > 0)
                {
                    var nominalTimeMs = ((counter * 8.0) / nominalSpeed.BitsPerSecond) * 1000;
                    var difference = nominalTimeMs - elapsed.TotalMilliseconds;
                    if (difference > 0)
                    {
                        extraMS += difference;
                        if (extraMS >= 10) // we wait until we have at least 10ms otherwise the overhead would be too great
                        {
                            var result = (int)Math.Round(extraMS);
                            extraMS = 0;
                            return result;
                        }
                    }
                }

                sw.Restart();
                counter = 0;
            }

            return 0;
        }
    }
}
