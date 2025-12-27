using Dapper;
using SiberMailer.Core.Enums;
using System.Data;

namespace SiberMailer.Data;

/// <summary>
/// Dapper type handler for PostgreSQL enum types.
/// Maps PostgreSQL string values to C# enums.
/// </summary>
public class UserRoleTypeHandler : SqlMapper.TypeHandler<UserRole>
{
    public override UserRole Parse(object value)
    {
        if (value is string stringValue)
        {
            return Enum.Parse<UserRole>(stringValue, ignoreCase: true);
        }
        return UserRole.Member;
    }

    public override void SetValue(IDbDataParameter parameter, UserRole value)
    {
        parameter.Value = value.ToString();
        parameter.DbType = DbType.String;
    }
}

/// <summary>
/// Registers all custom Dapper type handlers.
/// Call this once at application startup.
/// </summary>
public static class DapperConfig
{
    private static bool _isConfigured = false;

    public static void Configure()
    {
        if (_isConfigured) return;

        SqlMapper.AddTypeHandler(new UserRoleTypeHandler());
        
        // Add more type handlers here as needed
        // SqlMapper.AddTypeHandler(new ContactStatusTypeHandler());
        // SqlMapper.AddTypeHandler(new JobStatusTypeHandler());

        _isConfigured = true;
    }
}
