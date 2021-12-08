namespace MultiCache.Config
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Reflection;
    using System.Text.Json.Serialization;
    using Common.MultiCache.Config;
    using MultiCache.Models;
    using MultiCache.Network;
    using MultiCache.Scheduling;
    using MultiCache.Utils;

    /// <summary>
    /// A configuration parser with limited use of reflection
    /// </summary>
    public static class StaticConfigurationParser
    {
        public static AppConfiguration LoadAppConfigFromFile(string path) =>
            ParseAppConfiguration(File.ReadAllText(path));

        public static AppConfiguration ParseAppConfiguration(string configuration)
        {
            var dic = ParseFile(configuration);
            var appConfig = new AppConfiguration(dic["CachePath"]);
            Assign(appConfig, dic);

#pragma warning disable CA2000
            appConfig.HttpClient = new HttpClient(
                new HttpClientHandler()
                {
                    Proxy = appConfig.Proxy,
                    UseProxy = appConfig.Proxy is not null,
                    CheckCertificateRevocationList = true
                }
            );
#pragma warning restore CA2000
            return appConfig;
        }

        public static RepositoryConfiguration ParseRepositoryConfiguration(
            string packageManagerString,
            string prefix,
            string configuration,
            AppConfiguration appConfiguration
        )
        {
            var dic = ParseFile(configuration);
            var packageManagerType = Enum.Parse<PackageManagerType>(packageManagerString, true);
            var config = new RepositoryConfiguration(packageManagerType, prefix, appConfiguration);
            Assign(config, dic);
            return config;
        }

        public static RepositoryConfiguration ParseRepositoryConfiguration(
            FileInfo configFile,
            AppConfiguration appConfiguration
        )
        {
            return ParseRepositoryConfiguration(
                configFile.Extension[1..],
                Path.GetFileNameWithoutExtension(configFile.Name),
                File.ReadAllText(configFile.FullName),
                appConfiguration
            );
        }

        private static string? SerializeObject(object o)
        {
            if (o is ISimpleSerializable i)
            {
                return i.Serialize();
            }
            if (o is WebProxy proxy)
            {
                if (proxy.Credentials is null)
                {
                    return $"{proxy.Address}";
                }
            }
            if (o is IConvertible iConv)
            {
                iConv.ToString(CultureInfo.InvariantCulture);
            }

            return o.ToString();
        }

        private static IDictionary<string, string> ToDic(
            object source,
            object? defaultReference = null
        )
        {
            var output = new Dictionary<string, string>();
            foreach (
                var property in source
                    .GetType()
                    .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            )
            {
                if (Attribute.IsDefined(property, typeof(JsonIgnoreAttribute)))
                {
                    continue;
                }

                var value = property.GetValue(source);

                if (defaultReference is not null)
                {
                    if (
                        property.GetValue(source)?.Equals(property.GetValue(defaultReference))
                        == true
                    )
                    {
                        //we avoid saving default values
                        continue;
                    }
                }
                if (value is not null)
                {
                    if (value is IEnumerable<object> iEnum)
                    {
                        if (iEnum.Any())
                        {
                            output[property.Name] = string.Join(
                                ",",
                                iEnum.Select(x => SerializeObject(x))
                            );
                        }
                    }
                    else
                    {
                        var serializedValue = SerializeObject(value);
                        if (serializedValue is not null)
                        {
                            output[property.Name] = serializedValue;
                        }
                    }
                }
            }
            return output;
        }
        public static void SerializeConfigFile(
            object source,
            string destination,
            object? defaultReference = null
        )
        {
            File.WriteAllLines(
                destination,
                ToDic(source, defaultReference).Select((x) => $"{x.Key}={x.Value}")
            );
        }
        private static object DeserializeObject(string value, Type type)
        {
            #region COMMON_PRIMITIVES

            if (type == typeof(bool))
            {
                return bool.Parse(value);
            }

            if (type == typeof(int?) || type == typeof(int))
            {
                return value.ToIntInvariant();
            }
            if (type == typeof(long?) || type == typeof(long))
            {
                return value.ToLongInvariant();
            }
            if (type == typeof(string))
            {
                return value;
            }
            #endregion

            if (type == typeof(List<SchedulingOptions>))
            {
                return value
                    .Split(',')
                    .Select(x => (SchedulingOptions)DeserializeObject(x, typeof(SchedulingOptions)))
                    .ToList();
            }
            if (type == typeof(List<Mirror>))
            {
                return value
                    .Split(',')
                    .Select(x => (Mirror)DeserializeObject(x, typeof(Mirror)))
                    .ToList();
            }
            if (type == typeof(Speed))
            {
                return Speed.Parse(value);
            }
            if (type == typeof(Mirror))
            {
                return Mirror.Parse(value);
            }
            if (type == typeof(SchedulingOptions))
            {
                return SchedulingOptions.Parse(value);
            }
            else if (type.IsSubclassOf(typeof(Enum)))
            {
                return Enum.Parse(type, value, true);
            }
            else if (type == typeof(WebProxy))
            {
                var split = value.Split(';');
                var address = split[0];
                var proxy = new WebProxy(address);
                if (split.Length == 2)
                {
                    proxy.Credentials = new NetworkCredential(userName: split[1], string.Empty);
                }
                if (split.Length == 3)
                {
                    proxy.Credentials = new NetworkCredential(userName: split[1], split[1]);
                }

                return proxy;
            }
            throw new ArgumentException($"Unhandled type {type}");
        }

        private static void Assign(object destination, IDictionary<string, string> configuration)
        {
            foreach (var (key, value) in configuration)
            {
                var type = destination.GetType().GetProperty(key);
                if (type is null)
                    throw new ArgumentException($"Unknown parameter {key}");
                if (Attribute.IsDefined(type, typeof(JsonIgnoreAttribute)))
                    throw new ArgumentException($"Parameter {key} is not settable");
                type.SetValue(destination, DeserializeObject(value, type.PropertyType));
            }
        }
        private static Dictionary<string, string> ParseFile(string data)
        {
            var lines = data.Split(
                Environment.NewLine,
                StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries
            );
            return lines
                .Where(x => x[0] != '#')
                .Select(x => x.Split("=").Select(y => y.Trim()).ToArray())
                .ToDictionary(
                    x => x[0],
                    x => x[1].Split('#')[0].Trim(),
                    StringComparer.OrdinalIgnoreCase
                );
        }
    }
}
