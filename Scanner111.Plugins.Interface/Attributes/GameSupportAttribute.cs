namespace Scanner111.Plugins.Interface.Attributes;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public class GameSupportAttribute : Attribute
{
    public string GameId { get; }
    public string GameName { get; }
    public string[] ExecutableNames { get; }
    
    public GameSupportAttribute(
        string gameId,
        string gameName,
        params string[] executableNames)
    {
        GameId = gameId;
        GameName = gameName;
        ExecutableNames = executableNames;
    }
}