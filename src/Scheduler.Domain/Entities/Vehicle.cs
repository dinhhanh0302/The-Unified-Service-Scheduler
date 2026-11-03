namespace Scheduler.Domain.Entities;

public sealed class Vehicle
{
    public const int VinLength = 17;

    public Vehicle(Guid id, string vin, string make, string model, int year, Guid customerId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(vin);
        if (vin.Length != VinLength)
        {
            throw new ArgumentException($"VIN must be {VinLength} characters.", nameof(vin));
        }

        Id = id;
        Vin = vin;
        Make = make ?? string.Empty;
        Model = model ?? string.Empty;
        Year = year;
        CustomerId = customerId;
    }

    public Guid Id { get; }

    public string Vin { get; }

    public string Make { get; }

    public string Model { get; }

    public int Year { get; }

    public Guid CustomerId { get; }

    /// <summary>BR-08: the vehicle must belong to the requesting customer.</summary>
    public bool BelongsTo(Guid customerId) => CustomerId == customerId;
}
