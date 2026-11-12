using System.Collections.Generic;
using System.Linq;
using Typeshift.Generators.Models;
using Typeshift.Generators.Parsing;

namespace Typeshift.Generators.Emitting;

internal static class InterceptorEmitter
{
    private const string Services = "global::Microsoft.Extensions.DependencyInjection.IServiceCollection";

    private const string Builder = "global::Typeshift.TypeshiftBuilder";

    // D1: every call in the project registers this assembly's module and those of its references; D75: an app's call
    // builds its services the same way
    public static string Emit(
        IReadOnlyList<InterceptModel> calls,
        IReadOnlyList<string> modules,
        string idType,
        string idParser,
        string providerIdType)
    {
        var writer = new SourceWriter();

        writer.Line(SourceWriter.Header);

        using (writer.Block($"namespace {Names.GeneratedNamespace}"))
        using (writer.Block($"file static class {KnownTypes.RegistrationMethod}Interceptor"))
        {
            foreach (var call in calls.Where(call => !call.IsApp))
            {
                writer.Line(call.Attribute);
            }

            writer.Line($"public static {Builder} {KnownTypes.RegistrationMethod}(this {Services} services)");

            using (writer.Block())
            {
                writer.Line(
                    "var builder = global::Typeshift.Generated.ModuleRegistration"
                    + $".AddCoreServices<{idType}, {providerIdType}>(services, {idParser});");
                writer.Line();

                foreach (var module in modules)
                {
                    writer.Line($"{module}.Register(services);");
                }

                if (modules.Count > 0) writer.Line();

                writer.Line("return builder;");
            }

            if (calls.Any(call => call.IsApp)) WriteApp(writer, calls);
        }

        // Every generator that intercepts declares the attribute itself, file-local so the declarations never clash
        return writer
            .Lines(
                """

                namespace System.Runtime.CompilerServices
                {
                    [global::System.AttributeUsage(global::System.AttributeTargets.Method, AllowMultiple = true)]
                    file sealed class InterceptsLocationAttribute : global::System.Attribute
                    {
                        public InterceptsLocationAttribute(int version, string data)
                        {
                        }
                    }
                }
                """)
            .ToString();
    }

    private static void WriteApp(SourceWriter writer, IReadOnlyList<InterceptModel> calls)
    {
        writer.Line();

        foreach (var call in calls.Where(call => call.IsApp))
        {
            writer.Line(call.Attribute);
        }

        writer.Lines(
            $"""
            public static global::{KnownTypes.App} {KnownTypes.AppMethod}(
                global::Microsoft.Extensions.AI.IChatClient client,
                global::System.Action<{Builder}>? configure = null)
            """);

        using (writer.Block())
        {
            writer.Line("var services = new global::Microsoft.Extensions.DependencyInjection.ServiceCollection();");
            writer.Line(
                "return global::Typeshift.Generated.ModuleRegistration.CreateApp("
                + $"services, {KnownTypes.RegistrationMethod}(services), client, configure);");
        }
    }
}
