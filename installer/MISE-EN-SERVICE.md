# Mise en service de KyWigRemote

Tutoriel pas-à-pas pour installer et démarrer KyWigRemote sur un **poste serveur**, puis
déployer le client sur les postes des techniciens.

Durée : ~15 minutes. Aucune connaissance préalable de l'outil n'est requise.

> L'installeur et les manifestes ne sont **pas signés** pour l'instant : Windows affichera un
> avertissement « éditeur inconnu ». C'est normal, on peut continuer. (La signature par certificat
> pourra être ajoutée plus tard.)

---

## 0. Pré-requis

- Un poste **serveur** Windows (Windows 10/11 ou Windows Server 2019+), de préférence joint au
  domaine `kywig.ad`. C'est lui qui hébergera le service et la page d'installation.
- Les **droits administrateur local** sur ce poste.
- Le port **5080/TCP** libre (adaptable, voir §2).
- Le fichier **`KyWigRemote-Setup.msi`**, **construit pour le nom de ce serveur** (voir encadré).

Rien à installer au préalable : le runtime .NET est embarqué.

> **L'installeur est spécifique au serveur.** L'URL du serveur est gravée dans le client au moment
> de la construction. Construire le MSI avec le nom réel du serveur :
> ```powershell
> .\tools\build-release.ps1 -Version 1.0.0.0 -BaseUrl http://<nom-du-serveur>:5080/install
> ```
> Si les postes clients utilisent le nom court (`kers014`), graver `http://kers014:5080/install` ;
> s'ils utilisent le FQDN (`kers014.kywig.ad`), graver ce dernier. En cas de changement de serveur,
> reconstruire et réinstaller.

---

## 1. Installer

1. Copier `KyWigRemote-Setup.msi` sur le poste serveur.
2. Double-cliquer dessus (accepter l'élévation UAC ; ignorer l'avertissement « éditeur inconnu »).
3. Suivre l'assistant. L'installation se fait dans :

   ```
   C:\Program Files\KyWig Informatique\KyWigRemote\
   ├── server\   (serveur + gestionnaire de service)
   ├── admin\    (console d'administration)
   └── web\      (page d'installation ClickOnce du client)
   ```

   Deux raccourcis apparaissent au menu Démarrer : **« KyWigRemote — Gestion du serveur »** et
   **« KyWigRemote — Administration »**.

---

## 2. Configurer

Ouvrir avec un éditeur **lancé en administrateur** (le dossier Program Files est protégé) :

```
C:\Program Files\KyWig Informatique\KyWigRemote\server\appsettings.json
```

### a) Mode d'authentification

Section `KyWigRemote:Authentication:Providers`. Deux choix :

- **Active Directory (recommandé en production)** — les techniciens se connectent avec leur compte
  de domaine ; les droits découlent des groupes AD. Aucun mot de passe applicatif à gérer.

  ```json
  "Providers": [ "ActiveDirectory" ],
  "ActiveDirectory": {
    "Domain": "kywig.ad",
    "UserGroup": "GG_KyWigRemote_Users",
    "ConnectionAdminGroup": "GG_KyWigRemote_ConnectionAdmins",
    "AdminGroup": "GG_KyWigRemote_Admins"
  }
  ```

  Créer au préalable les **trois** groupes AD, correspondant aux **trois profils** :
  - `GG_KyWigRemote_Users` → **Utilisateur** (se connecte aux ressources autorisées) ;
  - `GG_KyWigRemote_ConnectionAdmins` → **Administrateur des connexions** (gère dossiers/connexions) ;
  - `GG_KyWigRemote_Admins` → **Administrateur global** (gère tout).

  Le profil découle du groupe le plus privilégié auquel appartient l'utilisateur.

  **Compte de service AD** : pour lire les groupes et **importer** les utilisateurs, le serveur a
  besoin d'un compte de service. Une fois le serveur démarré, ouvrir la console
  **« Administration »** (en administrateur global) → bouton **« Active Directory… »** : y saisir
  l'identifiant + mot de passe d'un compte de service (stocké **chiffré**), puis **« Tester la
  connexion »**. La **synchronisation** (import des membres des 3 groupes dans la liste) se lance
  via **« Utilisateurs AD… » → « Synchroniser maintenant »**, ou automatiquement si
  `ActiveDirectory:Sync:Enabled` est activé (intervalle `IntervalHours`).

