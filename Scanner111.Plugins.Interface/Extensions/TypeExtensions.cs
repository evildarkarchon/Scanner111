namespace Scanner111.Plugins.Interface.Extensions;

public static class TypeExtensions
{
    public static bool HasAttribute<T>(this Type type) where T : Attribute
    {
        return type.GetCustomAttributes(typeof(T), true).Length > 0;
    }
    
    public static T? GetAttribute<T>(this Type type) where T : Attribute
    {
        return (T?)type.GetCustomAttributes(typeof(T), true).FirstOrDefault();
    }
    
    public static IEnumerable<T> GetAttributes<T>(this Type type) where T : Attribute
    {
        return type.GetCustomAttributes(typeof(T), true).Cast<T>();
    }
}