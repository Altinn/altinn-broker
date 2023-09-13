    public class Receipt
    {
        public int ReceiptID { get; set; }
        public int ParentReceiptID { get; set; }
        public DateTime LastChanged { get; set; }
        public string Status { get; set; }
        public string Text { get; set; }
        public string SendersReference { get; set; }
        public string OwnerPartyReference { get; set; }
        public string PartyReference { get; set; }
        public List<SubReceipt> SubReceipts { get; set; }
    }

    public class SubReceipt
    {
        public int ReceiptID { get; set; }
        public int ParentReceiptID { get; set; }
        public DateTime LastChanged { get; set; }
        public string Status { get; set; }
        public string Text { get; set; }
        public string SendersReference { get; set; }
        public string PartyReference { get; set; }
        public string ReceiptHistory { get; set; }
    }

