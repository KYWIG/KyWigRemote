# 02 — Product Requirements Document (PM)

**Projet :** KyWigRemote — v1.0
**Entrée :** `01-project-brief.md`

---

## 1. Vision

Un technicien ouvre KyWigRemote, double-clique sur un serveur dans l'arborescence, et la session
s'ouvre dans un onglet — avec son compte à lui, ou avec le compte imposé, sans jamais chercher un
mot de passe ailleurs.

## 2. Exigences fonctionnelles

### Arborescence et connexions

| ID | Exigence | Priorité |
|---|---|---|
| FR-01 | L'arborescence affiche des dossiers imbriqués et des connexions, partagés par tous les utilisateurs | Must |
| FR-02 | Une connexion porte : nom, protocole (RDP/SSH), hôte, port, domaine, description, mode d'identifiants | Must |
| FR-03 | Un dossier peut définir un mode d'identifiants hérité par ses enfants | Must |
| FR-04 | La recherche filtre l'arborescence en temps réel sur le nom et l'hôte | Must |
| FR-05 | Le port par défaut est déduit du protocole (3389 / 22) et modifiable | Should |
| FR-06 | Les connexions peuvent être marquées en favori, regroupées en tête d'arborescence | Could |

### Identifiants

| ID | Exigence | Priorité |
|---|---|---|
| FR-10 | Mode **PERSONNEL** : l'utilisateur enregistre ses propres identifiants, chiffrés pour lui seul ; personne d'autre ne peut les lire, y compris l'administrateur | Must |
| FR-11 | Mode **IMPOSÉ** : l'administrateur associe un identifiant partagé ; l'utilisateur peut s'en servir sans jamais l'afficher | Must |
| FR-12 | Mode **DEMANDE** : saisie au lancement, rien n'est persisté | Must |
| FR-13 | Mode **HÉRITÉ** : la connexion reprend le mode du dossier parent | Must |
| FR-14 | Un identifiant personnel peut être global (réutilisé partout) ou spécifique à une connexion | Should |
| FR-15 | L'accès à un identifiant imposé est restreint à une liste de groupes AD | Must |
| FR-16 | L'utilisateur peut consulter et supprimer ses propres identifiants personnels | Must |

### Sessions

| ID | Exigence | Priorité |
|---|---|---|
| FR-20 | Une session RDP s'ouvre dans un onglet de l'application, redimensionnable | Must |
| FR-21 | Une session SSH s'ouvre dans un onglet, terminal utilisable au clavier | Must |
| FR-22 | Plusieurs sessions simultanées, chacune dans son onglet | Must |
| FR-23 | Fermer l'onglet ferme la session et libère les ressources | Must |
| FR-24 | Une session peut être détachée dans une fenêtre indépendante | Could |
| FR-25 | Reconnexion en un clic après une coupure | Should |
| FR-26 | Options RDP par connexion : résolution, redirection presse-papiers, lecteurs, imprimantes | Should |

### Administration

| ID | Exigence | Priorité |
|---|---|---|
| FR-30 | La console d'administration permet de créer, modifier, déplacer et supprimer dossiers et connexions | Must |
| FR-31 | Elle permet de gérer les identifiants imposés et leurs groupes autorisés | Must |
| FR-32 | Elle affiche le journal d'audit avec filtres (période, utilisateur, connexion) | Must |
| FR-33 | Import depuis un CSV et depuis un export mRemoteNG (XML) | Should |
| FR-34 | Export de l'arborescence en CSV, sans aucun secret | Should |
| FR-35 | Elle n'est accessible qu'aux membres du groupe AD administrateur | Must |

### Sécurité et audit

| ID | Exigence | Priorité |
|---|---|---|
| FR-40 | L'utilisateur est identifié par sa session Windows ; aucun mot de passe applicatif | Must |
| FR-41 | L'appartenance aux groupes AD détermine l'accès à l'application et à l'administration | Must |
| FR-42 | Chaque ouverture de session est journalisée : horodatage, utilisateur, connexion, mode d'identifiants, résultat | Must |
| FR-43 | Chaque modification de connexion ou d'identifiant imposé est journalisée | Must |
| FR-44 | Aucun secret n'apparaît en clair dans les journaux, les exports ou les messages d'erreur | Must |
| FR-45 | Le journal est en écriture seule pour les utilisateurs non administrateurs | Should |

