using System.Data.Common;

namespace HRMS.Application.Abstractions.Persistence;

/// <summary>Creates unopened connections to the configured HRMS database.</summary>
public interface IDbConnectionFactory
{
    DbConnection CreateConnection();
}
