using System.Reflection;
using System.Runtime.CompilerServices;
using Xunit;

namespace TailoredApps.Demo.IntegrationTests;

/// <summary>
/// How the async methods of the libraries and of the demo were compiled for the framework the tests run on:
/// runtime async for net11.0 and newer, compiler-generated state machines for net10.0 (eng/RuntimeAsync.targets).
/// The switch is one compiler feature flag; if it stopped reaching the compiler nothing else would notice.
/// This test project itself is compiled the classic way, so on net11.0 every other test here also crosses
/// from state-machine code into runtime-async code and back.
/// </summary>
public class RuntimeAsyncTests
{
    private static readonly List<MethodInfo> Methods = LoadMethods();

    private static List<MethodInfo> StateMachines => Methods.Where(m => m.IsDefined(typeof(AsyncStateMachineAttribute), inherit: false)).ToList();

    private static List<MethodInfo> RuntimeAsync => Methods.Where(m => (m.MethodImplementationFlags & MethodImplAttributes.Async) != 0).ToList();

    [Fact]
    public void When_Assemblies_Are_Inspected_Should_Cover_The_Libraries_And_The_Demo()
    {
        var assemblies = Methods.Select(m => m.Module.Assembly.GetName().Name).Distinct().ToList();

        Assert.Contains("TailoredApps.Shared.Demo", assemblies);
        Assert.Contains("TailoredApps.Shared.Payments", assemblies);
        Assert.Contains("TailoredApps.Shared.MediatR", assemblies);
        Assert.Contains("TailoredApps.Shared.EntityFramework", assemblies);
    }

#if NET11_0_OR_GREATER
    [Fact]
    public void When_Built_For_Net11_Should_Compile_Async_Methods_As_Runtime_Async()
    {
        Assert.NotEmpty(RuntimeAsync);
        Assert.Empty(StateMachines.Select(Describe));
    }
#else
    [Fact]
    public void When_Built_For_Net10_Should_Compile_Async_Methods_As_State_Machines()
    {
        Assert.NotEmpty(StateMachines);
        Assert.Empty(RuntimeAsync.Select(Describe));
    }
#endif

    private static string Describe(MethodInfo method) => $"{method.DeclaringType?.FullName}.{method.Name}";

    private static List<MethodInfo> LoadMethods()
    {
        const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

        var demo = typeof(Program).Assembly;
        return demo.GetReferencedAssemblies()
            .Where(name => name.Name!.StartsWith("TailoredApps.Shared.", StringComparison.Ordinal))
            .Select(Assembly.Load)
            .Append(demo)
            .SelectMany(LoadableTypes)
            .SelectMany(type => type.GetMethods(all))
            .ToList();
    }

    private static IEnumerable<Type> LoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException exception)
        {
            return exception.Types.OfType<Type>();
        }
    }
}
