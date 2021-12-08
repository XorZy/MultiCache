namespace MultiCache
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Net.Http;
    using System.Threading.Tasks;
    using MultiCache.Config;
    using MultiCache.Helpers;
    using MultiCache.PackageManager;

    public class Proxy
    {
        private readonly AppConfiguration _config;
        private readonly Dictionary<string, PackageManagerBase> _pkgManagers;

        public Proxy(
            AppConfiguration configuration,
            Dictionary<string, PackageManagerBase> pkgManagers
        )
        {
            _config = configuration;
            _pkgManagers = pkgManagers;
        }

        public async Task RunAsync()
        {
            using (var server = new HttpListener())
            {
                server.Prefixes.Add($"http://{_config.Hostname}:{_config.Port}/");

                server.Start();

                Log.Put("The following repositories are up and running:", LogLevel.Info);
                foreach (var pkgManager in _pkgManagers)
                {
                    Log.Put(
                        $"\t[bold mediumpurple1]{pkgManager.Key}[/]=>http://{_config.Hostname}:{_config.Port}/{pkgManager.Key}/$repo/$arch",
                        LogLevel.Info
                    );
                }

                if (_pkgManagers.Count == 0)
                {
                    Log.Put("No repositories found, check your configuration!", LogLevel.Error);
                }

                while (true)
                {
                    var incomingContext = await server.GetContextAsync().ConfigureAwait(false);
                    // we do not await so the request can be handled asynchronously
                    HandleRequestAsync(incomingContext);
                }
            }
        }

        private static (string prefix, string resource) ParseRequest(string resource)
        {
            var tmp = resource.Split('/', 3);
            if (tmp.Length != 3)
            {
                throw new ArgumentException("Invalid request");
            }

            return (tmp[1], "/" + tmp[2]);
        }

        private async Task HandleRequestAsync(HttpListenerContext incomingContext)
        {
            using (var clientResponse = incomingContext.Response)
            {
                var request = incomingContext.Request.Url?.AbsolutePath;
                if (string.IsNullOrWhiteSpace(request))
                {
                    clientResponse.StatusCode = (int)HttpStatusCode.BadRequest;
                    return;
                }

                var (prefix, requestedResource) = ParseRequest(request);

                if (!_pkgManagers.ContainsKey(prefix))
                {
                    incomingContext.Response.StatusCode = (int)HttpStatusCode.NotFound;
                    incomingContext.Response.StatusDescription = "Unknown prefix";
                    return;
                }

                var assignedRepository = _pkgManagers[prefix];

                if (requestedResource.StartsWith("/api/", StringComparison.OrdinalIgnoreCase))
                {
                    if (assignedRepository.Api is null)
                    {
                        incomingContext.Response.StatusCode = (int)HttpStatusCode.Forbidden;
                        incomingContext.Response.StatusDescription =
                            "The API endpoint has been disabled for this repository";
                        return;
                    }

                    await assignedRepository.Api
                        .HandleApiCall(requestedResource, incomingContext)
                        .ConfigureAwait(false);
                    return;
                }

                if (
                    !incomingContext.Request.HttpMethod.Equals(
                        "GET",
                        StringComparison.OrdinalIgnoreCase
                    )
                )
                {
                    clientResponse.StatusCode = (int)HttpStatusCode.Forbidden;
                    clientResponse.StatusDescription = "Only GET methods are allowed";
                    return;
                }

                try
                {
                    await assignedRepository
                        .HandleRequestAsync(
                            new Uri(requestedResource, UriKind.Relative),
                            incomingContext
                        )
                        .ConfigureAwait(false);
                }
                catch (HttpRequestException hEx)
                {
                    clientResponse.StatusCode = (int)(
                        hEx.StatusCode ?? HttpStatusCode.InternalServerError
                    );
                    clientResponse.StatusDescription = hEx.Message;
                }
                catch (Exception ex)
                {
                    Log.Put(ex);
                    clientResponse.StatusCode = (int)HttpStatusCode.InternalServerError;
                    throw;
                }
            }
        }
    }
}
