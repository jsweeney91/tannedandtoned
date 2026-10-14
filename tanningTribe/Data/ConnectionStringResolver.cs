namespace tanningTribe.Data;

public static class ConnectionStringResolver
{
    /// <summary>
    /// Resolves the MySQL connection string from (in order):
    /// ConnectionStrings:DefaultConnection, then a Railway-style MYSQL_URL / DATABASE_URL
    /// (mysql://user:password@host:port/database).
    /// </summary>
    public static string? Resolve(IConfiguration configuration)
    {
        var direct = configuration.GetConnectionString("DefaultConnection");
        if (!string.IsNullOrWhiteSpace(direct) && !direct.Contains("SET_IN_USER_SECRETS", StringComparison.OrdinalIgnoreCase))
        {
            return direct;
        }

        var url = configuration["MYSQL_URL"] ?? configuration["DATABASE_URL"];
        if (string.IsNullOrWhiteSpace(url))
        {
            return null;
        }

        var uri = new Uri(url);
        var userInfo = uri.UserInfo.Split(':', 2);
        var user = Uri.UnescapeDataString(userInfo[0]);
        var password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : string.Empty;
        var database = uri.AbsolutePath.Trim('/');
        var port = uri.IsDefaultPort ? 3306 : uri.Port;

        return $"Server={uri.Host};Port={port};Database={database};User={user};Password={password};SslMode=Preferred;";
    }
}
