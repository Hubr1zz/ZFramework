using System.Collections.Generic;
using System.Reflection;
using Cards3D;
using HuntingInDarkness.Data;
using HuntingInDarkness.ViewLayer.Tabletop;
using NUnit.Framework;
using UnityEngine;

namespace HuntingInDarkness.Adapter.PlayModeTests
{
    public sealed class TabletopHuntDeparturePanel3DTests
    {
        [Test]
        public void PresentSquad_RestoresStagedHunterOrderInsteadOfRosterOrder()
        {
            var root = new GameObject("DeparturePanelTestRoot");
            try
            {
                HunterInstance first = CreateHunter(101, "甲");
                HunterInstance second = CreateHunter(102, "乙");
                HunterInstance third = CreateHunter(103, "丙");
                TabletopHuntDeparturePanel3D panel = TabletopHuntDeparturePanel3D.Create(root.transform);

                panel.PresentSquad(Vector3.zero, new[] { first, second, third }, new[] { third.InstanceId, first.InstanceId }, _ => { }, () => { });

                SlotGrid squadGrid = (SlotGrid)typeof(TabletopHuntDeparturePanel3D).GetField("squadGrid", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(panel);
                var stagedIds = new List<int>();
                foreach (CardSlot slot in squadGrid.Slots)
                    if (slot.OccupantCard is HuntDepartureHunterCard3D card)
                        stagedIds.Add(card.Hunter.InstanceId);
                Assert.That(stagedIds, Is.EqualTo(new[] { third.InstanceId, first.InstanceId }));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static HunterInstance CreateHunter(int instanceId, string hunterName) => new(null, instanceId) { Name = hunterName };
    }
}
