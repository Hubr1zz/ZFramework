---
schemaVersion: 2
category: feature
title: "狩猎事件资源暂存与回营提交"
---

# Hunt Event Resource Staging Specification

## Purpose

保证狩猎事件获得或失去的资源属于当前狩猎小队的携带物，只有正式回营被 Campaign 接受后才进入营地库存、狩猎记录和后续制造循环。

## Requirements

### Requirement: Phase runners own event resource destinations
The shared event node SHALL accept an optional phase resource command. Settlement execution SHALL retain its existing inventory behavior, while Hunt execution SHALL inject a command that targets active-hunter collectibles.

#### Scenario: The same AddResource effect runs in Hunt
- **WHEN** the Hunt runner commits the event node
- **THEN** the resource is added to a current living hunter's collectibles
- **AND** Settlement inventory remains unchanged

### Requirement: Hunt resource mutations fail closed
The Hunt resource command SHALL accept only registered Resource items, positive amounts, and active Hunt actors. Removal SHALL validate the squad-wide carried total before mutating any collectible, and additions SHALL reject integer overflow.

#### Scenario: An event removes more than the squad carries
- **WHEN** the effect is committed
- **THEN** no hunter collectible is partially removed
- **AND** Settlement inventory is never used to cover the shortage

### Requirement: Retreat is the only inventory transfer boundary
Hunt event rewards SHALL join harvest collectibles and SHALL transfer through the existing prepared Hunt exit only after Campaign accepts the Hunt-to-Settlement transition.

#### Scenario: A stacked event reward returns to camp
- **WHEN** the Hunt record is prepared and the accepted exit commits
- **THEN** every carried unit is represented in the Hunt record
- **AND** the exact stack count is added to Settlement before collectibles are cleared

### Requirement: Carried-resource facts remain phase scoped
Hunt collectible changes SHALL publish a phase-scoped event-resource fact and SHALL NOT publish the Settlement `ResourceChangedEvent` before transfer.

#### Scenario: A Hunt event grants two resources
- **WHEN** the effect commits
- **THEN** observers receive one `HuntCollectibles`-scoped fact with old and new squad totals
- **AND** inactive Settlement resource views are not told that Settlement inventory changed
