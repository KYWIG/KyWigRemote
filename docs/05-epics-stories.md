# 05 — Epics et User Stories (PO / SM)

**Règle BMAD :** une story tient en une journée de développement. Si elle déborde, elle se découpe.
Pas de points de complexité. L'ordre est celui de l'implémentation : chaque story ne dépend que de
celles qui la précèdent.

Légende priorité : **M** = Must (v1), **S** = Should, **C** = Could.

---

## E1 — Socle technique

**Objectif :** une solution qui compile, se lance, journalise et lit sa configuration.

| # | Story | Prio |
|---|---|---|
| E1.1 | Créer la solution `KyWigRemote.sln` avec les 4 projets (Core, Client, Admin, Tests) ciblant .NET 8 Windows | M |
| E1.2 | Intégrer Serilog : fichier tournant dans `%LOCALAPPDATA%\KyWigRemote\logs`, niveau configurable | M |
| E1.3 | Charger `appsettings.json` dans une classe `AppConfig` typée, avec valeurs par défaut et validation au démarrage | M |
| E1.4 | Fenêtre principale vide avec DockPanel Suite : panneaux Connexions, Propriétés, zone centrale | M |
| E1.5 | Dépôt Git interne, `.gitignore`, README, convention de commits | M |

**Critères d'acceptation E1.1** — la solution compile en Debug et Release ; Client et Admin
référencent Core ; Tests exécute au moins un test vide qui passe.

**Critères d'acceptation E1.3** — une configuration absente ou invalide affiche un message clair et
arrête proprement, sans exception non gérée.

---

## E2 — Base de données

**Objectif :** le schéma existe, se crée seul, et la couche d'accès fonctionne.

