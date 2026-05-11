namespace Iris.Core.Plugins;

/// <summary>
/// Implemented by plugin assemblies to register their types with the host factory.
/// </summary>
public interface IPluginRegistrar
{
    void RegisterPlugins(IPluginFactory factory);
}
