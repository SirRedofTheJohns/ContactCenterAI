using System.Text.Json;
using Microsoft.Data.SqlClient;

var result = new Dictionary<string, object?> { ["scope"] = "B03/B04 agent SQL and Windows temporary-key compatibility; no business writes" };
try
{
    using var certificate = DiagnosticCertificate.Create();
    result["windowsTemporaryTlsKey"] = "PASS";
}
catch (Exception exception)
{
    result["windowsTemporaryTlsKey"] = "FAILED";
    result["keyFailureType"] = exception.GetType().Name;
    result["keyHResult"] = $"0x{exception.HResult:X8}";
}
try
{
    var options = new SqlConnectionStringBuilder
    {
        DataSource = "tcp:127.0.0.1,14333", InitialCatalog = "master", UserID = "sa",
        Password = Environment.GetEnvironmentVariable("CCAI_SQL_SA_PASSWORD"),
        Encrypt = SqlConnectionEncryptOption.Mandatory, TrustServerCertificate = true, ConnectTimeout = 5
    };
    await using var connection = new SqlConnection(options.ConnectionString);
    using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(8));
    await connection.OpenAsync(deadline.Token);
    await using var command = connection.CreateCommand();
    command.CommandText = "SELECT 1";
    if ((int)(await command.ExecuteScalarAsync(deadline.Token))! != 1) throw new InvalidOperationException();
    result["nativeEncryptedSql"] = "PASS";
}
catch (Exception exception)
{
    result["nativeEncryptedSql"] = "FAILED";
    result["sqlFailureType"] = exception.GetType().Name;
    result["sqlHResult"] = $"0x{exception.HResult:X8}";
    if (exception is SqlException sql) result["sqlNumber"] = sql.Number;
    if (exception.InnerException is { } inner)
    {
        result["sqlInnerType"] = inner.GetType().Name;
        result["sqlInnerHResult"] = $"0x{inner.HResult:X8}";
    }
}
Console.WriteLine(JsonSerializer.Serialize(result));
return result["nativeEncryptedSql"]?.ToString() == "PASS" && result["windowsTemporaryTlsKey"]?.ToString() == "PASS" ? 0 : 2;