| # | Story | Prio |
|---|---|---|
| E2.1 | Script de création du schéma (DDL du document d'architecture) exécuté si la base est absente | M |
| E2.2 | Mécanisme de migration par `schema_version` : la base se met à niveau au lancement | M |
| E2.3 | `IConnectionRepository` + implémentation SQLite : CRUD dossiers et connexions | M |
| E2.4 | `ICredentialRepository` : CRUD identifiants personnels et imposés | M |
| E2.5 | `IAuditRepository` : écriture d'événement et lecture filtrée | M |
| E2.6 | Configuration WAL, `busy_timeout`, transactions courtes ; test de deux écritures concurrentes | M |
| E2.7 | Jeu de données de démonstration pour le développement | S |

**Critères d'acceptation E2.2** — sur une base en version N-1, le lancement applique la migration et
journalise l'opération ; sur une version plus récente que le code, l'application refuse de démarrer
avec un message explicite.

**Critères d'acceptation E2.6** — deux instances écrivant simultanément ne provoquent ni corruption
ni exception non gérée ; la seconde attend puis aboutit.

---

## E3 — Authentification et droits

**Objectif :** l'application sait qui est l'utilisateur et ce qu'il a le droit de faire.

| # | Story | Prio |
|---|---|---|
| E3.1 | `AdIdentity` : récupérer SID, sAMAccountName et nom d'affichage de la session Windows | M |
| E3.2 | `GroupChecker` : tester l'appartenance à un groupe AD, avec cache mémoire de 5 minutes | M |
| E3.3 | Contrôle au démarrage du client : appartenance à `GG_KyWigRemote_Users`, sinon message et sortie | M |
| E3.4 | Contrôle au démarrage de l'admin : appartenance à `GG_KyWigRemote_Admins` | M |
| E3.5 | Enregistrer ou mettre à jour l'utilisateur dans `users` à chaque lancement (`last_seen_at`) | M |
| E3.6 | Mode dégradé hors domaine : message explicite, pas de plantage | S |

**Critères d'acceptation E3.3** — un compte hors groupe voit le message de refus, l'événement est
journalisé en `DENIED`, l'application se ferme sans afficher l'arborescence.

---

## E4 — Moteur d'identifiants

**Objectif :** le coeur du projet. À traiter avec le plus grand soin et la meilleure couverture de tests.

| # | Story | Prio |
|---|---|---|
| E4.1 | `DpapiVault` : chiffrer/déchiffrer un secret en portée CurrentUser | M |
| E4.2 | Enrôlement : génération RSA-3072 au premier lancement, clé privée protégée DPAPI, publique en base | M |
| E4.3 | `EnvelopeCrypto` : chiffrer un secret en AES-256-GCM, envelopper la clé de contenu en RSA-OAEP par utilisateur | M |
| E4.4 | Déverrouillage d'une enveloppe et déchiffrement du secret côté client | M |
| E4.5 | `CredentialResolver` : implémenter l'algorithme de résolution des 4 modes, héritage compris | M |
| E4.6 | Enregistrement d'un identifiant personnel, portée connexion ou globale | M |
| E4.7 | Contrôle des groupes autorisés avant tout accès à un identifiant imposé | M |
| E4.8 | Distribution des clés : créer les enveloppes manquantes pour les utilisateurs autorisés (action admin) | M |
| E4.9 | Révocation : suppression des enveloppes d'un utilisateur | M |
| E4.10 | Effacement des tampons de secret après usage ; aucun secret dans les journaux | M |
| E4.11 | Tests unitaires du résolveur : les 4 modes, héritage sur 3 niveaux, cas d'absence, cas de refus | M |

**Critères d'acceptation E4.3/E4.4** — un secret chiffré par l'utilisateur A et enveloppé pour A et
B est déchiffrable par les deux ; un utilisateur C sans enveloppe obtient un refus explicite et non
une exception cryptographique.

**Critères d'acceptation E4.11** — couverture supérieure à 90 % sur `CredentialResolver`.

---

## E5 — Sessions RDP

| # | Story | Prio |
|---|---|---|
| E5.1 | Interface `IRemoteSession` : Connect, Disconnect, événements Connected/Disconnected/Error | M |
| E5.2 | `RdpSession` hébergeant `AxMsRdpClient9NotSafeForScripting` dans un panneau | M |
| E5.3 | Injection des identifiants résolus ; passage par SecureString jusqu'à l'appel final | M |
| E5.4 | Ouverture dans un onglet dockable, titre = nom de la connexion, fermeture propre | M |
| E5.5 | Traduction des codes d'erreur RDP en messages lisibles | S |
| E5.6 | Options par connexion : résolution, presse-papiers, lecteurs, imprimantes (`options_json`) | S |
| E5.7 | Plein écran (F11) et retour | S |
| E5.8 | Reconnexion en un clic après coupure | S |
| E5.9 | Détacher un onglet dans une fenêtre indépendante | C |

**Critères d'acceptation E5.4** — trois sessions simultanées restent stables ; fermer un onglet
libère le contrôle ActiveX sans fuite de processus ni handle.

---

## E6 — Sessions SSH

| # | Story | Prio |
|---|---|---|
| E6.1 | `SshSession` : lancement de PuTTY et reparentage de sa fenêtre dans l'onglet | M |
| E6.2 | Redimensionnement de la fenêtre PuTTY suivant la taille de l'onglet | M |
| E6.3 | Fermeture de l'onglet = fermeture propre du processus PuTTY | M |
| E6.4 | Détection de l'absence de PuTTY, message clair et lien vers la configuration | M |
| E6.5 | Authentification par clé : chemin de clé privée par connexion | S |
| E6.6 | Documenter la limite du mode imposé en SSH (voir ADR-004) dans l'aide et à l'écran | M |
| E6.7 | Étude v2 : terminal natif via SSH.NET, pour lever la limite du mot de passe | C |

**Critères d'acceptation E6.3** — après fermeture de 10 onglets SSH, aucun `putty.exe` ne subsiste
dans le gestionnaire de tâches.

---

## E7 — Console d'administration

| # | Story | Prio |
|---|---|---|
| E7.1 | Arborescence éditable : créer, renommer, supprimer un dossier | M |
| E7.2 | Éditeur de connexion : tous les champs, validation, port par défaut selon protocole | M |
| E7.3 | Glisser-déposer pour réorganiser dossiers et connexions | S |
| E7.4 | Gestion des identifiants imposés : création, modification, groupes autorisés | M |
| E7.5 | Bouton « Distribuer les clés » avec compte rendu des enveloppes créées | M |
| E7.6 | Visionneuse d'audit : grille, filtres période/utilisateur/connexion/résultat, tri | M |
| E7.7 | Export CSV de l'arborescence, sans aucun secret | S |
| E7.8 | Import CSV | S |
| E7.9 | Import d'un fichier de connexions mRemoteNG (XML non chiffré) | S |
| E7.10 | Onglet Utilisateurs : liste des enrôlés, révocation | S |

**Critères d'acceptation E7.7** — l'export ne contient ni mot de passe, ni blob chiffré, ni clé.

---

## E8 — Interface et thème

| # | Story | Prio |
|---|---|---|
| E8.1 | Thème sombre appliqué à tous les contrôles (palette du document d'interface) | M |
| E8.2 | Arbre : icônes par protocole, pastille de session active, cadenas pour identifiants imposés | M |
| E8.3 | Recherche filtrante en temps réel | M |
| E8.4 | Panneau Propriétés en lecture seule côté client | M |
| E8.5 | Barre d'état : utilisateur, sessions ouvertes, source de données | M |
| E8.6 | Mémorisation de la disposition des panneaux par utilisateur | S |
| E8.7 | Raccourcis clavier du tableau de la spécification d'interface | S |
| E8.8 | Boîte de saisie d'identifiants conforme à la maquette | M |
| E8.9 | Écran À propos : version, licences des composants tiers | M |

---

## E9 — Journalisation et audit

| # | Story | Prio |
|---|---|---|
| E9.1 | Écrire un événement d'audit à chaque ouverture et fermeture de session | M |
| E9.2 | Écrire un événement à chaque refus d'accès | M |
| E9.3 | Écrire un événement à chaque modification en administration | M |
| E9.4 | Vérifier qu'aucun secret ne peut atteindre le journal (test dédié) | M |
| E9.5 | Purge configurable des événements de plus de N mois | S |
| E9.6 | Rapport mensuel : nombre de connexions par utilisateur et par serveur | C |

---

## E10 — Packaging et déploiement

| # | Story | Prio |
|---|---|---|
| E10.1 | Publication autonome (self-contained) x64 des deux exécutables | M |
| E10.2 | Paquet MSI ou installeur, incluant `appsettings.json` | M |
| E10.3 | Déploiement Intune en application Win32, avec règle de détection | M |
| E10.4 | Création du partage `\\kers015\KyWigRemote$` et des ACL NTFS par groupe AD | M |
| E10.5 | Tâche planifiée de sauvegarde quotidienne de la base | M |
| E10.6 | Documentation utilisateur, 2 pages | M |
| E10.7 | Documentation d'exploitation : restauration, révocation, ajout d'utilisateur | M |
| E10.8 | Fiche GLPI décrivant le service et ses dépendances | S |

---

## Ordre d'attaque conseillé

```
E1 → E2 → E3 → E4 → E5 → E8 (partiel) → E9 → E6 → E7 → E8 (reste) → E10
```

Justification : avoir une session RDP qui s'ouvre avec un identifiant personnel dès la fin de E5
donne un produit démontrable très tôt. E6 (SSH) vient après parce que le reparentage est la partie
la plus incertaine techniquement — mieux vaut l'aborder avec un socle stable.

## Jalons

| Jalon | Contenu | Valeur démontrable |
|---|---|---|
| J1 | E1 + E2 + E3 | L'application s'ouvre, lit la base, contrôle les droits AD |
| J2 | + E4 + E5 | Une session RDP s'ouvre avec les 3 modes d'identifiants |
| J3 | + E6 + E8 + E9 | Produit utilisable au quotidien, audité |
| J4 | + E7 + E10 | Administration complète, déployé sur les 3 postes |
