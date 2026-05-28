# PrediCop — Obligations Légales & Conformité RGPD

> Document à l'usage de l'éditeur (PrediCop) et des communes clientes.  
> Version 1.0 — Mai 2026

---

## 1. Cadre juridique applicable

PrediCop traite des données personnelles pour le compte des communes (Police Municipale). Les textes applicables sont :

| Texte | Portée |
|---|---|
| **RGPD** (UE 2016/679) | Règlement général — base de toute l'analyse |
| **Loi Informatique et Libertés** (modifiée 2018) | Transposition française du RGPD |
| **Code de la sécurité intérieure** | Encadre les données traitées par les PM |
| **Référentiel CNIL « Géolocalisation des véhicules »** | Spécifique au suivi GPS des agents |
| **Délibération CNIL 2019-030** | Drones et surveillance — applicable au géofencing |

---

## 2. Rôles : Responsable de traitement vs. Sous-traitant

| Acteur | Rôle RGPD | Obligations |
|---|---|---|
| **La commune** (Police Municipale) | Responsable de traitement (RT) | Définit les finalités, détient les droits d'accès, tient le registre |
| **PrediCop SaaS** (éditeur) | Sous-traitant (ST) | Traite uniquement sur instruction du RT, clause Art. 28 obligatoire |

**Action obligatoire** : signer une **Convention de Sous-traitance RGPD (Art. 28)** avec chaque commune cliente avant mise en production. Un modèle est fourni en annexe A.

---

## 3. Analyse des traitements par module

### 3.1 Gestion des appels et missions (module cœur)

| Donnée | Base légale | Durée de conservation | Niveau de risque |
|---|---|---|---|
| Identité de l'appelant (nom, téléphone) | Mission d'intérêt public (Art. 6.1.e) | 5 ans (prescription) | Modéré |
| Adresse de l'incident | Idem | 5 ans | Faible |
| Description de l'incident | Idem | 5 ans | Modéré |
| Tiers impliqués (nom, qualité) | Idem | 5 ans | Modéré |

### 3.2 Géolocalisation GPS des véhicules (module cœur)

**⚠️ Attention** : la géolocalisation en temps réel des agents est encadrée par la CNIL.

| Donnée | Base légale | Durée de conservation | Niveau de risque |
|---|---|---|---|
| Position GPS en temps réel | Mission d'intérêt public + accord agent | **30 jours maximum** (recommandation CNIL) | **Élevé** |
| Historique de trajet | Idem | 30 jours | **Élevé** |

**Obligations spécifiques GPS :**
- Information préalable des agents par note de service
- Désactivation possible hors service (agents non contraints de maintenir le GPS hors mission)
- Consultation des représentants du personnel avant déploiement
- Mention dans le registre des traitements de la commune

### 3.3 Module RH (optionnel)

| Donnée | Catégorie RGPD | Base légale | Durée | Risque |
|---|---|---|---|---|
| Planning / congés | Données ordinaires | Exécution du contrat | Durée emploi + 5 ans | Faible |
| **Groupe sanguin** | **Données de santé — Art. 9** | **Consentement explicite de l'agent** | Durée emploi | **Très élevé** |
| Contacts d'urgence | Données personnelles de tiers | Intérêts vitaux (Art. 9.2.c) | Durée emploi | Modéré |

**⚠️ Groupe sanguin — obligations strictes :**
1. La commune doit recueillir le **consentement écrit et explicite** de chaque agent concerné
2. Ce traitement doit être déclaré séparément dans le **registre des traitements**
3. Une **AIPD spécifique** pour les données de santé est recommandée par la CNIL
4. Seuls les personnels habilités (médecin de service, responsable RH) doivent y avoir accès
5. **Désactivable dans PrediCop** : paramètre `AgentBloodTypeEnabled = false` par défaut

### 3.4 Module Fourrière (optionnel)

| Donnée | Catégorie | Base légale | Durée | Risque |
|---|---|---|---|---|
| Immatriculation du véhicule | Données personnelles | Mission d'intérêt public | 5 ans | Modéré |
| Identité du propriétaire | Données personnelles | Idem | 5 ans | Modéré |
| N° pièce d'identité | Données personnelles | Idem | 5 ans | Modéré |
| Photos du véhicule | Potentiellement identifiantes | Idem | 5 ans | Modéré |

### 3.5 Module Verbalisation Électronique (optionnel)

**⚠️ Données relatives aux infractions — Art. 10 RGPD**

