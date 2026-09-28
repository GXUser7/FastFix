using System.ComponentModel.DataAnnotations;

namespace ServiceDesk.Contracts;

public class PartDto
{
    public int Id { get; set; }
    public string Sku { get; set; }
    public string Name { get; set; }
    public string Unit { get; set; }
    public decimal Price { get; set; }
    public int OnHand { get; set; }
    public int Reserved { get; set; }
    public int Available { get; set; }
    public int MinQuantity { get; set; }
    public string Location { get; set; }
    public bool BelowMinimum { get; set; }
    public bool IsActive { get; set; }
}

public class PartEditRequest
{
    [Required(ErrorMessage = "Укажите артикул"), StringLength(30)]
    public string Sku { get; set; }

    [Required(ErrorMessage = "Укажите наименование"), StringLength(150)]
    public string Name { get; set; }

    [StringLength(10)] public string Unit { get; set; } = "шт";

    [Range(0, 10_000_000, ErrorMessage = "Некорректная цена")]
    public decimal Price { get; set; }

    [Range(0, 100_000, ErrorMessage = "Некорректный минимальный остаток")]
    public int MinQuantity { get; set; }

    [StringLength(30)] public string Location { get; set; }

    public bool IsActive { get; set; } = true;
}

public class ReceiptRequest
{
    [Range(1, 100_000, ErrorMessage = "Количество — от 1")]
    public int Quantity { get; set; }

    [StringLength(300)] public string Comment { get; set; }
}

public class MovementDto
{
    public int Id { get; set; }
    public int PartId { get; set; }
    public string Sku { get; set; }
    public string PartName { get; set; }
    public int? OrderId { get; set; }
    public string Type { get; set; }
    public string TypeName { get; set; }
    public int Quantity { get; set; }
    public int BalanceAfter { get; set; }
    public string UserName { get; set; }
    public DateTime CreatedAt { get; set; }
    public string Comment { get; set; }
}

public class InventoryLine
{
    public int PartId { get; set; }

    [Range(0, 100_000, ErrorMessage = "Фактический остаток не может быть отрицательным")]
    public int ActualQuantity { get; set; }
}

public class InventoryRequest
{
    [StringLength(300)] public string Comment { get; set; }
    public List<InventoryLine> Items { get; set; } = new();
}

public class InventoryItemDto
{
    public int PartId { get; set; }
    public string Sku { get; set; }
    public string PartName { get; set; }
    public int Expected { get; set; }
    public int Actual { get; set; }
    public int Difference { get; set; }
}

public class InventoryDto
{
    public int Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; }
    public string Comment { get; set; }
    public int Discrepancies { get; set; }
    public List<InventoryItemDto> Items { get; set; } = new();
}
