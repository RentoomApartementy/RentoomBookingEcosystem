using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using RentoomBooking.SharedClasses.Integrations.Tpay;
using RentoomBooking.SharedClasses.Models.IdoBooking;
using RentoomBooking.SharedClasses.Models.IdoBooking.ReservationManagement;
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
        Task SaveClientInfoAsync(Guid reservationGuid, ClientInfoDto client, InvoiceInfoDto? invoice);
        Task<ReservationSummaryDto> BuildSummaryAsync(Guid reservationGuid);
        Task<PaymentInitResult> InitiatePaymentAsync(Guid reservationGuid);
        Task<PaymentStateDto> GetPaymentStateAsync(Guid reservationGuid);
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

        public async Task SaveClientInfoAsync(Guid reservationGuid, ClientInfoDto client, InvoiceInfoDto? invoice)
        {
            var record = await RequireReservationAsync(reservationGuid); //sprawdz czy rezerwacja istnieje
            record.State.Client = client ?? throw new ArgumentNullException(nameof(client));
            record.State.Invoice = invoice;
            await _store.UpdateAsync(record);
        }

        public async Task<ReservationSummaryDto> BuildSummaryAsync(Guid reservationGuid)
        {
            var record = await RequireReservationAsync(reservationGuid);
            record = await EnsureIdoReservationAsync(record);

            return new ReservationSummaryDto
            {
                ReservationGuid = reservationGuid,
                StartRequest = record.State.StartRequest,
                Client = record.State.Client,
                Invoice = record.State.Invoice,
                IdoReservationId = record.IdoReservationId,
                IdoStatus = record.IdoStatus,
                OfferPrice = record.State.StartRequest?.OfferPrice,
                Currency = record.State.StartRequest?.Currency ?? "PLN"
            };
        }


        public async Task<PaymentInitResult> InitiatePaymentAsync(Guid reservationGuid)
        {
            while (true)
            {
                var record = await RequireReservationAsync(reservationGuid);
                record = await EnsureIdoReservationAsync(record);

                if (record.PaymentStatus == PaymentStatuses.Paid && record.PaymentSessionGuid.HasValue)
                {
                    var redirectUrl = record.State.PaymentRedirectUrl ?? $"/rezerwuj/{reservationGuid}/podsumowanie-transakcji";

                    return new PaymentInitResult
                    {
                        ReservationGuid = reservationGuid,
                        PaymentSessionGuid = record.PaymentSessionGuid.Value,
                        ProviderTransactionId = record.ProviderTransactionId ?? string.Empty,
                        RedirectUrl = redirectUrl,
                        Provider = record.Provider ?? "TPAY"
                    };
                }

                if (record.PaymentStatus == PaymentStatuses.Initiated && record.PaymentSessionGuid.HasValue)
                {
                    var redirectUrl = record.State.PaymentRedirectUrl ?? $"/tpay-mock/{record.PaymentSessionGuid}?reservationGuid={reservationGuid}";

                    return new PaymentInitResult
                    {
                        ReservationGuid = reservationGuid,
                        PaymentSessionGuid = record.PaymentSessionGuid.Value,
                        ProviderTransactionId = record.ProviderTransactionId ?? string.Empty,
                        RedirectUrl = redirectUrl,
                        Provider = record.Provider ?? "TPAY"
                    };
                }

                var paymentSessionGuid = Guid.NewGuid();
                var amount = record.State.StartRequest?.OfferPrice ?? 0m;
                var currency = record.State.StartRequest?.Currency ?? "PLN";

                var paymentResult = await _tpayGateway.CreatePaymentAsync(reservationGuid, paymentSessionGuid, amount, currency);
                if (!paymentResult.Success)
                {
                    throw new InvalidOperationException("Failed to initiate payment session.");
                }

                record.PaymentSessionGuid = paymentSessionGuid;
                record.PaymentStatus = PaymentStatuses.Initiated;
                record.Provider = record.Provider ?? "TPAY";
                record.ProviderTransactionId = paymentResult.TransactionId;
                record.State.PaymentRedirectUrl = paymentResult.RedirectUrl;
                record.IdoStatus = ReservationStatusType.WaitingForPayment;

                try
                {
                    await _store.UpdateAsync(record);
                    await UpdateIdoStatusAsync(record, ReservationStatusType.WaitingForPayment);

                    return new PaymentInitResult
                    {
                        ReservationGuid = reservationGuid,
                        PaymentSessionGuid = paymentSessionGuid,
                        ProviderTransactionId = paymentResult.TransactionId,
                        RedirectUrl = paymentResult.RedirectUrl,
                        Provider = record.Provider ?? "TPAY"
                    };
                }
                catch (DbUpdateConcurrencyException)
                {
                    _logger.LogWarning("Concurrency conflict while initiating payment for {ReservationGuid}. Retrying.", reservationGuid);
                    await Task.Delay(TimeSpan.FromMilliseconds(50));
                }
            }
        }

        public async Task<PaymentStateDto> GetPaymentStateAsync(Guid reservationGuid)
        {
            var record = await RequireReservationAsync(reservationGuid);
            return new PaymentStateDto
            {
                ReservationGuid = reservationGuid,
                PaymentStatus = record.PaymentStatus,
                PaymentSessionGuid = record.PaymentSessionGuid,
                ProviderTransactionId = record.ProviderTransactionId,
                Provider = record.Provider,
                RedirectUrl = record.State.PaymentRedirectUrl,
                IdoStatus = record.IdoStatus
            };
        }
        public async Task HandleTpayWebhookAsync(TpayWebhookDto dto)
        {
            if (dto is null) throw new ArgumentNullException(nameof(dto));

            while (true)
            {
                var record = await RequireReservationAsync(dto.ReservationGuid);
                if (record.PaymentSessionGuid != dto.PaymentSessionGuid)
                {
                    _logger.LogWarning("Payment session guid mismatch for reservation {ReservationGuid}.", dto.ReservationGuid);
                    throw new InvalidOperationException("Payment session mismatch.");
                }

                if (!string.Equals(record.ProviderTransactionId, dto.ProviderTransactionId, StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogWarning("Provider transaction id mismatch for reservation {ReservationGuid}.", dto.ReservationGuid);
                    throw new InvalidOperationException("Transaction mismatch.");
                }

                if (string.IsNullOrWhiteSpace(record.PaymentStatus) || record.PaymentStatus == PaymentStatuses.None)
                {
                    _logger.LogWarning("Received webhook for reservation {ReservationGuid} without initiated payment.", dto.ReservationGuid);
                    throw new InvalidOperationException("Payment not initiated.");
                }

                if (record.PaymentStatus == PaymentStatuses.Paid)
                {
                    return;
                }

                var isPaid = string.Equals(dto.Status, "PAID", StringComparison.OrdinalIgnoreCase);
                record.PaymentStatus = isPaid ? PaymentStatuses.Paid : PaymentStatuses.Failed;
                record.IdoStatus = isPaid ? ReservationStatusType.Confirmed : record.IdoStatus;

                try
                {
                    await _store.UpdateAsync(record);
                    if (isPaid)
                    {
                        await UpdateIdoStatusAsync(record, ReservationStatusType.Confirmed);
                    }
                    return;
                }
                catch (DbUpdateConcurrencyException)
                {
                    _logger.LogWarning("Concurrency conflict while handling webhook for {ReservationGuid}. Retrying.", dto.ReservationGuid);
                    await Task.Delay(TimeSpan.FromMilliseconds(50));
                }
            }
        }



        private async Task<ReservationRecord> RequireReservationAsync(Guid reservationGuid)
        {
            var record = await _store.GetAsync(reservationGuid);
            return record ?? throw new InvalidOperationException($"Reservation {reservationGuid} not found.");
        }

        private async Task<ReservationRecord> EnsureIdoReservationAsync(ReservationRecord record)
        {
            while (true)
            {
                if (record.IdoReservationId is not null)
                {
                    return record;
                }

                var request = BuildReservationAddRequest(record);
                try
                {
                    var idoresponse = await _idoApi.AddReservationAsync(request);
                    
                    if (idoresponse?.Errors is not null )
                        throw new InvalidOperationException($"Reservation {record.ReservationGuid} couldn't be saved in Idobooking with error: {JsonConvert.SerializeObject(idoresponse.Errors)}.");

                   // var errorMessages = string.Join("; ", idoresponse.Errors.ErrorList.Select(e => e.Message));
                   //     throw new InvalidOperationException($"Failed to create IdoBooking reservation: {errorMessages}");
                    
                    record.IdoReservationId = idoresponse.Reservations[0].ReservationId;
                    record.IdoStatus = ReservationStatusType.Unconfirmed;

                    await _store.UpdateAsync(record);
                    return record;
                }
                catch (DbUpdateConcurrencyException ex)
                {
                    _logger.LogWarning("Concurrency conflict while creating IdoBooking reservation for {ReservationGuid}. Retrying.", record.ReservationGuid);
                    record = await RequireReservationAsync(record.ReservationGuid);
                }
            }
        }

        private async Task UpdateIdoStatusAsync(ReservationRecord record, string targetStatus)
        {
            if (record.IdoReservationId is null)
            {
                return;
            }

            var request = new EditReservationsStatusRequest
                                                            {
                                                                ReservationId = record.IdoReservationId.Value,
                                                                Status = targetStatus,
                                                                Notify = ReservationNotifyType.No,
                                                                NotifyService = ReservationNotifyType.No
                                                            };

            await _idoApi.ChangeReservationStatusAsync(request);
        }


        private static NewReservation BuildReservationAddRequest(ReservationRecord record)
        {
            if (record.State.StartRequest is null)
            {
                throw new InvalidOperationException("Reservation start request is missing.");
            }

            var start = record.State.StartRequest;
            var reservation = new NewReservation
            {
                DateFrom = start.StartDate.ToString("yyyy-MM-dd"),
                DateTo = start.EndDate.ToString("yyyy-MM-dd"),
                Price = start.OfferPrice.HasValue ? (float)start.OfferPrice.Value : null,
                Status = ReservationStatusType.Unconfirmed,
                InternalSource = ReservationInternalSourceType.Other,
                Items =
                [
                    new NewReservationItem
                {
                    ObjectItemId = start.ObjectItemId,
                    NumberOfAdults = start.Adults,
                    NumberOfBigChildren = start.Children,
                    Addons = start.SelectedAddons?.Select(a => new NewReservationAddon
                    {
                        AddonId = a.AddonId,
                        Persons = a.Persons,
                        Nights = a.Nights,
                        Quantity = a.Quantity,
                        Price = a.Price,
                        Vat = a.Vat
                    }).ToList()
                }
                ],
                Currency = start.Currency ?? "PLN",
                ClientData = MapClient(record.State.Client, record.State.Invoice)
            };

            /* return new ReservationAddRequest
             {
                 Params = new ReservationAddParams
                 {
                     Reservations = new List<NewReservation> { reservation }
                 }
             };*/
            return reservation;
        }

        private static ClientWithGuest? MapClient(ClientInfoDto? client, InvoiceInfoDto? invoice)
        {
            if (client is null) return null;

            var guests = new List<ClientGuest>
        {
            new()
            {
                FirstName = client.FirstName,
                LastName = client.LastName,
                City = client.City,
                CountryCode = client.CountryCode,
                Email = client.Email,
                Language = "pl",
                Phone = client.Phone,
                Street = client.Street,
                Zipcode = client.ZipCode
            }
        };

            return new ClientWithGuest
            {
                FirstName = client.FirstName,
                LastName = client.LastName,
                Email = client.Email,
                Phone = client.Phone,
                Street = client.Street,
                Zipcode = client.ZipCode,
                City = client.City,
                CountryCode = client.CountryCode,
                Guests = guests,
                InvoiceData = invoice is null
                    ? null
                    : new ClientInvoiceData
                    {
                        FirstName = client.FirstName,
                        LastName = client.LastName,
                        CompanyName = invoice.CompanyName,
                        TaxNumber = invoice.TaxNumber,
                        Street = invoice.Street,
                        Zipcode = invoice.ZipCode,
                        City = invoice.City,
                        CountryCode = client.CountryCode
                    }
            };
        }


    }
}
