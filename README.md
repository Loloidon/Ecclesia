# Ecclesia

Prototype de découverte de lieux de culte chrétiens autour de Liège : API ASP.NET Core, PostgreSQL/EF Core et interface Angular/Leaflet. Les lieux réels proviennent d'OpenStreetMap ; les informations absentes ne sont pas déduites.

## Démarrage local

Sous Windows, double-cliquez sur `demarrer.bat` pour lancer l’API et Angular dans deux terminaux. Le script installe les dépendances Angular si nécessaire ; PostgreSQL doit déjà être démarré et configuré. Fermez les deux terminaux pour arrêter les serveurs.

Prérequis : .NET SDK 10, Node.js, npm et PostgreSQL 16. Pour la base locale `EcclesiaDB` et l'utilisateur `postgres`, configurez une fois le mot de passe dans les User Secrets en exécutant ceci dans PowerShell depuis la racine du dépôt :

```powershell
$password = Read-Host 'Mot de passe PostgreSQL pour postgres' -AsSecureString
$credential = [System.Management.Automation.PSCredential]::new('postgres', $password)
$escaped = $credential.GetNetworkCredential().Password.Replace('"', '""')
$connection = 'Host=localhost;Port=5432;Database=EcclesiaDB;Username=postgres;Password="' + $escaped + '"'
@{ ConnectionStrings = @{ Ecclesia = $connection } } | ConvertTo-Json -Compress | dotnet user-secrets set --project backend/Ecclesia.Api
Remove-Variable password, credential, escaped, connection
```

Le mot de passe est saisi sans être affiché et n'est pas placé dans le dépôt. Les User Secrets sont stockés localement sans chiffrement ; ils sont réservés au développement. La variable d'environnement `ConnectionStrings__Ecclesia`, si elle est définie, prend priorité sur ces secrets. Relancez ensuite `demarrer.bat`. Le profil HTTP active l'environnement Development qui charge les User Secrets.

Au démarrage, l'API crée les tables si la base est vide et vérifie le schéma avant d'accepter des requêtes. `EnsureCreated` ne met pas à jour une base existante : si le schéma est incomplet ou ancien, adaptez-le ou utilisez une nouvelle base dédiée, sans supprimer les données existantes. Les lieux ne sont remplis qu'après import.

Avec Docker Compose, pour utiliser les identifiants du conteneur à la place de la configuration locale, dans un terminal compatible shell :

```sh
docker compose up -d
export ConnectionStrings__Ecclesia='Host=localhost;Port=5432;Database=ecclesia;Username=ecclesia;Password=ecclesia'
dotnet run --project backend/Ecclesia.Api -- --import-osm
dotnet run --project backend/Ecclesia.Api --launch-profile http
```

Dans un autre terminal :

```sh
cd frontend
npm ci
npm start
```

Ouvrir http://localhost:4200 ; l'interface appelle l'API sur http://localhost:5145. Au démarrage, l'API lance l'import en arrière-plan et la carte se met à jour quand il est terminé. Un import réussi est réutilisé pendant 24 heures ; passé ce délai, le prochain démarrage lance une nouvelle mise à jour. L'import nécessite Internet et peut échouer si Overpass est indisponible : la carte affiche alors un message et conserve les données existantes. Relancer l'API permet de réessayer.

Pour lancer l'import manuellement depuis la racine, sans attendre la fin du cache :

```powershell
dotnet run --project backend/Ecclesia.Api --launch-profile http -- --import-osm --force
```

La recherche couvre le rectangle 50.55–50.70° N, 5.45–5.70° E autour de Liège, pas l'ensemble de la province. Elle importe les lieux marqués `amenity=place_of_worship` et `religion=christian` : les lieux absents d'OSM ou étiquetés autrement ne sont pas garantis. Les identifiants OSM évitent de dupliquer un même objet lors des réimports ; deux objets OSM différents représentant le même lieu peuvent subsister. Les éléments supprimés d'OSM ne sont pas retirés automatiquement. Le rayon et les filtres de la carte réduisent les résultats affichés.

