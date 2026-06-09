using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace TFoodies.Infrastructure.Persistence.Scaffolded;

public partial class Purchase
{
    [Key]
    public Guid purchaseid { get; set; }

    public Guid supplierid { get; set; }

    [StringLength(15)]
    public string purchasecode { get; set; } = null!;

    public DateOnly purchasedate { get; set; }

    public Guid exchangeid { get; set; }

    public DateOnly? etd { get; set; }

    [StringLength(50)]
    public string payment { get; set; } = null!;

    [StringLength(50)]
    public string? deliverterm { get; set; }

    [Column(TypeName = "ntext")]
    public string? note { get; set; }

    public DateOnly createdate { get; set; }

    /// <summary>
    /// 1=&gt;未入庫; 2=&gt;已入庫; 3=&gt;部分入庫
    /// </summary>
    public int status { get; set; }

    public bool isexpenditure { get; set; }

    [InverseProperty("purchase")]
    public virtual ICollection<Expenditure> Expenditures { get; set; } = new List<Expenditure>();

    [InverseProperty("purchase")]
    public virtual ICollection<Purchasedetail> Purchasedetails { get; set; } = new List<Purchasedetail>();

    [ForeignKey("exchangeid")]
    [InverseProperty("Purchases")]
    public virtual Exchange exchange { get; set; } = null!;

    [ForeignKey("supplierid")]
    [InverseProperty("Purchases")]
    public virtual Supplier supplier { get; set; } = null!;
}
