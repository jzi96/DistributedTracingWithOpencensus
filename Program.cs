using Microsoft.ApplicationInsights.Extensibility;
using OpenCensus.Exporter.ApplicationInsights;
using OpenCensus.Exporter.Zipkin;
using OpenCensus.Stats;
using OpenCensus.Trace;
using OpenCensus.Trace.Config;
using OpenCensus.Trace.Sampler;
using System;

//Metric

using OpenCensus.Stats;
using OpenCensus.Stats.Aggregations;
using OpenCensus.Stats.Measurements;
using OpenCensus.Tags;
using OpenCensus.Stats.Measures;

namespace distributedtracing
{
    class Program
    {
        static IMeasureLong numberofInvocations = MeasureLong.Create("operation execs", "Number of invokations of operations", "Calls");
        static ITagKey tagEnv = TagKey.Create("environment");
        static ITagKey tagMachine = TagKey.Create("machine");

        static ITagContext tagContext = Tags.Tagger.CurrentBuilder
            .Put(tagEnv, TagValue.Create("demo"))
            .Put(tagMachine,TagValue.Create(Environment.MachineName))
            .Build();

        static IStatsRecorder recorder = Stats.StatsRecorder;
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

            //Force send of all data; gacefull shutdown
            Tracing.ExportComponent.SpanExporter.Dispose();
        }

        private static void RunChildOperation(int level = 1)
        {
            //increment the counter
            Stats.StatsRecorder.NewMeasureMap()
                .Put(numberofInvocations, 1)
                .Record(tagContext);
            var tracer = Tracing.Tracer;
            using (var span = tracer.SpanBuilder("op on level " + level).StartScopedSpan())
            {
                string space = string.Empty;
                for (int i = 0; i < level; i++)
                {
                    space += "  ";
                }
                Console.WriteLine(space + " level op " + level);
                if (level < 3)
                    RunChildOperation(level + 1);
                if (level < 5)
                    RunChildOperation(level + 1);
            }
        }

        private static void ConfigureOpencensus(string zipkinUri, string appInsightsKey)
        {
            //if we want to have multiple tracers
            //we should use agent mode and send first to agent

            var ocExp = new OpenCensus.Exporter.Ocagent.OcagentExporter(
                Tracing.ExportComponent
                , "http://localhost:55678"
                , Environment.MachineName
                , "distributedtracingdemo"
                );
            ocExp.Start();
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
            
            //Metrics
            //define the measure to be used
            // context fields defined, that will be attached
            var invokeView = View.Create(ViewName.Create("executioncounts"), "Number of execs", numberofInvocations, Count.Create(), new[] { tagEnv, tagMachine });
            //register
            Stats.ViewManager.RegisterView(invokeView);

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
