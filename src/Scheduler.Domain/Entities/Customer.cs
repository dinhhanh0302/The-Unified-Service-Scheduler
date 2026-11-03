namespace Scheduler.Domain.Entities;

public sealed class Customer
{
    public Customer(Guid id, string fullName, string email, string phone)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fullName);
        ArgumentException.ThrowIfNullOrWhiteSpace(email);

        Id = id;
        FullName = fullName;
        Email = email;
        Phone = phone ?? string.Empty;
    }

    public Guid Id { get; }

    public string FullName { get; }

    public string Email { get; }

    public string Phone { get; }
}
