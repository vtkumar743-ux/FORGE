using Gym.Core.Enums;

namespace Gym.Core.Entities;

public class Product : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public string? Barcode { get; set; }
    public ProductCategory Category { get; set; }
    public string? Brand { get; set; }
    public string? Description { get; set; }
    public string? ImageUrl { get; set; }

    public decimal CostPrice { get; set; }
    public decimal SellPrice { get; set; }
    /// <summary>Retail goods carry their own slab (12/18%) — POS builds mixed-GST invoices.</summary>
    public decimal GstRatePercent { get; set; } = 18m;
    public string? HsnCode { get; set; }
    public string Unit { get; set; } = "pc";
    public int LowStockThreshold { get; set; } = 5;
    public bool IsActive { get; set; } = true;

    public ICollection<BranchStock> Stock { get; set; } = new List<BranchStock>();
}

public class BranchStock : BaseEntity
{
    public int BranchId { get; set; }
    public Branch Branch { get; set; } = null!;
    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;
    public int QuantityOnHand { get; set; }
    public int QuantityReserved { get; set; }
    public DateTime? LastRestockedAtUtc { get; set; }
    public string? ShelfLocation { get; set; }
}

public class StockTransfer : BaseEntity
{
    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;
    public int FromBranchId { get; set; }
    public Branch FromBranch { get; set; } = null!;
    public int ToBranchId { get; set; }
    public Branch ToBranch { get; set; } = null!;

    public int Quantity { get; set; }
    public StockTransferStatus Status { get; set; } = StockTransferStatus.Requested;
    public DateTime RequestedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ReceivedAtUtc { get; set; }
    public string? RequestedBy { get; set; }
    public string? ReceivedBy { get; set; }
    public string? Notes { get; set; }
}

public class Order : BaseEntity
{
    public string OrderNumber { get; set; } = string.Empty;
    public int BranchId { get; set; }
    public Branch Branch { get; set; } = null!;
    public int? MemberId { get; set; }
    public Member? Member { get; set; }

    public DateTime PlacedAtUtc { get; set; } = DateTime.UtcNow;
    public OrderStatus Status { get; set; } = OrderStatus.Open;
    public decimal SubTotal { get; set; }
    public decimal DiscountTotal { get; set; }
    public decimal TaxTotal { get; set; }
    public decimal GrandTotal { get; set; }
    /// <summary>True when charged to the member's running tab instead of settled at the desk.</summary>
    public bool ChargedToMemberTab { get; set; }
    public string? SoldBy { get; set; }
    public string? Notes { get; set; }

    public ICollection<OrderLine> Lines { get; set; } = new List<OrderLine>();
}

public class OrderLine : BaseEntity
{
    public int OrderId { get; set; }
    public Order Order { get; set; } = null!;
    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;

    public int Quantity { get; set; } = 1;
    public decimal UnitPrice { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal GstRatePercent { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal LineTotal { get; set; }
}
