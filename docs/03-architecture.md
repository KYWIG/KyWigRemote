# 03 — Architecture (Architect)

**Projet :** KyWigRemote — v1.0
**Entrées :** `01-project-brief.md`, `02-prd.md`

---

## 1. Vue d'ensemble

```
        Poste technicien (Windows, joint au domaine)
   ┌────────────────────────────────────────────────┐
   │  KyWigRemote.Client.exe   KyWigRemote.Admin.exe │
   │              │                    │             │
   │              └────────┬───────────┘             │
   │                KyWigRemote.Core.dll             │
   │      ┌──────────┬─────────┬──────────┐          │
   │      │ Données  │ Crypto  │ Annuaire │ Protocoles│
   │      └────┬─────┴────┬────┴────┬─────┘     │    │
   └───────────┼──────────┼─────────┼───────────┼────┘
               │          │         │           │
        base SQLite   DPAPI local   AD      mstscax.dll
      (partage SMB)  (%LOCALAPPDATA%) kywig.ad   PuTTY
```

Pas de serveur applicatif. Le client lit et écrit directement une base SQLite posée sur un partage
réseau, ACLée pour le groupe des techniciens. C'est le compromis assumé de la v1 : zéro
infrastructure à maintenir, au prix d'une sécurité qui repose sur les ACL NTFS et le chiffrement
des secrets.

## 2. Pile technique

| Couche | Choix | Justification |
|---|---|---|
| Runtime | .NET 8 (LTS) | Support long, déjà présent sur le parc |
| Langage | C# 12 | Voir ADR-001 |
| Interface | WinForms | Voir ADR-002 |
| Docking / onglets | DockPanel Suite (MIT) | Bibliothèque utilisée par mRemoteNG ; licence permissive |
| Arborescence | TreeView natif, ou ObjectListView si besoin | Suffisant pour 500 entrées |
| Base | SQLite via `Microsoft.Data.Sqlite` | Voir ADR-003 |
| Annuaire | `System.DirectoryServices.AccountManagement` | Natif, pas de dépendance |
| Chiffrement | `System.Security.Cryptography` (AES-GCM, RSA, DPAPI) | Voir ADR-005 |
| RDP | `AxMsRdpClient9NotSafeForScripting` (mstscax.dll) | Contrôle ActiveX fourni par Windows |
| SSH | PuTTY embarqué par reparentage | Voir ADR-004 |
| Journalisation | Serilog, fichier tournant | Standard, simple |
| Tests | xUnit + FluentAssertions | |

## 3. Décisions d'architecture (ADR)

### ADR-001 — C# plutôt que Java

**Décision :** C# / .NET 8.

**Motifs :** l'environnement est intégralement Windows ; Java Web Start (JNLP) a été déprécié en
Java 9 puis retiré du JDK à partir de Java 11, ce qui condamne la piste du déploiement JNLP ;
le contrôle RDP ActiveX et DPAPI n'ont pas d'équivalent Java ; l'intégration AD est native en .NET.

### ADR-002 — WinForms plutôt que WPF

**Décision :** WinForms.

**Motifs :** l'hébergement du contrôle ActiveX RDP et le reparentage d'une fenêtre PuTTY sont
natifs en WinForms ; en WPF ils imposent `WindowsFormsHost` et ses problèmes d'airspace.
mRemoteNG, dont on reprend l'ergonomie, est lui-même en WinForms avec DockPanel Suite — la
correspondance visuelle est donc directe.

**Conséquence :** interface moins moderne à écrire, mais moins de pièges techniques.

### ADR-003 — SQLite en v1, migration SQL Server prévue

**Décision :** base SQLite unique sur partage SMB, mode WAL.

**Motifs :** aucun serveur à installer, sauvegarde par simple copie de fichier, adapté à 3
utilisateurs.

**Limites acceptées :** SQLite sur partage réseau supporte mal les écritures concurrentes
soutenues ; l'écriture est donc rare (administration) et la lecture majoritaire. Le verrou réseau
SMB peut poser problème si plusieurs écritures se croisent — d'où les transactions courtes et le
`busy_timeout`.

**Porte de sortie :** la couche d'accès passe par une interface `IConnectionRepository`. Le
basculement vers SQL Server consiste à écrire une seconde implémentation, sans toucher au reste.

