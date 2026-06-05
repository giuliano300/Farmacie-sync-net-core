using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HeronIntegration.Shared.Singletons
{
    public class FarmadatiJobManager
    {
        public CancellationTokenSource? CancellationTokenSource { get; set; }

        public bool IsRunning =>
            CancellationTokenSource != null &&
            !CancellationTokenSource.IsCancellationRequested;
    }
}
