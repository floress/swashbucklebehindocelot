using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using HtmlAgilityPack;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SoFtware.Swagger.Helpers;

namespace SoFtware.Swagger
{
    /// <summary>
    /// 
    /// </summary>
    public class SwaggerBehindProxyMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IOptions<SwaggerBehindProxyMiddlewareOptions> _options;
        private readonly ILogger _logger;
        private static readonly Regex JsonUrlRegex = new Regex("\"url\"\\:\"([\\/a-z0-9]+.json)\"");

        public SwaggerBehindProxyMiddleware(RequestDelegate next,
            ILoggerFactory loggerFactory,
            IOptions<SwaggerBehindProxyMiddlewareOptions> options)
        {
            _next = next;
            _options = options;
            _logger = loggerFactory.CreateLogger<SwaggerBehindProxyMiddleware>();
        }


        /// <summary>
        /// http://localhost:53783/swagger/index.html
        /// http://localhost:53659/microservice/swagger/index.html
        ///
        /// http://localhost:53783/swagger/v1/swagger.json
        /// http://localhost:53659/microservice/swagger/v1/swagger.json
        /// </summary>
        private string IndexUri(string prefix) => $"/{prefix}/index.html";

        private readonly ConcurrentDictionary<string, string> _prefixMapping = new ConcurrentDictionary<string, string>();

        public async Task Invoke(HttpContext context)
        {
            if (_options.Value == null || !_options.Value.Enabled) // Middleware is disabled
            {
                await _next(context);
                return;
            }

            var path = context.Request.PathBase.HasValue ? context.Request.PathBase.Add(context.Request.Path) : context.Request.Path;

            if (!path.HasValue || path.Value.Equals("/"))
            {
                await _next(context);
                return;
            }

            async Task<string> ReadResponse(HttpResponse resp)
            {
                resp.Body.Seek(0, SeekOrigin.Begin);
                var text = await new StreamReader(resp.Body).ReadToEndAsync();
                resp.Body.Seek(0, SeekOrigin.Begin);
                return text;
            }

            // http://localhost:53783/swagger/index.html
            if (_options.Value.Prefixes.Any(p => path.Value.EndsWith(IndexUri(p))) ||
                _prefixMapping.ContainsKey(path))
            {
                var referenceToOriginalBody = context.Response.Body;

                await using var nullBody = new MemoryStream();

                context.Response.Body = nullBody;

                await _next(context);

                var response = await ReadResponse(context.Response);

                byte[] buffer;

                if (_prefixMapping.ContainsKey(path))
                {
                    buffer = StringHelper.RewriteJsonBody(response, _prefixMapping[path]).ToArray();
                }
                else
                {
                    var html = new HtmlDocument();
                    html.LoadHtml(response);
                    var comment = html.CreateComment("<!-- Swashbuckle Behind Ocelot -->\n");
                    html.DocumentNode.InsertBefore(comment, html.DocumentNode.FirstChild);
                    var title = html.DocumentNode.SelectSingleNode("//title")?.InnerText ?? "";

                    if (title.Equals("Swagger UI", StringComparison.InvariantCultureIgnoreCase))
                    {
                        if (JsonUrlRegex.IsMatch(html.DocumentNode.InnerHtml))
                        {
                            var jsonPath = JsonUrlRegex.Match(html.DocumentNode.InnerHtml).Groups[1].Value;
                            var prefix = StringHelper.DetectPrefix(path.Value, jsonPath);
                            var newJsonPath = $"{prefix}{jsonPath}";
                            if (!_prefixMapping.TryAdd(newJsonPath, prefix))
                            {
                                _logger.LogWarning("Couldn't register json path mapping");
                            }
                            buffer = Encoding.UTF8.GetBytes(JsonUrlRegex.Replace(html.DocumentNode.InnerHtml, $"\"url\":\"{newJsonPath}\""));
                        }
                        else
                        {
                            buffer = Encoding.UTF8.GetBytes(response);
                        }
                    }
                    else
                    {
                        buffer = Encoding.UTF8.GetBytes(response);
                    }
                }

                // important! fix content length
                context.Response.Headers.ContentLength = buffer.Length;
                await referenceToOriginalBody.WriteAsync(buffer, 0, buffer.Length);
                //responseBody.Seek(0, SeekOrigin.Begin);
                //await responseBody.CopyToAsync(referenceToOriginalBody);
            }
            else
            {
                await _next(context);
            }
        }
    }
}
