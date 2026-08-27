namespace Lo.Rcc;

public interface IRccCallback
{

    Task OnJobOpenedAsync(string jobId, long placeId, double expirationInSeconds);

    Task OnSoapFaultAsync(string method, string fault, string? detail);
}
