using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;

namespace RealEstateRental.Models
{
    public class Contract
    {
        public int Id { get; set; }

        public int TenantId { get; set; }
        public int PropertyId { get; set; }

        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }

        public decimal MonthlyRent { get; set; }
        public string? DocumentFilePath { get; set; }

        public Tenant? Tenant { get; set; }
        public Property? Property { get; set; }
        public ICollection<Payment> Payments { get; set; } = new List<Payment>();

        [NotMapped]
        public decimal Debt
        {
            get
            {
                var today = DateTime.Today;
                var startDate = StartDate.Date;
                var effectiveEnd = (EndDate.HasValue && EndDate.Value.Date < today ? EndDate.Value.Date : today);

                if (effectiveEnd < startDate)
                    return 0;

                int totalMonths = ((effectiveEnd.Year - startDate.Year) * 12) + (effectiveEnd.Month - startDate.Month);
                if (effectiveEnd.Day >= startDate.Day)
                    totalMonths += 1;

                if (totalMonths < 0)
                    totalMonths = 0;

                var totalRent = totalMonths * MonthlyRent;
                var totalPayments = Payments
                    .Where(p => p.PaymentDate.Date <= today)
                    .Sum(p => p.Amount);
                return totalRent - totalPayments;
            }
        }

        [NotMapped]
        public bool IsSelectedForBulkDelete { get; set; }

        [NotMapped]
        public DebtStatus DebtStatus
        {
            get
            {
                var debt = Debt;
                if (debt <= 0)
                    return DebtStatus.FullyPaid;

                var totalPayments = Payments.Sum(p => p.Amount);
                if (totalPayments <= 0)
                    return DebtStatus.Overdue;

                if (EndDate.HasValue && EndDate.Value.Date < DateTime.Today)
                    return DebtStatus.Overdue;

                return DebtStatus.PartiallyPaid;
            }
        }

        [NotMapped]
        public string DebtStatusText => DebtStatus switch
        {
            DebtStatus.FullyPaid => "Полностью оплачено",
            DebtStatus.PartiallyPaid => "Частично оплачено",
            DebtStatus.Overdue => "Просрочено",
            _ => "Неизвестно"
        };

        [NotMapped]
        public string DisplayName
        {
            get
            {
                var tenantName = Tenant?.Name ?? $"Арендатор #{TenantId}";
                var propertyName = Property?.Name ?? $"Объект #{PropertyId}";
                return $"{tenantName} - {propertyName} (№{Id})";
            }
        }

        [NotMapped]
        public string ContractFileName => string.IsNullOrWhiteSpace(DocumentFilePath)
            ? "Нет файла"
            : System.IO.Path.GetFileName(DocumentFilePath);
    }
}