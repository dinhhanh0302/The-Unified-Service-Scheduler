-- 0002: demo / test seed data (SPEC §7, OQ-11). Ids must match Persistence/SeedIds.cs.
-- Enum values: DayOfWeek Sunday=0..Saturday=6; BayType General=0, Lift=1, Diagnostic=2;
-- Qualification General=0, Brakes=1, Diagnostics=2, HighVoltage=3.

INSERT INTO dealerships (id, name, time_zone_id, opening_time, closing_time, open_days) VALUES
    ('d1000000-0000-0000-0000-000000000001', 'Saigon Central Service',  'Asia/Ho_Chi_Minh', '08:00', '17:00', '{1,2,3,4,5,6}'),
    ('d1000000-0000-0000-0000-000000000002', 'Thu Duc Express Service', 'Asia/Ho_Chi_Minh', '08:00', '17:00', '{1,2,3,4,5}');

INSERT INTO service_bays (id, dealership_id, name, bay_type, is_active) VALUES
    ('ba000000-0000-0000-0000-000000000101', 'd1000000-0000-0000-0000-000000000001', 'Bay 1', 1, true),
    ('ba000000-0000-0000-0000-000000000102', 'd1000000-0000-0000-0000-000000000001', 'Bay 2', 1, true),
    ('ba000000-0000-0000-0000-000000000103', 'd1000000-0000-0000-0000-000000000001', 'Bay 3', 0, true),
    ('ba000000-0000-0000-0000-000000000104', 'd1000000-0000-0000-0000-000000000001', 'Bay 4', 2, true),
    ('ba000000-0000-0000-0000-000000000201', 'd1000000-0000-0000-0000-000000000002', 'Bay 1', 1, true),
    ('ba000000-0000-0000-0000-000000000202', 'd1000000-0000-0000-0000-000000000002', 'Bay 2', 2, true);

INSERT INTO technicians (id, dealership_id, full_name, qualifications, is_active) VALUES
    ('7e000000-0000-0000-0000-000000000101', 'd1000000-0000-0000-0000-000000000001', 'Nguyen Van An',  '{0,1}',   true),
    ('7e000000-0000-0000-0000-000000000102', 'd1000000-0000-0000-0000-000000000001', 'Tran Thi Binh',  '{0,2}',   true),
    ('7e000000-0000-0000-0000-000000000103', 'd1000000-0000-0000-0000-000000000001', 'Le Van Cuong',   '{0,2,3}', true),
    ('7e000000-0000-0000-0000-000000000104', 'd1000000-0000-0000-0000-000000000001', 'Pham Thi Dung',  '{0}',     true),
    ('7e000000-0000-0000-0000-000000000201', 'd1000000-0000-0000-0000-000000000002', 'Hoang Van Em',   '{0,1}',   true);

INSERT INTO service_types (id, code, name, duration_minutes, required_qualification, required_bay_type) VALUES
    ('5e000000-0000-0000-0000-000000000001', 'OIL_CHANGE',       'Oil change',          60, 0, 1),
    ('5e000000-0000-0000-0000-000000000002', 'BRAKE_SERVICE',    'Brake service',       90, 1, 1),
    ('5e000000-0000-0000-0000-000000000003', 'DIAGNOSTICS',      'Diagnostics check',   45, 2, 2),
    ('5e000000-0000-0000-0000-000000000004', 'EV_BATTERY_CHECK', 'EV battery check',   120, 3, 2);

INSERT INTO customers (id, full_name, email, phone) VALUES
    ('c0000000-0000-0000-0000-000000000001', 'An Nguyen', 'an.nguyen@example.com', '+84 900 000 001'),
    ('c0000000-0000-0000-0000-000000000002', 'Binh Tran', 'binh.tran@example.com', '+84 900 000 002'),
    ('c0000000-0000-0000-0000-000000000003', 'Chi Le',    'chi.le@example.com',    '+84 900 000 003');

INSERT INTO vehicles (id, vin, make, model, year, customer_id) VALUES
    ('ae000000-0000-0000-0000-000000000001', '1HGCM82633A004352', 'Honda',      'Accord',  2003, 'c0000000-0000-0000-0000-000000000001'),
    ('ae000000-0000-0000-0000-000000000002', 'JHMCM56557C404453', 'Honda',      'Civic',   2007, 'c0000000-0000-0000-0000-000000000001'),
    ('ae000000-0000-0000-0000-000000000003', '5YJ3E1EA7KF317000', 'Tesla',      'Model 3', 2019, 'c0000000-0000-0000-0000-000000000002'),
    ('ae000000-0000-0000-0000-000000000004', 'WVWZZZ1JZXW000001', 'Volkswagen', 'Golf',    1999, 'c0000000-0000-0000-0000-000000000003');
