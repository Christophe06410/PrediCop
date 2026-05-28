# PrediCop — Plateforme de Gestion de Police Municipale

> **La solution SaaS complète pour moderniser votre Police Municipale**

PrediCop centralise la gestion opérationnelle, le suivi terrain et le pilotage stratégique de votre service de police municipale dans une seule plateforme sécurisée, accessible depuis le bureau comme depuis le terrain.

---

## Réception & Traitement des Appels

**Gestion des appels entrants**
Saisissez rapidement les détails d'un appel entrant : identité de l'appelant, adresse de l'incident, catégorie, tiers impliqués et notes internes. Chaque appel est horodaté, référencé et traçable de la réception jusqu'à la clôture.

**Création de mission en un clic**
Depuis un appel, créez une mission d'intervention en un clic. Le système sélectionne automatiquement le véhicule disponible le plus proche grâce au dispatch intelligent.

---

## Dispatch & Gestion des Missions

**Dispatch automatique basé sur la proximité GPS**
L'algorithme de dispatch analyse en temps réel la position et la disponibilité de chaque patrouille pour proposer le véhicule le plus pertinent. Les agents reçoivent la proposition directement sur leur application mobile, même si elle est en arrière-plan.

**Notifications push en temps réel**
Les agents sont alertés instantanément des nouvelles missions via notifications push (Android & iOS) — même lorsque l'application est fermée — grâce à l'intégration Firebase Cloud Messaging.

**Suivi complet du cycle de vie**
Chaque mission passe par des statuts clairs : En attente → Proposée → Acceptée → En cours → Terminée. L'historique complet des propositions et refus est conservé, avec motif.

**Alertes manager automatiques**
Si une mission reste sans véhicule accepteur pendant plus de 15 minutes, les managers reçoivent automatiquement une alerte email.

---

## Application Mobile Agents (Android & iOS)

**Interface terrain dédiée**
L'application mobile permet aux agents de consulter leurs missions, mettre à jour leur statut, ajouter des notes de suivi, prendre des photos et vidéos directement depuis le terrain.

**Mode hors-ligne**
En zone à faible couverture réseau, l'application conserve l'accès à la mission active. Les entrées saisies hors ligne sont synchronisées automatiquement au retour du réseau — sans perte de données.

**Suivi GPS temps réel**
La position des véhicules est transmise en continu et visible sur la carte par les opérateurs BackOffice, permettant un dispatch précis et un suivi de la sécurité des agents.

---

## Carte Temps Réel & Analyse des Risques

**Cartographie en temps réel**
Visualisez simultanément la position de tous les véhicules, les zones de patrouille et le niveau de risque des rues sur une carte interactive (Leaflet.js).

**Indice de risque prédictif des rues**
Chaque rue dispose d'un score de risque calculé automatiquement chaque nuit à partir de l'historique des interventions sur 2 ans, de la densité urbaine et d'un facteur de décroissance temporelle. Un passage de patrouille fait immédiatement baisser le score.

**Zones de patrouille géographiques**
Définissez des secteurs géographiques sur la carte (polygones), assignez-les à des véhicules et importez automatiquement les rues du secteur via l'API OpenStreetMap.

**Géofencing par véhicule** *(configurable par commune)*
Recevez une alerte automatique (notification SignalR + email) lorsqu'un véhicule sort de sa zone de patrouille assignée. L'activation est paramétrable commune par commune.

**Événements de risque planifiés**
Anticipez les périodes à risque en créant des événements (marchés, fêtes, manifestations) qui augmentent automatiquement le score de risque des rues concernées sur la période définie.

---

## Documents Officiels & Main Courante

**Création de documents officiels**
Rédigez procès-verbaux, mains courantes, rapports d'intervention et autres documents directement dans la plateforme, avec un système d'entrées horodatées et de pièces jointes (photos, vidéos).

**Export PDF normalisé**
Chaque document peut être exporté en PDF au format officiel, prêt à être imprimé ou transmis au parquet ou au juge.

**Transmission automatique avec notification**
Lors du passage d'un document en statut "Transmis au Parquet" ou "Transmis au Juge", les managers reçoivent automatiquement un email de notification avec les détails du document.

