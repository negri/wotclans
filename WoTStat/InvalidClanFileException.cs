using System;
using System.Runtime.Serialization;

namespace Negri.Wot
{
    public class InvalidClanFileException : ApplicationException
    {
        
        public InvalidClanFileException(string clanFile, Exception innerException) : base($"Arquivo de clã '{clanFile}' é inválido.", innerException)
        {
            ClanFile = clanFile;
        }

        public string ClanFile { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="T:System.ApplicationException"/> class with serialized data.
        /// </summary>
        /// <param name="info">The object that holds the serialized object data. </param><param name="context">The contextual information about the source or destination. </param>
        protected InvalidClanFileException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}