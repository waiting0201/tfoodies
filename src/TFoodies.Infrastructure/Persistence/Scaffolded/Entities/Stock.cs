using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace TFoodies.Infrastructure.Persistence.Scaffolded;

public partial class Stock
{
    [Key]
    public Guid stockid { get; set; }

    public Guid purchasedetailid { get; set; }

    public Guid purchaseid { get; set; }

    /// <summary>
    /// 1 =&gt; 需申報; 2 =&gt; 不需申報
    /// </summary>
    public int stocktype { get; set; }

    /// <summary>
    /// 入庫日期
    /// </summary>
    public DateOnly createdate { get; set; }

    [StringLength(50)]
    public string? barcode { get; set; }

    /// <summary>
    /// 輸入許可通知號碼
    /// </summary>
    [StringLength(20)]
    public string? noticenumber { get; set; }

    /// <summary>
    /// 批號
    /// </summary>
    [StringLength(20)]
    public string? declarationnumber { get; set; }

    public int? item { get; set; }

    public DateOnly? manufacturedate { get; set; }

    /// <summary>
    /// 到期日
    /// </summary>
    public DateOnly? expiredate { get; set; }

    /// <summary>
    /// pce
    /// </summary>
    public int quantity { get; set; }

    /// <summary>
    /// 淨重
    /// </summary>
    [Column(TypeName = "decimal(18, 2)")]
    public decimal? weight { get; set; }

    /// <summary>
    /// 1=&gt;合格; 2=&gt;不合格待複檢
    /// </summary>
    public int? status { get; set; }

    [InverseProperty("stock")]
    public virtual ICollection<Warehousestock> Warehousestocks { get; set; } = new List<Warehousestock>();

    [ForeignKey("purchasedetailid")]
    [InverseProperty("Stocks")]
    public virtual Purchasedetail purchasedetail { get; set; } = null!;
}
