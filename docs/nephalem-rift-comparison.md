# Nephalem Rift – Wiki vs. C#-Code Vergleich

> **Quelle:** [diablo.fandom.com/wiki/Nephalem_Rift](https://diablo.fandom.com/wiki/Nephalem_Rift)  
> **Analysierte Code-Dateien:**
> - `src/DiIiS-NA/D3-GameServer/GSSystem/PowerSystem/Payloads/DeathPayload.cs`
> - `src/DiIiS-NA/D3-GameServer/GSSystem/QuestSystem/OpenWorld.cs`
> - `src/DiIiS-NA/D3-GameServer/GSSystem/ActorSystem/Implementations/NephalemStone.cs`
> - `src/DiIiS-NA/D3-GameServer/GSSystem/ActorSystem/Implementations/Artisans/Nephalem.cs`
> - `src/DiIiS-NA/D3-GameServer/GSSystem/ActorSystem/Portal.cs`
> - `src/DiIiS-NA/D3-GameServer/GSSystem/GeneratorsSystem/WorldGenerator.cs`
> - `src/DiIiS-NA/D3-GameServer/GameModsConfig.cs` / `GameServerConfig.cs`
> - `src/DiIiS-NA/D3-GameServer/GSSystem/GameSystem/Game.cs`

---

## 1. Zugang / Aktivierung

### Wiki
- Nur im **Adventure Mode** verfügbar.
- Zugang über den **Nephalem Obelisk** in jedem Stadtbereich (Town).
- **Keine Kosten**, kein Key erforderlich (seit Patch 2.3).
- Spieler klickt den Obelisk an, bestätigt den Eingang und betritt den Rift-Portal.

### Code (aktuell)
- Der Nephalem Obelisk (`x1_OpenWorld_LootRunObelisk_B`) ist als `NephalemStone`-Klasse implementiert.
- Bei `OnTargeted` wird die Nachricht `RiftStartEncounterMessage` gesendet – ein UI-Dialog öffnet sich.
- Kein Key-Abzug beim Betreten eines **normalen** Rifts vorhanden.
- Für **Greater Rifts** wird `BigPortalKey` (= Greater Rift Keystone) benötigt; die Logik existiert (Inventory-Feld `BigPortalKey`), der Abzug beim Betreten ist jedoch **nicht implementiert** – der Spieler kann Greater Rifts ohne Keystone betreten.

### ❌ Unterschied / Fehlend
| Punkt | Status |
|---|---|
| Normaler Rift: kostenlos | ✅ korrekt |
| Greater Rift benötigt Keystone | ⚠️ Key wird bei Abschluss vergeben, aber **nicht beim Betreten abgezogen** |
| UI-Dialog zur Rift-Auswahl (Typ/Level) | ⚠️ Nur rudimentär – `RiftStartAcceptedMessage.Field1` (bool); GR-Level-Auswahl ist möglicherweise nicht vollständig |

---

## 2. Welt-Generierung / Layout

### Wiki
- Zufällig generiertes Layout aus verschiedenen Tilesets (alle 5 Akte / Zonen gemischt).
- Mehrere Ebenen möglich (2 Floors).
- Monster aus allen Akten / Zonen mischen sich zufällig.
- Es gibt spezielle "Rift-only" Tileset-Kombinationen (z. B. Hex Maze).

### Code (aktuell)
- `WorldGenerator.cs` generiert Rift-Welten mit DRLG (zufällig, mehrere Versuche bei zu kleinen Layouts).
- Zwei Welt-Slots vorhanden: `WorldOfPortalNephalem` (1. Floor) und `WorldOfPortalNephalemSec` (2. Floor).
- Tileset `x1_lr_tileset_hexmaze` (u. a.) ist referenziert (Z. 401 WorldGenerator).
- Level-Area-IDs `288482` (1. Floor) und `288684` (2. Floor) sind korrekt zugewiesen.
- Der 2. Floor wird beim Portal-Durchgang korrekt aufgelöst (Portal.cs Z. 1068–1092).

### ⚠️ Unterschied / Fehlend
| Punkt | Status |
|---|---|
| Randomisiertes Tileset-Mixing | ✅ vorhanden |
| 2 Floors / Ebenen | ✅ vorhanden |
| Rift-only Monster-Pool | ⚠️ `RiftOnly`-Tag wird in `ActorFactory` geprüft, ob Rift-Only-Monster korrekt gefiltert werden ist nicht vollständig sichergestellt |
| Zu kleines Layout → Retry | ✅ vorhanden |

---

## 3. Fortschritt-Leiste (Progress Bar)

### Wiki
- Progress-Leiste füllt sich durch das Töten von Monstern.
- Stärkere/Elitemons­ter geben mehr Fortschritt.
- Elite-Packs spawnen **Progress Orbs** beim Tod (lila Kugeln), die aufgenommen werden und Extra-Fortschritt geben.
- Bei 100% spawnt der **Rift Guardian** (Rift-Boss).

### Code (aktuell)
- `ActiveNephalemProgress` (float) wird in `DeathPayload.cs` bei Monsterkill erhöht.
- Formel: `NephalemRiftProgressMultiplier * (Target.Quality + 1)`.
- Fortschritt wird per `DungeonFinderProgressMessage` an alle Spieler gesendet.
- Progress-Schwelle liegt bei `> 650` (nicht 100%).
- **Orbs** werden gespawnt (`p1_tiered_rifts_Orb` / `p1_normal_rifts_Orb`) **unter Bedingungen**:
  - Immer, wenn `Target.Quality > 1` (Elite-Pack).
  - Mit konfigurierbarer Wahrscheinlichkeit `NephalemRiftOrbsChance` für normale Monster.
- Auto-Finish-Funktion: Wenn weniger als `AutoFinishThreshold` (Standard: 2) lebende Monster → Progress auf 651 setzen.

### ❌ Unterschied / Fehlend
| Punkt | Status |
|---|---|
| Progress durch Monsterkill | ✅ implementiert |
| Elite-Monster geben mehr Fortschritt (Quality-Multiplikator) | ✅ implementiert |
| Progress Orbs für Elite-Packs | ✅ implementiert (bei Quality > 1) |
| Progress Orbs für normale Monster (optionale Chance) | ⚠️ Nur per Konfiguration – Standard ist 0% (deaktiviert); wiki beschreibt nur Elite-Orbs |
| Orbs werden aufgenommen und geben Fortschritt | ❌ **Orbs werden gespawnt, aber der Aufnahme-Mechanismus (Pickup → Progress) fehlt**. Die Orbs sind reine visuelle Indikatoren ohne Pickup-Logik |
| Progress-Schwelle 100% → Boss-Spawn | ⚠️ Intern > 650 statt normierter 100%-Balken; Anzeige im Client sollte aber korrekt normiert werden |

---

## 4. Rift Guardian (Boss-Spawn)

### Wiki
- Bei 100% Fortschritt spawnt ein zufälliger **Rift Guardian**.
- Ein einzigartiger Boss, der stärker ist als normale Bosse.
- Zu den möglichen Guardians gehören viele verschiedene Monster-Varianten.
- Guardian kann in der Nähe des letzten Monsters oder an einer fixen Spawn-Position erscheinen.

### Code (aktuell)
- `Player.SpawnNephalemBoss()` wählt aus `ActorSnoExtensions.NephalemPortalBosses.PickRandom()`.
- Boss bekommt `Is_Loot_Run_Boss = true` und `Bounty_Objective = true`.
- Beim normalen Rift: Boss wird über `OpenWorld.cs` (Quest 382695, Step 3) gespawnt – fixe Logik: immer `_x1_lr_boss_mistressofpain` (Mistress of Pain), keine Randomisierung!
- Beim Greater Rift: `SpawnNephalemBoss` mit zufälligem Boss aus dem Pool.
- Position: Für normalen Rift aus Scene-Position + Zufalls-Offset berechnet, für Greater Rift: Boss-Position = Position des Guardians.

### ❌ Unterschied / Fehlend
| Punkt | Status |
|---|---|
| Zufälliger Guardian-Typ | ⚠️ **Normaler Rift spawnt immer `mistressofpain`** – kein zufälliger Pool; Greater Rift hat Random-Pool |
| Guardian spawnt bei 100% | ✅ vorhanden (nach Progress > 650) |
| Guardian bei letztem Monster / in Nähe | ✅ (Position-Logik vorhanden) |
| Wetter-Effekt bei Guardian-Spawn | ✅ `SnoWeatherOverride = 362462` |

---

## 5. Belohnungen – Normaler Rift

### Wiki
- Guardian dropped: **Loot** (Legendaries/Sets, Craftmats, Edelsteine, Pläne), **Gold**, **Blood Shards** (für Kadala-Gambling).
- **Greater Rift Keystone** hat eine Chance zu droppen (wird nach Patch 2.3 aus dem Guardian-Pool garantiert).
- Normale Monster im Rift droppen normal Loot (Items, Gold, Mats).

### Code (aktuell)
- **Guardian-Kill (normaler Rift)**:
  - Blood Shards: 10–30 (zufällig) → `SpawnBloodShards`.
  - Gold: 3× `SpawnGold` aufgerufen.
  - **Greater Rift Keystone** (`BigPortalKey`): Anzahl via `GetGreaterRiftKeystoneRewardAmount(difficulty)`:
    - Difficulty ≤ 6 → 1 Keystone.
    - Pro 5 Difficultystufen über 6 → +1 Keystone.
  - Spieler bekommt Whisper mit Belohnungszusammenfassung.
  - Orek (NPC im Hub) bekommt Post-Rift-Dialog zugewiesen.
  - **Legendaries/Set-Items/Craftmats** werden **nicht explizit** durch den Guardian-Kill gespawnt – die normalen Loot-Tabellen greifen durch die übliche Item-Drop-Logik.

### ❌ Unterschied / Fehlend
| Punkt | Status |
|---|---|
| Blood Shards vom Guardian | ✅ 10–30 Shards |
| Gold vom Guardian | ✅ 3× Gold-Spawn |
| Greater Rift Keystone vom Guardian | ✅ 1–N Keystones je nach Difficulty |
| Loot (Items) vom Guardian | ⚠️ Keine explizite Keystone-garantierte Drop-Tabelle für Guardian; Loot-Drop läuft über allgemeine Drop-Logik |
| Death's Breath als spezieller Crafting-Mat | ⚠️ Nicht explizit für Rift-Guardian implementiert |
| Normale Monster droppen Loot | ✅ (allgemeine Loot-Logik) |

---

## 6. Belohnungen – Greater Rift

### Wiki
- **Kein Loot von normalen Monstern** innerhalb des Greater Rifts.
- **Nur der Rift Guardian droppt Loot**.
- Guardian dropped: Legendaries, Craftmats, Gold, Blood Shards (skaliert mit GR-Level), Legendary Gems.
- **Urshi** spawnt nach Guardian-Kill: ermöglicht Legendary Gem-Upgrades.
- Upgrade-Versuche: 3 Basis + 1 bei 0 Toden + 1 bei Empowered Rift (max. 5).
- GR-Timer: 15 Minuten; Tod = 5 Sekunden Zeitstrafe.
- Bei Abschluss in der Zeit → GR-Level + 1 für nächsten Run.
- Abschluss nach Zeit-Ablauf → kein Level-Aufstieg, aber immer noch Belohnungen.
- **Empowered Rift**: kostet Gold (250k+ skalierend) → +1 Upgrade-Versuch.

### Code (aktuell)
- **GR-Timer**: 900 Sekunden = 15 Minuten ✅ (`TiredRiftTimer = new SecondsTickTimer(game, 900.0f)`).
- **Tod-Strafe**: 5 Sekunden Timer-Abzug ✅ (`GreaterRiftDeathPenaltySeconds = 5`).
- **Closing-Tick**: `GreaterRiftClosingTick = 26396` (wird nach Guardian-Kill gesendet).
- **GR-Level-Aufstieg**: Bei Abschluss in der Zeit → `CurrentGreaterRiftLevel++` ✅.
- **Highest Solo Rift Level** wird gespeichert ✅.
- **Urshi-Spawn (Gem-Upgrades)**:
  - `Jewel_Upgrades_Max = 3` (Basis).
  - +1 bei 0 Toden (`Tiered_Loot_Run_Death_Count == 0`) ✅.
  - +1 bei `NephalemBuff` ✅ (aber: `NephalemBuff` ist kein "Empowered Rift", s. u.).
  - `JewelUpgrade` / `JewelUpgradeResultsMessage` implementiert ✅.
- **Kein Loot von normalen Monstern im GR**: ⚠️ Es gibt keine explizite Unterdrückung von Monster-Drops im GR; die normale Loot-Logik würde greifen.
- **Empowered Rift** (Gold-Kosten für +1 Upgrade): ❌ **Nicht implementiert**. `NephalemBuff` ist ein anderer Mechanismus (vermutlich Nephalem Valor-Buff), nicht das Empowered Rift-Feature.
- **Nur Guardian droppt Loot in GR**: ❌ **Nicht implementiert** – normale Monster in GR folgen der gleichen Loot-Logik wie überall sonst.
- **Monster werden nach Guardian-Kill entfernt**: ✅ `ClearGreaterRiftMonsters()` wird bei `PlayerIndex == 0` aufgerufen.

### ❌ Unterschied / Fehlend
| Punkt | Status |
|---|---|
| 15-Minuten-Timer | ✅ |
| 5 Sekunden Tod-Strafe | ✅ |
| GR-Level-Aufstieg bei Abschluss in der Zeit | ✅ |
| Urshi spawnt nach Guardian | ✅ (`P1_LR_TieredRift_Nephalem`) |
| 3 Basis-Upgrade-Versuche | ✅ |
| +1 Versuch bei 0 Toden | ✅ |
| +1 Versuch bei Empowered Rift | ❌ **Nicht implementiert** (`NephalemBuff` ≠ Empowered Rift) |
| Kein Monster-Loot innerhalb GR | ❌ **Nicht implementiert** |
| Monster nach Guardian-Kill entfernen | ✅ |
| Guardian spawnt Exit-Portal nach Kill | ✅ (`x1_openworld_lootrunportal` bei Position des Guardians) |

---

## 7. Greater Rift Keystone – Verbrauch beim Betreten

### Wiki
- Keystone wird beim Betreten des Greater Rifts **verbraucht** (1 Keystone pro Run).
- Keystone trägt keine Level-Information mehr (seit Patch 2.4); GR-Level wird am Obelisk gewählt.

### Code (aktuell)
- Beim Betreten über den Obelisk wird **kein Keystone verbraucht**.
- `BigPortalKey` wird nur beim Guardian-Kill des normalen Rifts **erhöht**, aber nie abgezogen.

### ❌ Unterschied / Fehlend
| Punkt | Status |
|---|---|
| Keystone beim GR-Betreten abziehen | ❌ **Nicht implementiert** |
| GR-Level am Obelisk wählen | ⚠️ Auswahl-UI existiert rudimentär, aber Level-Binding unklar |

---

## 8. Kadala (Blood Shard Gambling)

### Wiki
- Kadala ist im Stadtbereich verfügbar und nimmt Blood Shards entgegen.
- Blood Shards kommen hauptsächlich aus Rift-Guardians.

### Code (aktuell)
- `Kadala.cs` als `Vendor` implementiert; `GetBloodShardsAmount()` / `RemoveBloodShardsAmount()` vorhanden.
- Blood Shards als separate Währung funktioniert.

### ✅ Kein wesentlicher Unterschied

---

## 9. Zeitlimit und Schließen des Rifts

### Wiki
- Rift schließt sich **nach Abschluss** (Guardian-Kill), dann wird Exit-Portal gespawnt.
- Spieler haben keine feste Zeit für normalen Rift.
- Greater Rift hat 15-Minuten-Limit; danach kann der Rift theoretisch noch abgeschlossen werden, aber kein Level-Aufstieg.

### Code (aktuell)
- **Normaler Rift**: Kein Schließ-Mechanismus nach Guardian-Kill vorhanden – Rift bleibt offen.
- **Greater Rift**: `DungeonFinderClosingMessage` wird nach Guardian-Kill gesendet, aber der Closing-Tick `26396` ist ein **fixer Wert** (nicht dynamisch berechnet). Die Welt wird nicht aktiv geschlossen/entfernt.
- Exit-Portal (zurück in die Stadt) wird bei GR nach Guardian-Kill gespawnt ✅.
- Für **normalen Rift** wird kein Exit-Portal explizit gespawnt; nur Orek bekommt Dialog.

### ❌ Unterschied / Fehlend
| Punkt | Status |
|---|---|
| GR: Exit-Portal nach Guardian-Kill | ✅ |
| Normaler Rift: kein harter Zeitlimit | ✅ |
| Normaler Rift: Rift schließt sich nach Guardian-Kill | ❌ **Nicht implementiert** |
| GR: Welt wird nach Closing-Tick wirklich entfernt | ❌ **Nicht implementiert** (nur Client-Nachricht) |

---

## 10. Death's Breath und Crafting Materials

### Wiki
- **Death's Breath** ist ein exklusives Crafting-Material das von Elites / Guardians ab Torment-Difficulty droppt.
- Rift-Guardian droppt Death's Breath.

### Code (aktuell)
- Death's Breath (`DeathsBreath`) ist als Item definiert.
- Kein explizites Spawnen von Death's Breath beim Rift-Guardian.
- Drop läuft über die allgemeine Elite-Drop-Logik.

### ⚠️ Unterschied / Fehlend
| Punkt | Status |
|---|---|
| Death's Breath von Elites ab Torment | ✅ (über allgemeinen Elite-Drop) |
| Death's Breath garantiert vom Guardian | ⚠️ Nicht explizit sichergestellt |

---

## 11. Konfigurierbarkeit (Server-spezifisch, nicht in Wiki)

Der C#-Code bietet server-seitige Konfigurationsoptionen, die das offizielle Spiel nicht hat:

| Config-Key | Beschreibung |
|---|---|
| `NephalemRift.ProgressMultiplier` | Fortschritt-Multiplikator pro Monsterkill |
| `NephalemRift.AutoFinish` | Rift automatisch beenden wenn ≤ N Monster leben |
| `NephalemRift.AutoFinishThreshold` | Schwellwert für AutoFinish (Standard: 2) |
| `NephalemRift.OrbsChance` | Zusätzliche Orb-Spawn-Chance für normale Monster |

---

## Zusammenfassung der Änderungen, die gemacht werden müssen

### Priorität HOCH (spielbrechende Abweichungen)

1. **❌ Keystone wird nicht beim GR-Betreten verbraucht**  
   → `BigPortalKey` bei Spieler-Eintritt in Greater Rift um 1 reduzieren. Wenn kein Key vorhanden → Eintritt verweigern.
   - Relevante Dateien: `Portal.cs` (OnTargeted für GR-Portal), `NephalemStone.cs` (Obelisk-Logik)

2. **❌ Kein Loot von normalen Monstern in Greater Rifts**  
   → In `DeathPayload.cs` prüfen ob `game.NephalemGreater == true` und falls ja, Item-Drop-Generierung überspringen.
   - Relevante Datei: `DeathPayload.cs` (Loot-Abschnitt ~Zeile 1055ff)

3. **❌ Normaler Rift spawnt immer denselben Boss (Mistress of Pain)**  
   → In `OpenWorld.cs` Quest 382695, Step 3: Zufälligen Boss aus dem gleichen Pool wie `SpawnNephalemBoss()` wählen, statt hardcoded `_x1_lr_boss_mistressofpain`.
   - Relevante Datei: `OpenWorld.cs`

4. **❌ Progress Orbs haben keine Pickup-Logik (geben keinen Fortschritt)**  
   → Orb-Items (`p1_normal_rifts_Orb`) brauchen eine `OnPickup`-Implementierung, die `ActiveNephalemProgress` erhöht.
   - Relevante Dateien: Item-Pickup-Logik, Item-Implementierung für Orbs

### Priorität MITTEL (Gameplay-Abweichungen)

5. **⚠️ Empowered Rift nicht implementiert**  
   → Empowered Rift sollte am Obelisk gegen Gold wählbar sein, und bei Abschluss +1 Upgrade-Versuch geben. Aktuell ist `NephalemBuff` ein anderer Mechanismus.
   - Neue Logik notwendig in `NephalemStone.cs` und `DeathPayload.cs`

6. **⚠️ Normaler Rift schließt sich nicht nach Guardian-Kill**  
   → Nach Guardian-Kill im normalen Rift: Portal schließen / deaktivieren, Exit-Portal spawnen (analog zu GR).

7. **⚠️ GR-Welt wird nach Closing-Tick nicht wirklich entfernt**  
   → Welt sollte nach `DungeonFinderClosingMessage` + Wartezeit tatsächlich aus dem Spiel-State entfernt werden.

8. **⚠️ Death's Breath ist nicht explizit für Rift-Guardian gesichert**  
   → Guardian sollte bei Torment+ garantiert Death's Breath droppen.

### Priorität NIEDRIG (Minor Abweichungen)

9. **⚠️ GR-Level-Auswahl am Obelisk unklar**  
   → UI-Dialog zur GR-Level-Auswahl verifizieren; sicherstellen dass der gewählte Level korrekt in `CurrentGreaterRiftLevel` gesetzt wird.

10. **⚠️ Rift-only Monster-Pool (RiftOnly-Tag)**  
    → Verifizieren ob `RiftOnly`-getaggte Monster ausschließlich in Rift-Welten spawnen und normale Monster die Rift-Typen nicht mischen.

---

## Datei-Index (relevante C#-Dateien)

| Datei | Funktion |
|---|---|
| `GSSystem/PowerSystem/Payloads/DeathPayload.cs` | Fortschritt, Boss-Kill-Belohnungen, Urshi, Tod-Strafe |
| `GSSystem/QuestSystem/OpenWorld.cs` | Normaler Rift Quest-Flow, Boss-Spawn (hardcoded) |
| `GSSystem/ActorSystem/Implementations/NephalemStone.cs` | Nephalem Obelisk – Zugang |
| `GSSystem/ActorSystem/Implementations/Artisans/Nephalem.cs` | Urshi NPC (Gem-Upgrades nach GR) |
| `GSSystem/ActorSystem/Portal.cs` | Portal-Navigation, Rift-Eintritt, Exit-Logik |
| `GSSystem/GeneratorsSystem/WorldGenerator.cs` | Rift-Welt-Generierung, Tileset-Mixing |
| `GSSystem/GameSystem/Game.cs` | Spielzustand: Timer, Level, Flags |
| `GameModsConfig.cs` / `GameServerConfig.cs` | Server-Konfiguration für Rift-Parameter |
| `GSSystem/ActorSystem/Implementations/Kadala.cs` | Blood Shard Gambling |
| `GSSystem/MapSystem/World.cs` | Blood Shard + Gold Spawn-Hilfsmethoden |