**Recherche globale**
Retrouvez instantanément n'importe quel appel, mission, document ou entrée de suivi par mots-clés (adresse, nom, référence) depuis une barre de recherche unique.

---

## Rapport de Fin de Vacation

**Récapitulatif automatique de service**
À chaque fin de service, générez en un clic un rapport de vacation complet : heures de prise et fin de service, nombre de missions (acceptées, refusées, terminées), kilomètres estimés parcourus, documents produits et agents en service.

**Signature électronique & export PDF**
Le rapport peut être signé électroniquement par le chef de bord et exporté en PDF pour archivage.

---

## Dashboard Manager & Statistiques

**Tableau de bord en temps réel**
Suivez les indicateurs clés en direct : appels du jour, missions actives, véhicules disponibles, temps moyen de réponse, rues à haut risque. Un graphique horaire affiche la distribution des missions sur la journée.

**Top véhicules & performance**
Identifiez les patrouilles les plus actives avec le classement des véhicules par nombre de missions acceptées et taux d'acceptation.

**Export statistiques mensuelles**
Exportez en un clic toutes les statistiques du mois au format Excel (4 onglets : Missions, Véhicules, Appels, Documents) ou CSV, pour vos rapports d'activité et comptes-rendus au conseil municipal.

---

## Sécurité & Administration

**Authentification à deux facteurs (2FA)**
Les comptes Admin et Manager sont protégés par une authentification TOTP (Google Authenticator, Authy…) avec codes de récupération. La 2FA est activable par chaque agent depuis son profil.

**Journal d'audit complet**
Chaque action sur la plateforme (création, modification, suppression) est enregistrée avec l'identité de l'auteur, l'horodatage et les valeurs avant/après. Obligatoire RGPD et indispensable en cas de contestation juridique.

**Gestion multi-rôles**
Quatre niveaux d'accès distincts : Administrateur, Manager, Opérateur, Agent terrain — avec des droits finement contrôlés sur chaque fonctionnalité.

