# 06 — Évolutions post-pivot

Ce document fait autorité sur les décisions prises **après** la rédaction initiale des documents
BMAD (`01`–`05`). En cas de contradiction avec `01`–`05`, `CLAUDE.md` ou un ADR de
`03-architecture.md`, **le présent document l'emporte** pour les points qu'il traite.

Contexte : le projet a été réorienté d'un client lourd autonome vers une architecture
**client/serveur**, avec réutilisation de code tierce assumée. Les documents d'origine restent
utiles pour le problème, le périmètre fonctionnel et l'ergonomie ; seuls les points ci-dessous sont
amendés.

## 1. Licence — projet en GPL-2.0

Décision : l'ensemble du projet passe sous **GPL-2.0**, ce qui autorise la réutilisation de code
sous GPL (notamment mRemoteNG).

- **Remplace ADR-007** (« ne copier aucun extrait de mRemoteNG ») et la section correspondante de
  `04-ui-spec.md`.
- En pratique, le code livré a été écrit pour ce projet ; l'emprunt ciblé (hôte RDP, reparentage
  PuTTY, arbre) reste possible sans contrainte de licence supplémentaire.
- Les bibliothèques MIT (DockPanel Suite) restent utilisables sans que la GPL les affecte.

## 2. Architecture — vrai serveur backend

Décision : un **serveur applicatif** (`KyWigRemote.Server`, ASP.NET Core, API minimale) héberge la
base et l'authentification. Le client le choisit au lancement (« serveur d'authentification »).

- **Amende ADR-003** : la mention « zéro serveur applicatif » ne tient plus. La migration
  SQLite → SQL Server qu'ADR-003 anticipait reste valable (voir §5).
- Impacte le schéma de confiance : le serveur peut déchiffrer les secrets imposés (voir §4).
- Toute la logique métier reste dans `KyWigRemote.Core` ; le serveur et les clients n'en font que
  la présentation ou l'exposition HTTP.

## 3. Authentification — trois fournisseurs

Décision : trois fournisseurs, activables par configuration (`appsettings.json`) :

1. **Comptes locaux applicatifs** — hachage PBKDF2, login → jeton JWT. *Livré et vérifié.*
2. **Active Directory** — SSO Windows (Negotiate), appartenance de groupe requise, admin par
   groupe. *Livré et vérifié sur `kywig.ad`.*
3. **Microsoft 365 / Entra ID** — *câblé côté configuration, non livré* : nécessite un tenant Entra
   et le paquet MSAL (à valider). Voir §6.

## 4. Chiffrement — clé maître serveur (AES-256-GCM)

Décision : les secrets imposés sont chiffrés **au repos** avec **AES-256-GCM** sous une **clé maître
générée et persistée côté serveur** (jamais dans le code ni le dépôt).

- **Amende ADR-005** : l'enveloppe DPAPI + RSA était pensée pour un client lourd sans serveur. Dans
  le modèle client/serveur, le serveur détient la clé maître et peut déchiffrer les secrets pour les
  fournir à un client autorisé (autorisation par groupes AD, FR-15).
- Les primitives restent celles de `System.Security.Cryptography` (règle 1 de `CLAUDE.md`
  respectée : aucune primitive maison).
- Conséquence de sécurité à assumer : compromettre le serveur = accès aux secrets. Compensé par
  l'audit, le contrôle par groupes et le chiffrement au repos.

## 5. Base — SQLite en dev, SQL Server en prod (à finir)

Décision : **SQLite** pour le développement et les tests, **SQL Server** en production.

- Conforme à l'intention d'ADR-003. **État : SQLite livré ; le fournisseur SQL Server n'est pas
  implémenté** (le serveur lève une erreur explicite si `Provider = SqlServer`). Nécessite une
  instance SQL Server pour être développé et vérifié sérieusement. Voir §6.

## 6. Sessions distantes — RDP et SSH livrés

- **SSH (E6)** : PuTTY lancé et reparenté dans l'onglet (Win32). **Jamais `-pw`** : ADR-004
  respecté, l'authentification est saisie dans PuTTY. *Livré, à vérifier avec une cible SSH.*
- **RDP (E5)** : contrôle ActiveX `AxMsRdpClient9NotSafeForScripting` hébergé dans l'onglet
  (NLA, authentification serveur, adaptation de taille). L'interop est généré par AxImp depuis
  `mstscax.dll` et **committé** (`src/KyWigRemote.Client/lib/`) pour un build reproductible sans
  Visual Studio. L'identifiant résolu (imposé/personnel) est injecté avant `Connect()`.
  *Livré ; initiation vérifiée jusqu'à l'étape d'authentification, rendu visuel à valider sur poste
  avec bureau.*

## 7. Journal — Serilog livré

`KyWigRemote.Server` journalise via **Serilog** (console + fichier tournant quotidien, 14 jours).
Aucun secret journalisé (règle 2). L'audit métier (E9, base) reste distinct du journal technique.

## 8. Ce qui reste ouvert

| Sujet | État | Ce qu'il faut |
|---|---|---|
| M365 / Entra (§3) | non livré | tenant Entra + validation du paquet MSAL |
| SQL Server (§5) | non livré | instance SQL Server + implémentation du fournisseur |
| Distribution ClickOnce | **livré (CLI)** | `.\tools\publish-clickonce.ps1 -Clean` (Build Tools 2022 suffit) |
| Rendu visuel RDP/SSH | non vérifié ici | poste avec bureau interactif + cible de test |

### ClickOnce en ligne de commande

Contrairement à ce qui était supposé, la publication ClickOnce **ne nécessite ni
Visual Studio Community ni installation supplémentaire** : les charges Build Tools 2022
`ClickOnce`, `NetCore.Component.SDK` et `ManagedDesktop` suffisent. Le script
`tools\publish-clickonce.ps1` la reproduit et gère les deux conditions qui la font
échouer autrement :

1. le host `dotnet` doit être résoluble par MSBuild (SDK .NET sur le PATH) ;
2. `MSBuild.exe` doit tourner **dans** l'environnement développeur (`VsDevCmd.bat`),
   sinon la résolution des ressources satellites échoue (centaines d'erreurs `MSB3113`).

La sortie va dans `src\KyWigRemote.Client\dist\ClientClickOnce\` (ignorée par Git) :
manifeste `KyWigRemote.Client.application` + `Application Files\` (runtime .NET autonome
embarqué, rien à installer sur le poste cible). L'incrément de version (`ApplicationRevision`)
n'est **pas** automatique en CLI — le gérer à la main dans le profil avant chaque publication.

## Récapitulatif des ADR amendés

| ADR | Statut |
|---|---|
| ADR-001 (C#) | inchangé |
| ADR-002 (WinForms) | inchangé |
| ADR-003 (SQLite / pas de serveur) | **amendé** : architecture client/serveur ; SQL Server à venir |
| ADR-004 (PuTTY, jamais `-pw`) | inchangé, respecté |
| ADR-005 (enveloppe DPAPI/RSA) | **remplacé** : AES-256-GCM + clé maître serveur |
| ADR-006 (limite identifiants imposés) | inchangé, compensé par audit + groupes + injection |
| ADR-007 (pas de copie mRemoteNG) | **remplacé** : projet en GPL-2.0 |
