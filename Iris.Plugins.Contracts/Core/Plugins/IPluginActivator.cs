namespace Iris.Core.Plugins;

/// <summary>
/// Implemented by plugin assemblies to activate plugin instances based on configuration.
/// Called by <see cref="PluginBootstrapService"/> after all types are registered.
/// </summary>
public interface IPluginActivator
{
    Task ActivatePluginsAsync(IPluginFactory factory, IPluginRegistry registry, IServiceProvider services, CancellationToken cancellationToken);
}
