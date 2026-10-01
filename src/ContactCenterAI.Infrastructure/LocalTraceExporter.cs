using System.Diagnostics;
using System.Text.Json;
using ContactCenterAI.Application;
namespace ContactCenterAI.Infrastructure;

public sealed record SafeTrace(string TraceId,string SpanId,string? ParentSpanId,string Operation,double DurationMs,int? HttpStatus,string? Language,DateTimeOffset FinishedAt);
public sealed class LocalTraceExporter:IDisposable
{
    private readonly object gate=new();private readonly Queue<SafeTrace> recent=new();private readonly ActivityListener listener;
    private readonly string path;private long total,failed,dropped;
    public LocalTraceExporter(string directory)
    {
        Directory.CreateDirectory(directory);path=Path.Combine(directory,"traces.ndjson");
        listener=new(){ShouldListenTo=source=>source.Name=="ContactCenterAI.Workflow",Sample=(ref ActivityCreationOptions<ActivityContext> _)=>ActivitySamplingResult.AllDataAndRecorded,ActivityStopped=Record};
        ActivitySource.AddActivityListener(listener);
    }
    private void Record(Activity activity)
    {
        if(activity.OperationName is not("api.request" or "assistant.turn" or "knowledge.search" or "knowledge.rerank" or "source.read" or "source.cancel" or "source.receipt"))return;
        var language=activity.GetTagItem("language") as string;if(language is not("es" or "en"))language=null;
        var status=activity.GetTagItem("http.status") as int?;
        var item=new SafeTrace(activity.TraceId.ToString(),activity.SpanId.ToString(),activity.ParentSpanId==default?null:activity.ParentSpanId.ToString(),activity.OperationName,Math.Round(activity.Duration.TotalMilliseconds,2),status,language,DateTimeOffset.UtcNow);
        lock(gate)
        {
            total++;if(status>=400)failed++;recent.Enqueue(item);while(recent.Count>60)recent.Dequeue();
            try
            {
                if(File.Exists(path)&&new FileInfo(path).Length>1024*1024)File.Move(path,path+".previous",true);
                File.AppendAllText(path,JsonSerializer.Serialize(item,new JsonSerializerOptions(JsonSerializerDefaults.Web))+"\n");
            }catch(Exception error)when(error is IOException or UnauthorizedAccessException){dropped++;}
        }
    }
    public object Snapshot(){lock(gate)return new{scope="current-process bounded traces; transactional audit is separate",total,failed,dropped,recent=recent.Reverse().Take(20).ToArray()};}
    public void Dispose()=>listener.Dispose();
}
