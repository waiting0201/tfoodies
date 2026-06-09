using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace TFoodies.Infrastructure.Persistence.Scaffolded;

public partial class Expenditure
{
    [Key]
    public Guid expenditureid { get; set; }

    public Guid? supplierid { get; set; }

    [StringLength(15)]
    public string expenditurecode { get; set; } = null!;

    public DateOnly expendituredate { get; set; }

    /// <summary>
    /// 0 =&gt; 手動輸入; 1 =&gt; 自動帶入
    /// </summary>
    public int sourcetype { get; set; }

    public Guid? purchaseid { get; set; }

    [Column(TypeName = "ntext")]
    public string? note { get; set; }

    /// <summary>
    /// 0 =&gt; 未付款; 1 =&gt; 部分付款; 2 =&gt; 已付款
    /// </summary>
    public int status { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime createdate { get; set; }

    [InverseProperty("expenditure")]
    public virtual ICollection<Expendituredetail> Expendituredetails { get; set; } = new List<Expendituredetail>();

    [InverseProperty("expenditure")]
    public virtual ICollection<Outcome> Outcomes { get; set; } = new List<Outcome>();

    [ForeignKey("purchaseid")]
    [InverseProperty("Expenditures")]
    public virtual Purchase? purchase { get; set; }

    [ForeignKey("supplierid")]
    [InverseProperty("Expenditures")]
    public virtual Supplier? supplier { get; set; }
}
