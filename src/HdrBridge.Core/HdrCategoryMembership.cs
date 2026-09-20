using System;
using System.Collections.Generic;
using System.Linq;

namespace HdrBridge.Core
{
    public static class HdrCategoryMembership
    {
        public static CategoryMembershipChange Plan(
            IEnumerable<Guid> currentCategoryIds,
            Guid managedCategoryId,
            bool shouldHaveCategory)
        {
            if (managedCategoryId == Guid.Empty)
                throw new ArgumentException("Managed category ID cannot be empty.", nameof(managedCategoryId));

            var ids = (currentCategoryIds ?? Enumerable.Empty<Guid>())
                .Where(id => id != Guid.Empty)
                .Distinct()
                .ToList();
            var hasCategory = ids.Contains(managedCategoryId);

            if (shouldHaveCategory && !hasCategory)
            {
                ids.Add(managedCategoryId);
                return new CategoryMembershipChange
                {
                    Changed = true,
                    Added = true,
                    CategoryIds = ids,
                };
            }

            if (!shouldHaveCategory && hasCategory)
            {
                ids.RemoveAll(id => id == managedCategoryId);
                return new CategoryMembershipChange
                {
                    Changed = true,
                    Removed = true,
                    CategoryIds = ids,
                };
            }

            return new CategoryMembershipChange
            {
                CategoryIds = ids,
            };
        }
    }
}
