using System.Collections.Generic;
using Agentic.Generators.Models;
using Agentic.Generators.Parsing;

namespace Agentic.Generators.Emitting;

internal static class InterceptorEmitter
{
    private const string Services = "global::Microsoft.Extensions.DependencyInjection.IServiceCollection";

    public static string Emit(IReadOnlyList<InterceptModel> calls, IReadOnlyList<string> modules)
    {
        var writer = new SourceWriter();

        writer.Line(SourceWriter.Header);

        using (writer.Block($"namespace {Names.GeneratedNamespace}"))
        {
            using (writer.Block($"file static class {KnownTypes.RegistrationMethod}Interceptor"))
            {
                foreach (var call in calls)
                {
                    writer.Line(call.Attribute);
                }

                writer.Line($"public static {Services} {KnownTypes.RegistrationMethod}(");

                using (writer.Indent())
                {
                    writer.Line($"this {Services} services)");
                }

                using (writer.Block())
                {
                    writer.Line("global::Agentic.Modules.ModuleRegistration.AddCoreServices(services);");

                    foreach (var module in modules)
                    {
                        writer.Line($"{module}.Register(services);");
                    }

                    writer.Line();
                    writer.Line("return services;");
                }
            }
        }

        writer.Line();

        // Every generator that intercepts declares the attribute itself, file-local so the declarations never clash
        using (writer.Block("namespace System.Runtime.CompilerServices"))
        {
            writer.Line(
                "[global::System.AttributeUsage(global::System.AttributeTargets.Method, AllowMultiple = true)]");

            using (writer.Block("file sealed class InterceptsLocationAttribute : global::System.Attribute"))
            {
                writer.Line("public InterceptsLocationAttribute(int version, string data)");

                using (writer.Block())
                {
                }
            }
        }

        return writer.ToString();
    }
}
