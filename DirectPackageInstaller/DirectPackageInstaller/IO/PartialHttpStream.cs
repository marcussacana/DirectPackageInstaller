using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web;

namespace DirectPackageInstaller.IO
{
    public class PartialHttpStream : Stream, IDisposable
    {
        public WebProxy Proxy = null;

        public Action RefreshUrl = null;
        private string FinalURL;
        private string FinalContentType;

        public List<(string Key, string Value)> Headers = new List<(string Key, string Value)>();
        public CookieContainer Cookies = new CookieContainer();
        string _fn;
        public string Filename
        {
            get {
                if (Length == 0)
                    return null;
                return _fn;
            }
        }

        public int Timeout { get; set; }

        public bool TryBypassProxy { get; set; } = false;
        public bool KeepAlive { get; set; } = false;

        private const int CacheLen = 1024 * 8;

        // Cache for short requests.
        private readonly byte[] cache;
        private readonly int cacheLen;
        private Stream stream;
        //private WebResponse response;
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

        ~PartialHttpStream()
        {
            Dispose();
        }

        public string Url { get; protected set; }

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

            if (count > cacheLen)
            {
                int EmptyTries = 3;
                // large request, do not cache
                while (count > 0)
                {
                    int Readed;
                    Position += Readed = HttpRead(buffer, ref offset, ref count);

                    if (Readed == 0 && EmptyTries-- < 0)
                        break;
                    else if (Readed > 0)
                        EmptyTries = 3;
                }
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

        public new async Task<int> ReadAsync(byte[] buffer, int offset, int count)
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

            if (count > cacheLen)
            {
                // large request, do not cache
                while (count > 0)
                {
                    var Result = await HttpReadAsync(buffer, offset, count);
                    Position += Result.Readed;
                    offset = Result.Offset;
                    count = Result.Count;
                }
            }
            else if (count > 0)
            {
                // read to cache
                cachePosition = Position;

                var Result = await HttpReadAsync(buffer, offset, count);
                cacheCount = Result.Readed;
                offset = Result.Offset;
                count = Result.Count;

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

        HttpWebRequest req = null;
        WebResponse resp = null;
        Stream ResponseStream = null;
        long RespPos = 0;

        private int HttpRead(byte[] buffer, ref int offset, ref int count, int Tries = 0)
        {
            try
            {
                if (RespPos != Position || ResponseStream == null)
                {
                    HttpRequestsCount++;

                    if (ResponseStream != null)
                    {
                        ResponseStream?.Close();
                        ResponseStream?.Dispose();
                    }


                    if (req != null)
                        req.ServicePoint.CloseConnectionGroup(req.ConnectionGroupName);


                    if (resp != null)
                    {
                        resp?.Close();
                        resp?.Dispose();
                    }

                    req = HttpWebRequest.CreateHttp(Url);
                    req.ConnectionGroupName = new Guid().ToString();
                    req.CookieContainer = Cookies;
                    req.CachePolicy = new System.Net.Cache.RequestCachePolicy(System.Net.Cache.RequestCacheLevel.NoCacheNoStore);

                    req.KeepAlive = KeepAlive;
                    req.ServicePoint.SetTcpKeepAlive(KeepAlive, 1000 * 60 * 5, 1000);


                    if (!TryBypassProxy || (TryBypassProxy && Tries >= 2))
                        req.Proxy = Proxy;

                    foreach (var Header in Headers)
                        req.Headers[Header.Key] = Header.Value;

                    if (length != null || Position > 0)
                        req.AddRange(Position, Length - 1);

                    resp = req.GetResponse();

                    if (length != null && FinalURL != resp.ResponseUri.AbsoluteUri)
                    {
                        if (resp.ContentType.Contains("text/html") && resp.ContentType != FinalContentType)
                        {
                            FinalURL = resp.ResponseUri.AbsoluteUri;
                            FinalContentType = resp.ContentType;
                            throw new WebException("Link Expired?");
                        }
                    }

                    FinalURL = resp.ResponseUri.AbsoluteUri;
                    FinalContentType = resp.ContentType;

                    if (length == null)
                        ReadResponseInfo(resp);

                    ResponseStream = resp.GetResponseStream();
                    ResponseStream = new BufferedStream(ResponseStream);
                }

                int nread = 0;

                int Readed = 0;

                
                do
                {
                    Readed = ResponseStream.Read(buffer, offset + nread, count - nread);
                    nread += Readed;
                } while (Readed > 0 && count > 0);

                offset += nread;
                count -= nread;
                
                if (App.IsUnix && nread == 0 && count > 0)
                    throw new WebException();

                RespPos = Position + nread;

                return nread;

            }
            catch (IOException ex)
            {
                ResponseStream?.Dispose();
                ResponseStream = null;

                if (TryBypassProxy ? Tries < 5 : Tries < 3)
                {
                    RefreshUrl?.Invoke();
                    return HttpRead(buffer, ref offset, ref count, Tries + 1);
                }

                throw;
            }
            catch (Exception ex)
            {
                if (req != null)
                    req.ServicePoint.CloseConnectionGroup(req.ConnectionGroupName);

                ResponseStream?.Dispose();
                ResponseStream = null;

                if (TryBypassProxy ? Tries < 5 : Tries < 3)
                {
                    RefreshUrl?.Invoke();
                    return HttpRead(buffer, ref offset, ref count, Tries + 1);
                }
                
                if (ex is WebException)
                {
                    var response = (HttpWebResponse)((WebException)ex).Response;
                    if (response?.StatusCode == HttpStatusCode.RequestedRangeNotSatisfiable)
                        return 0;
                }

                throw;
            }
        }

        private async Task<(int Readed, int Offset, int Count)> HttpReadAsync(byte[] buffer, int offset, int count, int Tries = 0)
        {
            try
            {
                if (RespPos != Position || ResponseStream == null)
                {
                    HttpRequestsCount++;

                    if (ResponseStream != null)
                    {
                        ResponseStream?.Close();
                        ResponseStream?.Dispose();
                    }

                    if (req != null)
                        req.ServicePoint.CloseConnectionGroup(req.ConnectionGroupName);

                    if (resp != null)
                    {
                        resp?.Close();
                        resp?.Dispose();
                    }

                    req = HttpWebRequest.CreateHttp(Url);
                    req.CookieContainer = Cookies;
                    req.ConnectionGroupName = new Guid().ToString();
                    req.CachePolicy = new System.Net.Cache.RequestCachePolicy(System.Net.Cache.RequestCacheLevel.NoCacheNoStore);



                    req.KeepAlive = KeepAlive;
                    req.ServicePoint.SetTcpKeepAlive(KeepAlive, 1000 * 60 * 5, 1000);

                    if (!TryBypassProxy || (TryBypassProxy && Tries >= 2))
                        req.Proxy = Proxy;


                    foreach (var Header in Headers)
                        req.Headers[Header.Key] = Header.Value;

                    if (length != null || Position > 0)
                        req.AddRange(Position, Length - 1);

                    resp = await req.GetResponseAsync();

                    if (length != null && FinalURL != resp.ResponseUri.AbsoluteUri)
                    {
                        if (resp.ContentType.Contains("text/html") && resp.ContentType != FinalContentType)
                        {
                            FinalURL = resp.ResponseUri.AbsoluteUri;
                            FinalContentType = resp.ContentType;
                            throw new WebException("Link Expired?");
                        }
                    }

                    FinalURL = resp.ResponseUri.AbsoluteUri;
                    FinalContentType = resp.ContentType;

                    if (length == null)
                        ReadResponseInfo(resp);

                    ResponseStream = resp.GetResponseStream();
                    ResponseStream.ReadTimeout = (int)TimeSpan.FromMinutes(1).TotalMilliseconds;
                    ResponseStream = new BufferedStream(ResponseStream);
                }

                int nread = 0;

                int Readed = 0;
                do
                {
                    Readed = await ResponseStream.ReadAsync(buffer, offset + nread, count - nread);
                    nread += Readed;
                } while (Readed > 0 && count > 0);

                offset += nread;
                count -= nread;

                RespPos = Position + nread;

                return (nread, offset, count);

            }
            catch (IOException ex)
            {
                if (req != null)
                    req.ServicePoint.CloseConnectionGroup(req.ConnectionGroupName);

                ResponseStream?.Close();
                ResponseStream?.Dispose();
                ResponseStream = null;

                if (TryBypassProxy ? Tries < 5 : Tries < 3)
                {
                    RefreshUrl?.Invoke();
                    return await HttpReadAsync(buffer, offset, count, Tries + 1).ConfigureAwait(false);
                }

                throw;
            }
            catch (Exception ex)
            {
                if (req != null)
                    req.ServicePoint.CloseConnectionGroup(req.ConnectionGroupName);

                ResponseStream?.Close();
                ResponseStream?.Dispose();
                ResponseStream = null;

                if (TryBypassProxy ? Tries < 5 : Tries < 3)
                {
                    RefreshUrl?.Invoke();
                    return await HttpReadAsync(buffer, offset, count, Tries + 1).ConfigureAwait(false);
                }

                if (ex is WebException)
                {
                    var response = (HttpWebResponse)((WebException)ex).Response;
                    if (response?.StatusCode == HttpStatusCode.RequestedRangeNotSatisfiable)
                        return (0, offset, count);
                }

                throw;
            }
        }

        static Dictionary<string, (string Filename, long Length)> HeadCache = new Dictionary<string, (string Filename, long Length)>();
        
        private long HttpGetLength(bool NoHead = false)
        {
            if (HeadCache.ContainsKey(Url))
            {
                _fn = HeadCache[Url].Filename;
                return HeadCache[Url].Length;
            }

            HttpRequestsCount++;
            try
            {
                HttpWebRequest request = WebRequest.CreateHttp(Url);
                request.ConnectionGroupName = new Guid().ToString();
                request.KeepAlive = false;
                request.CookieContainer = Cookies;
                request.Proxy = Proxy;
                request.Method = NoHead ? "GET" : "HEAD";


                foreach (var Header in Headers)
                    request.Headers[Header.Key] = Header.Value;

                using var response = request.GetResponse();
                ReadResponseInfo(response);
                response?.Close();

                request.ServicePoint.CloseConnectionGroup(request.ConnectionGroupName);
            }
            catch
            {
                return HttpGetLength(true);
            }

            return Length;
        }

        void ReadResponseInfo(WebResponse Response)
        {
            FinalURL = Response.ResponseUri.AbsoluteUri;
            FinalContentType = Response.ContentType;

            if (Response.Headers.AllKeys.Contains("Content-Disposition"))
            {
                _fn = Response.Headers["Content-Disposition"];
                const string prefix = "filename=";
                _fn = _fn.Substring(_fn.IndexOf(prefix) + prefix.Length).Trim('"');
                _fn = HttpUtility.UrlDecode(_fn.Split(';').First().Trim('"'));
            }

            var Length = Response.ContentLength;

            length = Length;

            HeadCache[Url] = (_fn, Length);
        }

        private new void Dispose()
        {
            if (stream != null)
            {
                stream.Dispose();
                stream = null;
            }

            if (ResponseStream != null)
            {
                ResponseStream.Close();
                ResponseStream?.Dispose();
                ResponseStream = null;
            }

            if (resp != null)
            {
                resp?.Close();
                resp?.Dispose();
                resp = null;
            }

            base.Dispose();
        }
    }
}