### ADR-004 — SSH par PuTTY embarqué

**Décision :** lancer `putty.exe` et reparenter sa fenêtre dans un onglet via `SetParent`.

**Motifs :** c'est l'approche de mRemoteNG, éprouvée ; écrire un émulateur de terminal complet
(séquences ANSI, redimensionnement, encodages) représenterait à lui seul plus de travail que tout
le reste du projet.

**Limite majeure :** passer un mot de passe par `-pw` l'expose dans la ligne de commande du
processus, visible par tout utilisateur du poste via le gestionnaire de tâches.
**Conséquence :** en v1, pour SSH, on privilégie l'authentification par clé publique ; le mode
IMPOSÉ avec mot de passe SSH est signalé comme dégradé dans la documentation. Une v2 basée sur
SSH.NET et un terminal natif lèvera cette limite.

### ADR-005 — Schéma de chiffrement

**Décision :** aucune primitive écrite à la main. Deux mécanismes distincts selon le mode.

**Identifiants personnels — DPAPI**

Le secret est chiffré avec `ProtectedData.Protect(..., DataProtectionScope.CurrentUser)`. Le blob
est stocké dans la base partagée, mais seul le compte Windows qui l'a chiffré peut le déchiffrer.
L'administrateur, même avec un accès complet au fichier SQLite, ne lit rien.

**Identifiants imposés — chiffrement hybride (enveloppe)**

DPAPI ne convient pas ici : plusieurs utilisateurs doivent pouvoir déchiffrer le même secret.

1. À son premier lancement, chaque client génère une paire RSA-3072. La clé publique part dans la
   table `users` ; la clé privée reste sur le poste, protégée par DPAPI utilisateur.
2. Un identifiant imposé est chiffré avec une clé de contenu aléatoire (AES-256-GCM).
3. Cette clé de contenu est chiffrée séparément avec la clé publique RSA de chaque utilisateur
   autorisé, et chaque enveloppe est stockée dans `enforced_credential_keys`.
4. À l'ouverture, le client déverrouille l'enveloppe qui lui est destinée, obtient la clé de
   contenu, déchiffre le secret en mémoire, l'injecte dans la session, puis efface le tampon.

Révoquer un accès consiste à supprimer l'enveloppe correspondante. Un nouvel utilisateur reçoit ses
enveloppes lors d'une action d'administration explicite.

### ADR-006 — Limite structurelle assumée sur les identifiants imposés

Dans une architecture à client lourd, le secret doit être déchiffré sur le poste de l'utilisateur
pour ouvrir la session. Un utilisateur autorisé, techniquement compétent et malveillant, peut donc
l'extraire (débogueur, capture mémoire).

**Cette limite est identique dans RDM et dans tous les gestionnaires à client lourd.** Seule une
architecture à proxy (type Guacamole ou Teleport), où le secret ne quitte jamais le serveur, y
échappe.

**Décision :** limite acceptée et documentée. La protection vise l'usage courant, la diffusion
accidentelle et la lecture de la base, pas l'attaque interne délibérée. Le journal d'audit reste le
garde-fou : l'usage est tracé même s'il est détourné.

### ADR-007 — Statut du code de mRemoteNG

mRemoteNG est publié sous **GPL-2.0**. Copier son code source dans KyWigRemote imposerait à ce
dernier la même licence, avec obligation de fourniture du code à quiconque reçoit le binaire.

**Décision :** on reprend l'**ergonomie et l'apparence** (disposition, thème sombre, logique de
docking), pas le code. Les bibliothèques tierces utilisées par mRemoteNG restent libres d'emploi —
DockPanel Suite est sous licence MIT.

En cas de doute sur un fragment de code : le réécrire à partir de la documentation de la
bibliothèque, pas du dépôt mRemoteNG.

## 4. Modèle de données

