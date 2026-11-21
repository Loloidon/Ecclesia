# Ecclesia

Prototype de découverte de lieux de culte chrétiens autour de Liège : API ASP.NET Core, PostgreSQL/EF Core et interface Angular/Leaflet. Les lieux réels proviennent d'OpenStreetMap ; les informations absentes ne sont pas déduites.

## Démarrage local

Prérequis : .NET SDK 10, Node.js, npm et PostgreSQL 16 (ou Docker Compose). Avec Docker :

```sh
docker compose up -d
dotnet run --project backend/Ecclesia.Api -- --import-osm
dotnet run --project backend/Ecclesia.Api --urls http://localhost:5100
```

Dans un autre terminal :

```sh
cd frontend
npm ci
npm start
```

Ouvrir http://localhost:4200. Pour PostgreSQL existant, adapter la chaîne de connexion dans la configuration ou via la variable `ConnectionStrings__Ecclesia`. L'import crée le schéma initial ; il nécessite une connexion Internet et peut échouer si Overpass est indisponible. Il est limité à un import réussi par 24 heures et conserve les données existantes en cas d'échec. La zone initiale est un rectangle autour de Liège. Les éléments OSM supprimés ne sont pas encore retirés automatiquement.

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
