CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261101203610_InitialCreate') THEN
    CREATE TABLE audit_events (
        id uuid NOT NULL,
        event_type character varying(64) NOT NULL,
        succeeded boolean NOT NULL,
        occurred_at timestamp with time zone NOT NULL,
        user_id uuid,
        email character varying(256),
        actor_user_id uuid,
        ip_address character varying(64),
        user_agent character varying(512),
        details character varying(1000),
        CONSTRAINT "PK_audit_events" PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261101203610_InitialCreate') THEN
    CREATE TABLE permissions (
        id uuid NOT NULL,
        code character varying(100) NOT NULL,
        module character varying(50) NOT NULL,
        description character varying(250) NOT NULL,
        CONSTRAINT "PK_permissions" PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261101203610_InitialCreate') THEN
    CREATE TABLE roles (
        id uuid NOT NULL,
        name character varying(50) NOT NULL,
        description character varying(250),
        is_system boolean NOT NULL,
        created_at timestamp with time zone NOT NULL,
        CONSTRAINT "PK_roles" PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261101203610_InitialCreate') THEN
    CREATE TABLE users (
        id uuid NOT NULL,
        email character varying(256) NOT NULL,
        normalized_email character varying(256) NOT NULL,
        full_name character varying(100) NOT NULL,
        password_hash character varying(512) NOT NULL,
        email_confirmed boolean NOT NULL,
        is_active boolean NOT NULL,
        access_failed_count integer NOT NULL,
        lockout_end timestamp with time zone,
        created_at timestamp with time zone NOT NULL,
        last_login_at timestamp with time zone,
        password_changed_at timestamp with time zone,
        CONSTRAINT "PK_users" PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261101203610_InitialCreate') THEN
    CREATE TABLE role_permissions (
        role_id uuid NOT NULL,
        permission_id uuid NOT NULL,
        CONSTRAINT "PK_role_permissions" PRIMARY KEY (role_id, permission_id),
        CONSTRAINT "FK_role_permissions_permissions_permission_id" FOREIGN KEY (permission_id) REFERENCES permissions (id) ON DELETE CASCADE,
        CONSTRAINT "FK_role_permissions_roles_role_id" FOREIGN KEY (role_id) REFERENCES roles (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261101203610_InitialCreate') THEN
    CREATE TABLE refresh_tokens (
        id uuid NOT NULL,
        user_id uuid NOT NULL,
        token_hash character varying(64) NOT NULL,
        family_id uuid NOT NULL,
        created_at timestamp with time zone NOT NULL,
        expires_at timestamp with time zone NOT NULL,
        created_by_ip character varying(64),
        revoked_at timestamp with time zone,
        revoked_reason character varying(64),
        replaced_by_token_id uuid,
        CONSTRAINT "PK_refresh_tokens" PRIMARY KEY (id),
        CONSTRAINT "FK_refresh_tokens_users_user_id" FOREIGN KEY (user_id) REFERENCES users (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261101203610_InitialCreate') THEN
    CREATE TABLE user_roles (
        user_id uuid NOT NULL,
        role_id uuid NOT NULL,
        CONSTRAINT "PK_user_roles" PRIMARY KEY (user_id, role_id),
        CONSTRAINT "FK_user_roles_roles_role_id" FOREIGN KEY (role_id) REFERENCES roles (id) ON DELETE RESTRICT,
        CONSTRAINT "FK_user_roles_users_user_id" FOREIGN KEY (user_id) REFERENCES users (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261101203610_InitialCreate') THEN
    CREATE TABLE user_tokens (
        id uuid NOT NULL,
        user_id uuid NOT NULL,
        purpose character varying(32) NOT NULL,
        token_hash character varying(64) NOT NULL,
        created_at timestamp with time zone NOT NULL,
        expires_at timestamp with time zone NOT NULL,
        consumed_at timestamp with time zone,
        CONSTRAINT "PK_user_tokens" PRIMARY KEY (id),
        CONSTRAINT "FK_user_tokens_users_user_id" FOREIGN KEY (user_id) REFERENCES users (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261101203610_InitialCreate') THEN
    CREATE INDEX "IX_audit_events_event_type_occurred_at" ON audit_events (event_type, occurred_at);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261101203610_InitialCreate') THEN
    CREATE INDEX "IX_audit_events_occurred_at" ON audit_events (occurred_at);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261101203610_InitialCreate') THEN
    CREATE INDEX "IX_audit_events_user_id" ON audit_events (user_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261101203610_InitialCreate') THEN
    CREATE UNIQUE INDEX "IX_permissions_code" ON permissions (code);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261101203610_InitialCreate') THEN
    CREATE INDEX "IX_refresh_tokens_family_id" ON refresh_tokens (family_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261101203610_InitialCreate') THEN
    CREATE UNIQUE INDEX "IX_refresh_tokens_token_hash" ON refresh_tokens (token_hash);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261101203610_InitialCreate') THEN
    CREATE INDEX "IX_refresh_tokens_user_id_revoked_at" ON refresh_tokens (user_id, revoked_at);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261101203610_InitialCreate') THEN
    CREATE INDEX "IX_role_permissions_permission_id" ON role_permissions (permission_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261101203610_InitialCreate') THEN
    CREATE UNIQUE INDEX "IX_roles_name" ON roles (name);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261101203610_InitialCreate') THEN
    CREATE INDEX "IX_user_roles_role_id" ON user_roles (role_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261101203610_InitialCreate') THEN
    CREATE UNIQUE INDEX "IX_user_tokens_token_hash" ON user_tokens (token_hash);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261101203610_InitialCreate') THEN
    CREATE INDEX "IX_user_tokens_user_id_purpose" ON user_tokens (user_id, purpose);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261101203610_InitialCreate') THEN
    CREATE INDEX "IX_users_created_at" ON users (created_at);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261101203610_InitialCreate') THEN
    CREATE UNIQUE INDEX "IX_users_normalized_email" ON users (normalized_email);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261101203610_InitialCreate') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261101203610_InitialCreate', '10.0.14');
    END IF;
END $EF$;
COMMIT;