## 3. Exigences non fonctionnelles

| ID | Exigence | Mesure |
|---|---|---|
| NFR-01 | Démarrage de l'application | < 3 s sur un poste standard |
| NFR-02 | Ouverture d'une session RDP | < 10 s, saisie comprise |
| NFR-03 | Volumétrie supportée | 500 connexions, 20 utilisateurs sans dégradation |
| NFR-04 | Chiffrement des secrets | AES-256-GCM ; clés protégées par DPAPI et RSA-3072 |
| NFR-05 | Aucune primitive cryptographique implémentée à la main | revue de code |
| NFR-06 | Base concurrente | SQLite en mode WAL, transactions courtes |
| NFR-07 | Cible technique | .NET 8 LTS, Windows 10 22H2 minimum |
| NFR-08 | Langue | Interface et code commentés en français |
| NFR-09 | Portabilité des données | Migration SQLite → SQL Server possible sans refonte |
| NFR-10 | Couverture de tests | > 70 % sur la couche Core (crypto, résolution d'identifiants, accès données) |
| NFR-11 | Déploiement | MSI ou dossier autonome, poussé par Intune |
| NFR-12 | Journal applicatif | Fichier tournant local, niveau configurable |

## 4. Parcours principaux

### Ouvrir une session avec compte personnel

1. L'utilisateur lance KyWigRemote ; l'application vérifie son appartenance AD.
2. L'arborescence se charge depuis la base partagée.
3. Il double-clique sur `KERS015 - DVLS`.
4. La connexion est en mode PERSONNEL : l'application déchiffre l'identifiant de cet utilisateur.
5. Aucun identifiant enregistré → boîte de saisie avec case « retenir pour cette connexion ».
6. La session RDP s'ouvre dans un onglet. L'événement est journalisé.

### Ouvrir une session avec compte imposé

1. L'utilisateur double-clique sur `Switch coeur - Karantez`.
2. Mode IMPOSÉ : l'application vérifie que l'utilisateur appartient à un groupe autorisé.
3. Elle déverrouille la clé du secret avec sa clé privée locale, déchiffre le mot de passe en mémoire.
4. La session SSH s'ouvre. Le mot de passe n'est jamais affiché ni copiable depuis l'interface.
5. L'événement est journalisé avec la mention du compte imposé utilisé.

### Créer une connexion (administrateur)

1. Ouverture de la console d'administration (contrôle du groupe AD admin).
2. Clic droit sur le dossier `Karantez` → Nouvelle connexion.
3. Saisie : nom, protocole, hôte, port ; mode d'identifiants = IMPOSÉ, choix du compte partagé.
4. Enregistrement ; les autres postes voient la connexion au prochain rafraîchissement.

## 5. Epics

| Epic | Intitulé | Objectif |
|---|---|---|
| E1 | Socle technique | Solution .NET, projets, journalisation, configuration |
| E2 | Base de données | Schéma SQLite, migrations, couche d'accès |
| E3 | Authentification et droits | Identification AD, groupes, contrôle d'accès |
| E4 | Moteur d'identifiants | Les quatre modes, chiffrement, enrôlement des clés |
| E5 | Sessions RDP | Contrôle ActiveX embarqué, onglets |
| E6 | Sessions SSH | Terminal embarqué |
| E7 | Console d'administration | CRUD, import/export, consultation d'audit |
| E8 | Interface et thème | Docking, arborescence, thème sombre |
| E9 | Journalisation et audit | Écriture, consultation, filtres |
| E10 | Packaging et déploiement | MSI, Intune, documentation utilisateur |

Le détail des stories est dans `05-epics-stories.md`.

## 6. Définition de terminé (v1)

- Les 10 epics sont livrés, les stories Must validées.
- Un poste de test ouvre RDP et SSH dans les trois modes d'identifiants.
- Un utilisateur non administrateur ne peut pas lancer la console d'administration.
- Le journal d'audit contient les ouvertures de session avec l'utilisateur AD correct.
- La documentation utilisateur tient en deux pages.
