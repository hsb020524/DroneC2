using DroneC2.DataSource;
using DroneC2.UI;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace DroneC2.Tests
{
    public class UnitDetailStatusTests
    {
        [Test]
        public void Apply_WritesTextAndClass_WhenLabelHasPlaceholder()
        {
            var label = new Label("-");

            bool wrote = UnitDetailPresenter.ApplyStatusTo(label, UnitStatus.Normal);

            Assert.IsTrue(wrote);
            Assert.AreEqual("정상", label.text);
            Assert.IsTrue(label.ClassListContains("status-normal"));
            Assert.IsFalse(label.ClassListContains("status-lost"));
        }

        [Test]
        public void Apply_SameStatusTwice_DoesNotRewrite()
        {
            var label = new Label("-");
            UnitDetailPresenter.ApplyStatusTo(label, UnitStatus.Normal);

            Assert.IsFalse(UnitDetailPresenter.ApplyStatusTo(label, UnitStatus.Normal));
        }

        [Test]
        public void Apply_RecoversWhenLabelWasResetExternally()
        {
            // 같은 상태를 이미 적용한 뒤 UXML 재적용 등으로 라벨이 기본값 "-"로 되돌아간 경우.
            var label = new Label("-");
            UnitDetailPresenter.ApplyStatusTo(label, UnitStatus.Normal);
            label.text = "-";

            bool wrote = UnitDetailPresenter.ApplyStatusTo(label, UnitStatus.Normal);

            Assert.IsTrue(wrote);
            Assert.AreEqual("정상", label.text);
        }

        [Test]
        public void Apply_StatusChange_SwitchesClasses()
        {
            var label = new Label("-");
            UnitDetailPresenter.ApplyStatusTo(label, UnitStatus.Normal);

            UnitDetailPresenter.ApplyStatusTo(label, UnitStatus.Lost);

            Assert.AreEqual("두절", label.text);
            Assert.IsTrue(label.ClassListContains("status-lost"));
            Assert.IsFalse(label.ClassListContains("status-normal"));
            Assert.IsFalse(label.ClassListContains("status-warning"));
        }
    }
}
