using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using RentoomBooking.SharedClasses.Integrations.Bitrix.Models;
using RentoomBooking.SharedClasses.Integrations.Bitrix.Services;
using RentoomBooking.SharedClasses.Integrations.Tpay;
using RentoomBooking.SharedClasses.Models.IdoBooking;
using RentoomBooking.SharedClasses.Models.IdoBooking.Payments;
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
        Task HandleTpayWebhookAsync(TpayWebhookDto dto);
    }

    public class ReservationWorkflowService : IReservationWorkflowService
    {
        private readonly IReservationStore _store;
        private readonly IdoSellService _idoApi;
        private readonly ITpayGateway _tpayGateway;
        private readonly BitrixService _bitrixService;
        private readonly ILogger<ReservationWorkflowService> _logger;
        private const int BitrixAssignedByUserId = 208;
        public ReservationWorkflowService(
            IReservationStore store,
            IdoSellService idoApi,
            ITpayGateway tpayGateway,
            BitrixService bitrixService,
            ILogger<ReservationWorkflowService> logger)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _idoApi = idoApi ?? throw new ArgumentNullException(nameof(idoApi));
            _tpayGateway = tpayGateway ?? throw new ArgumentNullException(nameof(tpayGateway));
            _bitrixService = bitrixService ?? throw new ArgumentNullException(nameof(bitrixService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<Guid> StartAsync(StartReservationRequest request)
        {
            if (request is null) throw new ArgumentNullException(nameof(request));
            var record = await _store.CreateAsync(request);
            return record.ReservationGuid; //<== to jest tez reservation token dla staywell
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
                record = await EnsureBitrixContactAndDealAsync(record);

            if (record.IdoStatus != ReservationStatusType.Accepted && record.PaymentStatus != PaymentStatuses.Paid)
            {
                record = await EnsureIdoReservationAsync(record, ReservationStatusType.Accepted);
                record = await EnsureBitrixContactAndDealAsync(record);
                record.IdoStatus = ReservationStatusType.WaitingForPayment;

                await _store.UpdateAsync(record);
                await UpdateIdoStatusAsync(record, ReservationStatusType.WaitingForPayment);
                await UpdateBitrixDealAsync(record, "Reservation status updated");

                record = await RequireReservationAsync(reservationGuid);
            }

            return new ReservationSummaryDto
            {
                ReservationGuid = reservationGuid,
                StartRequest = record.State.StartRequest,
                Client = record.State.Client,
                Invoice = record.State.Invoice,
                IdoReservationId = record.IdoReservationId,
                IdoStatus = record.IdoStatus,
                OfferPrice = record.State.StartRequest?.OfferPrice,
                Currency = record.State.StartRequest?.Currency ?? "PLN",
                PaymentStatus = record.PaymentStatus,
                
            };
        }


        public async Task<PaymentInitResult> InitiatePaymentAsync(Guid reservationGuid)
        {
            while (true)
            {
                var record = await RequireReservationAsync(reservationGuid);
                record = await EnsureIdoReservationAsync(record, ReservationStatusType.WaitingForPayment);
                record = await EnsureBitrixContactAndDealAsync(record);

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
                    var redirectUrl = record.State.PaymentRedirectUrl;

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
                    //await AddIdoPaymentAsync(record, amount, currency, paymentResult.TransactionId);
                    await UpdateBitrixDealAsync(record, "Payment initiated");

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

         private async Task<int?> AddIdoPaymentAsync(ReservationRecord record, decimal amount, string currency, string? transactionId)
        {
            if (record.IdoReservationId is null || string.IsNullOrWhiteSpace(transactionId))
            {
                return null;
            }

            var payment = new PaymentAdd
            {
                ReservationId = record.IdoReservationId.Value,
                Value = Convert.ToSingle(amount),
                Currency = currency,
                ExternalPaymentId = transactionId,
            };

            var paymentresult = await _idoApi.AddPaymentAsync(payment);
            return paymentresult?.Results[0].Id;
        }

        private static string MapIdoPaymentStatus(string paymentStatus)
        {
            return paymentStatus switch
            {
                PaymentStatuses.Paid => PaymentStatus.Processed,
                PaymentStatuses.Failed => PaymentStatus.Cancelled,
                PaymentStatuses.Initiated => PaymentStatus.Pending,
                _ => PaymentStatus.Pending
            };
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
                record = await EnsureBitrixContactAndDealAsync(record);
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
                record.IdoStatus = isPaid ? ReservationStatusType.Accepted : record.IdoStatus;

                try
                {
                    await _store.UpdateAsync(record);

                    await _idoApi.FetchReservationByIDFromIdoSellAsync(record.IdoReservationId.Value, true,record.ReservationGuid.ToString("D"));

                    if (isPaid)
                    {
                        var paymentId = await AddIdoPaymentAsync(record, record.State.StartRequest?.OfferPrice ?? 0m, record.State.StartRequest?.Currency ?? "PLN", dto.ProviderTransactionId);
                        await ConfirmIdoPaymentAsync(paymentId.Value); 
                        await UpdateIdoStatusAsync(record, ReservationStatusType.Accepted);
                        //record = await EnsureBitrixContactAndDealAsync(record);
                    }
                    await UpdateBitrixDealAsync(record, "Payment status updated");
                    return;
                }
                catch (DbUpdateConcurrencyException)
                {
                    _logger.LogWarning("Concurrency conflict while handling webhook for {ReservationGuid}. Retrying.", dto.ReservationGuid);
                    await Task.Delay(TimeSpan.FromMilliseconds(50));
                }
            }
        }

        private async Task ConfirmIdoPaymentAsync(int paymentId)
        {
            await _idoApi.ConfirmPaymentsAsync([paymentId]);
        }

        private async Task<ReservationRecord> RequireReservationAsync(Guid reservationGuid)
        {
            var record = await _store.GetAsync(reservationGuid);
            return record ?? throw new InvalidOperationException($"Reservation {reservationGuid} not found.");
        }

        private async Task<ReservationRecord> EnsureIdoReservationAsync(ReservationRecord record, string initialStatus)
        {
            while (true)
            {
                if (record.IdoReservationId is not null)
                {
                    return record;
                }
               
                var request = BuildReservationAddRequest(record, initialStatus);
                try
                {
                    var idoresponse = await _idoApi.AddReservationAsync(request);
                    
                    if (idoresponse?.Errors is not null )
                        throw new InvalidOperationException($"Reservation {record.ReservationGuid} couldn't be saved in Idobooking with error: {JsonConvert.SerializeObject(idoresponse.Errors)}.");

                    if (idoresponse.Reservations is not null && idoresponse.Reservations.Count > 0)
                    {
                        var resAddResult = idoresponse.Reservations[0];
                        
                        if (resAddResult.Error is not null)
                            throw new InvalidOperationException($"Failed to create IdoBooking reservation: {resAddResult.Error.FaultString}");

                        record.IdoReservationId = idoresponse.Reservations[0].ReservationId;
                        record.IdoStatus = initialStatus;

                        await _store.UpdateAsync(record);
                    }
                    record = await EnsureBitrixContactAndDealAsync(record);
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


        private static NewReservation BuildReservationAddRequest(ReservationRecord record, string initialStatus)
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
                Status = initialStatus,
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
                Language = "pol",
                Phone = client.Phone,
                Street = client.Street,
                Zipcode = client.ZipCode,
                
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
                Currency = "PLN",
                Language = "pol",
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


        private async Task<ReservationRecord> EnsureBitrixContactAndDealAsync(ReservationRecord record)
        {
            if (record.IdoReservationId is null || record.State.Client is null)
            {
                return record;
            }

            var contactRequest = new CreateContactRequest
            {
                FirstName = record.State.Client.FirstName,
                LastName = record.State.Client.LastName,
                Email = record.State.Client.Email,
                Phone = record.State.Client.Phone,
                ReservationId = record.IdoReservationId,
                AssignedById = BitrixAssignedByUserId
            };

            var updated = false;

            if (!record.ClientBitrixId.HasValue)
            {
                record.ClientBitrixId = await _bitrixService.UpsertContactByEmailAsync(contactRequest);
                updated = true;
                _logger.LogInformation("Upserted Bitrix contact {ContactId} for reservation {ReservationGuid}.", record.ClientBitrixId, record.ReservationGuid);
            }
            else
            {
                await _bitrixService.UpdateContactAsync(record.ClientBitrixId.Value, contactRequest);
                updated = true;
                _logger.LogInformation("Updated Bitrix contact {ContactId} for reservation {ReservationGuid}.", record.ClientBitrixId, record.ReservationGuid);
            }

            if (!record.DealBitrixId.HasValue)
            {
                var pipelines = await _bitrixService.GetDealPipelinesAsync();
                var rentalPipeline = pipelines.FirstOrDefault(p => string.Equals(p.Name, "Rezerwacje", StringComparison.OrdinalIgnoreCase));
                var pipelineId = rentalPipeline?.Id ?? 0;
                var stages = await _bitrixService.GetDealStagesAsync(pipelineId);
                var newStage = stages.FirstOrDefault(s => string.Equals(s.Name, "W toku", StringComparison.OrdinalIgnoreCase));

                var dealTitle = record.IdoReservationId.HasValue
                    ? $"Reservation #{record.IdoReservationId}"
                    : $"Reservation {record.ReservationGuid:D}";

                record.DealBitrixId = await _bitrixService.AddDealAsync(new CreateDealRequest(
                    Title: dealTitle,
                    CategoryId: pipelineId,
                    StageId: newStage?.StageId ?? "NEW",
                    AssignedById: BitrixAssignedByUserId,
                    Opportunity: record.State.StartRequest?.OfferPrice,
                    CurrencyId: record.State.StartRequest?.Currency ?? "PLN",
                    ContactId: record.ClientBitrixId
                ));

                updated = true;
                _logger.LogInformation("Created Bitrix deal {DealId} for reservation {ReservationGuid}.", record.DealBitrixId, record.ReservationGuid);
            }

            if (updated)
            {
                await _store.UpdateAsync(record);
            }

            return record;
        }

        private async Task UpdateBitrixDealAsync(ReservationRecord record, string updateReason)
        {
            if (!record.DealBitrixId.HasValue)
            {
                return;
            }

            var fields = new Dictionary<string, object?>
            {
                ["COMMENTS"] = $"Reservation status: {record.IdoStatus ?? "Unknown"}, Payment status: {record.PaymentStatus} ({updateReason}).",

                //RB_Status_Platnosci
                ["UF_CRM_1768566732609"] = record.PaymentStatus

            };

            if (record.State.StartRequest?.OfferPrice is not null)
            {
                fields["OPPORTUNITY"] = record.State.StartRequest.OfferPrice.Value;
            }

            if (!string.IsNullOrWhiteSpace(record.State.StartRequest?.Currency))
            {
                fields["CURRENCY_ID"] = record.State.StartRequest.Currency;
            }

            if (record.ClientBitrixId.HasValue)
            {
                fields["CONTACT_ID"] = record.ClientBitrixId.Value;
            }
            
            //RB_Nazwa_Apartamentu
            fields["UF_CRM_1768566682522"] = "";
            
            //RB_Status_Rezerwacji
            fields["UF_CRM_1768566710921"] = "";

            //RB_KodTpay_Platnosci
            fields["UF_CRM_1768566766553"] = "";

            //RB_Poczatek_Rezerwacji
            fields["UF_CRM_1768566963962"] = "";
            
            //RB_Koniec_Rezerwacji
            fields["UF_CRM_1768566980297"] = "";

            //RB_ID_Rezrerwacji
            fields["UF_CRM_1768835556855"] = "";

            //RB_Link_StayWell
            fields["UF_CRM_1768835603310"] = "";

            //RB_Ilosc_Gosci
            fields["UF_CRM_1768836801823"] = "";

            //RB_Ilosc_Nocy
            fields["UF_CRM_1768836818927"] = "";


            await _bitrixService.UpdateDealAsync(record.DealBitrixId.Value, fields);
        }

    }
}
