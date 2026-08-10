using SO.Boss.HitLocation;
using HuntingInDarkness.GameCore.Hunters;

namespace GameplayBase.CombatSystem
{
    /// <summary>攻击流程完成（无论成功还是被中断）</summary>
    public struct AttackCompletedEvent
    {
        public int  AttackerId;
        public int  DefenderId;
        public bool AttackerIsBoss;
        public bool Completed;       // false = 被中断
        public string AbortReason;
    }

    /// <summary>角色受伤</summary>
    public struct CharacterWoundedEvent
    {
        public int CharacterId;
        public HunterBodyPart BodyPart;
        public int IncomingDamage;
        public int ArmorPrevented;
        public int HealthLost;
        public int RemainingHealth;
        public bool FatalInjuryTriggered;
        public int TemporaryWoundsAdded;
        public int PermanentWoundsAdded;
        public int TotalTemporaryWounds;
        public int TotalPermanentWounds;
    }

    /// <summary>角色抽到死亡牌后永久死亡；装备处置由上层角色/战役模块负责。</summary>
    public struct CharacterDiedEvent
    {
        public int CharacterId;
    }

    // ─── 受击部位卡事件 ──────────────────────────────────────────────────

    /// <summary>Boss受击部位卡开始洗牌动画</summary>
    public struct HitLocationShuffleStartedEvent { }

    /// <summary>受击部位卡翻至正面（被抽中）</summary>
    public struct HitLocationFlippedFaceUpEvent
    {
        public HitLocationCardData CardData;
    }

    /// <summary>受击部位卡翻回背面（结算后未摧毁）</summary>
    public struct HitLocationFlippedFaceDownEvent
    {
        public HitLocationCardData CardData;
    }

    /// <summary>受击部位卡血量归零，本局永久正面朝上</summary>
    public struct HitLocationDestroyedEvent
    {
        public HitLocationCardData CardData;
        public string PartName;
    }

    // ─── 行动卡范围预览（鼠标悬浮）──────────────────────────────────────

    /// <summary>悬浮某张角色行动卡，请求高亮其目标/范围格。</summary>
    public struct CardHoverPreviewEvent
    {
        public int CardInstanceId;
    }

    /// <summary>移开行动卡，清除范围高亮。</summary>
    public struct CardHoverPreviewEndEvent { }

    // ─── 战利品事件 ──────────────────────────────────────────────────────

    /// <summary>
    /// 受击部位效果触发掉落资源。
    /// 由 DropResourceEffect.Execute 发布，BossController 监听并累积。
    /// </summary>
    public struct ResourceDroppedEvent
    {
        public string ResourceName;
        public int    Count;
        public string SourceHitLocationName;
    }
}
