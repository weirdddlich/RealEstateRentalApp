using System;

namespace RealEstateRental.Models
{
    // DTO for displaying expected monthly rent payments.
    public class ContractScheduleItem
    {
        public DateTime ExpectedDate { get; set; }
        public decimal ExpectedAmount { get; set; }
        public decimal PaidAmount { get; set; }

        public bool IsPaid => PaidAmount >= ExpectedAmount;
        public bool IsOverdue => !IsPaid && ExpectedDate.Date < DateTime.Today;
        public bool HasPartialPayment => PaidAmount > 0m && !IsPaid;

        public string StatusText
        {
            get
            {
                if (IsPaid)
                    return "Оплачено";
                if (PaidAmount > 0)
                    return "Частично";
                if (ExpectedDate.Date < DateTime.Today)
                    return "Просрочено";
                return "Ожидается";
            }
        }
    }
}

