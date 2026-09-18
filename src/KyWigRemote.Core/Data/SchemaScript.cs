namespace KyWigRemote.Core.Data;

/// <summary>
/// Script de création du schéma (DDL), repris du §4 de docs/03-architecture.md.
/// La table <c>schema_version</c> est gérée à part par <see cref="Database"/> : elle
/// n'apparaît pas ici pour ne pas dupliquer la logique de versionnement.
/// </summary>
internal static class SchemaScript
{
    /// <summary>Version de schéma produite par ce script (dernière migration incluse).</summary>
    public const int Version = 3;

    /// <summary>DDL de la version 1 : tables du domaine, dans l'ordre des dépendances de clés étrangères.</summary>
    public const string V1 = """
        -- Utilisateurs connus et leur clé publique d'enveloppe (E4)
        CREATE TABLE users (
            sid           TEXT PRIMARY KEY,
            sam_account   TEXT NOT NULL,
            display_name  TEXT,
            public_key    BLOB,
            enrolled_at   TEXT,
            last_seen_at  TEXT
        );

        -- Identifiants imposés (partagés, chiffrés par enveloppe) (E4)
        CREATE TABLE enforced_credentials (
            id             INTEGER PRIMARY KEY AUTOINCREMENT,
            label          TEXT NOT NULL,
            username       TEXT NOT NULL,
            domain         TEXT,
            secret_cipher  BLOB NOT NULL,
            secret_nonce   BLOB NOT NULL,
            secret_tag     BLOB NOT NULL,
            allowed_groups TEXT,
            created_at     TEXT NOT NULL,
            updated_at     TEXT NOT NULL,
            updated_by     TEXT
        );

        -- Dossiers de l'arborescence
        CREATE TABLE folders (
            id             INTEGER PRIMARY KEY AUTOINCREMENT,
            parent_id      INTEGER REFERENCES folders(id) ON DELETE CASCADE,
            name           TEXT NOT NULL,
            sort_order     INTEGER NOT NULL DEFAULT 0,
            credential_mode TEXT CHECK (credential_mode IN
                             ('PERSONAL','ENFORCED','PROMPT','INHERITED')) DEFAULT 'INHERITED',
            enforced_credential_id INTEGER REFERENCES enforced_credentials(id),
            created_at     TEXT NOT NULL,
            updated_at     TEXT NOT NULL,
            updated_by     TEXT
        );

        -- Connexions
        CREATE TABLE connections (
            id             INTEGER PRIMARY KEY AUTOINCREMENT,
            folder_id      INTEGER REFERENCES folders(id) ON DELETE SET NULL,
            name           TEXT NOT NULL,
            protocol       TEXT NOT NULL CHECK (protocol IN ('RDP','SSH')),
            host           TEXT NOT NULL,
            port           INTEGER NOT NULL,
            domain         TEXT,
            description    TEXT,
            credential_mode TEXT NOT NULL CHECK (credential_mode IN
                             ('PERSONAL','ENFORCED','PROMPT','INHERITED')) DEFAULT 'INHERITED',
            enforced_credential_id INTEGER REFERENCES enforced_credentials(id),
            options_json   TEXT,
            sort_order     INTEGER NOT NULL DEFAULT 0,
            created_at     TEXT NOT NULL,
            updated_at     TEXT NOT NULL,
            updated_by     TEXT
        );

        CREATE INDEX idx_connections_folder ON connections(folder_id);

        -- Une enveloppe de clé par utilisateur autorisé (E4)
        CREATE TABLE enforced_credential_keys (
            credential_id  INTEGER NOT NULL REFERENCES enforced_credentials(id) ON DELETE CASCADE,
            user_sid       TEXT    NOT NULL REFERENCES users(sid) ON DELETE CASCADE,
            wrapped_key    BLOB    NOT NULL,
            created_at     TEXT    NOT NULL,
            PRIMARY KEY (credential_id, user_sid)
        );

        -- Identifiants personnels (chiffrés DPAPI utilisateur) (E4)
        CREATE TABLE personal_credentials (
            id             INTEGER PRIMARY KEY AUTOINCREMENT,
            user_sid       TEXT NOT NULL REFERENCES users(sid) ON DELETE CASCADE,
            connection_id  INTEGER REFERENCES connections(id) ON DELETE CASCADE,
            label          TEXT,
            username       TEXT NOT NULL,
            domain         TEXT,
            secret_blob    BLOB NOT NULL,
            created_at     TEXT NOT NULL,
            updated_at     TEXT NOT NULL
        );

        CREATE UNIQUE INDEX idx_personal_unique
            ON personal_credentials(user_sid, IFNULL(connection_id, -1));

        -- Journal d'audit (E9)
        CREATE TABLE audit_log (
            id             INTEGER PRIMARY KEY AUTOINCREMENT,
            occurred_at    TEXT NOT NULL,
            user_sid       TEXT,
            user_name      TEXT,
            workstation    TEXT,
            action         TEXT NOT NULL,
            target_type    TEXT,
            target_id      INTEGER,
            credential_mode TEXT,
            result         TEXT,
            details        TEXT
        );

        CREATE INDEX idx_audit_date ON audit_log(occurred_at);
        CREATE INDEX idx_audit_user ON audit_log(user_sid);

        -- Paramètres applicatifs
        CREATE TABLE settings (
            key   TEXT PRIMARY KEY,
            value TEXT
        );
        """;

    /// <summary>
    /// Migration v2 : comptes locaux applicatifs (authentification hors AD).
    /// Le mot de passe n'est stocké que sous forme de hachage PBKDF2.
    /// </summary>
    public const string V2 = """
        CREATE TABLE local_accounts (
            id            INTEGER PRIMARY KEY AUTOINCREMENT,
            username      TEXT NOT NULL UNIQUE COLLATE NOCASE,
            display_name  TEXT,
            password_hash TEXT NOT NULL,
            is_admin      INTEGER NOT NULL DEFAULT 0,
            disabled      INTEGER NOT NULL DEFAULT 0,
            created_at    TEXT NOT NULL,
            updated_at    TEXT NOT NULL
        );
        """;

    /// <summary>
    /// Migration v3 : identifiants personnels adaptés au modèle client/serveur.
    /// L'ancienne table (schéma DPAPI par SID, jamais utilisée) est remplacée par une table
    /// possédée par « owner » (l'utilisateur connecté) et chiffrée en AES-256-GCM.
    /// </summary>
    public const string V3 = """
        DROP TABLE IF EXISTS personal_credentials;

        CREATE TABLE personal_credentials (
            id             INTEGER PRIMARY KEY AUTOINCREMENT,
            owner          TEXT NOT NULL,
            connection_id  INTEGER REFERENCES connections(id) ON DELETE CASCADE, -- NULL = global
            username       TEXT NOT NULL,
            domain         TEXT,
            secret_cipher  BLOB NOT NULL,
            secret_nonce   BLOB NOT NULL,
            secret_tag     BLOB NOT NULL,
            created_at     TEXT NOT NULL,
            updated_at     TEXT NOT NULL
        );

        CREATE UNIQUE INDEX idx_personal_owner_conn
            ON personal_credentials(owner, IFNULL(connection_id, -1));
        """;
}
