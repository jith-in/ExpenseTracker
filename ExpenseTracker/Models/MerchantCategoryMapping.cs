using SQLite;

namespace ExpenseTracker.Models
{
    [Table("MerchantCategoryMappings")]
    public class MerchantCategoryMapping
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        public string Merchant { get; set; } = string.Empty;

        public string Category { get; set; } = string.Empty;
    }
}
