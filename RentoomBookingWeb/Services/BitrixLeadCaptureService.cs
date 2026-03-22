using RentoomBooking.SharedClasses.Integrations.Bitrix.Models;
using RentoomBooking.SharedClasses.Integrations.Bitrix.Services;

namespace RentoomBookingWeb.Services;

public sealed record BitrixLeadCaptureRequest(
    string FirstName,
    string LastName,
    string Email,
    string Phone,
    string Message,
    string DealTitle,
    IReadOnlyList<KeyValuePair<string, string?>>? CommentFields = null
);

public sealed record BitrixLeadCaptureResult(int ContactId, int DealId);

public sealed class BitrixLeadCaptureException : Exception
{
    public BitrixLeadCaptureException(bool contactCreated, Exception innerException)
        : base(contactCreated ? "ContactCreatedDealFailed" : "LeadCaptureFailed", innerException)
    {
        ContactCreated = contactCreated;
    }

    public bool ContactCreated { get; }
}

public sealed class BitrixLeadCaptureService
{
    private const string ContactTypeId = "546";
    private const string PipelineName = "Pozyskanie Klienta";

    private readonly BitrixService _bitrixService;
    private readonly ILogger<BitrixLeadCaptureService> _logger;

    public BitrixLeadCaptureService(BitrixService bitrixService, ILogger<BitrixLeadCaptureService> logger)
    {
        _bitrixService = bitrixService;
        _logger = logger;
    }

    public async Task<BitrixLeadCaptureResult> SubmitAsync(BitrixLeadCaptureRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        int? contactId = null;

        try
        {
            var contactRequest = new CreateContactRequest
            {
                FirstName = request.FirstName.Trim(),
                LastName = request.LastName.Trim(),
                Email = request.Email.Trim(),
                Phone = request.Phone.Trim()
            };

            contactId = await _bitrixService.UpsertContactByEmailAsync(contactRequest, ContactTypeId);

            var pipelines = await _bitrixService.GetDealPipelinesAsync();
            var pipeline = pipelines.FirstOrDefault(p => string.Equals(p.Name, PipelineName, StringComparison.OrdinalIgnoreCase));

            if (pipeline is null)
            {
                throw new InvalidOperationException($"Bitrix pipeline '{PipelineName}' was not found.");
            }

            var stages = await _bitrixService.GetDealStagesAsync(pipeline.Id);
            var initialStage = ResolveInitialStage(stages);

            if (initialStage is null)
            {
                throw new InvalidOperationException($"Bitrix pipeline '{PipelineName}' does not have an initial stage.");
            }

            var dealId = await _bitrixService.AddDealAsync(new CreateDealRequest(
                Title: request.DealTitle.Trim(),
                CategoryId: pipeline.Id,
                StageId: initialStage.StageId,
                ContactId: contactId.Value,
                CustomFields: new Dictionary<string, object?>
                {
                    ["COMMENTS"] = BuildComments(request)
                }
            ));

            return new BitrixLeadCaptureResult(contactId.Value, dealId);
        }
        catch (Exception ex) when (ex is not BitrixLeadCaptureException)
        {
            _logger.LogError(ex, "Failed to capture Bitrix lead for {Email}.", request.Email);
            throw new BitrixLeadCaptureException(contactId.HasValue, ex);
        }
    }

    private static BitrixDealStage? ResolveInitialStage(IReadOnlyCollection<BitrixDealStage> stages)
    {
        return stages.FirstOrDefault(stage =>
                   stage.StageId.EndsWith(":NEW", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(stage.StageId, "NEW", StringComparison.OrdinalIgnoreCase))
               ?? stages.FirstOrDefault(stage => string.Equals(stage.Name, "Nowy", StringComparison.OrdinalIgnoreCase))
               ?? stages.FirstOrDefault(stage => string.Equals(stage.Name, "New", StringComparison.OrdinalIgnoreCase))
               ?? stages.FirstOrDefault(stage => string.Equals(stage.Name, "W toku", StringComparison.OrdinalIgnoreCase))
               ?? stages.FirstOrDefault();
    }

    private static string BuildComments(BitrixLeadCaptureRequest request)
    {
        var lines = new List<string>();

        AppendCommentLine(lines, "Imie", request.FirstName);
        AppendCommentLine(lines, "Nazwisko", request.LastName);
        AppendCommentLine(lines, "Email", request.Email);
        AppendCommentLine(lines, "Telefon", request.Phone);

        if (request.CommentFields is not null)
        {
            foreach (var field in request.CommentFields)
            {
                AppendCommentLine(lines, field.Key, field.Value);
            }
        }

        lines.Add(string.Empty);
        lines.Add("Wiadomosc:");
        lines.Add(request.Message.Trim());

        return string.Join(Environment.NewLine, lines);
    }

    private static void AppendCommentLine(ICollection<string> lines, string label, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            lines.Add($"{label}: {value.Trim()}");
        }
    }
}
