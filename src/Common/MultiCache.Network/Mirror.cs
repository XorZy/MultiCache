namespace MultiCache.Network
{
    using System.Net;
    using System.Net.NetworkInformation;

    public class Mirror
    {
        public Mirror(Uri uri)
        {
            RootUri = uri;
        }

        public static Mirror Parse(string uri) => new Mirror(new Uri(uri));

        public Uri RootUri { get; }

        public async Task<PingReply> PingAsync(int timeout = 2000)
        {
            using (var ping = new Ping())
            {
                return await ping.SendPingAsync(RootUri.Host, timeout).ConfigureAwait(false);
            }
        }

        // sometimes, servers may respond to ping but not to http(s) so we make sure everything works
        public async Task<bool> TryConnectAsync(
            HttpClient client,
            CancellationToken ct = default,
            int timeoutMs = 15000
        )
        {
            try
            {
                using (var timeoutCtSource = new CancellationTokenSource(timeoutMs))
                {
                    using (
                        var combinedCtSource = CancellationTokenSource.CreateLinkedTokenSource(
                            ct,
                            timeoutCtSource.Token
                        )
                    )
                    {
                        using (var headRequest = new HttpRequestMessage(HttpMethod.Head, RootUri))
                        {
                            var reply = await client
                                .SendAsync(
                                    headRequest,
                                    HttpCompletionOption.ResponseHeadersRead,
                                    combinedCtSource.Token
                                )
                                .ConfigureAwait(false);
                            return reply.StatusCode != HttpStatusCode.Forbidden
                                && reply.StatusCode != HttpStatusCode.ServiceUnavailable
                                && reply.StatusCode != HttpStatusCode.InternalServerError
                                && reply.StatusCode != HttpStatusCode.GatewayTimeout
                                && reply.StatusCode != HttpStatusCode.BadGateway;
                        }
                    }
                }
            }
            catch
            {
                return false;
            }
        }

        public override string ToString() => RootUri.AbsoluteUri;
    }
}