| Donnée | Catégorie RGPD | Base légale | Durée | Risque |
|---|---|---|---|---|
| Immatriculation (PV) | Données ordinaires | Mission d'intérêt public | 5 ans | Modéré |
| Type et montant d'infraction | **Art. 10 RGPD** — données judiciaires | Habilitation légale (CSI) | 5 ans | **Élevé** |
| Position GPS du PV | Données de localisation | Mission d'intérêt public | 5 ans | Modéré |
| Photos probatoires | Potentiellement identifiantes | Idem | 5 ans | Modéré |

**Les données relatives aux infractions (Art. 10)** ne peuvent être traitées que par :
- Les autorités publiques compétentes (✅ Police Municipale = ok)
- Sur base d'une habilitation légale nationale (✅ Code de la sécurité intérieure)

**Export ANTAI** : la transmission à l'ANTAI est encadrée — vérifier que la convention ANTAI de la commune est à jour.

### 3.6 Géofencing (configurable par tenant)

| Donnée | Catégorie | Base légale | Durée | Risque |
|---|---|---|---|---|
| Zone de patrouille assignée | Données de localisation | Mission d'intérêt public | Durée emploi | Modéré |
| Alertes de sortie de zone | Surveillance des agents | Mission + accord collectif | 30 jours | **Élevé** |

**Obligations géofencing :**
- Consultation obligatoire des représentants du personnel
- Note d'information aux agents
- Mention explicite dans le registre des traitements
- Conformité à la délibération CNIL sur la surveillance des salariés

### 3.7 Journal d'audit (module cœur)

| Donnée | Catégorie | Base légale | Durée | Risque |
|---|---|---|---|---|
| Actions utilisateurs horodatées | Données de traçabilité | Obligation légale + intérêt légitime | **1 an minimum** (recommandation ANSSI) | Faible |

---

## 4. Analyse d'Impact sur la Protection des Données (AIPD)

Une **AIPD est obligatoire** (Art. 35 RGPD) si le traitement est "susceptible d'engendrer un risque élevé". PrediCop **remplit au moins 2 des 9 critères CNIL** :

- ✅ **Surveillance systématique** (géolocalisation GPS en continu)
- ✅ **Données sensibles** (groupe sanguin si activé)
- ✅ **Personnes vulnérables** (agents en service)
- ✅ **Croisement de données** (appels + positions + historique)

**→ L'AIPD est obligatoire pour chaque commune déployant PrediCop.**

Un modèle d'AIPD pré-rempli pour PrediCop est disponible sur demande à `contact@predicop.fr`.

---

## 5. Registre des Traitements (Art. 30)

Chaque commune cliente doit enregistrer les traitements suivants dans son registre :

| N° | Traitement | Finalité | RT | ST |
|---|---|---|---|---|
| 1 | Gestion des appels PM | Traitement des appels et dispatch | Commune | PrediCop |
| 2 | Géolocalisation des patrouilles | Sécurité des agents + dispatch | Commune | PrediCop |
| 3 | Gestion des missions | Suivi des interventions | Commune | PrediCop |
| 4 | Main courante numérique | Traçabilité légale | Commune | PrediCop |
| 5 | Verbalisation électronique | Constatation des infractions | Commune | PrediCop |
| 6 | Gestion RH agents PM | Administration du personnel | Commune | PrediCop |
| 7 | Fourrière | Enlèvement et garde de véhicules | Commune | PrediCop |
| 8 | Journal d'audit | Sécurité + traçabilité | Commune | PrediCop |

---

## 6. Durées de rétention recommandées

| Catégorie de données | Durée | Base |
|---|---|---|
| Données GPS (positions) | **30 jours** | Recommandation CNIL |
| Appels et missions | **5 ans** | Prescription pénale |
| PV et infractions | **5 ans** | Prescription pénale |
| Documents officiels | **5 ans** | Prescription pénale |
| Journal d'audit | **1 an** | Recommandation ANSSI |
| Données RH (planning, congés) | **5 ans après fin de contrat** | Code du travail |
| Groupe sanguin | **Durée du contrat uniquement** | RGPD Art. 9 |
| Contacts d'urgence | **Durée du contrat** | RGPD |

PrediCop implémente une purge automatique configurable par tenant pour les données GPS.

---

## 7. Hébergement