Les champs OSM `opening_hours` et `service_times` sont affichés tels quels, comme non confirmés, et ne sont pas interprétés comme des célébrations vérifiées ni utilisés par les filtres horaires. L'absence d'un champ signifie « non renseigné ». Une vérification auprès de la communauté reste nécessaire. Les deux colonnes facultatives correspondantes sont ajoutées au démarrage sur une base existante ; le compte doit disposer du droit de modification de la table. Cette adaptation ne remplace pas un système de migrations.

Pour créer une entrée **fictive** distincte, lancer `dotnet run --project backend/Ecclesia.Api -- --seed-demo`, puis cocher « Données de démonstration fictives ». Son horaire est fictif et ne doit pas servir à planifier une visite.

## Vérifications

```sh
dotnet test Ecclesia.slnx
cd frontend
npm run build
```

## État et limites

Ce dépôt est un prototype, pas une application prête pour la production. Il reste notamment à construire une classification contrôlée et extensible (confession, juridiction, rite et célébration), un processus de collecte et de validation des sources officielles, et un calendrier calculant les occurrences, exceptions, annulations et fêtes. Le modèle comprend des champs pour ces éléments, mais la recherche actuelle utilise uniquement des horaires hebdomadaires et ne tient pas compte des dates ni des exceptions. Aucune célébration réelle n'est importée automatiquement. Les filtres textuels avancés exigent une correspondance exacte et ne trouvent que des données déjà renseignées. Les données de stationnement ne sont pas importées.

Avant publication : ajouter migrations EF Core, tests d'intégration avec PostgreSQL, gestion sécurisée des secrets, authentification et validation de la saisie des sources, configuration de l'URL API pour la production, observabilité, limites de débit, politique de confidentialité et conformité aux politiques d'utilisation des tuiles OSM et d'Overpass. Les coordonnées géographiques sont envoyées à l'API lors des recherches ; la géolocalisation du navigateur demande le consentement et exige HTTPS hors localhost. Les distances sont approximatives à vol d'oiseau.


## Dépannage PostgreSQL local

L’erreur PostgreSQL `28P01` signifie que le serveur a refusé les identifiants utilisés par l’API. Un serveur PostgreSQL déjà installé sur le port 5432 ne reprend pas automatiquement les identifiants du fichier Compose. Les variables `POSTGRES_USER` et `POSTGRES_PASSWORD` de Compose ne modifient pas non plus un volume PostgreSQL déjà initialisé.

Configurez l’API avec les identifiants réels d’un rôle disposant des droits nécessaires sur une base dédiée. Ne transmettez pas votre mot de passe dans le chat et ne le committez pas. Dans un terminal PowerShell, définissez la chaîne dans la session locale, puis lancez les commandes depuis le dossier `backend/Ecclesia.Api` :

```powershell
$env:ConnectionStrings__Ecclesia = "Host=localhost;Port=5432;Database=NOM_BASE;Username=NOM_UTILISATEUR;Password=MOT_DE_PASSE"
dotnet run -- --import-osm
dotnet run --launch-profile http
```

Remplacez les valeurs par celles de votre installation. La variable ne vaut que pour ce terminal ; ne conservez pas le mot de passe dans l’historique partagé. La commande d’import crée le schéma initial si la base est vide et remplit les lieux réels. Sans import, une base déjà créée mais dépourvue des tables peut produire une autre erreur. Si le compte ou la base n’existent pas, leur création nécessite les droits d’administration PostgreSQL. Ne réinitialisez pas le volume ou les comptes existants pour corriger cette erreur.

Depuis la racine du dépôt, utilisez les commandes complètes de la section « Démarrage local ». Si vous êtes déjà dans `backend/Ecclesia.Api`, utilisez les commandes ci-dessus sans `--project backend/Ecclesia.Api`.
