namespace SharedKernel;

/// <summary>
/// Endpoint metadata: the address of the resource an endpoint creates, where a GET reads it. The host sends it in the Location header
/// of the 201 (RFC 9110, section 10.2.2). An endpoint whose resource has no GET declares none.
/// </summary>
public sealed class CreatedLocation
{
    private readonly Func<object, string> _of;

    private CreatedLocation(Func<object, string> of) => _of = of;

    /// <summary>The address of each created <typeparamref name="T"/>.</summary>
    public static CreatedLocation Of<T>(Func<T, string> location) => new(created => location((T)created));

    public string For(object created) => _of(created);
}
