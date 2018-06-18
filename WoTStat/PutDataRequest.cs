using Newtonsoft.Json;
using System.Text;

namespace Negri.Wot
{
    /// <summary>
    /// A request to put data on the remote server
    /// </summary>
    public class PutDataRequest
    {
        /// <summary>
        /// The API Key
        /// </summary>
        public string ApiKey { get; set; }

        /// <summary>
        /// The context of what is being put
        /// </summary>
        public string Context { get; set; }

        /// <summary>
        /// The data, as a zipped json
        /// </summary>
        public byte[] Data { get; set; }

        /// <summary>
        /// Set a string as data (zip compressed)
        /// </summary>
        public void SetString(string content)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                Data = null;
                return;
            }

            Data = content.Zip();
        }

        /// <summary>
        /// Get the data as string
        /// </summary>
        /// <returns></returns>
        public string GetString()
        {
            if ((Data == null) || (Data.Length == 0))
            {
                return string.Empty;
            }
            return Data.Unzip();
        }

        /// <summary>
        /// Set an object as the data
        /// </summary>
        public void SetObject<T>(T obj)
        {
            if (obj == null)
            {
                Data = null;
                return;
            }

            var s = JsonConvert.SerializeObject(obj);
            SetString(s);
        }

        /// <summary>
        /// Get the data object
        /// </summary>
        public T GetObject<T>()
        {
            if ((Data == null) || (Data.Length == 0))
            {
                return default(T);
            }

            var s = GetString();
            return JsonConvert.DeserializeObject<T>(s);
        }

    }
}