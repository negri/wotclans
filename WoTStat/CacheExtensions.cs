using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Web.Caching;
using log4net;

namespace Negri.Wot
{
    /// <summary>
    /// Extensões para usar o cache com mais comodidade
    /// </summary>
    public static class CacheExtensions
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(CacheExtensions));

        private static readonly object Lock = new object();

        public static T Get<T>(this Cache cache, string key, int lifeTimeMinutes, Func<T> generator)
        {
            lock (Lock)
            {
                var result = cache[key];
                if (result == null)
                {
                    Log.DebugFormat("Cache miss on {0}", key);
                    result = generator();
                    if (lifeTimeMinutes > 0)
                    {
                        cache.Insert(key, result, null, DateTime.UtcNow.AddMinutes(lifeTimeMinutes),
                            Cache.NoSlidingExpiration,
                            CacheItemPriority.Normal, null);
                        Log.DebugFormat("Added {0} on cache with life of {1} minutes.", key, lifeTimeMinutes);
                    }
                    
                }                
                return (T)result;
            }
        }

        public static T Pop<T>(this ConcurrentStack<T> stack, int millisecondsTimeout = 1000)
        {
            T item;
            while(!stack.TryPop(out item))
            {
                Thread.Sleep(millisecondsTimeout);
            }
            return item;
        }

        public static T Dequeue<T>(this ConcurrentQueue<T> stack, int millisecondsTimeout = 1000)
        {
            T item;
            while (!stack.TryDequeue(out item))
            {
                Thread.Sleep(millisecondsTimeout);
            }
            return item;
        }
    }
}