# Adventure Mode – Implementierungsplan

> Erstellt: 2026-05-18  
> Basis: Vergleich [Diablo Fandom Wiki – Adventure Mode](https://diablo.fandom.com/wiki/Adventure_Mode) mit aktuellem Emulator-Code )

---

## Fortschritt

| Feature | Status |
|---|---|
| 1. Bounty-Reagenzien in Horadric Cache | - [x] Erledigt |
| 2. Act-spezifische Legendaries im Cache | - [ ] Offen |
| 3. Horadric Bonus-Cache (alle 5 Akte) | - [ ] Offen |
| 4. GR-Keystone-Drop aus Nephalem-RG | - [ ] Offen |
| 5. Urshi-NPC / Legendary Gem Upgrade | - [ ] Offen |
| 6. GR-Abschluss (Closure, Exit-Portal, Mob-Removal) | - [ ] Offen |
| 7. GR Level-Progression & Skalierung | - [ ] Offen |
| 8. GR In-Game Leaderboard | - [ ] Offen |
| 9. Challenge Rifts (Logik) | - [ ] Offen |
| 10. Event-Bounties (CompleteEvent vollständig) | - [ ] Offen |
| 11. Seasonal Journey (Kapitel / Rewards) | - [ ] Offen |

---

## Detailplan

---

### 1. Bounty-Reagenzien in Horadric Cache

**Beschreibung:**  
Jeder Horadric Cache soll act-spezifische Crafting-Materialien (Bounty-Reagenzien) enthalten:
- Act 1 → `p2_ActBountyReagent_01` (Khanduran Rune)
- Act 2 → `p2_ActBountyReagent_02` (Caldeum Nightshade)
- Act 3 → `p2_ActBountyReagent_03` (Arreat War Tapestry)
- Act 4 → `p2_ActBountyReagent_04` (Corrupted Angel Flesh)
- Act 5 → `p2_ActBountyReagent_05` (Westmarch Holy Water)

**Betroffene Dateien:**
- `src/DiIiS-NA/D3-GameServer/GSSystem/PlayerSystem/Player.cs` – Zeilen 1713–1717: `case`-Zweige für `p2_ActBountyReagent_*` füllen
- `src/DiIiS-NA/D3-GameServer/GSSystem/QuestSystem/Events.cs` – nach dem Cache-Cook jeweils 2–4 Reagenzien des entsprechenden Typs generieren und ins Inventar legen

**Implementierungsschritte:**
1. In `ItemGenerator` oder `LootManager` prüfen, ob `p2_ActBountyReagent_01–05` als Item-Definitionen geladen werden können (GBID aus MPQ).
2. In `Events.cs`, nach `ItemGenerator.Cook(plr, "HoradricCacheAx")`, zusätzlich `x` Reagenz-Stacks via `ItemGenerator.Cook(plr, "p2_ActBountyReagent_0x")` erzeugen und per `plr.Inventory.PickUp(reagent)` hinzufügen (Menge: 2–8, difficulty-abhängig).
3. Die leeren `break`-Cases in `Player.cs` mit dem korrekten Pickup-Handling füllen (falls Reagenzien direkt aufgenommen werden statt aus Cache geöffnet).

---

### 2. Act-spezifische Legendaries im Horadric Cache

**Beschreibung:**  
Jeder Horadric Cache hat eine erhöhte Drop-Chance für act-exklusive Legendary Items:
- Act 1 → z. B. *Avarice Band*, *Ring of Royal Grandeur*
- Act 2 → z. B. *Cain's Set*-Teile
- etc.

**Betroffene Dateien:**
- `src/DiIiS-NA/D3-GameServer/GSSystem/ItemsSystem/ItemGenerator.cs` – neue Methode `GenerateCacheItems(Player plr, BountyData.ActT act)`
- `src/DiIiS-NA/D3-GameServer/GSSystem/QuestSystem/Events.cs` – Aufruf nach Cache-Cook

**Implementierungsschritte:**
1. In `ItemGenerator.cs` eine Dictionary-Tabelle anlegen: `ActT → List<string> exclusiveLegendaryNames`.
2. Beim Cache-Öffnen (oder beim Pickup aus Cache-Item) mit `~15 % Chance` ein act-spezifisches Legendary generieren und ins Inventar legen.
3. Basis-Loot-Tabelle aus dem Original-Spiel via MPQ-Daten ableiten (GBID-Listen).

---

### 3. Horadric Bonus-Cache (alle 5 Akte abgeschlossen)

**Beschreibung:**  
Wenn alle 5 Akte in einer Spielsitzung vollständig gebountet sind, erhält der Spieler einen zusätzlichen **Bonus-Horadric-Cache** (`BonusHoradricCache`) von Tyrael.

**Betroffene Dateien:**
- `src/DiIiS-NA/D3-GameServer/GSSystem/GameSystem/Game.cs` – neues Feld `AllActsBountied` oder Counter in `BountiesCompleted`
- `src/DiIiS-NA/D3-GameServer/GSSystem/GameSystem/QuestManager.cs` – nach dem letzten Akt-Abschluss prüfen, ob alle 5 Akte fertig sind
- `src/DiIiS-NA/D3-GameServer/GSSystem/QuestSystem/Events.cs` – neuer SideQuest-Eintrag für Bonus-Cache

**Implementierungsschritte:**
1. In `Game.cs` ein Feld `public bool AllActsBountied = false;` hinzufügen.
2. In `QuestManager.cs` nach `++BountiesCompleted[Act] == 5` prüfen, ob alle Akte `== 5` sind → dann `AllActsBountied = true` setzen und einen neuen SideQuest starten (`LaunchSideQuest(BonusCacheQuestId, true)`).
3. In `Events.cs` den neuen SideQuest registrieren, der einen `BonusHoradricCache` mit erhöhtem Loot generiert und abgibt.

---

### 4. Greater Rift Keystone Drop aus Nephalem-Rift-Guardian

**Beschreibung:**  
Beim Tod des Rift Guardians in einem **Nephalem Rift** soll immer mindestens 1 Greater Rift Keystone (`TieredRiftKey`) droppen.

**Betroffene Dateien:**
- `src/DiIiS-NA/D3-GameServer/GSSystem/PowerSystem/Payloads/DeathPayload.cs` – im RG-Death-Pfad Key droppen
- `src/DiIiS-NA/D3-GameServer/GSSystem/ItemsSystem/ItemGenerator.cs` – `Cook(world, "TieredRiftKey")` oder entsprechendes GBID

**Implementierungsschritte:**
1. In `DeathPayload.cs` den RG-Tod-Pfad (die Stelle mit `x1_lr_boss_*` SNO-Check) identifizieren.
2. Nach dem normalen Loot-Drop: `ItemGenerator.Cook(world, "TieredRiftKey")` aufrufen und im Loot-Radius fallen lassen.
3. Drop-Menge difficulty-abhängig skalieren (Normal: 1, Torment I: 1, Torment VI: 2, etc.).

---

### 5. Urshi-NPC & Legendary Gem Upgrade

**Beschreibung:**  
Nach dem Töten des Greater-Rift-Guardians erscheint **Urshi** – ein NPC, der Legendary Gems upgraden kann. Der Spieler erhält 3 Upgrade-Versuche (bei Time-Clear +1 = 4 Versuche).

**Betroffene Dateien:**
- `src/DiIiS-NA/D3-GameServer/GSSystem/ActorSystem/Implementations/` – neue Datei `Urshi.cs`
- `src/DiIiS-NA/D3-GameServer/GSSystem/PowerSystem/Payloads/DeathPayload.cs` – Urshi nach GR-RG-Tod spawnen
- `src/DiIiS-NA/D3-GameServer/GSSystem/PlayerSystem/Inventory.cs` – Gem-Upgrade-Logik (Rank erhöhen, Affix-Stärke skalieren)
- `src/DiIiS-NA/D3-GameServer/MessageSystem/GameAttribute.List.cs` – `CubeEnchantedGemRank` (Zeile 451) bereits vorhanden

**Implementierungsschritte:**
1. `Urshi.cs` als `InteractiveNPC`-Subklasse erstellen, mit `[HandledSNO(ActorSno._x1_npc_angel_common_event_urshi)]`.
2. Interaction-Handler: Zeige Liste der Legendary Gems im Inventar. Für jeden Upgrade-Versuch: `GemRank += 1`, Erfolgswahrscheinlichkeit sinkt bei höherem Rank (Formel: `100% - (Rank - GRLevel) * Multiplikator`).
3. Nach GR-RG-Tod in `DeathPayload.cs` (Pfad neben `TiredRiftTimer.Stop()`): `world.SpawnMonster(ActorSno._x1_npc_angel_common_event_urshi, position)` aufrufen.
4. Upgrade-Anzahl anhand Zeit-Clear (Timer > 0 = 4 Versuche, Timer abgelaufen = 3 Versuche) tracken.
5. Legendary Gems als eigenen Item-Typ markieren (Flag `IsLegendaryGem`) und Rank-Attribut persistieren.

---

### 6. Greater Rift Abschluss (Closure, Exit-Portal, Mob-Removal)

**Beschreibung:**  
*(Bereits in Roadmap der `agent.md` als aktive Baustelle eingetragen)*  
Nach dem Tod des GR-Guardians sollen:
- Alle verbleibenden Monster despawnen
- Ein Exit-Portal erscheinen (zurück in die Stadt)
- Die Abschluss-UI (`DungeonFinderClosingMessage`) korrekt befüllt werden
- Der Death-Penalty für Spieler-Tod im GR (Zeitverlust) greifen

**Betroffene Dateien:**
- `src/DiIiS-NA/D3-GameServer/GSSystem/PowerSystem/Payloads/DeathPayload.cs` – GR-Guardian-Tod-Logik (ca. Zeile 900–920)
- `src/DiIiS-NA/D3-GameServer/MessageSystem/Message/Definitions/Artisan/DungeonFinderClosingMessage.cs` – `RiftLevel` befüllen
- `src/DiIiS-NA/D3-GameServer/GSSystem/MapSystem/World.cs` – Methode zum Despawnen aller Monster im Rift
- `src/DiIiS-NA/D3-GameServer/GSSystem/ActorSystem/Portal.cs` – Exit-Portal spawnen

**Implementierungsschritte:**
1. In `DeathPayload.cs` nach `TiredRiftTimer.Stop()`: Alle Monster in `nephalem`-World per `world.GetActorsByType(ActorType.Monster)` destroyen.
2. Exit-Portal in der Mitte der aktuellen Szene spawnen (`ActorSno._x1_openworld_tiered_rifts_portal` oder entsprechendes Exit-SNO).
3. `DungeonFinderClosingMessage` mit `RiftLevel = Game.CurrentGreaterRiftLevel`, `IsFinishedInTime = (TiredRiftTimer != null)` senden.
4. Death-Penalty: Beim Spielertod im Rift (DeathPayload) prüfen ob `Game.WorldOfPortalNephalem != NONE` → `TiredRiftTimer.TimeoutTick -= deathPenaltyTicks` (5 Sekunden pro Tod).

---

### 7. Greater Rift Level-Progression & Skalierung

**Beschreibung:**  
GR-Level steigt nach erfolgreichem Abschluss. Inhalt skaliert mit Level: Monster-HP und -Schaden steigen.

**Betroffene Dateien:**
- `src/DiIiS-NA/D3-GameServer/GSSystem/GameSystem/Game.cs` – neues Feld `public int CurrentGreaterRiftLevel = 1;`
- `src/DiIiS-NA/D3-GameServer/GSSystem/ActorSystem/Portal.cs` – GR-Level beim Öffnen übergeben
- `src/DiIiS-NA/D3-GameServer/GSSystem/AISystem/` – Skalierungsformeln für Monster-HP/Dmg
- `src/DiIiS-NA/D3-GameServer/MessageSystem/Message/Definitions/Player/PlayerIntValMessage.cs` – `HighestHeroSoloRiftLevelMessage` senden

**Implementierungsschritte:**
1. In `Game.cs` `CurrentGreaterRiftLevel` hinzufügen; beim GR-Start via Portal aus dem verwendeten Keystone laden.
2. Nach erfolgreichem GR-Abschluss (Zeit nicht abgelaufen): `CurrentGreaterRiftLevel++`, `player.Toon.HighestSoloRiftLevel` ggf. aktualisieren, `HighestHeroSoloRiftLevelMessage` senden.
3. Monster-Health-Multiplier = `baseHP * (1.17 ^ GRLevel)` (Original-Formel).
4. Entsprechende `HealthMultiplier`/`DamageMultiplier` in `GameModsConfig.cs` oder direkt in der Spawn-Logik setzen.

---

### 8. Greater Rift In-Game Leaderboard

**Beschreibung:**  
Zeigt den besten GR-Abschluss je Klasse/Spieler im Spiel (Solo/4-Spieler).

**Betroffene Dateien:**
- `src/DiIiS-NA/Core/Storage/AccountDataBase/Entities/DBGameAccount.cs` – Feld `HighestSoloRiftLevel`
- `src/DiIiS-NA/REST/Manager/ServerStatsManager.cs` – neue Methode `GetRiftLeaderboard()`
- `src/DiIiS-NA/D3-GameServer/GSSystem/GameSystem/Game.cs` – nach GR-Abschluss in DB persistieren

**Implementierungsschritte:**
1. In `DBGameAccount` / `DBToon` ein Feld `HighestSoloRiftLevel` (int) hinzufügen und in der FluentNHibernate-Mapper-Klasse mappen.
2. In `ServerStatsManager.cs` analog zu `GetKillsLeaderboard()` eine `GetRiftLeaderboard()`-Methode ergänzen.
3. Nach GR-Abschluss in `DeathPayload.cs` oder `Game.cs`: `toon.HighestSoloRiftLevel = Math.Max(toon.HighestSoloRiftLevel, currentLevel)` persistieren.
4. REST-Endpoint in `REST/`-Controller verknüpfen.

---

### 9. Challenge Rifts (Logik)

**Beschreibung:**  
Challenge Rifts sind wöchentlich rotierende Runs mit einem vorgegebenen Charakter-Build. Abschluss belohnt Kanai's Cube-Materialien.

**Betroffene Dateien:**
- `src/DiIiS-NA/D3-GameServer/GSSystem/ActorSystem/Implementations/ChallengeObelisk.cs` – Interaktions-Logik
- `src/DiIiS-NA/D3-GameServer/GSSystem/GameSystem/Game.cs` – `IsChallengeRift`-Flag
- `src/DiIiS-NA/Core/Storage/` – Wöchentliche Challenge-Daten (Build-Snapshot)

**Implementierungsschritte:**
1. `ChallengeObelisk.cs` mit einer `OnInteract(Player)`-Methode vervollständigen, die eine Challenge-Rift-Session startet.
2. Challenge-Build als DB-Snapshot speichern (wöchentlich rotierend, manuell konfigurierbar über `config.mods.json`).
3. `In_Tiered_Challenge_Rift`-Attribut (Attribut-ID 1464) bei Spielern setzen, die sich im Challenge-Rift befinden.
4. Belohnungs-Logik: Bei Abschluss `Eligible_For_Weekly_Challenge_Reward`-Attribut setzen und Kanai-Würfel-Materialien droppen.

---

### 10. Event-Bounties (CompleteEvent vollständig)

**Beschreibung:**  
Bounties vom Typ `CompleteEvent` sind aktuell nur teilweise mit dem CursedChest-System verbunden. Cursed Shrines und andere Event-Trigger fehlen.

**Betroffene Dateien:**
- `src/DiIiS-NA/D3-GameServer/GSSystem/QuestSystem/Events.cs` – Event-Trigger und Fortschritts-Callbacks
- `src/DiIiS-NA/D3-GameServer/GSSystem/QuestSystem/QuestManager.cs` – `CompleteEvent`-Bounty-Abschluss-Logik
- `src/DiIiS-NA/D3-GameServer/GSSystem/ActorSystem/Implementations/CursedChest.cs` – Bounty-Callback beim Aktivieren

**Implementierungsschritte:**
1. In `QuestManager.cs` eine Methode `OnEventCompleted(World world)` ergänzen, die aktive `CompleteEvent`-Bounties in dieser Welt abschließt.
2. `CursedChest.cs` am Ende des Events `QuestManager.OnEventCompleted(World)` aufrufen.
3. Analoges Callback für Cursed Shrines (`CursedShrine`-Implementierung prüfen oder erstellen).
4. Sicherstellen, dass der `LevelArea`-Match der Bounty mit der Event-Welt übereinstimmt.

---

### 11. Seasonal Journey (Kapitel & Rewards)

**Beschreibung:**  
Die Seasonal Journey besteht aus mehreren Kapiteln mit je mehreren Objectives. Beim Abschluss eines Kapitels erhält der Spieler **Haedrig's Gift** (Set-Item-Caches).

**Betroffene Dateien:**
- `src/DiIiS-NA/D3-GameServer/GSSystem/PlayerSystem/Player.cs` – Journey-Fortschritt tracken, Criteria-Grants
- `src/DiIiS-NA/D3-GameServer/GSSystem/ItemsSystem/ItemGenerator.cs` – `HaedrigsGift`-Cache generieren
- `src/DiIiS-NA/Core/Storage/AccountDataBase/Entities/DBToon.cs` – Felder für Journey-Fortschritt
- neues File `src/DiIiS-NA/D3-GameServer/GSSystem/QuestSystem/SeasonalJourney.cs`

**Implementierungsschritte:**
1. `SeasonalJourney.cs` erstellen: Definiere Kapitel (I–IV) mit je einer Liste von Criteria-IDs als Objectives.
2. In `Player.GrantCriteria()` nach jedem Grant prüfen, ob alle Objectives eines Kapitels erfüllt sind.
3. Bei Kapitel-Abschluss: Haedrig's Gift (Kapitel II, III, IV) als Cache-Item spawnen und an den Spieler übergeben.
4. Journey-Fortschritt in `DBToon` persistieren (bitmask oder separate Tabelle).
5. Saisonspezifische Haedrig-Sets per `config.mods.json` konfigurierbar machen.

---

## Implementierungs-Reihenfolge (Empfehlung)

```
Phase 1 – Loot-Korrekturen (niedrige Komplexität, hoher Spielwert)
  → #1 Bounty-Reagenzien
  → #2 Act-spezifische Legendaries
  → #3 Bonus-Cache (alle 5 Akte)

Phase 2 – Greater Rift Kernmechanik
  → #6 GR-Abschluss (Closure / Exit-Portal / Mob-Removal)   ← bereits in Arbeit
  → #4 GR-Keystone-Drop
  → #7 GR Level-Progression
  → #5 Urshi / Gem Upgrade

Phase 3 – Erweiterte Systeme
  → #10 Event-Bounties (CompleteEvent)
  → #8 GR Leaderboard
  → #11 Seasonal Journey
  → #9 Challenge Rifts
```

---

## Hinweise für Entwickler

- **Tests:** Derzeit keine automatisierten Unit-Tests vorhanden. Änderungen bitte mit einer lokalen D3-Client-Session (Version 2.7.4.84161) testen.
- **Datenbankmigrationen:** Neue Felder in `DBToon`/`DBGameAccount` erfordern SQL-Migrationsskripte unter `db/`.
- **Konfiguration:** Neue Tuning-Parameter (Drop-Chancen, Gem-Upgrade-Wahrscheinlichkeit etc.) immer in `config.mods.json` auslagern und in `docs/game-world-settings.md` dokumentieren.
