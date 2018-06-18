using System;
using System.Runtime.Serialization;

namespace Negri.Wot
{
    public class DisbandedClanException : ApplicationException
    {
        public DisbandedClanException(long clanId) : base($"Clã id {clanId} foi desfeito!")
        {
            ClanId = clanId;
        }

        public DisbandedClanException(long clanId, Exception innerException) : base($"Clã id {clanId} foi desfeito!", innerException)
        {
            ClanId = clanId;
        }

        public long ClanId { get; set; }

        protected DisbandedClanException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}