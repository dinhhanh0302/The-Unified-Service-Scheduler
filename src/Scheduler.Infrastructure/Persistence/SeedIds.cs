namespace Scheduler.Infrastructure.Persistence;

/// <summary>Fixed ids of the seed data in <c>Migrations/0002_seed_data.sql</c>. Used by tests and http/scheduler.http.</summary>
public static class SeedIds
{
    public static class Dealerships
    {
        /// <summary>D1 in SPEC §7: Mon–Sat 08:00–17:00, Asia/Ho_Chi_Minh.</summary>
        public static readonly Guid D1 = Guid.Parse("d1000000-0000-0000-0000-000000000001");

        /// <summary>Second dealership, used for cross-dealership vehicle overlap (AC-01.18).</summary>
        public static readonly Guid D2 = Guid.Parse("d1000000-0000-0000-0000-000000000002");
    }

    public static class Bays
    {
        public static readonly Guid D1Bay1Lift = Guid.Parse("ba000000-0000-0000-0000-000000000101");
        public static readonly Guid D1Bay2Lift = Guid.Parse("ba000000-0000-0000-0000-000000000102");
        public static readonly Guid D1Bay3General = Guid.Parse("ba000000-0000-0000-0000-000000000103");
        public static readonly Guid D1Bay4Diagnostic = Guid.Parse("ba000000-0000-0000-0000-000000000104");
        public static readonly Guid D2Bay1Lift = Guid.Parse("ba000000-0000-0000-0000-000000000201");
        public static readonly Guid D2Bay2Diagnostic = Guid.Parse("ba000000-0000-0000-0000-000000000202");
    }

    public static class Technicians
    {
        public static readonly Guid D1An = Guid.Parse("7e000000-0000-0000-0000-000000000101");
        public static readonly Guid D1Binh = Guid.Parse("7e000000-0000-0000-0000-000000000102");
        public static readonly Guid D1Cuong = Guid.Parse("7e000000-0000-0000-0000-000000000103");
        public static readonly Guid D1Dung = Guid.Parse("7e000000-0000-0000-0000-000000000104");
        public static readonly Guid D2Em = Guid.Parse("7e000000-0000-0000-0000-000000000201");
    }

    public static class ServiceTypes
    {
        public static readonly Guid OilChange = Guid.Parse("5e000000-0000-0000-0000-000000000001");
        public static readonly Guid BrakeService = Guid.Parse("5e000000-0000-0000-0000-000000000002");
        public static readonly Guid Diagnostics = Guid.Parse("5e000000-0000-0000-0000-000000000003");
        public static readonly Guid EvBatteryCheck = Guid.Parse("5e000000-0000-0000-0000-000000000004");
    }

    public static class Customers
    {
        public static readonly Guid An = Guid.Parse("c0000000-0000-0000-0000-000000000001");
        public static readonly Guid Binh = Guid.Parse("c0000000-0000-0000-0000-000000000002");
        public static readonly Guid Chi = Guid.Parse("c0000000-0000-0000-0000-000000000003");
    }

    public static class Vehicles
    {
        public static readonly Guid AnAccord = Guid.Parse("ae000000-0000-0000-0000-000000000001");
        public static readonly Guid AnCivic = Guid.Parse("ae000000-0000-0000-0000-000000000002");
        public static readonly Guid BinhModel3 = Guid.Parse("ae000000-0000-0000-0000-000000000003");
        public static readonly Guid ChiGolf = Guid.Parse("ae000000-0000-0000-0000-000000000004");
    }
}
