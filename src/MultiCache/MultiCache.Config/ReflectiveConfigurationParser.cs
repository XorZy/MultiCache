/*namespace MultiCache.Config
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Reflection;
    using System.Text.Json.Serialization;
    using Common.MultiCache.Config;
    using Cronos;
    using MultiCache.Models;

    public static class ReflectiveConfigurationParser
    {
        public static AppConfiguration AppFromFile(string path) =>
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

        private static string CleanupCron(CronExpression expression)
        {
            var split = expression.ToString().Split();
            // for some reasons seconds are included
            // so we get rid of them
            return string.Join(" ", split[1..]);
        }

        private static string SerializeObject(object o)
        {
            if (o is CronExpression cExp)
            {
                return CleanupCron(cExp);
            }
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
                else { }
            }

            var toStringInvariant = o.GetType()
                .GetMethod(
                    "ToString",
                    BindingFlags.Static | BindingFlags.Public,
                    new[] { typeof(IFormatProvider) }
                );
            if (toStringInvariant is not null)
            {
                return (string)toStringInvariant.Invoke(o, new[] { CultureInfo.InvariantCulture });
            }

            return o.ToString();
        }

        private static IDictionary<string, string> ToDic(object source)
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
                        output[property.Name] = SerializeObject(value);
                    }
                }
            }
            return output;
        }

        public static void SerializeConfigFile(object source, string destination)
        {
            File.WriteAllLines(destination, ToDic(source).Select((x) => $"{x.Key}={x.Value}"));
        }

        private static object Deserialize(string value, Type type)
        {
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
            {
                return Deserialize(value, type.GetGenericArguments()[0]);
            }
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
            {
                IList list = (IList)Activator.CreateInstance(type);
                foreach (
                    var option in value
                        .Split(',')
                        .Select(x => Deserialize(x, type.GetGenericArguments()[0]))
                )
                {
                    list.Add(option);
                }
                return list;
            }
            if (type == typeof(string))
            {
                return value;
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
            else
            {
                const BindingFlags bindings = BindingFlags.Static | BindingFlags.Public;
                var parseMethodInvariant = type.GetMethod(
                    "Parse",
                    bindings,
                    new[] { typeof(string), typeof(IFormatProvider) }
                );
                if (parseMethodInvariant is not null)
                {
                    return parseMethodInvariant.Invoke(
                        null,
                        new object[] { value, CultureInfo.InvariantCulture }
                    );
                }
                var parseMethod = type.GetMethod("Parse", bindings, new[] { typeof(string) });
                if (parseMethod is not null)
                {
                    return parseMethod.Invoke(null, new[] { value });
                }
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
                type.SetValue(destination, Deserialize(value, type.PropertyType));
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
*/
