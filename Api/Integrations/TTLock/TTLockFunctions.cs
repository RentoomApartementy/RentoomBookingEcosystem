using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using RentoomBooking.SharedClasses.Integrations.TTLock;
using System.Net;

namespace RentoomBooking.Api.Locks
{
    public class TTLockFunctions
    {
        private readonly TTLockService _lockService;

        public TTLockFunctions(TTLockService lockService)
        {
            _lockService = lockService;
        }

        [Function("TTLockUnlock")]
        public async Task<HttpResponseData> Unlock([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "locks/{lockId}/unlock")] HttpRequestData req, int lockId)
        {
            var result = await _lockService.UnlockAsync(lockId);
            var res = req.CreateResponse(result.IsSuccess ? HttpStatusCode.OK : HttpStatusCode.BadRequest);
            await res.WriteAsJsonAsync(result);
            return res;
        }

        [Function("TTLockLock")]
        public async Task<HttpResponseData> Lock([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "locks/{lockId}/lock")] HttpRequestData req, int lockId)
        {
            var result = await _lockService.LockAsync(lockId);
            var res = req.CreateResponse(result.IsSuccess ? HttpStatusCode.OK : HttpStatusCode.BadRequest);
            await res.WriteAsJsonAsync(result);
            return res;
        }

        [Function("TTLockGetState")]
        public async Task<HttpResponseData> GetState([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "locks/{lockId}/state")] HttpRequestData req, int lockId)
        {
            var result = await _lockService.GetLockStateAsync(lockId);
            var res = req.CreateResponse(result.IsSuccess ? HttpStatusCode.OK : HttpStatusCode.BadRequest);
            await res.WriteAsJsonAsync(result);
            return res;
        }

        [Function("TTLockGetBattery")]
        public async Task<HttpResponseData> GetBattery([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "locks/{lockId}/battery")] HttpRequestData req, int lockId)
        {
            var result = await _lockService.GetBatteryLevelAsync(lockId);
            var res = req.CreateResponse(result.IsSuccess ? HttpStatusCode.OK : HttpStatusCode.BadRequest);
            await res.WriteAsJsonAsync(result);
            return res;
        }
    }
}