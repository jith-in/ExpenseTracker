using SQLite;

namespace ExpenseTracker.Models
{
    [Table("PaymentMethods")]
    public class PaymentMethod
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;
    }
}
