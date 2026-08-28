---
schemaVersion: 2
category: feature
title: 狩猎会话事件链恢复
---

## ADDED Requirements

### Requirement: Active Hunt recovery preserves a paid event reroll

An active Hunt snapshot SHALL capture and restore the optional reroll continuation of each pending event occurrence together with its existing coordinate, actor and ancestor context. Restoring that occurrence SHALL reuse the shared event transaction recovery path and SHALL NOT introduce a Hunt-specific reroll rule.

#### Scenario: Hunt is saved after reroll payment

- **WHEN** an active Hunt snapshot is captured after a pending event commits its reroll checkpoint but before final resolution
- **THEN** restoring the Hunt SHALL keep the same occurrence, actor anchor and rerolled check result
- **AND** the next event continuation SHALL not start another initial or reroll random presentation
