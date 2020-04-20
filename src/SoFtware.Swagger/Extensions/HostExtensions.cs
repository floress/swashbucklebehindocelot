using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace SoFtware.Swagger.Extensions
{
    /// <summary>
    /// 
    /// </summary>
    public static class HostExtensions
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="webBuilder"></param>
        /// <returns></returns>
        public static IWebHostBuilder AddSwaggerBehindProxy(this IWebHostBuilder webBuilder)
        {
            webBuilder.ConfigureServices((context, services) =>
            {
                services.Configure<SwaggerBehindProxyMiddlewareOptions>(context.Configuration.GetSection("SwaggerBehindProxy"));
            });

            return webBuilder;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="app"></param>
        /// <returns></returns>
        public static IApplicationBuilder UseSwaggerBehindProxy(this IApplicationBuilder app)
        {
            app.UseMiddleware<SwaggerBehindProxyMiddleware>();

            return app;
        }
    }
}
