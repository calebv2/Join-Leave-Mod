using System;
using System.Collections.Generic;
using System.IO;
using Mono.Cecil;

internal sealed class GameAssemblyResolver : IAssemblyResolver
{
    private readonly string directory;
    private readonly Dictionary<string, AssemblyDefinition> loaded = new Dictionary<string, AssemblyDefinition>();
    internal GameAssemblyResolver(string directory) { this.directory = directory; }
    public AssemblyDefinition Resolve(AssemblyNameReference name) { return Resolve(name, new ReaderParameters()); }
    public AssemblyDefinition Resolve(AssemblyNameReference name, ReaderParameters parameters)
    {
        AssemblyDefinition assembly;
        if (loaded.TryGetValue(name.Name, out assembly)) return assembly;
        string path = Path.Combine(directory, name.Name + ".dll");
        if (!File.Exists(path)) throw new AssemblyResolutionException(name);
        parameters.AssemblyResolver = this;
        assembly = AssemblyDefinition.ReadAssembly(path, parameters);
        loaded.Add(name.Name, assembly);
        return assembly;
    }
    public void Dispose() { foreach (AssemblyDefinition assembly in loaded.Values) assembly.Dispose(); }
}

internal static class RuntimeCompatibility
{
    private static int Main(string[] args)
    {
        int failures = 0;
        int checkedMethods = 0;
        using (var resolver = new GameAssemblyResolver(args[1]))
        using (var assembly = AssemblyDefinition.ReadAssembly(args[0], new ReaderParameters { AssemblyResolver = resolver }))
        {
            foreach (MemberReference member in assembly.MainModule.GetMemberReferences())
            {
                MethodReference method = member as MethodReference;
                if (method == null) continue;
                string scope = method.DeclaringType.GetElementType().Scope.Name;
                if (scope != "mscorlib" && scope != "System" && scope != "System.Core") continue;
                checkedMethods++;
                try
                {
                    if (method.Resolve() != null) continue;
                    Console.Error.WriteLine("Missing game runtime method: " + method.FullName);
                }
                catch (AssemblyResolutionException) { Console.Error.WriteLine("Cannot resolve game runtime method: " + method.FullName); }
                failures++;
            }
        }
        Console.WriteLine("Checked " + checkedMethods + " framework method references against the game's runtime: " + failures + " missing.");
        return failures == 0 ? 0 : 1;
    }
}
