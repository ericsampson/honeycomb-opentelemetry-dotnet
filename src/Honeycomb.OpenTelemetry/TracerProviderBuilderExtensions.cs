using Microsoft.Extensions.Configuration;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace Honeycomb.OpenTelemetry
{
    /// <summary>
    /// Extension methods to configure <see cref="TracerProviderBuilder"/> to send telemetry data to Honeycomb.
    /// </summary>
    public static class TracerProviderBuilderExtensions
    {
        /// <summary>
        /// Configures the <see cref="TracerProviderBuilder"/> to send telemetry data to Honeycomb using options created from command line arguments.
        /// </summary>
        public static TracerProviderBuilder AddHoneycomb(this TracerProviderBuilder builder, string[] args)
        {
            return builder.AddHoneycomb(HoneycombOptions.FromArgs(args));
        }

        /// <summary>
        /// Configures the <see cref="TracerProviderBuilder"/> to send telemetry data to Honeycomb using options created from an instance of <see cref="IConfiguration"/>.
        /// </summary>
        public static TracerProviderBuilder AddHoneycomb(this TracerProviderBuilder builder, IConfiguration configuration)
        {
            return builder.AddHoneycomb(HoneycombOptions.FromConfiguration(configuration));
        }

        /// <summary>
        /// Configures the <see cref="TracerProviderBuilder"/> to send telemetry data to Honeycomb using an instance of <see cref="HoneycombOptions"/>.
        /// </summary>
        public static TracerProviderBuilder AddHoneycomb(this TracerProviderBuilder builder, HoneycombOptions options)
        {
            if (string.IsNullOrWhiteSpace(options.ApiKey))
                throw new ArgumentException("API key cannot be empty");
            if (string.IsNullOrWhiteSpace(options.Dataset))
                throw new ArgumentException("Dataset cannot be empty");

            builder
                .AddSource(options.ServiceName)
                .SetSampler(new DeterministicSampler(options.SampleRate))
                .AddOtlpExporter(otlpOptions =>
                {
                    otlpOptions.Endpoint = new Uri(options.Endpoint);
                    otlpOptions.Headers = string.Format("x-honeycomb-team={0},x-honeycomb-dataset={1}", options.ApiKey, options.Dataset);
                })
                .SetResourceBuilder(
                    ResourceBuilder
                        .CreateDefault()
                        .AddAttributes(new List<KeyValuePair<string, object>>
                        {
                            new KeyValuePair<string, object>("honeycomb.distro.language", "dotnet"),
                            new KeyValuePair<string, object>("honeycomb.distro.version", GetFileVersion()),
                            new KeyValuePair<string, object>("honeycomb.distro.runtime_version",
                                Environment.Version.ToString()),
                        })
                        .AddEnvironmentVariableDetector()
                        .AddService(serviceName: options.ServiceName, serviceVersion: options.ServiceVersion)
                )
                .AddProcessor(new BaggageSpanProcessor())
                .AddHttpClientInstrumentation()
                .AddSqlClientInstrumentation();

            if (options.RedisConnection != null)
            {
                builder.AddRedisInstrumentation(options.RedisConnection);
            }

#if NET461
            builder.AddAspNetInstrumentation(opts =>
                opts.Enrich = (activity, eventName, _) =>
                {
                    if (eventName == "OnStartActivity")
                        foreach (KeyValuePair<string, string> entry in Baggage.Current)
                        {
                            activity.SetTag(entry.Key, entry.Value);
                        }
                });
#endif

#if NETSTANDARD2_1
            builder.AddGrpcClientInstrumentation();
#endif

            return builder;
        }

        private static string GetFileVersion()
        {
            var version = typeof(TracerProviderBuilderExtensions)
                .Assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                .InformationalVersion;

            // AssemblyInformationalVersionAttribute may include the latest commit hash in
            // the form `{version_prefix}{version_suffix}+{commit_hash}`.
            // We should trim the hash if present to just leave the version prefix and suffix
            var i = version.IndexOf("+");
            return i > 0 
                ? version.Substring(0, i)
                : version;
        }
    }
}