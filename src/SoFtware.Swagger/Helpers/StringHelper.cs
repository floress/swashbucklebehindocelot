using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SoFtware.Swagger.Extensions;

namespace SoFtware.Swagger.Helpers
{
    public static class StringHelper
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="response"></param>
        /// <param name="prefix"></param>
        /// <returns></returns>
        public static MemoryStream RewriteJsonBody(string response, string prefix)
        {
            using var ms = new MemoryStream();
            using var wr = new JsonTextWriter(new StreamWriter(ms))
            {
                Indentation = 2,
                IndentChar = ' ',
                Formatting = Formatting.Indented
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
                        wr.WritePropertyName($"{prefix.EnsureBeginSlash()}{originalPath.Name}");
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
            return ms;
        }

        public static string DetectPrefix(string indexPath, string jsonPath)
        {
            //Detect prefix
            // path = /microservice/swagger/index.html
            // jsonPath = /swagger/v1/swagger.json
            var indexSegments = indexPath.GetSegments();
            var jsonSegments = jsonPath.GetSegments();

            var segmentCount = 0;

            while (!indexSegments[segmentCount].Equals(jsonSegments[0]))
                segmentCount++;

            return indexSegments.Take(segmentCount).ToArray().Join('/').EnsureBeginSlash();
        }
    }
}
