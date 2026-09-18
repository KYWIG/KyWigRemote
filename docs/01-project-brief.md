# 01 — Project Brief (Analyst)

**Projet :** KyWigRemote
**Auteur :** Nolhan Marchand — KyWig Informatique
**Version :** 1.0 — 18/09/2026

---

## 1. Problème

L'équipe IT gère trois entités (KyWig Informatique, Kermazegan, Karantez) avec un annuaire
Active Directory unique `kywig.ad`. Les accès distants aux serveurs, hyperviseurs et équipements
réseau se font aujourd'hui sans outil centralisé : chacun maintient ses propres raccourcis RDP et
ses propres sessions PuTTY, et les identifiants des comptes de service circulent hors de tout
coffre.

Conséquences :

- pas de référentiel commun des connexions — un nouveau serveur n'est connu que de celui qui l'a monté ;
- pas de traçabilité sur l'usage des comptes à privilèges ;
- les mots de passe partagés se transmettent par des canaux non maîtrisés ;
- le départ d'un membre de l'équipe (dont l'alternant) emporte une partie de la connaissance.

## 2. Objectif

Un client Windows unique qui affiche une arborescence de connexions partagée, et qui résout les
identifiants selon une règle définie **par connexion** :

1. **Personnel** — chaque technicien se connecte avec son propre compte ; l'identifiant est stocké chiffré pour lui seul.
2. **Imposé** — l'administrateur fixe le compte à utiliser ; l'utilisateur ouvre la session sans jamais afficher le mot de passe.
3. **À la demande** — rien n'est stocké, saisie au lancement.

## 3. Périmètre

### Dans le périmètre (v1)

- Protocoles RDP et SSH.
- Arborescence de dossiers et de connexions partagée par toute l'équipe.
- Les trois modes d'identifiants ci-dessus, avec héritage au niveau dossier.
- Authentification des utilisateurs par Active Directory ; droits par groupe AD.
- Deux exécutables : console de connexion (tous) et console d'administration (admins).
- Journal des ouvertures de session et des modifications.
- Interface sombre en onglets, inspirée de mRemoteNG.

### Hors périmètre (v1)

- VNC, Telnet, HTTP, ICA, AnyDesk — envisageables en v2.
- Enregistrement vidéo des sessions.
- Client macOS ou Linux.
- Rotation automatique des mots de passe.
- Accès depuis l'extérieur sans VPN.
- Multi-tenant : une base, une équipe.

## 4. Utilisateurs

| Persona | Qui | Besoin | Fréquence |
|---|---|---|---|
| Technicien | 2-3 personnes du service IT | Ouvrir vite la bonne session sans chercher l'IP ni le mot de passe | plusieurs fois par jour |
| Administrateur | Nolhan | Créer les connexions, fixer les comptes imposés, gérer les droits | hebdomadaire |
| Auditeur | Direction / audit externe | Savoir qui a utilisé quel compte à privilèges et quand | ponctuel |

## 5. Contraintes

| Contrainte | Détail |
|---|---|
| Budget | 0 € de licence |
| Parc | 100 % Windows 10/11, postes joints au domaine |
| Annuaire | AD `kywig.ad`, groupes de sécurité déjà en place |
| Base | SQLite en v1 — pas de serveur à administrer |
| Déploiement | Intune (parc déjà géré) ou partage réseau |
| Compétence | Un seul développeur, en alternance ; le code doit rester lisible et repris par un tiers |
| Réseau | Usage interne uniquement, LAN et VPN |

## 6. Risques

| Risque | Impact | Probabilité | Mitigation |
|---|---|---|---|
| Erreur de conception cryptographique | Critique | Moyenne | Aucune primitive écrite à la main : DPAPI + RSA + AES-GCM de .NET, schéma documenté en §3 de l'architecture |
| Un secret imposé reste extractible côté client | Moyen | **Certaine** | Limite structurelle assumée (voir ADR-006) ; la protection vise l'usage courant, pas un collègue déterminé |
| Corruption de la base SQLite (accès concurrent sur partage) | Élevé | Moyenne | Mode WAL, écriture courte, sauvegarde quotidienne, migration vers SQL Server prévue |
| Projet abandonné au départ de l'alternant | Élevé | Moyenne | Documentation BMAD complète, code commenté en français, dépôt Git interne |
| Dérive fonctionnelle (on veut refaire RDM) | Moyen | Élevée | Périmètre v1 gelé ; toute idée nouvelle part dans un backlog v2 |

## 7. Critères de succès

- Les 3 techniciens utilisent l'outil quotidiennement au lieu de leurs raccourcis personnels.
- Aucun mot de passe de compte à privilèges ne circule plus par mail ou messagerie.
- Ouvrir une session RDP depuis le lancement de l'application prend moins de 10 secondes.
- L'audit permet de répondre à « qui s'est connecté à tel serveur le mois dernier ».

## 8. Alternatives écartées

| Alternative | Motif du rejet |
|---|---|
| RDM Free | Mono-utilisateur, aucune source de données partagée |
| RDM Team + SQL/DVLS | ~900 $/an, hors budget |
| DVLS gratuit 2025.3.10 | Le mode gratuit à 10 utilisateurs ne couvre pas l'accès depuis le client RDM (erreur « Invalid license (Product: RDM) ») |
| mRemoteNG | Pas de coffre personnel, pas de permissions, fonction SQL marquée expérimentale |
| RDM + MySQL | Source de données supprimée du produit depuis la 2022.1.11 |
| Guacamole | Répond au besoin mais impose un accès web ; choix écarté au profit d'un client lourd |
| Teleport CE | Trop lourd à exploiter pour 3 utilisateurs |
