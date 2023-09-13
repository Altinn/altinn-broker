

public class ReceiptRepository {
    
    private readonly Dictionary<string, Receipt> _receipts;
    public ReceiptRepository(){
        _receipts = new Dictionary<string, Receipt>();
    }

    public Receipt GetReceipt(string ReceiptId) => _receipts[ReceiptId];

    public void StoreReceipt(string ReceiptId, Receipt Receipt) => _receipts[ReceiptId] = Receipt;
}