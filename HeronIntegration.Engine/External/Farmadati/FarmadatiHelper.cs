using System;
using System.Threading.Tasks;

namespace HeronIntegration.Engine.External.Farmadati;
public static class FarmadatiHelper
{
    public static async Task<T> ExecuteWithRetry<T>(
        Func<Task<T>> action,
        int maxRetries = 3,
        int delayMs = 2000
    )
    {
        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                var result = await action();

                // 💣 NULL = errore → retry
                if (result == null)
                    throw new TimeoutException("Farmadati returned null");

                return result;
            }
            catch (Exception ex) when (IsTimeout(ex))
            {
                if (attempt == maxRetries)
                    throw;

                await Task.Delay(delayMs * attempt);
            }
        }

        throw new Exception("Retry failed");
    }

    private static bool IsTimeout(Exception ex)
    {
        return ex is TimeoutException ||
                ex.InnerException is TimeoutException ||
                ex.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase);
    }
}
