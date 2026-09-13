namespace Kapea.Client.Components;

/// <summary>Un tramo del reparto: nombre, importe y la serie que le da color.</summary>
public sealed record AllocationSlice(string Name, decimal Value, string Series);
