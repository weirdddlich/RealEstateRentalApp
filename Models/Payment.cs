using System;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;

namespace RealEstateRental.Models
{
    public class Payment
    {
        public int Id { get; set; }

        public int ContractId { get; set; }
        public DateTime PaymentDate { get; set; }
        public decimal Amount { get; set; }

        public Contract? Contract { get; set; }

        [NotMapped]
        public bool IsSelectedForBulkDelete { get; set; }

        [NotMapped]
        public decimal RemainingDebtAfterThisPayment
        {
            get
            {
                if (Contract == null)
                    return 0m;

                var today = DateTime.Today;
                var effectiveEnd = Contract.EndDate.HasValue && Contract.EndDate.Value < today ? Contract.EndDate.Value : today;
                if (effectiveEnd < Contract.StartDate)
                    return 0m;

                int totalMonths = ((effectiveEnd.Year - Contract.StartDate.Year) * 12) + (effectiveEnd.Month - Contract.StartDate.Month);
                if (effectiveEnd.Day >= Contract.StartDate.Day)
                    totalMonths += 1;

                if (totalMonths < 0)
                    totalMonths = 0;

                var orderedPayments = Contract.Payments
                    .OrderBy(p => p.PaymentDate)
                    .ThenBy(p => p.Id)
                    .ToList();

                if (!orderedPayments.Any(p => p.Id == Id))
                    orderedPayments.Add(this);

                orderedPayments = orderedPayments
                    .OrderBy(p => p.PaymentDate)
                    .ThenBy(p => p.Id)
                    .ToList();

                var index = orderedPayments.FindIndex(p => p.Id == Id);
                if (index < 0)
                    return Contract.Debt;

                var paymentsSum = orderedPayments.Take(index + 1).Sum(p => p.Amount);
                var expectedRent = totalMonths * Contract.MonthlyRent;
                return expectedRent - paymentsSum;
            }
        }

        [NotMapped]
        public bool IsOverdue
        {
            get
            {
                if (PaymentDate.Date >= DateTime.Today)
                    return false;

                // If the contract still has remaining debt after this payment, then it didn't clear overdue arrears.
                return RemainingDebtAfterThisPayment > 0m;
            }
        }

        [NotMapped]
        public string IsOverdueText => IsOverdue ? "Да" : "Нет";
    }
}