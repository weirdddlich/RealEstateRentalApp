using System.Collections.Generic;

namespace RealEstateRental.Models
{
    public class Property
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal PricePerMonth { get; set; }

        public ICollection<Contract> Contracts { get; set; } = new List<Contract>();
    }
}