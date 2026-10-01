using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ContactCenterAI.Application;
namespace ContactCenterAI.Infrastructure;

public sealed class OllamaEmbeddingProvider : ITextEmbeddingProvider,IDisposable
{
    public const string Tag="bge-m3:latest",Digest="7907646426070047a77226ac3e684fbbe8410524f7b4a74d02837e43f2146bab";
    private readonly HttpClient client;
    public string ModelId=>"bge-m3@"+Digest;
    public OllamaEmbeddingProvider(HttpMessageHandler? handler=null)
    {client=new(handler??new SocketsHttpHandler{AllowAutoRedirect=false,UseProxy=false}){BaseAddress=new("http://127.0.0.1:11435/"),Timeout=Timeout.InfiniteTimeSpan};}
    public async Task<IReadOnlyList<float[]>> EmbedAsync(IReadOnlyList<string> texts,CancellationToken ct)
    {
        if(texts.Count is <1 or >8||texts.Any(text=>string.IsNullOrWhiteSpace(text)||text.Length>4096))throw new RequestRejected(400,"EMBEDDING_INPUT_REJECTED");
        using var deadline=CancellationTokenSource.CreateLinkedTokenSource(ct);deadline.CancelAfter(TimeSpan.FromSeconds(12));
        using var tags=await client.GetAsync("api/tags",HttpCompletionOption.ResponseHeadersRead,deadline.Token);
        try{using var data=await ParseAsync(tags,deadline.Token);
        if(!data.RootElement.GetProperty("models").EnumerateArray().Any(item=>item.GetProperty("name").GetString()==Tag&&item.GetProperty("digest").GetString()==Digest))throw new RequestRejected(503,"EMBEDDING_PIN_MISMATCH");}
        catch(Exception error)when(error is JsonException or InvalidOperationException or KeyNotFoundException){throw new RequestRejected(503,"EMBEDDING_RESPONSE_REJECTED");}
        using var request=new HttpRequestMessage(HttpMethod.Post,"api/embed"){Content=JsonContent.Create(new{model=Tag,input=texts,truncate=false,keep_alive="30m",options=new{num_gpu=0,num_thread=8}})};
        using var response=await client.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,deadline.Token);
        try
        {
            using var data=await ParseAsync(response,deadline.Token);var root=data.RootElement;
            if(root.GetProperty("model").GetString()!=Tag)throw new JsonException();
            var vectors=root.GetProperty("embeddings").EnumerateArray().Select(row=>Normalize(row.EnumerateArray().Select(x=>x.GetSingle()).ToArray())).ToArray();
            if(vectors.Length!=texts.Count)throw new JsonException();return vectors;
        }catch(Exception error)when(error is JsonException or InvalidOperationException or KeyNotFoundException or FormatException){throw new RequestRejected(503,"EMBEDDING_RESPONSE_REJECTED");}
    }
    public static float[] Normalize(float[] vector)
    {
        if(vector.Length!=1024||vector.Any(v=>!float.IsFinite(v)))throw new RequestRejected(503,"EMBEDDING_VECTOR_REJECTED");
        var norm=Math.Sqrt(vector.Sum(v=>(double)v*v));if(norm<1e-10)throw new RequestRejected(503,"EMBEDDING_VECTOR_REJECTED");
        return vector.Select(v=>(float)(v/norm)).ToArray();
    }
    private static async Task<JsonDocument> ParseAsync(HttpResponseMessage response,CancellationToken ct)
    {
        if(response.StatusCode!=HttpStatusCode.OK)throw new RequestRejected(503,"EMBEDDING_HTTP_REJECTED");
        using var stream=await response.Content.ReadAsStreamAsync(ct);using var bytes=new MemoryStream();var block=new byte[8192];
        int length;while((length=await stream.ReadAsync(block,ct))>0){if(bytes.Length+length>512*1024)throw new RequestRejected(503,"EMBEDDING_RESPONSE_REJECTED");await bytes.WriteAsync(block.AsMemory(0,length),ct);}
        try{return JsonDocument.Parse(bytes.ToArray());}catch(JsonException){throw new RequestRejected(503,"EMBEDDING_RESPONSE_REJECTED");}
    }
    public void Dispose()=>client.Dispose();
}
