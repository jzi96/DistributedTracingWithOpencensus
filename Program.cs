using Microsoft.ApplicationInsights.Extensibility;
using OpenCensus.Exporter.ApplicationInsights;
using OpenCensus.Exporter.Zipkin;
using OpenCensus.Stats;
using OpenCensus.Trace;
using OpenCensus.Trace.Config;
using OpenCensus.Trace.Sampler;
using System;

namespace distributedtracing
{
    class Program
    {
        static void Main(string[] args)
        {
            string z = args.Length > 0 ? args[0] : null;
            string i = args.Length > 1 ? args[1] : null;
            ConfigureOpencensus(z, i);

            var tracer = Tracing.Tracer;
            using(var span = tracer.SpanBuilder("rootspan").StartScopedSpan())
            {
                Console.WriteLine("myoperations");
                RunChildOperation();
                RunChildOperation();
            }
        }

        private static void RunChildOperation(int level = 1)
        {
            var tracer = Tracing.Tracer;
            using (var span = tracer.SpanBuilder("op on level " + level).StartScopedSpan())
            {
                string space = string.Empty;
                for (int i = 0; i < level; i++)
                {
                    space += "  ";
                }
                Console.WriteLine(space + " level op");
                if(level<5)
                    RunChildOperation(level+1);
            }
        }

        private static void ConfigureOpencensus(string zipkinUri, string appInsightsKey)
        {
            if (!string.IsNullOrEmpty(zipkinUri))
            {
                var zipK = new ZipkinTraceExporter(
                    new ZipkinTraceExporterOptions()
                    {
                        Endpoint = new Uri(zipkinUri),
                        ServiceName = "tracing-to-zipkin-service",
                    },
                    Tracing.ExportComponent
                    );
                zipK.Start();
            }
            if (!string.IsNullOrEmpty(appInsightsKey))
            {
                var config = new TelemetryConfiguration(appInsightsKey);
                var appI = new ApplicationInsightsExporter(
                    Tracing.ExportComponent,
                    Stats.ViewManager,
                    config);
                appI.Start();
            }
            // 2. Configure 100% sample rate for the purposes of the demo
            ITraceConfig traceConfig = Tracing.TraceConfig;
            ITraceParams currentConfig = traceConfig.ActiveTraceParams;
            var newConfig = currentConfig.ToBuilder()
                .SetSampler(Samplers.AlwaysSample)
                .Build();
            traceConfig.UpdateActiveTraceParams(newConfig);

        }
    }
}
