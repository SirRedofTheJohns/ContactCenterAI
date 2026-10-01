using System.Net.Http.Json;
using System.Text.Json;
using ContactCenterAI.Application;
namespace ContactCenterAI.Infrastructure;

public sealed class OllamaKnowledgeReranker:IKnowledgeReranker,IDisposable
{
    private readonly HttpClient client;private readonly bool owned;
    public const string Version="evidence-selection-v2";
    private static readonly string SystemPrompt=LoadPrompt();
    private static string LoadPrompt(){using var stream=typeof(OllamaKnowledgeReranker).Assembly.GetManifestResourceStream("ContactCenterAI.Infrastructure.Prompts.evidence-selection-v2.system.txt")!;using var reader=new StreamReader(stream);return reader.ReadToEnd();}
    public OllamaKnowledgeReranker(HttpClient? client=null){owned=client is null;this.client=client??OllamaIntentProvider.CreateClient();}
    public async Task<int?> SelectAsync(string text,string language,IReadOnlyList<RankedKnowledge> candidates,CancellationToken ct)
    {
        if(language is not("es" or "en")||string.IsNullOrWhiteSpace(text)||text.Length>4096||candidates.Count is <1 or >5)throw new RequestRejected(400,"RERANK_INPUT_REJECTED");
        using var activity=DemoTelemetry.Activities.StartActivity("knowledge.rerank");activity?.SetTag("language",language);
        using var deadline=CancellationTokenSource.CreateLinkedTokenSource(ct);deadline.CancelAfter(TimeSpan.FromSeconds(6));
        try
        {
            using var tags=await client.GetAsync("http://127.0.0.1:11434/api/tags",HttpCompletionOption.ResponseHeadersRead,deadline.Token);using var tagData=await OllamaIntentProvider.ReadAsync(tags,deadline.Token);
            if(!tagData.RootElement.GetProperty("models").EnumerateArray().Any(model=>model.GetProperty("name").GetString()==OllamaIntentProvider.ModelTag&&model.GetProperty("digest").GetString()==OllamaIntentProvider.ModelDigest))throw new RequestRejected(503,"LOCAL_MODEL_PIN_MISMATCH");
            var user=JsonSerializer.Serialize(new{language,question=text,candidates=candidates.Select((item,index)=>new{choice=index+1,title=item.Evidence.Title,passage=item.Evidence.Content[..Math.Min(item.Evidence.Content.Length,600)]})});
            var schema=new{type="object",properties=new{choice=new{type="integer",@enum=Enumerable.Range(0,candidates.Count+1).ToArray()}},required=new[]{"choice"},additionalProperties=false};
            var payload=new{model=OllamaIntentProvider.ModelTag,prompt="<|im_start|>system\n"+SystemPrompt+"<|im_end|>\n<|im_start|>user\n"+user+"<|im_end|>\n<|im_start|>assistant\n<think>\n\n</think>\n\n",raw=true,stream=false,think=false,format=schema,keep_alive="15m",options=new{temperature=0,num_ctx=2048,num_predict=32,num_gpu=0,num_thread=8}};
            using var request=new HttpRequestMessage(HttpMethod.Post,"http://127.0.0.1:11434/api/generate"){Content=JsonContent.Create(payload)};
            using var response=await client.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,deadline.Token);using var data=await OllamaIntentProvider.ReadAsync(response,deadline.Token);var root=data.RootElement;
            if(root.GetProperty("model").GetString()!=OllamaIntentProvider.ModelTag||!root.GetProperty("done").GetBoolean()||root.GetProperty("done_reason").GetString()!="stop"||root.GetProperty("prompt_eval_count").GetInt32() is <1 or >1900||root.TryGetProperty("tool_calls",out var tools)&&tools.GetArrayLength()!=0)throw new JsonException();
            var raw=root.GetProperty("response").GetString();if(raw is null||raw.Length>64)throw new JsonException();using var selected=JsonDocument.Parse(raw);
            if(selected.RootElement.ValueKind!=JsonValueKind.Object||selected.RootElement.EnumerateObject().Count()!=1||!selected.RootElement.TryGetProperty("choice",out var choice)||!choice.TryGetInt32(out var number)||number<0||number>candidates.Count)throw new JsonException();
            return number==0?null:number-1;
        }catch(Exception error)when(error is JsonException or InvalidOperationException or KeyNotFoundException or IOException){throw new RequestRejected(502,"RERANK_RESPONSE_REJECTED");}
    }
    public void Dispose(){if(owned)client.Dispose();}
}
