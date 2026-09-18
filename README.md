# KyWigRemote

Gestionnaire de connexions distantes (RDP / SSH) développé en interne pour KyWig Informatique.

**Besoin fondateur :** une arborescence de connexions identique pour toute l'équipe, mais trois
modes d'identifiants au choix par connexion — personnels à chaque utilisateur, imposés par
l'administrateur, ou demandés au lancement.

---

## Pourquoi ce projet existe

Les solutions du marché ne répondent pas à la contrainte budgétaire :

| Solution | Partage | Identifiants perso | Identifiants imposés | Coût 3 utilisateurs |
|---|---|---|---|---|
| RDM Free | non | oui (local) | non | 0 |
| RDM Team + DVLS | oui | oui | oui | ~900 $/an |
| DVLS Free 2025.3.10 | oui | oui | oui | 0 mais **accès client RDM non inclus** |
| mRemoteNG | oui | non | non | 0 |
| Guacamole | oui | oui | oui | 0 (mais web, pas de client lourd) |

D'où la décision de développer un client lourd Windows, adapté au périmètre réel :
3 techniciens, 3 entités (KyWig, Kermazegan, Karantez), un AD unique `kywig.ad`.

## Organisation BMAD

Méthode BMAD (Breakthrough Method of Agile AI-Driven Development) : chaque phase produit un
artefact explicite, versionné, qui sert d'entrée à la suivante.

| Phase | Agent | Artefact | Fichier |
|---|---|---|---|
| 1. Analyse | Analyst | Project Brief | `docs/01-project-brief.md` |
| 2. Planification | PM | PRD (FR / NFR / epics) | `docs/02-prd.md` |
| 3. Architecture | Architect | Architecture + ADR | `docs/03-architecture.md` |
| 3bis. UX | Design Architect | Spécification d'interface | `docs/04-ui-spec.md` |
| 4. Découpage | PO / SM | Epics et user stories | `docs/05-epics-stories.md` |
| 5. Implémentation | Dev / QA | Code + tests | `src/` |

Suivi opérationnel : projet **KyWigRemote** dans l'ERP (projet #20), une tâche par epic.

## Arborescence cible

```
kywigremote/
├── README.md
├── docs/
│   ├── 01-project-brief.md
│   ├── 02-prd.md
│   ├── 03-architecture.md
│   ├── 04-ui-spec.md
│   └── 05-epics-stories.md
└── src/
    ├── KyWigRemote.Core/        # modèle, données, crypto, AD, protocoles
    ├── KyWigRemote.Client/      # console de connexion (utilisateurs)
    ├── KyWigRemote.Admin/       # console d'administration
    └── KyWigRemote.Tests/       # tests unitaires
```

## Démarrage

1. Lire `docs/01-project-brief.md` puis `docs/02-prd.md` — le quoi et le pourquoi.
2. Lire `docs/03-architecture.md` — les décisions techniques et leurs justifications.
3. Prendre la première story de `docs/05-epics-stories.md` (E1.1) et coder.

Une story = une journée de développement maximum. Si elle déborde, elle se découpe.

## État

| Elément | Etat |
|---|---|
| Cadrage BMAD | fait |
| Suivi ERP | projet #20 créé |
| Code | non démarré |
