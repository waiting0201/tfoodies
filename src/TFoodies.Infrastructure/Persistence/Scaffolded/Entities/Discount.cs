using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace TFoodies.Infrastructure.Persistence.Scaffolded;

public partial class Discount
{
    [Key]
    public Guid discountid { get; set; }

    [StringLength(8)]
    public string discountcode { get; set; } = null!;

    /// <summary>
    /// 0:折扣; 1:金額
    /// </summary>
    public int istype { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? startdate { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? expiredate { get; set; }

    /// <summary>
    /// 0:否; 1:是; 2:每會員限用一次
    /// </summary>
    public int isonetime { get; set; }

    [Column(TypeName = "decimal(6, 2)")]
    public decimal v { get; set; }

    [Column(TypeName = "ntext")]
    public string? memo { get; set; }

    /// <summary>
    /// 0:否; 1:是
    /// </summary>
    public int isdisable { get; set; }

    [InverseProperty("discountNavigation")]
    public virtual ICollection<Order> Orders { get; set; } = new List<Order>();
}
