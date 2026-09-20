using HdrBridge.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;

namespace HdrBridge.Core.Tests
{
    [TestClass]
    public class HdrCategoryMembershipTests
    {
        private static readonly Guid Managed = Guid.Parse("11111111-1111-1111-1111-111111111111");
        private static readonly Guid Other = Guid.Parse("22222222-2222-2222-2222-222222222222");

        [TestMethod]
        public void HdrGame_AddsManagedCategoryWithoutTouchingOthers()
        {
            var result = HdrCategoryMembership.Plan(new[] { Other }, Managed, true);

            Assert.IsTrue(result.Changed);
            Assert.IsTrue(result.Added);
            CollectionAssert.AreEquivalent(new[] { Other, Managed }, result.CategoryIds);
        }

        [TestMethod]
        public void SdrGame_RemovesOnlyManagedCategory()
        {
            var result = HdrCategoryMembership.Plan(new[] { Other, Managed }, Managed, false);

            Assert.IsTrue(result.Changed);
            Assert.IsTrue(result.Removed);
            CollectionAssert.AreEqual(new[] { Other }, result.CategoryIds.ToArray());
        }

        [TestMethod]
        public void CorrectMembership_IsNoOp()
        {
            var hdr = HdrCategoryMembership.Plan(new[] { Managed }, Managed, true);
            var sdr = HdrCategoryMembership.Plan(new[] { Other }, Managed, false);

            Assert.IsFalse(hdr.Changed);
            Assert.IsFalse(sdr.Changed);
        }

        [TestMethod]
        public void EmptyManagedId_IsRejected()
        {
            Assert.ThrowsException<ArgumentException>(() =>
                HdrCategoryMembership.Plan(Array.Empty<Guid>(), Guid.Empty, true));
        }
    }
}
