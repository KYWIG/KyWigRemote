# 04 — Spécification d'interface (Design Architect)

**Référence visuelle :** mRemoteNG, thème sombre.
**Principe directeur :** une interface d'outil d'administration système — dense, sobre, au
service de la tâche. Pas de grandes zones vides, pas de dégradés, pas de coins très arrondis,
pas d'emojis, pas de cartes flottantes. Un technicien doit y retrouver ses repères en trois
secondes parce qu'elle ressemble à ce qu'il utilise déjà.

---

## 1. Disposition — console de connexion

```
┌──────────────────────────────────────────────────────────────────────┐
│ Fichier  Affichage  Outils  Aide                          barre menu │
├──────────────────────────────────────────────────────────────────────┤
│ [Nouvelle] [Connecter] [Déconnecter] │ [Plein écran] │  barre outils  │
├───────────────────┬──────────────────────────────────────────────────┤
│ Connexions     ⌕  │  ┌ KERS015 ─ x ┬ SW-KAR2 ─ x ┬ + ┐               │
│                   │  │                                               │
│ ▾ KyWig           │  │                                               │
│   ▾ Serveurs      │  │        zone de session (RDP / SSH)            │
│     ▪ KERS014     │  │                                               │
│     ▪ KERS015     │  │                                               │
│   ▾ Réseau        │  │                                               │
│ ▾ Kermazegan      │  │                                               │
│ ▾ Karantez        │  │                                               │
│   ▪ SW-KAR2       │  │                                               │
├───────────────────┤  │                                               │
│ Propriétés        │  │                                               │
│ Hôte  10.50.1.15  │  │                                               │
│ Proto RDP:3389    │  │                                               │
│ Ident. Personnel  │  │                                               │
├───────────────────┴──────────────────────────────────────────────────┤
│ Connecté : n.marchand  │  3 sessions ouvertes  │  base : kers015     │
└──────────────────────────────────────────────────────────────────────┘
```

Panneaux dockables (DockPanel Suite), position mémorisée par utilisateur dans
`%LOCALAPPDATA%\KyWigRemote\layout.config`.

- **Connexions** (gauche, haut) : arbre + champ de recherche filtrant.
- **Propriétés** (gauche, bas) : lecture seule dans le client, éditable dans l'admin.
- **Sessions** (centre) : onglets, un par session ouverte.
- **Barre d'état** : utilisateur AD, nombre de sessions, source de données.

## 2. Palette (thème sombre)

Reprise de la palette « Visual Studio Dark » dont s'inspire le thème sombre de mRemoteNG.
À vérifier à l'oeil sur le poste, ces valeurs sont un point de départ.

| Usage | Couleur |
|---|---|
| Fond principal | `#2D2D30` |
| Fond des panneaux | `#252526` |
| Fond des champs de saisie | `#333337` |
| Bordures / séparateurs | `#3F3F46` |
| Texte principal | `#F1F1F1` |
| Texte secondaire | `#9B9B9B` |
| Accent / sélection | `#007ACC` |
| Ligne sélectionnée (arbre) | `#094771` |
| Survol | `#3E3E42` |
| Succès (connecté) | `#4EC9B0` |
| Alerte | `#CE9178` |
| Erreur | `#F44747` |

Un thème clair reprenant les couleurs système peut être ajouté plus tard ; il n'est pas prioritaire.

## 3. Typographie

| Élément | Police |
|---|---|
| Interface | Segoe UI 9 pt |
| Titres de panneaux | Segoe UI Semibold 9 pt |
| Terminal SSH | Consolas 10 pt |
| Journal d'audit | Consolas 9 pt |

Pas de police personnalisée : ce qui est présent sur tout poste Windows.

## 4. Icônes

16×16 px dans l'arbre et les barres d'outils, 32×32 pour les boîtes de dialogue.
Source recommandée : **Fugue Icons** (CC-BY 3.0) ou **Material Symbols** (Apache 2.0) — attribution
à porter dans l'écran À propos.

Codes visuels de l'arbre :

