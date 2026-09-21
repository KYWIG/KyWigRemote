# CLAUDE.md — KyWigRemote

Ce fichier est chargé automatiquement au démarrage de chaque session Claude Code dans ce dépôt.
Il fait autorité sur les conventions du projet.

## Contexte

KyWigRemote est un gestionnaire de connexions distantes (RDP / SSH) développé en interne pour
KyWig Informatique, société de services informatiques gérant trois entités (KyWig, Kermazegan,
Karantez) sur un Active Directory unique `kywig.ad`.

Il remplace une absence d'outil : aujourd'hui chaque technicien a ses propres raccourcis RDP et ses
propres sessions PuTTY, et les mots de passe des comptes de service circulent hors de tout coffre.

Développeur unique : Nolhan Marchand, alternant TSSR. Le code doit rester lisible et repris par un
tiers.

Les solutions du marché ont été évaluées et écartées (budget zéro, RDM Team à ~900 $/an, DVLS
gratuit ne couvrant pas l'accès client) — le détail est dans `docs/01-project-brief.md` §8. Ne pas
rouvrir ce débat.

## Documents de référence

Lis-les avant toute implémentation. En cas de contradiction avec ce fichier, ils font foi.

- `docs/01-project-brief.md` — problème, périmètre, contraintes, risques
- `docs/02-prd.md` — exigences fonctionnelles (FR-xx) et non fonctionnelles (NFR-xx)
- `docs/03-architecture.md` — pile technique, ADR, schéma SQL, schéma cryptographique
- `docs/04-ui-spec.md` — disposition, palette, comportements, messages
- `docs/05-epics-stories.md` — epics E1 à E10 et stories, avec critères d'acceptation
- `docs/06-evolutions.md` — **décisions post-pivot (client/serveur, GPL, multi-auth, crypto).
  Fait autorité sur les points qu'il traite, y compris contre ce fichier et les ADR.**

> ⚠️ Le projet a pivoté vers une architecture **client/serveur**, sous **GPL-2.0**, avec chiffrement
> **AES-256-GCM à clé maître serveur**. Plusieurs passages ci-dessous (interdiction de copie
> mRemoteNG, « zéro serveur », enveloppe DPAPI/RSA) sont amendés par `docs/06-evolutions.md`.

## Pile technique

| Élément | Choix | Ne pas remplacer sans validation |
|---|---|---|
| Runtime | .NET 8 (LTS), Windows x64 | oui |
| Langage | C# 12 | oui |
| Interface | WinForms | oui — voir ADR-002 |
| Docking | DockPanel Suite (MIT) | oui |
| Base | SQLite via `Microsoft.Data.Sqlite` | oui — voir ADR-003 |
| Annuaire | `System.DirectoryServices.AccountManagement` | oui |
| Crypto | `System.Security.Cryptography` (DPAPI, AES-GCM, RSA) | **jamais** |
| RDP | `AxMsRdpClient9NotSafeForScripting` | oui |
| SSH | PuTTY embarqué par reparentage | oui — voir ADR-004 |
| Journal | Serilog | non |
| Tests | xUnit + FluentAssertions | non |

WinForms est un choix délibéré, pas un héritage : l'hébergement du contrôle ActiveX RDP et le
reparentage de la fenêtre PuTTY y sont natifs, alors qu'en WPF ils imposent `WindowsFormsHost` et
ses problèmes d'airspace. Ne propose pas de migrer vers WPF, MAUI, Avalonia ou une interface web.

## Structure

```
src/
├── KyWigRemote.Core/      Data/ Model/ Security/ Directory/ Protocols/ Logging/
├── KyWigRemote.Client/    Forms/ Theme/          (console de connexion, tous utilisateurs)
├── KyWigRemote.Admin/     Forms/                 (console d'administration, admins AD)
└── KyWigRemote.Tests/
docs/                      les 5 documents BMAD
```

Toute la logique métier va dans Core. Les projets d'interface ne contiennent que de la présentation.
Aucun accès direct à SQLite depuis Client ou Admin : tout passe par les repositories de Core.

## Règles de sécurité — non négociables

1. **N'écris jamais de primitive cryptographique.** Utilise exclusivement `ProtectedData`,
   `AesGcm`, `RSA` et `RandomNumberGenerator` de .NET. Pas de XOR maison, pas de dérivation
   artisanale, pas de bibliothèque tierce non auditée.
2. **Aucun secret dans un journal**, un message d'erreur, une exception, un export ou un commentaire.
   Si tu écris un log dans un chemin qui manipule un secret, log l'identifiant de la connexion, pas
   la valeur.
3. **Aucun secret en `string` plus longtemps que nécessaire.** Utilise `SecureString` ou `char[]`
   effacé après usage, jusqu'au dernier appel d'API qui exige une chaîne.
4. **Jamais `-pw` sur la ligne de commande PuTTY** — le mot de passe deviendrait visible dans le
   gestionnaire de tâches. Voir ADR-004.
5. **Aucun secret en dur** dans le code, les tests ou les fichiers de configuration. Les jeux de
   test utilisent des valeurs factices explicitement nommées comme telles.
6. **Pas de secret dans Git.** Vérifie le `.gitignore` avant tout commit qui touche à la config.

## Règles de développement

- **Français** pour les commentaires, les messages utilisateur et les libellés d'interface.
  **Anglais** pour les noms de classes, méthodes et variables.
- Une story à la fois. Ne commence jamais la suivante sans validation explicite.
- Ne crée pas de fichier hors du périmètre de la story en cours.
- Toute méthode publique de Core est testée. Le `CredentialResolver` vise plus de 90 % de couverture.
- Pas de dépendance NuGet nouvelle sans me demander d'abord, en justifiant licence et taille.
- Pas de refactoring spontané du code existant : propose, attends l'accord.
- Les exceptions attrapées sont journalisées puis traduites en message utilisateur lisible. Pas de
  `catch { }` silencieux.

## Style d'interface

L'apparence cible est celle de mRemoteNG en thème sombre : dense, sobre, outil d'administration
système. Palette et disposition dans `docs/04-ui-spec.md`.

À proscrire : dégradés, ombres portées, grands rayons d'arrondi, emojis dans l'interface, zones
vides décoratives, animations, cartes flottantes. Si un écran ressemble à une page d'accueil de
produit SaaS, c'est raté.

**mRemoteNG est sous GPL-2.0.** Ne copie aucun extrait de son code source : cela imposerait la GPL à
tout le projet. On s'inspire de l'ergonomie et de l'apparence, on écrit notre code. Les
bibliothèques qu'il utilise restent utilisables séparément (DockPanel Suite est en MIT).

## Commandes

```bash
dotnet build                                    # compiler
dotnet test                                     # tous les tests
dotnet test --filter FullyQualifiedName~Credential   # une famille de tests
dotnet run --project src/KyWigRemote.Client     # lancer le client
dotnet run --project src/KyWigRemote.Admin      # lancer l'administration
dotnet format                                   # formatage avant commit
```

Instance de dev complète en une commande (serveur + amorçage admin + client + admin) :

```powershell
.\tools\dev-run.ps1 -AdminPassword '<mot-de-passe-dev>' -Reset
```

Publication ClickOnce du client (Build Tools 2022 suffit, sans Visual Studio) :

```powershell
.\tools\publish-clickonce.ps1 -Clean
```

## Git

- Une branche par story : `feature/E4.3-envelope-crypto`
- Messages de commit en français, à l'impératif, préfixés de la story :
  `E4.3 - ajoute le chiffrement par enveloppe des identifiants imposés`
- Un commit par story terminée, pas un commit par fichier.
- Ne commite jamais sans que `dotnet build` et `dotnet test` passent.

## Environnement

- Poste de développement Windows, joint au domaine `kywig.ad`.
- **Ne te connecte à aucun serveur de production.** `kers015` héberge un Devolutions Server avec des
  mots de passe réels : ni test, ni écriture, ni lecture depuis ce projet.
- La base de développement est un fichier SQLite local, jamais le partage réseau.
- Les tests qui nécessitent l'AD sont marqués `[Trait("Category","RequiresAD")]` et exclus par défaut.

## Ce que j'attends de toi

- Si une story est ambiguë, pose la question avant de coder.
- Si une décision d'architecture te paraît mauvaise, dis-le — mais argumente, ne contourne pas
  silencieusement.
- Si tu ne sais pas, dis-le. Ne produis pas de code plausible sur une API que tu n'es pas sûr de
  connaître : vérifie la documentation.
- Signale les limites de ce que tu livres. Un code qui compile n'est pas un code qui marche.
