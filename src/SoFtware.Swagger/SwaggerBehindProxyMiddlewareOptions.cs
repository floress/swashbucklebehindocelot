using System.Collections.Generic;

namespace SoFtware.Swagger
{
    public class SwaggerBehindProxyMiddlewareOptions
    {
        /// <summary>
        /// 
        /// </summary>
        public bool Enabled { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public IEnumerable<string> Prefixes { get; set; }
    }
}