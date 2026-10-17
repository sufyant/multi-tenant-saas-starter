namespace SharedKernel;

/// <summary>
/// Endpoint metadata: the endpoint whose GET reads the resource this endpoint creates, and the route values of a created resource. The
/// host builds the address from them and sends it in the Location header of the 201 (RFC 9110, section 10.2.2). An endpoint whose
/// resource has no GET declares none.
/// </summary>
public sealed class CreatedLocation
{
    private readonly Func<object, object> _routeValues;

    private CreatedLocation(string endpointName, Func<object, object> routeValues)
    {
        EndpointName = endpointName;
        _routeValues = routeValues;
    }

    /// <summary>The name of the endpoint that reads a created resource.</summary>
    public string EndpointName { get; }

    /// <summary>Each created <typeparamref name="T"/> is read by the endpoint of this name, with these route values.</summary>
    public static CreatedLocation Of<T>(string endpointName, Func<T, object> routeValues) =>
        new(endpointName, created => routeValues((T)created));

    public object RouteValuesFor(object created) => _routeValues(created);
}
