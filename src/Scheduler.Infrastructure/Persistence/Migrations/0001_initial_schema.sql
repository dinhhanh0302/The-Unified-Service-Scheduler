-- 0001: initial schema (SPEC §5, §10). Forward-only; applied once by DatabaseMigrator.

CREATE EXTENSION IF NOT EXISTS btree_gist;

CREATE TABLE dealerships (
    id            uuid         PRIMARY KEY,
    name          varchar(200) NOT NULL,
    time_zone_id  varchar(64)  NOT NULL,
    opening_time  time         NOT NULL,
    closing_time  time         NOT NULL,
    open_days     integer[]    NOT NULL,
    CONSTRAINT ck_dealerships_hours CHECK (opening_time < closing_time)
);

CREATE TABLE service_bays (
    id             uuid        PRIMARY KEY,
    dealership_id  uuid        NOT NULL REFERENCES dealerships (id),
    name           varchar(50) NOT NULL,
    bay_type       integer     NOT NULL,
    is_active      boolean     NOT NULL DEFAULT true,
    CONSTRAINT ux_service_bays_dealership_name UNIQUE (dealership_id, name)
);

CREATE TABLE technicians (
    id              uuid         PRIMARY KEY,
    dealership_id   uuid         NOT NULL REFERENCES dealerships (id),
    full_name       varchar(200) NOT NULL,
    qualifications  integer[]    NOT NULL,
    is_active       boolean      NOT NULL DEFAULT true
);

CREATE INDEX ix_technicians_dealership ON technicians (dealership_id);

CREATE TABLE service_types (
    id                      uuid         PRIMARY KEY,
    code                    varchar(50)  NOT NULL,
    name                    varchar(200) NOT NULL,
    duration_minutes        integer      NOT NULL,
    required_qualification  integer      NOT NULL,
    required_bay_type       integer      NOT NULL,
    CONSTRAINT ux_service_types_code UNIQUE (code),
    CONSTRAINT ck_service_types_duration
        CHECK (duration_minutes BETWEEN 15 AND 480 AND duration_minutes % 15 = 0)
);

CREATE TABLE customers (
    id         uuid         PRIMARY KEY,
    full_name  varchar(200) NOT NULL,
    email      varchar(320) NOT NULL,
    phone      varchar(50)  NOT NULL DEFAULT '',
    CONSTRAINT ux_customers_email UNIQUE (email)
);

CREATE TABLE vehicles (
    id           uuid         PRIMARY KEY,
    vin          varchar(17)  NOT NULL,
    make         varchar(100) NOT NULL,
    model        varchar(100) NOT NULL,
    year         integer      NOT NULL,
    customer_id  uuid         NOT NULL REFERENCES customers (id),
    CONSTRAINT ux_vehicles_vin UNIQUE (vin),
    CONSTRAINT ck_vehicles_vin_length CHECK (char_length(vin) = 17)
);

CREATE INDEX ix_vehicles_customer ON vehicles (customer_id);

CREATE TABLE appointments (
    id                uuid         PRIMARY KEY,
    customer_id       uuid         NOT NULL REFERENCES customers (id),
    vehicle_id        uuid         NOT NULL REFERENCES vehicles (id),
    dealership_id     uuid         NOT NULL REFERENCES dealerships (id),
    service_type_id   uuid         NOT NULL REFERENCES service_types (id),
    technician_id     uuid         NOT NULL REFERENCES technicians (id),
    service_bay_id    uuid         NOT NULL REFERENCES service_bays (id),
    start_utc         timestamptz  NOT NULL,
    end_utc           timestamptz  NOT NULL,
    status            varchar(20)  NOT NULL,
    notes             varchar(500),
    created_at_utc    timestamptz  NOT NULL,
    cancelled_at_utc  timestamptz,
    idempotency_key   varchar(100),
    request_hash      varchar(64),
    CONSTRAINT ck_appointments_window CHECK (start_utc < end_utc),
    CONSTRAINT ck_appointments_status CHECK (status IN ('Confirmed', 'Cancelled')),

    -- BR-11: no two Confirmed appointments may overlap on the same bay, technician or vehicle.
    -- '[)' = half-open windows (BR-01); Cancelled rows are excluded (BR-12).
    CONSTRAINT ex_appointments_bay_no_overlap EXCLUDE USING gist (
        service_bay_id WITH =,
        tstzrange(start_utc, end_utc, '[)') WITH &&
    ) WHERE (status = 'Confirmed'),

    CONSTRAINT ex_appointments_technician_no_overlap EXCLUDE USING gist (
        technician_id WITH =,
        tstzrange(start_utc, end_utc, '[)') WITH &&
    ) WHERE (status = 'Confirmed'),

    CONSTRAINT ex_appointments_vehicle_no_overlap EXCLUDE USING gist (
        vehicle_id WITH =,
        tstzrange(start_utc, end_utc, '[)') WITH &&
    ) WHERE (status = 'Confirmed')
);

-- FR-08: one appointment per idempotency key.
CREATE UNIQUE INDEX ux_appointments_idempotency_key
    ON appointments (idempotency_key)
    WHERE idempotency_key IS NOT NULL;

-- Day snapshots per dealership (SPEC §10, performance).
CREATE INDEX ix_appointments_dealership_start ON appointments (dealership_id, start_utc);
