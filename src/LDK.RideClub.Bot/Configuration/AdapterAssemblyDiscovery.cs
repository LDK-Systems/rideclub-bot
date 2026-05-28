// ---------------------------------------------------------------------------
// RideClub Bot — AdapterAssemblyDiscovery
// ---------------------------------------------------------------------------

using System.Reflection;

namespace LDK.RideClub.Bot.Configuration;

/// <summary>
/// Provides a single, cached set of adapter assemblies discovered from the application
/// base directory. Used by both Autofac's <c>RegisterAssemblyModules</c> for module
/// loading and by <see cref="ConfigurationExtensions"/> for options registration,
/// ensuring a single discovery mechanism.
/// </summary>
internal static class AdapterAssemblyDiscovery
{
    /// <summary>
    /// Gets the adapter assemblies matching the naming convention "LDK.RideClub.Bot.Adapters.*.dll"
    /// from the application base directory. Results are cached after first access.
    /// </summary>
    public static Assembly[] Assemblies { get; } = DiscoverAdapterAssemblies();

    private static Assembly[] DiscoverAdapterAssemblies()
    {
        string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;

        return [.. Directory
            .GetFiles(baseDirectory, "LDK.RideClub.Bot.Adapters.*.dll")
            .Select(Assembly.LoadFrom)];
    }
}
