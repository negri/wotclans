using System;

namespace Negri.Wot
{
    /// <summary>
    /// The response for the call to the remote site cleanup old data
    /// </summary>
    public class CleanOldDataResponse
    {
        /// <summary>
        /// Time Taken in ms
        /// </summary>
        public long TimeTaken { get; set; }

        /// <summary>
        /// Elapsed time on deletions
        /// </summary>
        public TimeSpan Elapsed => TimeSpan.FromMilliseconds(TimeTaken);

        public long Deleted { get; set; }
        
        public long DeletedBytes { get; set; }

        public double DeletedMBytes => DeletedBytes / 1024.0 / 1024.0;

        public string[] Errors { get; set; }
    }
}