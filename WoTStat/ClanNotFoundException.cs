using System;
using System.Net;
using System.Runtime.Serialization;

namespace Negri.Wot
{
    public class ClanNotFoundException : ApplicationException
    {
        
        public ClanNotFoundException(Platform platform, long clanId, string clanTag, string url, WebException innerException) :
            base($"{clanId}.{clanTag}@{platform} não achado (404) em '{url}'", innerException)
        {
            Platform = platform;
            ClanTag = clanTag;
            Url = url;
            ClanId = clanId;
        }

        /// <summary>
        ///     Initializes a new instance of the <see cref="T:System.ApplicationException" /> class with serialized data.
        /// </summary>
        /// <param name="info">The object that holds the serialized object data. </param>
        /// <param name="context">The contextual information about the source or destination. </param>
        protected ClanNotFoundException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }

        public string ClanTag { get; private set; }

        public long ClanId { get; private set; }

        public string Url { get; private set; }

        public Platform Platform { get; private set; }
    }
}