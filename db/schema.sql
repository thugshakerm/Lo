CREATE TABLE IF NOT EXISTS users (
    id                  BIGSERIAL PRIMARY KEY,
    name                VARCHAR(255) NOT NULL UNIQUE,
    email               VARCHAR(255) UNIQUE,
    password            VARCHAR(255),
    avatar              JSONB,
    banned_until        TIMESTAMP,
    banned_reason       VARCHAR(255),
    email_verified_at   TIMESTAMP,
    remember_token      VARCHAR(100),
    created_at          TIMESTAMP,
    updated_at          TIMESTAMP
);

CREATE TABLE IF NOT EXISTS assets (
    id              BIGSERIAL PRIMARY KEY,
    name            VARCHAR(255) NOT NULL,
    description     TEXT,
    asset_type      SMALLINT NOT NULL,
    creator_id      BIGINT,
    price           INTEGER NOT NULL DEFAULT 0,
    is_for_sale     BOOLEAN NOT NULL DEFAULT FALSE,
    is_approved     BOOLEAN NOT NULL DEFAULT FALSE,
    visibility      CHAR(1) NOT NULL DEFAULT 'n',
    thumb_hash      VARCHAR(255),
    storage_path    VARCHAR(1024),
    mime_type       VARCHAR(255),
    created_at      TIMESTAMP,
    updated_at      TIMESTAMP,

    CONSTRAINT assets_creator_fk FOREIGN KEY (creator_id)
        REFERENCES users(id) ON DELETE SET NULL
);
CREATE INDEX IF NOT EXISTS assets_browse_idx
    ON assets (asset_type, visibility, is_approved);

CREATE TABLE IF NOT EXISTS asset_ownership (
    id          BIGSERIAL PRIMARY KEY,
    asset_id    BIGINT NOT NULL,
    user_id     BIGINT NOT NULL,
    created_at  TIMESTAMP,
    updated_at  TIMESTAMP,

    CONSTRAINT asset_ownership_asset_fk FOREIGN KEY (asset_id)
        REFERENCES assets(id) ON DELETE CASCADE,
    CONSTRAINT asset_ownership_user_fk FOREIGN KEY (user_id)
        REFERENCES users(id) ON DELETE CASCADE,
    CONSTRAINT asset_ownership_unique UNIQUE (asset_id, user_id)
);
CREATE INDEX IF NOT EXISTS asset_ownership_user_idx ON asset_ownership (user_id);
CREATE INDEX IF NOT EXISTS asset_ownership_asset_idx ON asset_ownership (asset_id);

CREATE TABLE IF NOT EXISTS places (
    id              BIGSERIAL PRIMARY KEY,
    universe_id     BIGINT NOT NULL,
    creator_id      BIGINT,
    name            VARCHAR(255) NOT NULL,
    max_players     INTEGER NOT NULL DEFAULT 20,
    r15_morphing    BOOLEAN NOT NULL DEFAULT FALSE,
    storage_path    VARCHAR(1024),
    created_at      TIMESTAMP,
    updated_at      TIMESTAMP,

    CONSTRAINT places_creator_fk FOREIGN KEY (creator_id)
        REFERENCES users(id) ON DELETE SET NULL
);
CREATE INDEX IF NOT EXISTS places_universe_idx ON places (universe_id);

CREATE TABLE IF NOT EXISTS game_servers (
    id                  BIGSERIAL PRIMARY KEY,
    job_id              VARCHAR(255) NOT NULL UNIQUE,
    place_id            BIGINT NOT NULL DEFAULT 0,
    port                INTEGER NOT NULL DEFAULT 0,
    max_players         INTEGER NOT NULL DEFAULT 0,
    private_server      BOOLEAN NOT NULL DEFAULT FALSE,
    status              VARCHAR(32) NOT NULL DEFAULT 'starting',
    lease_expires_at    TIMESTAMP NOT NULL,
    last_ping_at        TIMESTAMP NOT NULL,
    created_at          TIMESTAMP,
    updated_at          TIMESTAMP
);
CREATE INDEX IF NOT EXISTS game_servers_status_idx
    ON game_servers (status, lease_expires_at);

CREATE TABLE IF NOT EXISTS game_passes (
    id              BIGSERIAL PRIMARY KEY,
    asset_id        BIGINT NOT NULL UNIQUE,
    creator_id      BIGINT,
    name            VARCHAR(255) NOT NULL,
    price           INTEGER NOT NULL DEFAULT 0,
    created_at      TIMESTAMP,
    updated_at      TIMESTAMP,

    CONSTRAINT game_passes_creator_fk FOREIGN KEY (creator_id)
        REFERENCES users(id) ON DELETE SET NULL
);

CREATE TABLE IF NOT EXISTS audit_log (
    id          BIGSERIAL PRIMARY KEY,
    event       VARCHAR(64) NOT NULL,
    user_id     BIGINT,
    ip          INET,
    detail      TEXT,
    occurred_at TIMESTAMP NOT NULL
);
CREATE INDEX IF NOT EXISTS audit_log_event_idx ON audit_log (event, occurred_at);
CREATE INDEX IF NOT EXISTS audit_log_user_idx  ON audit_log (user_id);

CREATE TABLE IF NOT EXISTS rcc_soap_faults (
    id          BIGSERIAL PRIMARY KEY,
    method      VARCHAR(64) NOT NULL,
    fault       VARCHAR(255) NOT NULL,
    detail      TEXT,
    occurred_at TIMESTAMP NOT NULL
);
CREATE INDEX IF NOT EXISTS rcc_soap_faults_method_idx
    ON rcc_soap_faults (method, occurred_at);

CREATE TABLE IF NOT EXISTS user_badges (
    id          BIGSERIAL PRIMARY KEY,
    user_id     BIGINT NOT NULL,
    badge_id    BIGINT NOT NULL,
    awarded_at  TIMESTAMP NOT NULL,

    CONSTRAINT user_badges_user_fk FOREIGN KEY (user_id)
        REFERENCES users(id) ON DELETE CASCADE,
    CONSTRAINT user_badges_unique UNIQUE (user_id, badge_id)
);
CREATE INDEX IF NOT EXISTS user_badges_badge_idx ON user_badges (badge_id);