```sql
PRAGMA journal_mode = WAL;
PRAGMA foreign_keys = ON;
PRAGMA busy_timeout = 5000;

-- Version de schéma, pour les migrations
CREATE TABLE schema_version (
    version     INTEGER NOT NULL,
    applied_at  TEXT    NOT NULL
);

-- Utilisateurs connus et leur clé publique d'enveloppe
CREATE TABLE users (
    sid           TEXT PRIMARY KEY,          -- SID AD, identifiant stable
    sam_account   TEXT NOT NULL,
    display_name  TEXT,
    public_key    BLOB,                      -- RSA-3072 publique, format DER
    enrolled_at   TEXT,
    last_seen_at  TEXT
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
    options_json   TEXT,                     -- options RDP/SSH spécifiques
    sort_order     INTEGER NOT NULL DEFAULT 0,
    created_at     TEXT NOT NULL,
    updated_at     TEXT NOT NULL,
    updated_by     TEXT
);

CREATE INDEX idx_connections_folder ON connections(folder_id);

-- Identifiants imposés (partagés, chiffrés par enveloppe)
CREATE TABLE enforced_credentials (
    id             INTEGER PRIMARY KEY AUTOINCREMENT,
    label          TEXT NOT NULL,
    username       TEXT NOT NULL,
    domain         TEXT,
    secret_cipher  BLOB NOT NULL,            -- AES-256-GCM
    secret_nonce   BLOB NOT NULL,
    secret_tag     BLOB NOT NULL,
    allowed_groups TEXT,                     -- groupes AD, séparés par ;
    created_at     TEXT NOT NULL,
    updated_at     TEXT NOT NULL,
    updated_by     TEXT
);

-- Une enveloppe de clé par utilisateur autorisé
CREATE TABLE enforced_credential_keys (
    credential_id  INTEGER NOT NULL REFERENCES enforced_credentials(id) ON DELETE CASCADE,
    user_sid       TEXT    NOT NULL REFERENCES users(sid) ON DELETE CASCADE,
    wrapped_key    BLOB    NOT NULL,         -- clé de contenu chiffrée RSA-OAEP
    created_at     TEXT    NOT NULL,
    PRIMARY KEY (credential_id, user_sid)
);

-- Identifiants personnels (chiffrés DPAPI utilisateur)
CREATE TABLE personal_credentials (
    id             INTEGER PRIMARY KEY AUTOINCREMENT,
    user_sid       TEXT NOT NULL REFERENCES users(sid) ON DELETE CASCADE,
    connection_id  INTEGER REFERENCES connections(id) ON DELETE CASCADE, -- NULL = global
    label          TEXT,
    username       TEXT NOT NULL,
    domain         TEXT,
    secret_blob    BLOB NOT NULL,            -- DPAPI CurrentUser
    created_at     TEXT NOT NULL,
    updated_at     TEXT NOT NULL
);

CREATE UNIQUE INDEX idx_personal_unique
    ON personal_credentials(user_sid, IFNULL(connection_id, -1));

-- Journal d'audit
CREATE TABLE audit_log (
    id             INTEGER PRIMARY KEY AUTOINCREMENT,
    occurred_at    TEXT NOT NULL,
    user_sid       TEXT,
    user_name      TEXT,
    workstation    TEXT,
    action         TEXT NOT NULL,            -- SESSION_OPEN, SESSION_CLOSE, CONN_CREATE, ...
    target_type    TEXT,                     -- CONNECTION, FOLDER, CREDENTIAL
    target_id      INTEGER,
    credential_mode TEXT,
    result         TEXT,                     -- OK, DENIED, ERROR
    details        TEXT                      -- jamais de secret
);

CREATE INDEX idx_audit_date ON audit_log(occurred_at);
CREATE INDEX idx_audit_user ON audit_log(user_sid);

-- Paramètres applicatifs
CREATE TABLE settings (
    key   TEXT PRIMARY KEY,
    value TEXT
);
```

## 5. Résolution des identifiants

Algorithme central, à implémenter dans `CredentialResolver` et à couvrir par des tests.

```
resolve(connexion, utilisateur) :
    mode ← connexion.credential_mode
    si mode = INHERITED :
        mode ← remonter les dossiers parents jusqu'au premier mode explicite
        si aucun trouvé → mode = PROMPT

    selon mode :
      PERSONAL :
          cred ← personal_credentials(user, connexion)
                 ou personal_credentials(user, global)
          si absent → demander, proposer l'enregistrement
          retourner déchiffrement DPAPI

      ENFORCED :
          si utilisateur ∉ allowed_groups → refuser, journaliser DENIED
          enveloppe ← enforced_credential_keys(credential, user)
          si absente → refuser, signaler « enrôlement requis »
          clé ← RSA-déchiffrement(enveloppe, clé privée locale)
          retourner AES-GCM-déchiffrement(secret, clé)

      PROMPT :
          demander, ne rien enregistrer
```

