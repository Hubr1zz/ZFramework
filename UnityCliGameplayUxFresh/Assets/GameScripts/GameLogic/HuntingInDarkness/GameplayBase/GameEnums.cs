namespace GameplayBase
{
    /// <summary>游戏大阶段（三段游戏循环）</summary>
    public enum GamePhase
    {
        Settlement, // 营地建设阶段
        Hunt,       // 狩猎阶段
        BossFight   // Boss决战阶段
    }

    /// <summary>回合阶段</summary>
    public enum TurnPhase
    {
        PlayerTurn,
        BossTurn,
        Transition // 回合切换的过渡阶段（结算溢出时点等）
    }

    /// <summary>卡牌朝向</summary>
    public enum CardFace
    {
        FaceUp,   // 正面 — 主效果可用
        FaceDown  // 背面 — 通常无效果，需恢复
    }

    /// <summary>角色行动状态</summary>
    public enum CharacterActionState
    {
        Idle,       // 等待玩家选择
        Acting,     // 正在行动中
        Exhausted,  // 本次行动使时点超过上限
        Overtime,   // 结转债务超过新上限，需要援助
        Done        // 主动结束行动
    }

    /// <summary>翻面触发时机</summary>
    public enum FlipTriggerTiming
    {
        OnPlay,              // 打出时翻面
        OnTurnEnd,           // 回合结束时自动翻/恢复
        OnOtherCardFlipped,  // 其他卡牌翻面时
        OnOtherCardRestored, // 其他卡牌恢复时
        OnOtherCardDiscarded,// 其他卡牌被弃置时
        OnPayCost,           // 支付费用时
        OnConditionMet,      // 自定义条件满足
    }

    /// <summary>卡牌效果目标类型（预留棋盘扩展）</summary>
    public enum TargetType
    {
        Self,
        SingleAlly,
        SingleEnemy,
        AllAllies,
        AllEnemies,
        BoardTile,   // 预留：棋盘格子
        Area,        // 预留：范围
    }
}
