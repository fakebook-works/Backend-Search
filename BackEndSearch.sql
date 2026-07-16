CREATE SCHEMA IF NOT EXISTS search;
SET search_path TO search;

CREATE TABLE IF NOT EXISTS objects (
    id       BIGINT PRIMARY KEY,
    type     SMALLINT NOT NULL,
    sort_key INTEGER NOT NULL DEFAULT 0,
    CONSTRAINT ck_objects_id_positive CHECK (id > 0),
    CONSTRAINT ck_objects_type CHECK (type BETWEEN 0 AND 4),
    CONSTRAINT ck_objects_sort_key_non_negative CHECK (sort_key >= 0)
);

-- Canonical types: 0 user, 1 group, 2 feed post, 3 group post, 4 reel.

CREATE TABLE IF NOT EXISTS tokens (
    id              BIGINT PRIMARY KEY,
    token_text      VARCHAR(255) UNIQUE NOT NULL,
    CONSTRAINT ck_tokens_id_positive CHECK (id > 0)
);

-- Supports prefix matching generated from string.StartsWith under non-C collations.
CREATE INDEX IF NOT EXISTS idx_tokens_token_text_prefix
    ON tokens (token_text varchar_pattern_ops);

CREATE TABLE IF NOT EXISTS token_object (
    token_id        BIGINT NOT NULL REFERENCES tokens(id) ON DELETE CASCADE,
    object_id       BIGINT NOT NULL REFERENCES objects(id) ON DELETE CASCADE,

    PRIMARY KEY (token_id, object_id)
);

CREATE INDEX IF NOT EXISTS idx_token_object_obj_id ON token_object(object_id);

-- Supports stable per-type ranking used by typed slow search.
CREATE INDEX IF NOT EXISTS idx_objects_type_rank
    ON objects(type, sort_key DESC, id);

-- One ranking impression per authenticated viewer/object/UTC day.
CREATE TABLE IF NOT EXISTS search_object_views (
    user_id    BIGINT NOT NULL,
    object_id  BIGINT NOT NULL REFERENCES objects(id) ON DELETE CASCADE,
    viewed_on  DATE NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (user_id, object_id, viewed_on),
    CONSTRAINT ck_search_object_views_user_positive CHECK (user_id > 0),
    CONSTRAINT ck_search_object_views_object_positive CHECK (object_id > 0)
);

CREATE INDEX IF NOT EXISTS ix_search_object_views_object_date
    ON search_object_views(object_id, viewed_on DESC);
