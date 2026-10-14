using System.Collections.Generic;
using Typeshift.Generators.Models;
using Typeshift.Generators.Parsing;

namespace Typeshift.Generators.Emitting;

internal static class InterceptorEmitter
{
    private const string Services = "global::Microsoft.Extensions.DependencyInjection.IServiceCollection";

    // D1: every call in the project registers this assembly's module and those of its references
    public static string Emit(
        IReadOnlyList<InterceptModel> calls,
        IReadOnlyList<string> modules,
        string idType,
        string idParser)
    {
        var writer = new SourceWriter();

        writer.Line(SourceWriter.Header);

        using (writer.Block($"namespace {Names.GeneratedNamespace}"))
        using (writer.Block($"file static class {KnownTypes.RegistrationMethod}Interceptor"))
        {
            foreach (var call in calls)
            {
                writer.Line(call.Attribute);
            }

            writer.Line(
                $"public static global::Typeshift.TypeshiftBuilder {KnownTypes.RegistrationMethod}("
                + $"this {Services} services)");

            using (writer.Block())
            {
                writer.Line(
                    "var builder = global::Typeshift.Generated.ModuleRegistration"
                    + $".AddCoreServices<{idType}>(services, {idParser});");
                writer.Line();

                foreach (var module in modules)
                {
                    writer.Line($"{module}.Register(services);");
                }

                if (modules.Count > 0) writer.Line();

                writer.Line("return builder;");
            }
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
}
