using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using RentoomBooking.SharedClasses.Database;
using RentoomBooking.SharedClasses.Integrations.Bitrix.Models;
using RentoomBooking.SharedClasses.Integrations.Bitrix.Services;
using RentoomBooking.SharedClasses.Integrations.RentoomApp.Partners.Models;
using RentoomBooking.SharedClasses.Integrations.Tpay;
using RentoomBooking.SharedClasses.Models;
using RentoomBooking.SharedClasses.Models.IdoBooking;
using RentoomBooking.SharedClasses.Models.IdoBooking.Payments;
using RentoomBooking.SharedClasses.Models.IdoBooking.ReservationManagement;
using RentoomBooking.SharedClasses.Models.ReservationWorkflow;
using RentoomBooking.SharedClasses.Models.Upsell;
using RentoomBooking.SharedClasses.Services.Upsell;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace RentoomBooking.SharedClasses.Services.ReservationWorkflow
{

    public interface IReservationWorkflowService
    {
        Task<Guid> StartAsync(StartReservationRequest request);
        Task SaveClientInfoAsync(Guid reservationGuid, ClientInfoDto client, InvoiceInfoDto? invoice);
        Task<ReservationSummaryDto> BuildSummaryAsync(Guid reservationGuid);
        Task<ReservationSummaryDto> BuildDraftSummaryAsync(Guid reservationGuid);
        Task<PaymentInitResult> InitiatePaymentAsync(Guid reservationGuid);
        Task<PaymentStateDto> GetPaymentStateAsync(Guid reservationGuid);
        Task HandleTpayWebhookAsync(TpayWebhookDto dto);
        Task MarkPaymentAsPaidAsync(Guid reservationGuid); //for dummy scenario
        Task<DealEmailStatusDto> GetDealEmailStatusAsync(Guid reservationGuid);
        Task SaveCustomerTermsAsync(Guid reservationGuid, Dictionary<int, bool> termSelections);
    }

    public class ReservationWorkflowService : IReservationWorkflowService
    {
        private readonly IReservationStore _store;
        private readonly ApartmentRepository _apartmentStore;
        private readonly IdoSellService _idoApi;
        private readonly ITpayGateway _tpayGateway;
        private readonly BitrixService _bitrixService;
        private readonly IConfiguration _configuration;

        private readonly IUpsellCatalogService _upsellCatalogService;
        private readonly IUpsellOrderWorkflowService _upsellOrderWorkflowService;


        private readonly CustomerTermsRepository _termsRepository;
        private readonly ILogger<ReservationWorkflowService> _logger;
        private const int BitrixAssignedByUserId = 208;
        public ReservationWorkflowService(
            IReservationStore store,
            IdoSellService idoApi,
            ITpayGateway tpayGateway,
            BitrixService bitrixService,
            IConfiguration configuration,
            ApartmentRepository apartmentStore,
             IUpsellCatalogService upsellCatalogService,
             IUpsellOrderWorkflowService upsellOrderWorkflowService,
            CustomerTermsRepository termsRepository,
        ILogger<ReservationWorkflowService> logger)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _idoApi = idoApi ?? throw new ArgumentNullException(nameof(idoApi));
            _tpayGateway = tpayGateway ?? throw new ArgumentNullException(nameof(tpayGateway));
            _bitrixService = bitrixService ?? throw new ArgumentNullException(nameof(bitrixService));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _apartmentStore = apartmentStore ?? throw new ArgumentNullException(nameof(apartmentStore));
            _upsellCatalogService = upsellCatalogService ?? throw new ArgumentNullException(nameof(upsellCatalogService));
            _upsellOrderWorkflowService = upsellOrderWorkflowService ?? throw new ArgumentNullException(nameof(upsellOrderWorkflowService));
            _termsRepository = termsRepository;
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
            return await BuildSummaryFromRecordAsync(reservationGuid, record);
        }

        public async Task<ReservationSummaryDto> BuildDraftSummaryAsync(Guid reservationGuid)
        {
            var record = await RequireReservationAsync(reservationGuid);
            return await BuildSummaryFromRecordAsync(reservationGuid, record);
        }

        private static ReservationSummaryDto BuildSummaryFromRecord(Guid reservationGuid, ReservationRecord record)
        {
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

        private async Task<ReservationSummaryDto> BuildSummaryFromRecordAsync(Guid reservationGuid, ReservationRecord record)
        {
            var startRequest = record.State.StartRequest;
            var upsellLines = new List<UpsellSummaryLineDto>();
            var upsellsTotal = 0m;

            if (startRequest?.SelectedUpsells?.Count > 0)
            {
                var culture = CultureInfo.CurrentUICulture.Name;
                var tiles = await _upsellCatalogService.GetUpsellTilesForApartmentAsync(startRequest.ObjectId, culture,"all");
                
                var tileLookup = tiles.ToDictionary(tile => tile.PartnerServiceId);
                
                var pricingContext = new ReservationPricingContext
                {
                    StartDate = startRequest.StartDate,
                    EndDate = startRequest.EndDate,
                    Adults = startRequest.Adults,
                    Children = startRequest.Children,
                    Currency = startRequest.Currency ?? "PLN"
                };

                foreach (var selected in startRequest.SelectedUpsells)
                {
                    if (!tileLookup.TryGetValue(selected.PartnerServiceId, out var tile))
                    {
                        continue;
                    }

                    var quantity = Math.Max(1, selected.Quantity);
                    
                    
                    var lineTotal = UpsellPricingCalculator.CalculateTotal(
                        tile.PricingModel,
                        tile.Price,
                        pricingContext.Nights,
                        pricingContext.TotalGuests,
                        quantity);

                    upsellLines.Add(new UpsellSummaryLineDto
                    {
                        PartnerServiceId = tile.PartnerServiceId,
                        Title = tile.Title,
                        PricingModel = tile.PricingModel,
                        Quantity = quantity,
                        UnitPriceGross = tile.Price,
                        Nights = pricingContext.Nights,
                        TotalGuests = pricingContext.TotalGuests,
                        LineTotalGross = lineTotal,
                        DisplayText = $"{tile.Price} {tile.Currency}"
                    });

                    upsellsTotal += lineTotal;
                }
            }

            decimal addonsTotal = 0m;//startRequest?.SelectedAddons?.Sum(addon => (decimal)addon.Price * addon.Quantity) ?? 0m; //wyliczenie do sprawdzenia bo nie bierze pod uwage typu addonu

            foreach (var addon in startRequest?.SelectedAddons ?? [])
            {
                addonsTotal +=AddonPricingCalculator.CalculateTotal(addon.PaymentType, (decimal)addon.Price, addon.Nights, startRequest.Adults + startRequest.Children, addon.Quantity);
            }

            var offerPrice = startRequest?.OfferPrice ?? 0m;
            
            decimal grandTotal = offerPrice + addonsTotal + upsellsTotal;

            return new ReservationSummaryDto
            {
                ReservationGuid = reservationGuid,
                StartRequest = startRequest,
                Client = record.State.Client,
                Invoice = record.State.Invoice,
                IdoReservationId = record.IdoReservationId,
                IdoStatus = record.IdoStatus,
                OfferPrice = startRequest?.OfferPrice,
                Currency = startRequest?.Currency ?? "PLN",
                PaymentStatus = record.PaymentStatus,
                Upsells = upsellLines,
                UpsellsTotal = upsellsTotal,
                GrandTotal = grandTotal
            };
        }

        private async Task EnsurePaymentTotalsAsync(Guid reservationGuid, ReservationRecord record)
        {
            var summary = await BuildSummaryFromRecordAsync(reservationGuid, record);
            var updated = false;

            if (record.State.PaymentUpsellsTotal != summary.UpsellsTotal)
            {
                record.State.PaymentUpsellsTotal = summary.UpsellsTotal;
                updated = true;
            }

            if (record.State.PaymentGrandTotal != summary.GrandTotal)
            {
                record.State.PaymentGrandTotal = summary.GrandTotal;
                updated = true;
            }

            if (updated)
            {
                await _store.UpdateAsync(record);
            }
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
                    await EnsurePaymentTotalsAsync(reservationGuid, record);
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
                    await EnsurePaymentTotalsAsync(reservationGuid, record);
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

                var summary = await BuildSummaryFromRecordAsync(reservationGuid, record);
                var amount = summary.GrandTotal;

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
                record.State.PaymentUpsellsTotal = summary.UpsellsTotal;
                record.State.PaymentGrandTotal = summary.GrandTotal;

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
                 //   return;
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
                        GetDealEmailStatusAsync(record.ReservationGuid);
                        await CreatePaidUpsellOrderAsync(record, dto.ProviderTransactionId);
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



        //metoda tylko dla RentoomBooking (Nie Staywell, ):
        //po potwierdzeniu płatności za rezerwację wywołuję CreatePaidOrderAsync
        //z danymi  wierszy uzyskanymi z wybranych ofert dodatkowych rezerwacji (z rekordu rezerwacji) ,
        //aby upselle zakupione w ramach rezerwacji zalogować w tej samej tabeli wierszy co zakupy w StayWell!
        private async Task CreatePaidUpsellOrderAsync(ReservationRecord record, string providerTransactionId)
        {
            var request = record.State.StartRequest;
            if (request is null || request.SelectedUpsells.Count == 0)
            {
                return;
            }

            var culture = CultureInfo.CurrentUICulture.Name;
            var tiles = await _upsellCatalogService.GetUpsellTilesForApartmentAsync(request.ObjectId, culture, "rentoombooking"); //<<--tylko dla rentoombooking!
            var tileLookup = tiles.ToDictionary(tile => tile.PartnerServiceId);

            var pricingContext = new ReservationPricingContext
            {
                StartDate = request.StartDate,
                EndDate = request.EndDate,
                Adults = request.Adults,
                Children = request.Children,
                Currency = request.Currency ?? "PLN"
            };

            var lines = new List<UpsellOrderLineRecord>();
            foreach (var selected in request.SelectedUpsells)
            {
                if (!tileLookup.TryGetValue(selected.PartnerServiceId, out var tile))
                {
                    continue;
                }

                var quantity = Math.Max(1, selected.Quantity);
                var lineTotal = UpsellPricingCalculator.CalculateTotal(
                    tile.PricingModel,
                    tile.Price,
                    pricingContext.Nights,
                    pricingContext.TotalGuests,
                    quantity);

                lines.Add(new UpsellOrderLineRecord
                {
                    UpsellOrderGuid = Guid.Empty,
                    PartnerServiceId = tile.PartnerServiceId,
                    TitleSnapshot = tile.Title,
                    PricingModel = tile.PricingModel,
                    Quantity = quantity,
                    UnitPriceGross = tile.Price,
                    Nights = pricingContext.Nights,
                    TotalGuests = pricingContext.TotalGuests,
                    LineTotalGross = lineTotal,
                    Currency = request.Currency ?? "PLN",
                    LineStatus = UpsellLineStatuses.Paid,
                    UpsellDefinitionSnapshot = tile.PartnerServiceInfo
                });
            }

            if (lines.Count == 0)
            {
                return;
            }
            //pełny order
            var orderRequest = new UpsellOrderRequest
            {
                ReservationGuid = record.ReservationGuid,
                ApartmentId = request.ObjectId,
                StartDate = request.StartDate,
                EndDate = request.EndDate,
                Adults = request.Adults,
                Children = request.Children,
                Currency = request.Currency ?? "PLN",
                Buyer = new UpsellBuyerDto
                {
                    Email = record.State.Client?.Email ?? string.Empty,
                    Name = string.Join(" ", new[] { record.State.Client?.FirstName, record.State.Client?.LastName }.Where(x => !string.IsNullOrWhiteSpace(x)))
                },
                SelectedUpsells = request.SelectedUpsells //<<-- one też trafiają do osobnej tabeli gdzie mozna dowolnie je skanselowac, odwołać . Tu  jako JSON snapshot początkowy - taki audit log pierwotnego zakupu
                    .Select(u => new UpsellOrderLineRequest { PartnerServiceId = u.PartnerServiceId, Quantity = u.Quantity })
                    .ToList()
            };

            await _upsellOrderWorkflowService.CreatePaidOrderAsync(
                orderRequest,
                lines,
                providerTransactionId,
                record.Provider,
                DateTime.UtcNow);
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
                RentoomResrvationID = record.ReservationGuid, //TODO 7.02.26: sprawdzic czy bedzie dzialac do zapisu idobooking -czy pominie to pole.
                DateFrom = start.StartDate.ToString("yyyy-MM-dd") +" " +start.CheckInTime.ToString("HH:mm"),
                DateTo = start.EndDate.ToString("yyyy-MM-dd") + " " + start.CheckOutTime.ToString("HH:mm"),
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
                        Vat = a.Vat,
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


            Reservation? idoReservation = null;
            ApartmentObject? apartmentInf = null;
            if (record.IdoReservationId.HasValue)
            {
                var idoResponse = await _idoApi.FetchReservationByIDFromIdoSellAsync(record.IdoReservationId.Value, false);
                idoReservation = idoResponse?.ReservationResponse?.result?.Reservations?.FirstOrDefault();

                apartmentInf = _apartmentStore.FindApartmentInPostgres(record.State.StartRequest.ObjectId);
            }

            //pola UF_CRM* to pola customowe - tu sa wpisane na sztywno ale mozna je pobrac z bitrixa dynamicznie jesli trzeba.. ewentualne TODO.

            var fields = new Dictionary<string, object?>
            {
                ["COMMENTS"] = $"{DateTime.Now.ToString()}: Status Rezerwacji (z IDB): {record.IdoStatus ?? "Unknown"}, Status Platnosci TPAY: {record.PaymentStatus} ({updateReason}).",

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
            fields["UF_CRM_1768566682522"] = idoReservation.Items[0].objectName;

            //RB_Adres_Apartamentu
            fields["UF_CRM_1768840472108"] = apartmentInf.ObjectLocation.LocalizationItem.ZipCode + " " +
                apartmentInf.ObjectLocation.LocalizationItem.City +", ul. " + apartmentInf.ObjectLocation.LocalizationItem.Street;

            

            //RB_Status_Rezerwacji
            fields["UF_CRM_1768566710921"] = record.IdoStatus;

            //RB_KodTpay_Platnosci
            fields["UF_CRM_1768566766553"] = string.Empty;

            //RB_Poczatek_Rezerwacji
            fields["UF_CRM_1768566963962"] = idoReservation.ReservationDetails.dateFrom;
            fields["BEGINDATE"] = idoReservation.ReservationDetails.dateFrom; //deal field


            //RB_Koniec_Rezerwacji
            fields["UF_CRM_1768566980297"] = idoReservation.ReservationDetails.dateTo;
            fields["CLOSEDATE"] = idoReservation.ReservationDetails.dateTo; //deal field

            //RB_ID_Rezrerwacji
            fields["UF_CRM_1768835556855"] = record.IdoReservationId;

            //RB_Link_StayWell
            fields["UF_CRM_1768835603310"] = BuildStayWellLink(record.ReservationGuid.ToString());

            //RB_Ilosc_Gosci
            fields["UF_CRM_1768836801823"] = idoReservation.Client.Guests.Count;

            //RB_Ilosc_Nocy
            fields["UF_CRM_1768836818927"] = idoReservation.ReservationDetails.getDuration();


            await _bitrixService.UpdateDealAsync(record.DealBitrixId.Value, fields);
        }

        private string? BuildStayWellLink(string? resToken)
        {
            var baseUrl =
                Environment.GetEnvironmentVariable("StayWell__ReservationUrlBase") ??
                Environment.GetEnvironmentVariable("StayWellReservationUrlBase") ??
                _configuration["StayWell:ReservationUrlBase"] ??
                _configuration["StayWellReservationUrlBase"];

            if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(resToken))
            {
                return null;
            }

            return baseUrl.Replace("{resToken}", resToken).TrimEnd('/');
        }

        public async Task<DealEmailStatusDto> GetDealEmailStatusAsync(Guid reservationGuid)
        {
            var record = await RequireReservationAsync(reservationGuid);

            if (!record.DealBitrixId.HasValue)
            {
                record = await EnsureBitrixContactAndDealAsync(record);
            }

            if (!record.DealBitrixId.HasValue)
            {
            
                
                
                return new DealEmailStatusDto
                {
                    EmailSent = false,
                    HasActivities = false
                };
            }

            //najpierw sprawdz czy w bazie jest.
            if (!String.IsNullOrEmpty(record.DealBitrixSentConfirmationEmailId))
            {
                return new DealEmailStatusDto
                {
                    EmailSent = true,
                    HasActivities = true
                };
            }

            var activities = await _bitrixService.ListDealEmailActivitiesAsync(record.DealBitrixId.Value);
            
            var latest = activities.FirstOrDefault();
            var emailSent = latest is not null
                && string.Equals(latest.Completed, "Y", StringComparison.OrdinalIgnoreCase)
                && string.Equals(latest.Status, "2", StringComparison.OrdinalIgnoreCase);
            record.DealBitrixSentConfirmationEmailId = emailSent ? latest.Id : null;
            
           await _store.UpdateAsync(record); 

            return new DealEmailStatusDto
            {
                EmailSent = emailSent, // czy byl wyslany mail z bitrixa
                HasActivities = activities.Count > 0, // czy w ogole sa maile w pipeline
                LatestActivity = latest,
                Activities = activities
            };
        }

        public async Task SaveCustomerTermsAsync(Guid reservationGuid, Dictionary<int, bool> termSelections)
        {
            var record = await RequireReservationAsync(reservationGuid);

            var agreedEntities = termSelections.Select(kvp => new CustomerAgreedTerms
            {
                ReservationGuid = reservationGuid,
                TermsSourceId = kvp.Key,
                IsAccepted = kvp.Value, 
                AgreedAt = DateTime.UtcNow,
                ClientBitrixId = record.ClientBitrixId,
            }).ToList();

            await _termsRepository.AddAgreedTermsAsync(agreedEntities);

            _logger.LogInformation("Saved {Count} term states for reservation {Guid}", agreedEntities.Count, reservationGuid);
        }

        //<summary>
        /// Metoda do ręcznego oznaczania płatności jako opłaconej. Używana tylko przy lokalnym ustawieniu _idoApi.UseDummyIdoBooking = true, czyli przy testach bez integracji z Idobooking. Oznacza rezerwację jako opłaconą zarówno w naszej bazie, jak i w Bitrixie, ale nie dodaje płatności do Idobooking ani nie zmienia statusu rezerwacji w Idobooking (bo w trybie dummy nie tworzymy rezerwacji w Idobooking).
        /// W trybie dummy też nie dostajemy webhooków z TPAY (gdy na lokalnej maszynie), więc ta metoda pozwala zasymulować sytuację po opłaceniu rezerwacji, żeby móc przetestować dalsze kroki workflow, takie jak wysyłka maili z Bitrixa czy zmiana statusu rezerwacji.
        //</summary>
        public async Task MarkPaymentAsPaidAsync(Guid reservationGuid)
        {
            if (!_idoApi.UseDummyIdoBooking)
            {
                return;
            }

            var record = await RequireReservationAsync(reservationGuid);
            if (record.PaymentStatus == PaymentStatuses.Paid)
            {
                return;
            }

            record.PaymentStatus = PaymentStatuses.Paid;
            record.IdoStatus = ReservationStatusType.Accepted;
            await _store.UpdateAsync(record);

            var dto = new TpayWebhookDto
            {
                ReservationGuid = record.ReservationGuid,
                PaymentSessionGuid = record.PaymentSessionGuid.Value,
                ProviderTransactionId = record.ProviderTransactionId,
                Status = PaymentStatuses.Paid,
                Signature = "validated"
            };

            await HandleTpayWebhookAsync(dto);

            
            var fields = new Dictionary<string, object?>()
            {
                //RB_Status_Rezerwacji
                ["UF_CRM_1768566710921"] = ReservationStatusType.Accepted,
                //RB_Status_Platnosci
                ["UF_CRM_1768566732609"] = record.PaymentStatus

            };
            await _bitrixService.UpdateDealAsync(record.DealBitrixId.Value, fields);
            
        }
    }
}
