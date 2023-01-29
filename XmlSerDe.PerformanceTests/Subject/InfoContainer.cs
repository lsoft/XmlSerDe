#nullable disable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace XmlSerDe.PerformanceTests.Subject
{
    public partial class InfoContainer
    {
        public List<BaseInfo> InfoCollection {get; set;}

        internal void Reset()
        {
            if(InfoCollection == null)
            {
                InfoCollection = new List<BaseInfo>();
                return;
            }

            InfoCollection.Clear();
        }
    }

    public static class CachedInfoContainer
    {
        public static readonly InfoContainer InfoContainerInstance = new InfoContainer();

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public static InfoContainer Reuse()
        {
            InfoContainerInstance.Reset();
            return InfoContainerInstance;
        }

    }
}
