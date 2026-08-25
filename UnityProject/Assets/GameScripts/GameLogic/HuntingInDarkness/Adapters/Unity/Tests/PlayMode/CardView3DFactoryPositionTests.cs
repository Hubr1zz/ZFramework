using System.Collections;
using Cards3D;
using HuntingInDarkness.Data;
using HuntingInDarkness.GameCore.Hunters;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HuntingInDarkness.Adapter.PlayModeTests
{
    public sealed class CardView3DFactoryPositionTests
    {
        [UnityTest]
        public IEnumerator Factories_PlaceDirectPanelCardsAtRequestedLocalPositions()
        {
            var root = new GameObject("CardFactoryPositionRoot");
            HunterData template = ScriptableObject.CreateInstance<HunterData>();
            template.hunterName = "候选猎人";
            var hunter = new HunterInstance(template, 1) { Name = "余烬" };
            Vector3 recruitmentPosition = new(-1.4f, 0.03f, 0.45f);
            Vector3 recoveryPosition = new(0.8f, 0.03f, -0.05f);
            Vector3 launcherPosition = new(-3.25f, 0.03f, -2.65f);

            RecruitmentTemplateCard3D recruitment = RecruitmentTemplateCard3D.Create(template, root.transform, recruitmentPosition);
            HunterRecoveryCard3D recovery = HunterRecoveryCard3D.Create(hunter, HunterBodyPart.Torso, root.transform, recoveryPosition);
            RecruitmentLauncherCard3D launcher = RecruitmentLauncherCard3D.Create(root.transform, launcherPosition);

            Assert.That(recruitment.transform.localPosition, Is.EqualTo(recruitmentPosition));
            Assert.That(recovery.transform.localPosition, Is.EqualTo(recoveryPosition));
            Assert.That(launcher.transform.localPosition, Is.EqualTo(launcherPosition));

            Object.Destroy(root);
            Object.Destroy(template);
            yield return null;
        }
    }
}