| État | Indice visuel |
|---|---|
| Dossier | icône dossier |
| Connexion RDP | icône écran |
| Connexion SSH | icône console |
| Session ouverte | pastille verte sur l'icône |
| Identifiants imposés | petit cadenas en surimpression |
| Accès refusé | icône grisée, infobulle explicative |

## 5. Comportements

| Interaction | Résultat |
|---|---|
| Double-clic sur une connexion | Ouvre la session dans un nouvel onglet |
| Entrée sur une connexion sélectionnée | Idem |
| Clic droit sur une connexion | Connecter / Connecter dans une nouvelle fenêtre / Propriétés / Copier l'hôte |
| Frappe dans la recherche | Filtre l'arbre à chaque touche, dossiers vides masqués |
| Ctrl+F | Focus sur la recherche |
| Ctrl+W | Ferme l'onglet actif |
| F5 | Recharge l'arborescence depuis la base |
| F11 | Session en plein écran |
| Glisser-déposer dans l'arbre | Déplace (console d'administration uniquement) |

## 6. Boîte de saisie d'identifiants

Affichée en mode PERSONNEL sans identifiant enregistré, et en mode DEMANDE.

```
┌─ Identifiants — KERS015 ──────────────────┐
│                                           │
│ Utilisateur  [ kywig\n.marchand_t1     ]  │
│ Mot de passe [ ••••••••••••            ]  │
│                                           │
│ [x] Retenir pour cette connexion          │
│ [ ] Retenir pour toutes mes connexions    │
│                                           │
│ Chiffré pour votre compte Windows.        │
│ Personne d'autre ne peut le lire.         │
│                                           │
│             [ Connecter ]  [ Annuler ]    │
└───────────────────────────────────────────┘
```

Le champ utilisateur est prérempli avec le compte Windows courant. La phrase d'explication est
délibérément présente : elle répond à la question que tout technicien se pose avant d'enregistrer un
mot de passe quelque part.

En mode DEMANDE, les cases à cocher sont absentes et un libellé indique que rien ne sera conservé.

## 7. Console d'administration

Même disposition, avec en plus :

- édition en place dans le panneau Propriétés ;
- onglet **Identifiants imposés** : liste, groupes autorisés, bouton « Distribuer les clés » (crée les enveloppes des utilisateurs manquants) ;
- onglet **Audit** : grille filtrable par période, utilisateur, connexion, résultat ; export CSV ;
- onglet **Utilisateurs** : comptes enrôlés, date d'enrôlement, révocation.

Aucun écran ne doit permettre d'afficher un secret imposé en clair — pas même à l'administrateur.
S'il doit connaître le mot de passe, il le connaît déjà puisque c'est lui qui l'a saisi.

## 8. Messages

Ton factuel, en français, sans excuse ni exclamation.

| Situation | Message |
|---|---|
| Groupe AD manquant | « Accès refusé. Votre compte n'appartient pas au groupe GG_KyWigRemote_Users. » |
| Base injoignable | « Base de données inaccessible : \\kers015\KyWigRemote$. Vérifiez le VPN ou contactez l'administrateur. » |
| Clé d'enveloppe absente | « Ce compte imposé n'a pas encore été distribué à votre utilisateur. Demandez la distribution des clés à l'administrateur. » |
| Échec RDP | « Connexion refusée par KERS015 (code 0x...). » suivi du libellé Windows |
| PuTTY introuvable | « PuTTY est introuvable à l'emplacement configuré. Installez-le ou corrigez appsettings.json. » |

## 9. Point de licence

mRemoteNG est distribué sous **GPL-2.0**. On s'en inspire visuellement, on ne copie pas son code —
sans quoi KyWigRemote deviendrait lui-même GPL, avec obligation de fourniture du source.

Une apparence et une disposition ne sont pas protégées de la même manière qu'un code source, mais
la frontière se tient : regarder l'application pour s'en inspirer, oui ; ouvrir son dépôt pour en
recopier des fichiers, non.

Les bibliothèques qu'il utilise restent libres d'emploi séparément — DockPanel Suite est sous
licence MIT.
