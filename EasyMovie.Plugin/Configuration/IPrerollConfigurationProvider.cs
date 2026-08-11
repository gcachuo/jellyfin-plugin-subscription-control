namespace EasyMovie.Plugin.Configuration;

public interface IPrerollConfigurationProvider
{
    PluginConfiguration? GetConfiguration();
}

public sealed class PrerollConfigurationProvider : IPrerollConfigurationProvider
{
    public PluginConfiguration? GetConfiguration() => Plugin.Instance?.Configuration;
}
