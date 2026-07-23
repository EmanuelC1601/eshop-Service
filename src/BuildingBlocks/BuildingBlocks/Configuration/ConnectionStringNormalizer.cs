using System.Data.Common;

namespace BuildingBlocks.Configuration;

public static class ConnectionStringNormalizer
{
    public static string NormalizePostgres(string connectionString)
    {
        if (!Uri.TryCreate(connectionString, UriKind.Absolute, out var uri) ||
            (uri.Scheme != "postgres" && uri.Scheme != "postgresql"))
        {
            return connectionString;
        }

        var credentials = uri.UserInfo.Split(':', 2);
        var builder = new DbConnectionStringBuilder
        {
            ["Host"] = uri.Host,
            ["Port"] = uri.Port > 0 ? uri.Port : 5432,
            ["Database"] = Uri.UnescapeDataString(uri.AbsolutePath.Trim('/')),
            ["Username"] = Uri.UnescapeDataString(credentials[0]),
            ["SSL Mode"] = "Require",
            ["Trust Server Certificate"] = true
        };

        if (credentials.Length == 2)
        {
            builder["Password"] = Uri.UnescapeDataString(credentials[1]);
        }

        return builder.ConnectionString;
    }

    public static string NormalizeRedis(string connectionString)
    {
        if (!Uri.TryCreate(connectionString, UriKind.Absolute, out var uri) ||
            (uri.Scheme != "redis" && uri.Scheme != "rediss"))
        {
            return connectionString;
        }

        var credentials = uri.UserInfo.Split(':', 2);
        var password = credentials.Length == 2
            ? Uri.UnescapeDataString(credentials[1])
            : Uri.UnescapeDataString(credentials[0]);

        return $"{uri.Host}:{(uri.Port > 0 ? uri.Port : 6379)},password={password},ssl={uri.Scheme == "rediss"},abortConnect=false,connectRetry=3";
    }
}
