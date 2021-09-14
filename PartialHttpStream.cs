using System;
using System.IO;
using System.Net;

namespace DirectPackageInstaller
{
    //98% Stolen From: https://codereview.stackexchange.com/a/204766
    public class PartialHttpStream : Stream, IDisposable
    {
        private const int CacheLen = 1024 * 100;

        // Cache for short requests.
        private readonly byte[] cache;
        private readonly int cacheLen;
        private Stream stream;
        //private WebResponse response;
        private long position = 0;
        private long? length;
        private long cachePosition;
        private int cacheCount;

        public PartialHttpStream(string url, int cacheLen = CacheLen)
        {
            if (string.IsNullOrEmpty(url))
                throw new ArgumentException("url empty");
            if (cacheLen <= 0)
                throw new ArgumentException("cacheLen must be greater than 0");

            Url = url;
            this.cacheLen = cacheLen;
            cache = new byte[cacheLen];
        }

        public string Url { get; private set; }

        public override bool CanRead { get { return true; } }
        public override bool CanWrite { get { return false; } }
        public override bool CanSeek { get { return true; } }

        public override long Position { get; set; }

        /// <summary>
        /// Lazy initialized length of the resource.
        /// </summary>
        public override long Length
        {
            get
            {
                if (length == null)
                    length = HttpGetLength();
                return length.Value;
            }
        }

        /// <summary>
        /// Count of HTTP requests. Just for statistics reasons.
        /// </summary>
        public int HttpRequestsCount { get; private set; }

        public override void SetLength(long value)
        { throw new NotImplementedException(); }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (count == 0)
                return 0;

            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));
            if (offset < 0 || offset >= buffer.Length)
                throw new ArgumentException(nameof(offset));
            if (count < 0 || offset + count > buffer.Length)
                throw new ArgumentException(nameof(count));

            long curPosition = Position;
            Position += ReadFromCache(buffer, ref offset, ref count);
            //if (Position > curPosition)
            //    Console.WriteLine($"Cache hit {Position - curPosition}");
            if (count > cacheLen)
            {
                // large request, do not cache
                while (count > 0)
                    Position += HttpRead(buffer, ref offset, ref count);
            }
            else if (count > 0)
            {
                // read to cache
                cachePosition = Position;
                int off = 0;
                int len = cacheLen;
                cacheCount = HttpRead(cache, ref off, ref len);
                Position += ReadFromCache(buffer, ref offset, ref count);
            }

            return (int)(Position - curPosition);
        }

        public override void Write(byte[] buffer, int offset, int count)
        { throw new NotImplementedException(); }

        public override long Seek(long pos, SeekOrigin origin)
        {
            switch (origin)
            {
                case SeekOrigin.End:
                    Position = Length + pos;
                    break;

                case SeekOrigin.Begin:
                    Position = pos;
                    break;

                case SeekOrigin.Current:
                    Position += pos;
                    break;
            }
            return Position;
        }

        public override void Flush()
        {
        }

        private int ReadFromCache(byte[] buffer, ref int offset, ref int count)
        {
            if (cachePosition > Position || (cachePosition + cacheCount) <= Position)
                return 0; // cache miss
            int ccOffset = (int)(Position - cachePosition);
            int ccCount = Math.Min(cacheCount - ccOffset, count);
            Array.Copy(cache, ccOffset, buffer, offset, ccCount);
            offset += ccCount;
            count -= ccCount;
            return ccCount;
        }

        private int HttpRead(byte[] buffer, ref int offset, ref int count, int Tries = 0)
        {
            HttpRequestsCount++;
            HttpWebRequest req = HttpWebRequest.CreateHttp(Url);
            req.AddRange(Position, Position + count - 1);
            try
            {
                using var response = req.GetResponse();


                int nread = 0;
                using (Stream stream = response.GetResponseStream())
                {
                    int Readed = 0;
                    do
                    {
                        Readed = stream.Read(buffer, offset + nread, count - nread);
                        nread += Readed;
                    } while (Readed > 0 && count > 0);
                }

                offset += nread;
                count -= nread;
                return nread;
            }
            catch (WebException ex)
            {
                if (Tries < 2)
                    return HttpRead(buffer, ref offset, ref count, Tries + 1);

                var response = (HttpWebResponse)ex.Response;
                if (response?.StatusCode == HttpStatusCode.RequestedRangeNotSatisfiable)
                    return 0;
                throw ex;
            }
        }

        private long HttpGetLength()
        {
            HttpRequestsCount++;
            HttpWebRequest request = HttpWebRequest.CreateHttp(Url);
            request.Method = "HEAD";
            return request.GetResponse().ContentLength;
        }

        private new void Dispose()
        {
            base.Dispose();
            if (stream != null)
            {
                stream.Dispose();
                stream = null;
            }
        }
    }
}
