using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;

namespace JsonLD.Core
{
    public class JsonNormalizer
    {
        private JsonLdOptions options;
        private Object normalizedObject;
        private Object initialObject;

        public JsonNormalizer(object @object, JsonLdOptions options)
        {
            if (options == null)
            {
                this.options = new JsonLdOptions();
                this.options.format = "application/nquads";
            }
            this.options = options;
            this.initialObject = @object;
            try
            {
                options.algorithm = JsonLdOptions.URDNA2015;
                this.normalizedObject = JsonLdProcessor.Normalize(JToken.FromObject(this.initialObject), this.options);
                Console.Out.WriteLine(normalizedObject);

            }
            catch (JsonLdError ex)
            {
                Console.Error.WriteLine(ex);
            }
        }    



    }
}
