using System.Reflection;
using JasperFx.CodeGeneration;
using JasperFx.CodeGeneration.Model;
using JasperFx.RuntimeCompiler;

namespace Api.IntegrationTests.Fixtures;

// Compiles handler code at runtime as Wolverine's own generator does, then runs a full garbage collection, so the native memory of
// each compile does not pile up (section 9, rule 11). ApiFactory and PipelineHost register it after Wolverine's generator, and the
// last registration wins.
internal sealed class CollectingAssemblyGenerator : IAssemblyGenerator
{
    private readonly AssemblyGenerator _generator = new();

    public string? AssemblyName
    {
        get => _generator.AssemblyName;
        set => _generator.AssemblyName = value;
    }

    public void ReferenceAssembly(Assembly assembly) => _generator.ReferenceAssembly(assembly);

    public void ReferenceAssemblyContainingType<T>() => _generator.ReferenceAssemblyContainingType<T>();

    public Assembly Generate(Action<ISourceWriter> write) => Collected(_generator.Generate(write));

    public Assembly Generate(string code) => Collected(_generator.Generate(code));

    public void Compile(GeneratedAssembly generatedAssembly, IServiceVariableSource? services = null) => Compile(generatedAssembly, services, out _);

    public void Compile(GeneratedAssembly generatedAssembly, IServiceVariableSource? services, out string code)
    {
        _generator.Compile(generatedAssembly, services, out code);
        GC.Collect();
    }

    private static Assembly Collected(Assembly assembly)
    {
        GC.Collect();
        return assembly;
    }
}
