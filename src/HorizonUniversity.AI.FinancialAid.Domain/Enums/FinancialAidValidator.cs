using HorizonUniversity.AI.FinancialAid.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace HorizonUniversity.AI.FinancialAid.Domain.Enums
{
    public static class FinancialAidValidator
    {
        public static IReadOnlyList<string> Validate(FinancialAidDraft draft)
        {
            var errors = new List<string>();

            if (draft.AidType == AidType.Other && string.IsNullOrWhiteSpace(draft.OtherAidDescription))
                errors.Add("Description is required when requesting 'Other' assistance.");

            if (draft.FamilyMembersCount is < 1 or > 25)
                errors.Add("Family members count must be a whole number between 1 and 25 including the applicant.");

            if (draft.Father.MonthlySalary < 0 || draft.Mother.MonthlySalary < 0)
                errors.Add("Father and mother monthly salaries must be 0 or greater.");

            if (string.IsNullOrWhiteSpace(draft.Justification) || draft.Justification.Trim().Length < 20)
                errors.Add("Justification must be at least 20 characters in the student's own words.");

            return errors;
        }
    }
}
