using Microsoft.Extensions.Configuration;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace RentoomBooking.StayWell.Services
{
    public sealed class FunctionsApiConcurrencyHandler : DelegatingHandler
    {
        private readonly SemaphoreSlim _semaphore;

        public FunctionsApiConcurrencyHandler(IConfiguration configuration)
        {
            var maxParallel = configuration.GetValue<int?>("FunctionsApi:MaxParallelRequests") ?? 4;
            if (maxParallel < 1)
            {
                maxParallel = 1;
            }

            _semaphore = new SemaphoreSlim(maxParallel, maxParallel);
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            await _semaphore.WaitAsync(cancellationToken);
            try
            {
                return await base.SendAsync(request, cancellationToken);
            }
            finally
            {
                _semaphore.Release();
            }
        }
    }
}
