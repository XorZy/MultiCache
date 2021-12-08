namespace MultiCache.Api
{
    using MultiCache.PackageManager;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Threading.Tasks;

    public class ApiProvider
    {
        private readonly PackageManagerBase _pkgManager;

        public ApiProvider(PackageManagerBase pkgManager)
        {
            _pkgManager = pkgManager;
        }

        public async Task HandleApiCall(
            string requestedResource,
            HttpListenerContext incomingContext
        )
        {
            switch (requestedResource.ToLowerInvariant())
            {
                case "/api/packages":
                    await HandlePackageEndpoint(incomingContext).ConfigureAwait(false);
                    break;

                case "/api/maintain":
                    _pkgManager.MaintainAsync(); // this could take a while so we don't await
                    incomingContext.Response.StatusCode = (int)HttpStatusCode.NoContent;
                    break;

                default:
                    incomingContext.Response.StatusCode = (int)HttpStatusCode.NotFound;
                    break;
            }
        }

        private async Task HandlePackageEndpoint(HttpListenerContext incomingContext)
        {
            var architecture = incomingContext.Request.QueryString["arch"];
            if (architecture is null)
            {
                incomingContext.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                incomingContext.Response.StatusDescription = "Parameter {arch} must be provided";
                return;
            }

            _ = bool.TryParse(incomingContext.Request.QueryString["now"], out var now);
            switch (incomingContext.Request.HttpMethod)
            {
                case "POST":
                    incomingContext.Response.StatusCode = (int)HttpStatusCode.NoContent;
                    var packages = new List<string>();
                    using (var inputStream = incomingContext.Request.InputStream)
                    {
                        using (var reader = new StreamReader(inputStream))
                        {
                            while (true)
                            {
                                var line = await reader.ReadLineAsync().ConfigureAwait(false);
                                if (line is null)
                                {
                                    break;
                                }

                                var tmp = line.Split();
                                packages.Add(tmp[0]);
                            }
                        }

                        await _pkgManager
                            .SeedPackagesAsync(architecture, packages)
                            .ConfigureAwait(false);
                        if (now)
                        {
                            _pkgManager.MaintainAsync().ConfigureAwait(false); // this could take a while so we don't await
                        }
                    }
                    break;

                case "GET":
                    incomingContext.Response.ContentType = "application/json; charset=utf-8";

                    using (var outputSream = incomingContext.Response.OutputStream)
                    {
                        var localPackages = _pkgManager.PackageStorage
                            .GetStoredPackages()
                            .Select(
                                x =>
                                    new
                                    {
                                        name = x.Package.Name,
                                        architecture = x.Package.Architecture,
                                        versions = x.StoredVersions.Select(
                                            x =>
                                                new
                                                {
                                                    version = x.Package.Version.VersionString,
                                                    downloaded = x.FullFile.Exists,
                                                }
                                        ),
                                    }
                            );
                        await System.Text.Json.JsonSerializer
                            .SerializeAsync(outputSream, localPackages)
                            .ConfigureAwait(false);
                    }
                    break;

                default:
                    incomingContext.Response.StatusCode = (int)HttpStatusCode.NotFound;
                    break;
            }
        }
    }
}