## 6. Ouverture des sessions

### RDP

```csharp
var rdp = new AxMsRdpClient9NotSafeForScripting();
tabPanel.Controls.Add(rdp);
rdp.Server   = connexion.Host;
rdp.UserName = identifiants.Username;
rdp.AdvancedSettings9.RDPPort           = connexion.Port;
rdp.AdvancedSettings9.ClearTextPassword = identifiants.Secret; // en mémoire uniquement
rdp.AdvancedSettings9.EnableCredSspSupport = true;
rdp.Connect();
```

Le secret est fourni sous forme de `SecureString` jusqu'au dernier moment, puis le tampon est
effacé. Les événements `OnDisconnected` alimentent l'audit et proposent la reconnexion.

### SSH

```csharp
var psi = new ProcessStartInfo("putty.exe",
    $"-ssh {user}@{host} -P {port}");   // jamais -pw : voir ADR-004
var process = Process.Start(psi);
process.WaitForInputIdle();
SetParent(process.MainWindowHandle, tabPanel.Handle);
// retrait des bordures, ajustement à la taille de l'onglet
```

## 7. Structure de la solution

```
KyWigRemote.sln
├── KyWigRemote.Core/
│   ├── Data/          DbContext, repositories, migrations
│   ├── Model/         Folder, Connection, Credential, AuditEntry
│   ├── Security/      DpapiVault, EnvelopeCrypto, CredentialResolver
│   ├── Directory/     AdIdentity, GroupChecker
│   ├── Protocols/     IRemoteSession, RdpSession, SshSession
│   └── Logging/
├── KyWigRemote.Client/
│   ├── Forms/         MainForm, ConnectionTreePanel, SessionTabPanel, PromptDialog
│   └── Theme/
├── KyWigRemote.Admin/
│   └── Forms/         AdminForm, ConnectionEditor, CredentialEditor, AuditViewer
└── KyWigRemote.Tests/
```

## 8. Configuration

`appsettings.json`, déployé à côté de l'exécutable :

```json
{
  "Database": { "Path": "\\\\kers015\\KyWigRemote$\\kywigremote.db" },
  "ActiveDirectory": {
    "Domain": "kywig.ad",
    "UserGroup":  "GG_KyWigRemote_Users",
    "AdminGroup": "GG_KyWigRemote_Admins"
  },
  "Ssh": { "PuttyPath": "%ProgramFiles%\\PuTTY\\putty.exe" },
  "Logging": { "Path": "%LOCALAPPDATA%\\KyWigRemote\\logs\\app-.log", "Level": "Information" }
}
```

## 9. Sécurité — synthèse

| Menace | Parade |
|---|---|
| Lecture du fichier SQLite copié | Tous les secrets chiffrés ; DPAPI hors du poste = inexploitable |
| Utilisateur non autorisé lançant l'application | Contrôle d'appartenance AD au démarrage + ACL NTFS sur le partage |
| Administrateur curieux lisant les identifiants personnels | DPAPI utilisateur : techniquement impossible |
| Utilisateur autorisé extrayant un secret imposé | Non couvert — ADR-006, compensé par l'audit |
| Mot de passe SSH visible dans la ligne de commande | `-pw` proscrit ; authentification par clé recommandée |
| Suppression de traces par un utilisateur | Droits d'écriture seule sur `audit_log` ; sauvegarde quotidienne |
| Perte du poste d'un utilisateur | Sa clé privée est protégée par DPAPI et liée à son profil ; révocation en supprimant ses enveloppes |

## 10. Sauvegarde

Tâche planifiée quotidienne sur kers015 :

```powershell
$src  = "D:\KyWigRemote\kywigremote.db"
$dst  = "D:\Backups\KyWigRemote\kywigremote_$(Get-Date -f yyyyMMdd).db"
sqlite3.exe $src ".backup '$dst'"     # sauvegarde à chaud, cohérente
Get-ChildItem D:\Backups\KyWigRemote | Where-Object CreationTime -lt (Get-Date).AddDays(-30) |
    Remove-Item
```

Une copie simple du fichier n'est pas fiable en mode WAL : la commande `.backup` est obligatoire.
