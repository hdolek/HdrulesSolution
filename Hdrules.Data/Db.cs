
using System.Data;
using Oracle.ManagedDataAccess.Client;

namespace Hdrules.Data;

public class DbConnectionFactory
{
    private readonly string _connStr;
    public DbConnectionFactory(string connStr) => _connStr = connStr;
    public IDbConnection Create() => new OracleConnection(_connStr);
}
