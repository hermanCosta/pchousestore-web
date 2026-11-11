-- DATA-05 draft: authentication persistence only.
-- Requires existing `company`, `shop`, and `employee` tables with signed BIGINT IDs,
-- plus candidate keys (shop.id, shop.company_id) and (employee.id, employee.company_id).
-- Do not run against a live database. Convert to reviewed EF Core migrations later.

CREATE TABLE shop_access_credential (
    shop_id BIGINT NOT NULL,
    company_id BIGINT NOT NULL,
    password_hash VARCHAR(512) NOT NULL,
    credential_version BIGINT NOT NULL DEFAULT 1,
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    created_at DATETIME(6) NOT NULL,
    rotated_at DATETIME(6) NULL,
    updated_at DATETIME(6) NOT NULL,
    updated_by_employee_id BIGINT NULL,
    PRIMARY KEY (shop_id),
    CONSTRAINT fk_shop_access_credential_shop
        FOREIGN KEY (shop_id, company_id) REFERENCES shop (id, company_id)
        ON DELETE RESTRICT,
    CONSTRAINT fk_shop_access_credential_updated_by
        FOREIGN KEY (updated_by_employee_id, company_id) REFERENCES employee (id, company_id)
        ON DELETE RESTRICT,
    CONSTRAINT ck_shop_access_credential_version CHECK (credential_version > 0)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

CREATE TABLE employee_role_assignment (
    id BIGINT NOT NULL AUTO_INCREMENT,
    company_id BIGINT NOT NULL,
    employee_id BIGINT NOT NULL,
    role_code VARCHAR(20) NOT NULL,
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    assigned_at DATETIME(6) NOT NULL,
    assigned_by_employee_id BIGINT NULL,
    PRIMARY KEY (id),
    UNIQUE KEY uq_employee_role (employee_id, role_code),
    UNIQUE KEY uq_employee_role_id_company (id, company_id),
    CONSTRAINT fk_employee_role_employee_company
        FOREIGN KEY (employee_id, company_id) REFERENCES employee (id, company_id)
        ON DELETE RESTRICT,
    CONSTRAINT fk_employee_role_assigned_by_company
        FOREIGN KEY (assigned_by_employee_id, company_id) REFERENCES employee (id, company_id)
        ON DELETE RESTRICT,
    CONSTRAINT ck_employee_role_code CHECK (role_code IN ('MANAGER', 'TECHNICIAN'))
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

CREATE TABLE shop_access_credential_event (
    id BIGINT NOT NULL AUTO_INCREMENT,
    shop_id BIGINT NOT NULL,
    company_id BIGINT NOT NULL,
    event_type VARCHAR(24) NOT NULL,
    credential_version BIGINT NOT NULL,
    occurred_at DATETIME(6) NOT NULL,
    actor_type VARCHAR(24) NOT NULL,
    actor_employee_id BIGINT NULL,
    reason VARCHAR(500) NOT NULL,
    PRIMARY KEY (id),
    KEY ix_shop_credential_event_shop_time (shop_id, occurred_at),
    CONSTRAINT fk_shop_credential_event_shop
        FOREIGN KEY (shop_id, company_id) REFERENCES shop (id, company_id)
        ON DELETE RESTRICT,
    CONSTRAINT fk_shop_credential_event_actor_company
        FOREIGN KEY (actor_employee_id, company_id) REFERENCES employee (id, company_id)
        ON DELETE RESTRICT,
    CONSTRAINT ck_shop_credential_event_type CHECK (event_type IN ('PROVISIONED', 'ROTATED', 'DISABLED', 'ENABLED', 'RESET')),
    CONSTRAINT ck_shop_credential_event_actor CHECK (actor_type IN ('EMPLOYEE', 'DEPLOYMENT_OPERATOR')),
    CONSTRAINT ck_shop_credential_event_version CHECK (credential_version > 0)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

CREATE TABLE shop_session (
    id BIGINT NOT NULL AUTO_INCREMENT,
    company_id BIGINT NOT NULL,
    shop_id BIGINT NOT NULL,
    oidc_authorization_id VARCHAR(100) NULL,
    issued_at DATETIME(6) NOT NULL,
    last_seen_at DATETIME(6) NULL,
    idle_expires_at DATETIME(6) NULL,
    absolute_expires_at DATETIME(6) NULL,
    revoked_at DATETIME(6) NULL,
    revocation_reason VARCHAR(80) NULL,
    row_version BIGINT NOT NULL DEFAULT 1,
    PRIMARY KEY (id),
    UNIQUE KEY uq_shop_session_id_shop (id, shop_id),
    UNIQUE KEY uq_shop_session_oidc_authorization (oidc_authorization_id),
    KEY ix_shop_session_active_shop (shop_id, revoked_at, absolute_expires_at),
    CONSTRAINT fk_shop_session_shop
        FOREIGN KEY (shop_id, company_id) REFERENCES shop (id, company_id)
        ON DELETE RESTRICT,
    CONSTRAINT ck_shop_session_expiry_order CHECK (
        (idle_expires_at IS NULL OR idle_expires_at > issued_at)
        AND (absolute_expires_at IS NULL OR absolute_expires_at > issued_at)
    ),
    CONSTRAINT ck_shop_session_row_version CHECK (row_version > 0)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

CREATE TABLE employee_action_authorization (
    id BIGINT NOT NULL AUTO_INCREMENT,
    authorization_token_hash BINARY(32) NOT NULL,
    shop_session_id BIGINT NOT NULL,
    company_id BIGINT NOT NULL,
    shop_id BIGINT NOT NULL,
    target_shop_id BIGINT NULL,
    employee_id BIGINT NOT NULL,
    action_code VARCHAR(100) NOT NULL,
    resource_type VARCHAR(80) NULL,
    resource_id VARCHAR(100) NULL,
    operation_context_hash BINARY(32) NULL,
    authenticated_at DATETIME(6) NOT NULL,
    expires_at DATETIME(6) NOT NULL,
    consumed_at DATETIME(6) NULL,
    row_version BIGINT NOT NULL DEFAULT 1,
    PRIMARY KEY (id),
    UNIQUE KEY uq_employee_action_token_hash (authorization_token_hash),
    KEY ix_employee_action_session_state (shop_session_id, shop_id, consumed_at, expires_at),
    KEY ix_employee_action_employee_time (employee_id, authenticated_at),
    KEY ix_employee_action_target_shop (target_shop_id, company_id),
    CONSTRAINT fk_employee_action_session_shop
        FOREIGN KEY (shop_session_id, shop_id) REFERENCES shop_session (id, shop_id)
        ON DELETE RESTRICT,
    CONSTRAINT fk_employee_action_shop_company
        FOREIGN KEY (shop_id, company_id) REFERENCES shop (id, company_id)
        ON DELETE RESTRICT,
    CONSTRAINT fk_employee_action_target_shop_company
        FOREIGN KEY (target_shop_id, company_id) REFERENCES shop (id, company_id)
        ON DELETE RESTRICT,
    CONSTRAINT fk_employee_action_employee_company
        FOREIGN KEY (employee_id, company_id) REFERENCES employee (id, company_id)
        ON DELETE RESTRICT,
    CONSTRAINT ck_employee_action_expiry CHECK (expires_at > authenticated_at),
    CONSTRAINT ck_employee_action_row_version CHECK (row_version > 0)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

CREATE TABLE system_services (
    id BIGINT NOT NULL AUTO_INCREMENT,
    service_name VARCHAR(80) NOT NULL,
    key_id VARCHAR(100) NOT NULL,
    key_purpose VARCHAR(24) NOT NULL,
    algorithm VARCHAR(24) NOT NULL,
    public_certificate_der MEDIUMBLOB NOT NULL,
    private_key_ciphertext MEDIUMBLOB NULL,
    cipher_nonce BINARY(12) NULL,
    cipher_tag BINARY(16) NULL,
    kek_reference VARCHAR(255) NULL,
    status VARCHAR(20) NOT NULL,
    active_signing_slot VARCHAR(80)
        GENERATED ALWAYS AS (CASE WHEN key_purpose = 'SIGNING' AND status = 'ACTIVE' THEN service_name ELSE NULL END) STORED,
    not_before DATETIME(6) NOT NULL,
    not_after DATETIME(6) NOT NULL,
    created_at DATETIME(6) NOT NULL,
    activated_at DATETIME(6) NULL,
    retired_at DATETIME(6) NULL,
    PRIMARY KEY (id),
    UNIQUE KEY uq_system_services_name_key (service_name, key_id),
    UNIQUE KEY uq_system_services_active_signing_slot (active_signing_slot),
    KEY ix_system_services_use (service_name, key_purpose, status, not_before, not_after),
    CONSTRAINT ck_system_services_purpose CHECK (key_purpose = 'SIGNING'),
    CONSTRAINT ck_system_services_status CHECK (status IN ('STAGED', 'ACTIVE', 'VERIFY_ONLY', 'REVOKED')),
    CONSTRAINT ck_system_services_algorithm CHECK (algorithm IN ('RS256')),
    CONSTRAINT ck_system_services_dates CHECK (not_after > not_before),
    CONSTRAINT ck_system_services_private_material CHECK (
        (private_key_ciphertext IS NULL AND cipher_nonce IS NULL AND cipher_tag IS NULL AND kek_reference IS NULL)
        OR
        (private_key_ciphertext IS NOT NULL AND cipher_nonce IS NOT NULL AND cipher_tag IS NOT NULL AND kek_reference IS NOT NULL)
    )
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

CREATE TABLE system_service_key_event (
    id BIGINT NOT NULL AUTO_INCREMENT,
    system_service_id BIGINT NOT NULL,
    event_type VARCHAR(32) NOT NULL,
    occurred_at DATETIME(6) NOT NULL,
    actor_type VARCHAR(24) NOT NULL,
    actor_employee_id BIGINT NULL,
    reason VARCHAR(500) NOT NULL,
    PRIMARY KEY (id),
    KEY ix_system_service_key_event_service_time (system_service_id, occurred_at),
    CONSTRAINT fk_system_service_key_event_service
        FOREIGN KEY (system_service_id) REFERENCES system_services (id)
        ON DELETE RESTRICT,
    CONSTRAINT fk_system_service_key_event_employee
        FOREIGN KEY (actor_employee_id) REFERENCES employee (id)
        ON DELETE RESTRICT,
    CONSTRAINT ck_system_service_key_event_actor CHECK (actor_type IN ('EMPLOYEE', 'DEPLOYMENT_OPERATOR'))
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
