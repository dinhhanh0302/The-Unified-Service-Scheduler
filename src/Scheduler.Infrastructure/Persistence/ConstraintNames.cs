namespace Scheduler.Infrastructure.Persistence;

/// <summary>Names of the constraints in 0001_initial_schema.sql that the application reacts to (SPEC §10).</summary>
public static class ConstraintNames
{
    public const string BayNoOverlap = "ex_appointments_bay_no_overlap";
    public const string TechnicianNoOverlap = "ex_appointments_technician_no_overlap";
    public const string VehicleNoOverlap = "ex_appointments_vehicle_no_overlap";
    public const string IdempotencyKeyUnique = "ux_appointments_idempotency_key";
}