| Module | Exigence d'hébergement |
|---|---|
| Données ordinaires (appels, missions) | Hébergeur UE ou pays adéquat |
| **Données de santé (groupe sanguin)** | **Hébergeur certifié HDS obligatoire** |
| Données de géolocalisation | Hébergeur UE |

**Recommandation** : désactiver le champ groupe sanguin (`AgentBloodTypeEnabled = false`) sauf si un hébergeur HDS est contractualisé.

---

## 8. Droits des personnes concernées

PrediCop intègre un module RGPD permettant de traiter les demandes d'exercice de droits :

| Droit | Art. RGPD | Délai de réponse | Pris en charge par PrediCop |
|---|---|---|---|
| Accès | Art. 15 | 1 mois | ✅ Via formulaire public |
| Rectification | Art. 16 | 1 mois | ✅ Via formulaire public |
| Effacement | Art. 17 | 1 mois | ✅ Via formulaire public |
| Opposition | Art. 21 | 1 mois | ✅ Via formulaire public |
| Portabilité | Art. 20 | 1 mois | ✅ Via formulaire public |

Les demandes sont transmises automatiquement à l'email DPO configuré par la commune.

---

## 9. Obligations de la commune (checklist)

Avant mise en production de PrediCop :

- [ ] Signer la **Convention de Sous-traitance RGPD** avec PrediCop (fournie par PrediCop)
- [ ] Réaliser l'**AIPD** (modèle PrediCop disponible)
- [ ] Enregistrer les traitements dans le **registre de la commune**
- [ ] Désigner ou confirmer un **DPO** et configurer son email dans PrediCop
- [ ] Prendre une **délibération du conseil municipal** actant l'usage du logiciel
- [ ] Informer les agents par **note de service** (géolocalisation, traçabilité)
- [ ] Consulter les **représentants du personnel** si géofencing activé
- [ ] Obtenir le **consentement écrit** de chaque agent pour le groupe sanguin (si activé)
- [ ] Vérifier que l'hébergeur est **HDS certifié** si le groupe sanguin est activé
- [ ] Vérifier la **convention ANTAI** si verbalisation électronique activée

---

## 10. Configuration des modules sensibles dans PrediCop

PrediCop permet de désactiver par tenant les modules et champs sensibles. Paramètres disponibles dans **Administration > Paramètres** :

### Modules (désactivés par défaut)
| Paramètre | Par défaut | Risque RGPD si activé |
|---|---|---|
| `ModuleRhEnabled` | ❌ Désactivé | Données RH + santé si BloodType |
| `ModuleFourriereEnabled` | ❌ Désactivé | Données personnelles propriétaires |
| `ModuleFleetEnabled` | ❌ Désactivé | Données kilométriques |
| `ModuleLogisticsEnabled` | ❌ Désactivé | Données dotations agents |
| `ModuleVerbalisationEnabled` | ❌ Désactivé | Art. 10 RGPD — infractions |

### Champs sensibles
| Paramètre | Par défaut | Risque RGPD |
|---|---|---|
| `AgentBloodTypeEnabled` | ❌ Désactivé | **Art. 9 — données de santé — HDS requis** |
| `AgentEmergencyContactEnabled` | ✅ Activé | Données personnelles de tiers |
| `GpsTrackingEnabled` | ✅ Activé | Surveillance des agents |
| `GeofencingEnabled` | ❌ Désactivé | Surveillance étendue des agents |
| `PhotoAttachmentsEnabled` | ✅ Activé | Photos identifiantes |

### Rétention automatique
| Paramètre | Valeur par défaut | Base |
|---|---|---|
| `GpsDataRetentionDays` | 30 jours | Recommandation CNIL |
| `AuditLogRetentionDays` | 365 jours | Recommandation ANSSI |

---

## Annexe A — Points clés de la Convention de Sous-traitance (Art. 28)

La convention PrediCop-Commune doit mentionner :
1. La description des traitements sous-traités
2. Les mesures de sécurité techniques et organisationnelles (chiffrement, contrôle d'accès, sauvegardes)
3. L'engagement de non-divulgation à des tiers
4. Le droit d'audit de la commune sur PrediCop
5. L'obligation de notifier les violations de données dans les 72h
6. La localisation des données (hébergement UE)
7. Les conditions de suppression des données en fin de contrat
8. La liste des sous-traitants ultérieurs (hébergeur, Stripe, Firebase)

---

*Document rédigé par PrediCop — contact@predicop.fr — www.predicop.fr*  
*Ce document est informatif et ne remplace pas une consultation juridique spécialisée.*
