using System.Text.Json;
using Microsoft.Data.SqlClient;

if(args.Length<1)throw new ArgumentException("Repository root required.");
var repo=Path.GetFullPath(args[0]);var managed=args.Contains("--managed");
if(managed)AppContext.SetSwitch("Switch.Microsoft.Data.SqlClient.UseManagedNetworkingOnWindows",true);
var values=File.ReadLines(Path.Combine(repo,"deploy/local/.env")).Where(x=>x.Contains('=')&&!x.StartsWith('#')).Select(x=>x.Split('=',2)).ToDictionary(x=>x[0],x=>x[1]);
var config=new SqlConnectionStringBuilder{DataSource="tcp:127.0.0.1,14333",InitialCatalog="ContactCenterAI_Operations",UserID="ccai_app",Password=values["CCAI_OPERATIONAL_DB_PASSWORD"],Encrypt=SqlConnectionEncryptOption.Mandatory,TrustServerCertificate=true,ConnectTimeout=5};
var checks=new List<string>();var result="BLOCKED";int? sqlError=null;string? cause=null;
try{
 await using var connection=new SqlConnection(config.ConnectionString);await connection.OpenAsync();
 await using var command=connection.CreateCommand();command.CommandText="SELECT COUNT(*) FROM dbo.Principal";
 if(Convert.ToInt32(await command.ExecuteScalarAsync())!=8)throw new InvalidOperationException("Expected eight existing synthetic bindings.");
 checks.Add("Restricted .NET application login reads existing SQL bindings with mandatory encryption");
 var store=new ContactCenterAI.Infrastructure.SqlOperationalStore(values["CCAI_OPERATIONAL_DB_PASSWORD"]);
 if(!(await store.CheckAsync(default)).IsReady)throw new InvalidOperationException("Product SQL readiness failed.");
 checks.Add("Existing product SqlOperationalStore readiness uses actual SQL Server");result="PASS";
}catch(SqlException error){sqlError=error.Number;Exception deepest=error;while(deepest.InnerException is{} inner)deepest=inner;cause=deepest.GetType().Name+":"+deepest.HResult.ToString("X8");Environment.ExitCode=1;}
var report=new{result,observedAtUtc=DateTimeOffset.UtcNow,scope="Existing product .NET SQL driver/readiness only; not OIDC browser, leases, cancellation or full enterprise acceptance",networking=managed?"ManagedDiagnosticOnly":"NativeDefault",mandatoryEncryption=true,syntheticLocalSelfSignedCertificate=true,privilegedApplicationLogin=false,checks,sqlError,cause,credentialsExported=false};
var path=Path.Combine(repo,"docs/progress",managed?"sql-managed-v0.9.json":"sql-native-v0.9.json");File.WriteAllText(path,JsonSerializer.Serialize(report,new JsonSerializerOptions(JsonSerializerDefaults.Web){WriteIndented=true}));
Console.WriteLine(result+": .NET SQL product acceptance; "+checks.Count+" checks; private credentials excluded.");
