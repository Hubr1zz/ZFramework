namespace Cards3D
{
    /// <summary>
    /// 卡牌类别标识，供 ResizableCardSlot / SlotGrid 做类型过滤。
    /// </summary>
    public enum CardCategory
    {
        Any,
        HunterAction,
        Equipment,
        BossAction,
        BossHitLocation,
        Resource,
        // ─── 营地阶段 ───
        Invention,
        Workshop,
        WorkshopRecipe,
        HunterProfile,
        Narrative,
        GatheringPoint,
        Event,
    }
}
