---
schemaVersion: 2
category: feature
title: 桌游式随机交互
---

## ADDED Requirements

### Requirement: A paid reroll resumes as the same authoritative check

An event occurrence with a committed reroll checkpoint SHALL resume from the stored option, actor, rerolled value and frozen bonus. The resumed transaction SHALL be marked as already rerolled, SHALL NOT request another initial random presentation, and SHALL NOT allow another reroll payment for that occurrence.

#### Scenario: Player continues after paying for a reroll

- **WHEN** a saved Settlement or active Hunt occurrence contains a valid paid reroll checkpoint
- **THEN** the 3D event check SHALL present the stored rerolled outcome without selecting the option or rolling again
- **AND** accepting it SHALL commit the final event result exactly once without spending more Willpower or gaining more Fate

#### Scenario: A restored actor still has additional Willpower

- **WHEN** an actor paid one reroll before saving and still has enough Willpower to pay again
- **THEN** the restored occurrence SHALL NOT offer or accept a second reroll
- **AND** the actor's remaining Willpower and Fate SHALL remain unchanged until another distinct gameplay transaction changes them
