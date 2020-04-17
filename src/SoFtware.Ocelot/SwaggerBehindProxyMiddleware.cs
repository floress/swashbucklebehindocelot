using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace SoFtware.Ocelot
{
    public class SwaggerBehindProxyMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger _logger;
        private string _swaggerPrefix = "swagger";
        private string _swaggerDocName = "v1";

        public SwaggerBehindProxyMiddleware(RequestDelegate next,
            ILoggerFactory loggerFactory)
        {
            _next = next;
            _logger = loggerFactory.CreateLogger<SwaggerBehindProxyMiddleware>();
        }

        private string IndexUri => $"/{_swaggerPrefix}/index.html";
        private string JsonUri => $"/{_swaggerPrefix}/{_swaggerDocName}/swagger.json";
        private string[] PrefixSegments => _swaggerPrefix.Trim('/')
                                            .Split('/')
                                            .Select(s => s.Trim('/')).ToArray();

        public async Task Invoke(HttpContext context)
        {
            var path = context.Request.Path;
            if (path.HasValue && (path.Value.EndsWith(IndexUri) ||
                                  path.Value.EndsWith(JsonUri)))
            {
                var requestSegments = context.Request.Path.Value.Trim('/')
                    .Split("/")
                    .Select(s => s.Trim('/'))
                    .ToArray();

                var miroserviceName = requestSegments.Reverse()
                    .Skip(PrefixSegments.Length + 1) //Segments and file 
                    .Take(1)
                    .SingleOrDefault();

                var referenceToOriginalBody = context.Response.Body;

                await using var responseBody = new MemoryStream();

                context.Response.Body = responseBody;

                await _next(context);

                async Task<string> ReadResponse(HttpResponse resp)
                {
                    resp.Body.Seek(0, SeekOrigin.Begin);
                    var text = await new StreamReader(resp.Body).ReadToEndAsync();
                    resp.Body.Seek(0, SeekOrigin.Begin);
                    return text;
                }

                var response = await ReadResponse(context.Response);

                byte[] buffer;

                if (path.Value.EndsWith(JsonUri))
                {
                    // frontend reading json
                    _logger.LogInformation($"Recreating swagger.json for {miroserviceName} service");
                    await using var ms = new MemoryStream();
                    using var wr = new JsonTextWriter(new StreamWriter(ms))
                    {
                        Indentation = 2, IndentChar = ' ', Formatting = Formatting.Indented
                    };
                    var o = JObject.Parse(response);
                    wr.WriteStartObject();
                    foreach (var jProperty in o.Children<JProperty>())
                    {
                        if (jProperty.Name == "paths")
                        {
                            wr.WritePropertyName(jProperty.Name);
                            wr.WriteStartObject();
                            foreach (var pathProperty in jProperty.Value.Children())
                            {
                                var originalPath = (JProperty)pathProperty;
                                wr.WritePropertyName($"/{requestSegments[0]}{originalPath.Name}");
                                originalPath.Value.WriteTo(wr);
                            }
                            wr.WriteEndObject();
                        }
                        else
                        {
                            jProperty.WriteTo(wr);
                        }
                    }
                    wr.WriteEndObject();
                    wr.Flush();
                    wr.Close();

                    buffer = ms.ToArray();
                } 
                else if (path.Value.EndsWith(IndexUri) && response.Contains(JsonUri))
                {
                    // replace url swagger configObject configuration
                    response = response.Replace($"/{_swaggerPrefix}/{_swaggerDocName}/swagger.json", $"/{requestSegments[0]}/{_swaggerPrefix}/{_swaggerDocName}/swagger.json");
                    buffer = Encoding.UTF8.GetBytes(response);
                }
                else
                {
                    // to prevents undesiderable errors
                    return;
                }

                context.Response.Headers.ContentLength = buffer.Length;
                await referenceToOriginalBody.WriteAsync(buffer, 0, buffer.Length);
                responseBody.Seek(0, SeekOrigin.Begin);
                await responseBody.CopyToAsync(referenceToOriginalBody);
            }
            else
            {
                await _next(context);
            }
        }
    }
}
