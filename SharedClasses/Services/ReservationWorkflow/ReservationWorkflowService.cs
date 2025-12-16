using Microsoft.Extensions.Logging;
using RentoomBooking.SharedClasses.Integrations.Tpay;
using RentoomBooking.SharedClasses.Models.IdoBooking.ReservationWorkflow;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RentoomBooking.SharedClasses.Services.ReservationWorkflow
{

    public interface IReservationWorkflowService
    {
        Task<Guid> StartAsync(StartReservationRequest request);
       // Task SaveClientInfoAsync(Guid reservationGuid, ClientInfoDto client, InvoiceInfoDto? invoice);
     //   Task<ReservationSummaryDto> BuildSummaryAsync(Guid reservationGuid);
       // Task<PaymentInitResult> InitiatePaymentAsync(Guid reservationGuid);
       // Task<PaymentStateDto> GetPaymentStateAsync(Guid reservationGuid);
       // Task HandleTpayWebhookAsync(TpayWebhookDto dto);
    }

    public class ReservationWorkflowService : IReservationWorkflowService
    {
        private readonly IReservationStore _store;
        private readonly IdoSellService _idoApi;
        private readonly ITpayGateway _tpayGateway;
        private readonly ILogger<ReservationWorkflowService> _logger;

        public ReservationWorkflowService(
            IReservationStore store,
            IdoSellService idoApi,
            ITpayGateway tpayGateway,
            ILogger<ReservationWorkflowService> logger)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _idoApi = idoApi ?? throw new ArgumentNullException(nameof(idoApi));
            _tpayGateway = tpayGateway ?? throw new ArgumentNullException(nameof(tpayGateway));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<Guid> StartAsync(StartReservationRequest request)
        {
            if (request is null) throw new ArgumentNullException(nameof(request));
            var record = await _store.CreateAsync(request);
            return record.ReservationGuid;
        }


    }
}
