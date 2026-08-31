using HealthGoalsTracker.Functions.Contracts;

namespace HealthGoalsTracker.Functions.Services;

public class CloudApiService
{
    public ICloudRepository Repository { get; }
    public ContractValidator Validator { get; }

    public CloudApiService(ICloudRepository repository, CursorCodec cursorCodec)
    {
        Repository = repository;
        Validator = new ContractValidator(cursorCodec);
    }

    public async Task<ApiOperationResult<SyncResponse>> SyncAsync(
        string subject,
        SyncRequest request,
        CancellationToken cancellationToken)
    {
        var errors = Validator.ValidateSync(subject, request);
        if (errors.Count > 0)
            return ApiOperationResult<SyncResponse>.Invalid(errors);

        try
        {
            return ApiOperationResult<SyncResponse>.Success(
                await Repository.SyncAsync(subject, request, cancellationToken));
        }
        catch (InvalidCursorException)
        {
            return ApiOperationResult<SyncResponse>.Invalid(
                new Dictionary<string, string[]>
                {
                    ["cursor"] = ["Cursor is ahead of the current server state."]
                });
        }
    }

    public async Task<ApiOperationResult<List<GoalContract>>> GetGoalsAsync(
        string subject,
        CancellationToken cancellationToken) =>
        ApiOperationResult<List<GoalContract>>.Success(
            await Repository.GetGoalsAsync(subject, cancellationToken));

    public async Task<ApiOperationResult<List<DailyRecordContract>>> GetRecordsAsync(
        string subject,
        string? from,
        string? to,
        CancellationToken cancellationToken)
    {
        var errors = Validator.ValidateDateRange(from, to);
        if (errors.Count > 0)
            return ApiOperationResult<List<DailyRecordContract>>.Invalid(errors);

        ContractValidator.TryParseDate(from, out var fromDate);
        ContractValidator.TryParseDate(to, out var toDate);
        return ApiOperationResult<List<DailyRecordContract>>.Success(
            await Repository.GetRecordsAsync(subject, fromDate, toDate, cancellationToken));
    }

    public async Task<ApiOperationResult<List<MeasurementContract>>> GetMeasurementsAsync(
        string subject,
        string? from,
        string? to,
        CancellationToken cancellationToken)
    {
        var errors = Validator.ValidateDateRange(from, to);
        if (errors.Count > 0)
            return ApiOperationResult<List<MeasurementContract>>.Invalid(errors);

        ContractValidator.TryParseDate(from, out var fromDate);
        ContractValidator.TryParseDate(to, out var toDate);
        return ApiOperationResult<List<MeasurementContract>>.Success(
            await Repository.GetMeasurementsAsync(subject, fromDate, toDate, cancellationToken));
    }
}

public class ApiOperationResult<T>
{
    public T? Value { get; set; }
    public Dictionary<string, string[]>? ValidationErrors { get; set; }
    public bool IsValid => ValidationErrors == null;

    public static ApiOperationResult<T> Success(T value) => new() { Value = value };

    public static ApiOperationResult<T> Invalid(Dictionary<string, string[]> errors) =>
        new() { ValidationErrors = errors };
}
