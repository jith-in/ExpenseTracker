namespace ExpenseTracker.Models
{
    public class SmsMessageData
    {
        public string Body { get; set; } = string.Empty;
        public DateTime ReceivedDate { get; set; }
    }
}