# Installeur KyWigRemote

Installeur MSI unique regroupant, pour le **poste serveur** :

- le **serveur** KyWigRemote (autonome, runtime .NET embarqué) ;
- le **gestionnaire de service** (`KyWigRemote.ServerManager.exe`) qui installe et pilote le
  serveur en service Windows ;
- la **console d'administration** (`KyWigRemote.Admin.exe`) ;
- la **distribution web ClickOnce** du client, servie par le serveur lui-même.

Les postes **clients** n'installent rien manuellement : ils ouvrent une page web servie par le
serveur et cliquent sur « Installer » (ClickOnce).

> Pour installer et démarrer pas-à-pas, suivre le tutoriel **[MISE-EN-SERVICE.md](MISE-EN-SERVICE.md)**.

## Construire l'installeur

```powershell
.\tools\build-release.ps1 -Version 1.0.0.0
```

Produit `dist\release\KyWigRemote-Setup.msi`. Prérequis : SDK .NET, Build Tools 2022 (ClickOnce)
et l'outil WiX (`dotnet tool install --global wix --version 5.0.2`).

> WiX v5 est retenu volontairement : c'est la dernière version **libre** (licence MS-RL). WiX v6+
> impose l'« Open Source Maintenance Fee » (redevance pour usage commercial), écartée ici.

## Déployer (poste serveur)

1. Lancer `KyWigRemote-Setup.msi` **en administrateur**. Installe dans
   `C:\Program Files\KyWig Informatique\KyWigRemote\` et crée les raccourcis du menu Démarrer.
2. Ouvrir **« KyWigRemote — Gestion du serveur »** → **Installer** → **Démarrer**.
   Le serveur tourne alors en service Windows (sans session ouverte).
3. Ouvrir le **pare-feu** sur le port **5080/TCP** (entrant) pour que les postes clients
   atteignent le serveur et la page d'installation.

Le serveur écoute sur `0.0.0.0:5080` et sert la page ClickOnce sur `/install`
(dossier `web\`, à côté de `server\` — configuré par `Distribution:WebRoot = ..\web`).

## Installer le client (postes techniciens)

Ouvrir dans un navigateur : `http://<nom-du-serveur>:5080/install` puis cliquer
**« Installer KyWigRemote »**. Windows (ClickOnce) installe le client pour la session, avec un
raccourci au menu Démarrer. Rien d'autre à installer (runtime .NET embarqué).

## Administrer

Raccourci **« KyWigRemote — Administration »**, ou l'exécutable
`admin\KyWigRemote.Admin.exe`. Se connecter au serveur avec un compte administrateur.

## Notes

- Le **service n'est pas** installé par le MSI : il se pose via le gestionnaire (contrôle fin
  installer/démarrer/arrêter/redémarrer + journal en direct).
- En service, la base SQLite est dans le profil du compte de service. Pour la production,
  configurer un chemin fixe ou SQL Server (`appsettings.json`, section `KyWigRemote:Database`).
- Le manifeste ClickOnce n'est pas signé (éditeur « inconnu » à l'installation). Pour supprimer
  l'avertissement, signer avec un certificat de signature de code.