- **Comptes locaux applicatifs (simple, pour tester)** — l'outil gère ses propres comptes.

  ```json
  "Providers": [ "Local" ]
  ```

### b) Emplacement de la base (important en service)

Par défaut, en service Windows, le serveur tourne sous *LocalSystem* et sa base SQLite atterrit
dans le profil système. Pour un emplacement maîtrisé et sauvegardable, fixer :

```json
"Database": {
  "Provider": "Sqlite",
  "SqlitePath": "C:\\ProgramData\\KyWigRemote\\kywigremote.db"
}
```

(Créer le dossier `C:\ProgramData\KyWigRemote` et autoriser le compte de service en écriture.)

**Sauvegarde** : `.\tools\backup-db.ps1 -DbPath 'C:\ProgramData\KyWigRemote\kywigremote.db'` copie la
base (et son journal WAL) dans un sous-dossier horodaté, avec rotation. À planifier (tâche
Windows) pour une sauvegarde régulière ; pour une cohérence maximale sur base très active, arrêter
le service le temps de la copie.

### c) Port et distribution — déjà réglés

`Server:Urls` vaut `http://0.0.0.0:5080` (écoute sur le réseau) et la section `Distribution`
(`WebRoot`, `RequestPath`) est déjà configurée pour servir la page d'installation. **Ne pas y
toucher** sauf si le port 5080 est occupé — dans ce cas, changer le nombre aux deux endroits utiles
(l'URL d'écoute et l'adresse communiquée aux techniciens).

Enregistrer le fichier.

---

## 3. Installer et démarrer le service

1. Menu Démarrer → **« KyWigRemote — Gestion du serveur »**.
2. Si la fenêtre indique « droits administrateur absents », cliquer **« Relancer en
   administrateur »** (UAC).
3. Cliquer **Installer** → le service Windows `KyWigRemoteServer` est créé (démarrage automatique).
4. Cliquer **Démarrer**.
5. Vérifier : l'état passe à **« Service démarré »** (vert) et le journal défile dans l'encadré.

Le serveur tourne désormais **sans session ouverte**, y compris après un redémarrage du poste.

---

## 4. Ouvrir le pare-feu

Dans une **PowerShell administrateur** sur le serveur :

```powershell
New-NetFirewallRule -DisplayName "KyWigRemote 5080" -Direction Inbound -Protocol TCP -LocalPort 5080 -Action Allow
```

---

## 5. Créer le premier administrateur

- **Mode Active Directory** : rien à faire. Tout membre du groupe `GG_KyWigRemote_Admins` est
  administrateur ; les autres membres de `GG_KyWigRemote_Users` sont techniciens.

- **Mode comptes locaux** : créer le tout premier compte administrateur (possible tant qu'aucun
  compte n'existe). Dans une PowerShell **sur le serveur**, en remplaçant le mot de passe :

  ```powershell
  $motDePasse = Read-Host "Mot de passe du compte admin" -AsSecureString
  $clair = [Runtime.InteropServices.Marshal]::PtrToStringAuto(
    [Runtime.InteropServices.Marshal]::SecureStringToBSTR($motDePasse))
  Invoke-RestMethod -Method Post -Uri "http://localhost:5080/api/auth/bootstrap-admin" `
    -ContentType "application/json" `
    -Body (@{ username = "admin"; password = $clair; displayName = "Administrateur" } | ConvertTo-Json)
  ```

  Une réponse contenant `username` et `isAdmin = true` confirme la création. Un message
  « Conflit / 409 » signifie qu'un compte existe déjà (amorçage déjà fait).

---

## 6. Vérifier

Depuis le serveur (ou un poste du réseau en remplaçant `localhost` par le nom du serveur) :

- **Santé** : ouvrir `http://localhost:5080/health` → doit afficher `status: ok` et la liste des
  fournisseurs d'authentification actifs.
- **Page d'installation** : ouvrir `http://localhost:5080/install` (ou `http://localhost:5080`, qui
  redirige) → la page « Installer KyWigRemote » doit s'afficher avec le logo.
- **Manifeste ClickOnce (contrôle essentiel)** : vérifier que le serveur sert un manifeste avec
  l'URL de déploiement gravée. Dans une PowerShell qui joint le serveur :

  ```powershell
  (Invoke-WebRequest "http://<serveur>:5080/install/KyWigRemote.Client.application" -UseBasicParsing).Content | Select-String "deploymentProvider"
  ```

  On doit voir `deploymentProvider codebase="http://<serveur>:5080/install/KyWigRemote.Client.application"`.
  Si rien ne s'affiche (ancien manifeste sans URL), ou si l'URL ne correspond pas au nom que les
  clients utilisent, reconstruire avec le bon `-BaseUrl` (§ « Mettre à jour ») puis redéployer
  **avant** de continuer.

---

## 7. Déployer le client sur les postes des techniciens

L'URL du serveur est **gravée** dans le client à la construction (`-BaseUrl`) : elle doit être
**joignable depuis tous les postes clients**, car c'est elle que ClickOnce utilise pour télécharger
l'application (peu importe ce que le technicien tape dans le navigateur).

Sur chaque poste technicien :

1. Ouvrir dans un navigateur `http://<nom-du-serveur>:5080/install`.
2. Cliquer **« Installer KyWigRemote »** → le navigateur télécharge `KyWigRemote.Client.application`.
3. **Ouvrir ce fichier tel quel** (double-clic depuis les téléchargements). **Ne pas le déplacer,
   copier ni renommer** avant : ClickOnce récupère les fichiers depuis le serveur via l'URL gravée.
4. Accepter (l'avertissement « éditeur inconnu » est normal, non signé). Un raccourci apparaît au
   menu Démarrer.

À la première ouverture, le client demande l'**adresse du serveur** (`http://<serveur>:5080`) puis
les identifiants (compte AD ou compte local selon le mode choisi).

---

## 8. Administrer

Sur le serveur, raccourci **« KyWigRemote — Administration »** (ou
`admin\KyWigRemote.Admin.exe`). Se connecter avec un compte administrateur. On y gère : comptes
locaux, arborescence des connexions, identifiants imposés, et journal d'audit.

---

## Dépannage

| Symptôme | Piste |
|---|---|
| Le service ne démarre pas | Consulter le journal du gestionnaire ; souvent un `SqlitePath` non accessible en écriture par le compte de service (§2b). |
| `http://localhost:5080/health` inaccessible | Le service n'est pas démarré (§3), ou le port 5080 est occupé/changé (§2c). |
| `/install` renvoie une erreur | Vérifier que le dossier `web\` existe à côté de `server\` et que `Distribution:WebRoot` vaut `..\web`. |
| Un poste client ne joint pas le serveur | Pare-feu (§4), et vérifier que `Server:Urls` est bien `0.0.0.0` et non `localhost`. |
| « Éditeur inconnu » à l'installation | Normal (non signé). Continuer, ou ajouter un certificat de signature plus tard. |
| « Les zones de sécurité ne correspondent pas » | Le MSI a été construit sans le bon `-BaseUrl` (ou avec `localhost`). Reconstruire avec `http://<nom-du-serveur>:5080/install` et réinstaller. Ne pas non plus déplacer le `.application` avant de l'ouvrir. |

---

## Mettre à jour plus tard

- **Serveur / admin / gestionnaire** : reconstruire un MSI avec un numéro de version supérieur
  **et l'URL du serveur** (`.\tools\build-release.ps1 -Version 1.0.2.0 -BaseUrl http://<serveur>:5080/install`)
  puis le réinstaller ; l'ancienne version est remplacée automatiquement (mise à jour majeure).
  Arrêter le service avant de réinstaller (fichiers verrouillés).
- **Client** : incrémenter `ApplicationRevision` dans le profil ClickOnce avant de republier, sinon
  les postes ne verront pas la nouvelle version.
