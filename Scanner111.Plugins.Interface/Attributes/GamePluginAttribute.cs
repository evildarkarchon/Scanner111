namespace Scanner111.Plugins.Interface.Attributes;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public class GamePluginAttribute : Attribute
{
    public string Id { get; }
    public string Name { get; }
    public string Description { get; }
    public string[] SupportedGameIds { get; }
    public string Version { get; }
    
    public GamePluginAttribute(
        string id,
        string name,
        string description,
        string version,
        params string[] supportedGameIds)
    {
        Id = id;
        Name = name;
        Description = description;
        Version = version;
        SupportedGameIds = supportedGameIds;
    }
}