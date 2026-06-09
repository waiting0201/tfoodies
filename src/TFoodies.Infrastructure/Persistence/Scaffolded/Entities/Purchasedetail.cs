using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace TFoodies.Infrastructure.Persistence.Scaffolded;

public partial class Purchasedetail
{
    [Key]
    public Guid purchasedetailid { get; set; }

    public Guid purchaseid { get; set; }

    public Guid productid { get; set; }

    [Column(TypeName = "decimal(18, 2)")]
    public decimal unitprice { get; set; }

    public int qty { get; set; }

    [Column(TypeName = "decimal(18, 2)")]
    public decimal subtotal { get; set; }

    /// <summary>
    /// 0=&gt;未入庫; 1=&gt;已入庫; 2=&gt;缺; 3=&gt;多
    /// </summary>
    public int status { get; set; }

    [InverseProperty("purchasedetail")]
    public virtual ICollection<Stock> Stocks { get; set; } = new List<Stock>();

    [ForeignKey("productid")]
    [InverseProperty("Purchasedetails")]
    public virtual Product product { get; set; } = null!;

    [ForeignKey("purchaseid")]
    [InverseProperty("Purchasedetails")]
    public virtual Purchase purchase { get; set; } = null!;
}