**Habilitations & qualifications des agents**
Suivez les habilitations de vos agents (APJA, port d'arme, permis de conduire, secourisme, habilitation préfectorale…) avec alertes automatiques avant expiration. Plus aucun agent n'est en service avec une autorisation périmée.

---

## Module RH — Gestion des Ressources Humaines *(optionnel)*

**Planning des vacations**
Planifiez les créneaux horaires de chaque agent semaine par semaine, avec affectation à un véhicule. Le planning est consultable par les agents depuis leur application mobile.

**Gestion des congés**
Chaque agent soumet ses demandes de congé directement depuis la plateforme (congés payés, RTT, maladie, récupération, formation). Les managers approuvent ou refusent en un clic, avec motif. Alertes automatiques pour les demandes en attente d'approbation.

**Profil complet de l'agent**
Centralisez les informations opérationnelles de chaque agent : groupe sanguin, contacts d'urgence (2 contacts avec lien de parenté), notes médicales. Accessible uniquement aux managers et administrateurs.

---

## Module Fourrière *(optionnel)*

**Enregistrement des enlèvements**
Saisissez chaque enlèvement de véhicule : immatriculation, marque, modèle, couleur, catégorie, motif (stationnement gênant, dangereux, épave, abandon, sans assurance…), adresse d'origine, lieu de stockage, position GPS et état constaté à l'enlèvement.

**Suivi du cycle de vie**
Chaque véhicule suit un cycle clair : En fourrière → Restitué → Détruit. La restitution enregistre l'identité du propriétaire et son numéro de pièce d'identité. L'historique complet est conservé et auditable.

**Tableau de bord fourrière**
Indicateurs en temps réel : nombre de véhicules en stock, restitués et détruits ce mois. Filtres par motif, agent verbalisateur et plaque d'immatriculation.

---

## Gestion de la Flotte *(optionnel)*

**Carnet de bord numérique**
Chaque prise et fin de service est enregistrée avec le kilométrage, le carburant ajouté et la destination. Le kilométrage total par véhicule et par mois est calculé automatiquement.

**Maintenance planifiée**
Planifiez les révisions, contrôles techniques, réparations et changements de pneumatiques. Le système envoie des alertes 30 jours avant chaque échéance. Les maintenances effectuées sont archivées avec le kilométrage, le coût et le prestataire.

**Alertes flotte**
Un tableau de bord centralisé affiche tous les véhicules avec maintenance en retard ou imminente, le nombre de trajets et les kilomètres parcourus ce mois.

---

## Module Logistique & Équipements *(optionnel)*

**Catalogue de dotations**
Créez un catalogue des équipements distribués à vos agents (gilets, tenues, chaussures, armement, matériel informatique…) avec durée de vie standard et unité de mesure.

**Remises d'équipement**
Enregistrez chaque remise d'équipement à un agent (quantité, taille, numéro de série) avec calcul automatique de la date d'expiration. Bouton "Rendu" pour clôturer la dotation à la restitution.

**Tailles uniformes**
Chaque agent dispose d'une fiche de tailles (veste, pantalon, chemise, chaussures, couvre-chef) évitant les commandes erronées et les stocks inadaptés.

**Alertes logistique**
Alertes automatiques 60 jours avant l'expiration d'un équipement pour anticiper les renouvellements.

---

## Verbalisation Électronique *(optionnel)*

**Procès-verbaux électroniques**
Les agents verbalisent directement depuis leur terminal (mobile ou tablette embarquée) : immatriculation, marque, couleur, type d'infraction, article du code de la route, montant. Chaque PV est horodaté, géolocalisé et signé électroniquement.

**Numérotation automatique**
Chaque PV reçoit un numéro séquentiel unique par commune et par année (format PV-2026-00001), garantissant la traçabilité et l'absence de doublons.

**Suivi des statuts**
Chaque PV suit un cycle de vie : Émis → Payé → En litige → Annulé. Les motifs d'annulation sont enregistrés.

**Statistiques de verbalisation**
Dashboard dédié : nombre de PV par type d'infraction, par agent, par jour de la semaine, montants totaux. Identifiez les infractions récurrentes et les agents les plus actifs.

**Export ANTAI**
Exportez vos PV au format CSV ou Excel (fichier structuré) pour transmission à l'ANTAI (Agence Nationale de Traitement Automatisé des Infractions), compatible avec les exigences réglementaires.

---

## Conformité RGPD

**Page de politique de confidentialité**
Une page dédiée et accessible publiquement expose l'ensemble des traitements de données personnelles : base légale, durées de conservation, mentions géofencing conformes aux recommandations CNIL.

**Formulaire de demande RGPD**
Toute personne peut soumettre une demande d'exercice de ses droits (accès, suppression, rectification, portabilité, opposition). La demande est automatiquement transmise au Délégué à la Protection des Données (DPO) de la commune.

**Suivi des demandes RGPD**
Les administrateurs disposent d'un tableau de bord dédié pour suivre et traiter les demandes RGPD reçues, avec historique et notes de traitement.

**Email DPO configurable par commune**
Chaque commune configure son propre email de contact DPO. Les demandes RGPD et alertes de conformité sont routées automatiquement vers le bon interlocuteur.

---

## Architecture & Déploiement

| Composant | Technologie |
|---|---|
| Back-office web | ASP.NET Core 10 Razor Pages + Bootstrap 5 |
| API REST + temps réel | ASP.NET Core 10 Web API + SignalR |
| Application mobile | .NET MAUI (Android & iOS natif) |
| Base de données | SQL Server (cloud ou on-premise) |
| Cartographie | Leaflet.js + OpenStreetMap |
| Push notifications | Firebase Cloud Messaging (Android & iOS) |
| Authentification | JWT + Cookie + 2FA TOTP |
| Export | PDF (QuestPDF) + Excel (ClosedXML) |

**Modèle SaaS multi-tenant** : chaque commune dispose de son propre espace de données isolé, de sa configuration et de ses droits d'accès. Aucune donnée n'est partagée entre communes.

---

## Pour en savoir plus

📧 contact@predicop.fr
🌐 www.predicop.fr

*PrediCop — Modernisons ensemble la Police Municipale.*
