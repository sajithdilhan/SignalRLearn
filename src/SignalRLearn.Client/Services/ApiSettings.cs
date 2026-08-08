namespace SignalRLearn.Client.Services;

public sealed record ApiSettings(Uri BaseAddress)
{
    public Uri ScalarUrl => new(BaseAddress, "scalar");
}
