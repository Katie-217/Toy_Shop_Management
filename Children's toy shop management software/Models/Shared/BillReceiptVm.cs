namespace Children_s_toy_shop_management_software.Models.Shared;

public sealed class BillReceiptVm
{
    public string? RootId { get; set; }
    public string? ExtraClass { get; set; }

    public string Brand { get; set; } = "Children's Toy Shop";
    public string AddressLine { get; set; } = "Address: 19 Nguyen Huu Tho, District 7 TP.HCM";
    public string HotlineLine { get; set; } = "Hotline: 0909.123.456";
    public string EmailLine { get; set; } = "Email: childrenstoyshop@gmail.com";
    public string Title { get; set; } = "BILL OF SALE";

    public string DateText { get; set; } = string.Empty;
    public string TimeText { get; set; } = string.Empty;
    public string VoucherText { get; set; } = "—";
    public string CashierText { get; set; } = "—";
    public string CustomerName { get; set; } = "—";
    public string CustomerPhone { get; set; } = "—";
    public string CustomerAddress { get; set; } = "—";

    public string TotalText { get; set; } = "0 VND";
    public string AmountWords { get; set; } = string.Empty;
    public string CashPaidText { get; set; } = "0 ₫";
    public string ChangeText { get; set; } = "0 ₫";

    public bool ShowCashInfo { get; set; } = true;
    public bool ShowVnPayInfo { get; set; }
    public string VnPayInfoText { get; set; } = "Payment method: VNPay (QR)";
    public bool ShowQr { get; set; } = true;
    public string QrHint { get; set; } = "Scan QR to pay";
    public string? QrCanvasId { get; set; }

    public string? DateElementId { get; set; }
    public string? TimeElementId { get; set; }
    public string? VoucherElementId { get; set; }
    public string? CustomerNameElementId { get; set; }
    public string? CustomerPhoneElementId { get; set; }
    public string? CustomerAddressElementId { get; set; }
    public string? TotalElementId { get; set; }
    public string? AmountWordsElementId { get; set; }
    public string? CashInfoElementId { get; set; }
    public string? VnPayInfoElementId { get; set; }
    public string? CashElementId { get; set; }
    public string? ChangeElementId { get; set; }

    public List<BillReceiptLineVm> Lines { get; set; } = new();
}

public sealed class BillReceiptLineVm
{
    public string ProductName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
}
