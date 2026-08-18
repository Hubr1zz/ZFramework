using System;
using System.Collections.Generic;
using HuntingInDarkness.Data;
using UnityEngine;

namespace HuntingInDarkness.Settlement
{
    [Serializable]
    public sealed class PlayableWorkshopCost
    {
        [SerializeField] private ItemData item;
        [SerializeField, Min(1)] private int amount = 1;

        public ItemData Item => item;
        public int Amount => Mathf.Max(1, amount);
    }

    [Serializable]
    public sealed class PlayableWorkshopDefinition
    {
        [SerializeField] private string workshopId;
        [SerializeField] private string displayName;
        [SerializeField, TextArea] private string description;
        [SerializeField] private InventionData requiredInvention;
        [SerializeField] private List<PlayableWorkshopCost> costs = new();

        public string WorkshopId => workshopId;
        public string DisplayName => displayName;
        public string Description => description;
        public InventionData RequiredInvention => requiredInvention;
        public IReadOnlyList<PlayableWorkshopCost> Costs
        {
            get
            {
                if (costs == null) return Array.Empty<PlayableWorkshopCost>();
                return costs;
            }
        }
    }

    [CreateAssetMenu(fileName = "PlayableWorkshopCatalog", menuName = "Hunting in Darkness/Workshop Catalog")]
    public sealed class PlayableWorkshopCatalog : ScriptableObject
    {
        [SerializeField] private List<PlayableWorkshopDefinition> workshops = new();

        public IReadOnlyList<PlayableWorkshopDefinition> Workshops
        {
            get
            {
                if (workshops == null) return Array.Empty<PlayableWorkshopDefinition>();
                return workshops;
            }
        }
    }
}
